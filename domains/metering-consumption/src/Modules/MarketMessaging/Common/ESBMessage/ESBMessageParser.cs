using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage;

public static class ESBMessageParser
{
    // Operator files arrive from outside the platform, so every reader refuses DTDs and resolves nothing.
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        IgnoreWhitespace = true,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = false,
    };

    private static readonly ConcurrentDictionary<Type, MappedProperty[]> MappedProperties = new();

    private static readonly ConcurrentDictionary<Type, ChildProperty[]> ChildProperties = new();

    public static XmlReader OpenReader(Stream content) => XmlReader.Create(content, ReaderSettings);

    public static T ParseDocument<T>(Stream content) where T : class
    {
        ArgumentNullException.ThrowIfNull(content);

        using var reader = OpenReader(content);

        return ParseTree<T>(XDocument.Load(reader).Root!);
    }

    public static T ParseTree<T>(XElement element) where T : class
    {
        ArgumentNullException.ThrowIfNull(element);

        return (T)ParseTree(typeof(T), element);
    }

    private static object ParseTree(Type type, XElement element)
    {
        var target = ParseAttributes(type, element);

        foreach (var child in ChildrenOf(type))
        {
            if (child.ItemType is null)
            {
                var single = element.Element(child.ElementName);
                if (single is not null)
                {
                    child.Info.SetValue(target, ParseTree(child.Info.PropertyType, single));
                }

                continue;
            }

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(child.ItemType))!;
            foreach (var item in element.Elements(child.ElementName))
            {
                list.Add(ParseTree(child.ItemType, item));
            }

            child.Info.SetValue(target, list);
        }

        return target;
    }

    public static T Parse<T>(XElement element) where T : new()
    {
        ArgumentNullException.ThrowIfNull(element);

        return (T)ParseAttributes(typeof(T), element);
    }

    private static object ParseAttributes(Type type, XElement element)
    {
        var target = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"{type.Name} has no parameterless constructor.");

        foreach (var property in MapOf(type))
        {
            var attribute = element.Attribute(property.AttributeName);

            if (attribute is null || attribute.Value.Length is 0)
            {
                if (property.IsRequired)
                    throw new InvalidOperationException($"<{element.Name.LocalName}> carries no '{property.AttributeName}' attribute, which {type.Name} declares as required.");

                continue;
            }

            property.Info.SetValue(target, Convert(attribute.Value, property.ValueType, element, property.AttributeName));
        }

        return target;
    }

    public static IReadOnlyList<T> ParseAll<T>(IEnumerable<XElement> elements) where T : new() =>
        [.. elements.Select(Parse<T>)];

    public static IReadOnlyList<T> ParseAll<T>(XElement parent, string elementName) where T : new()
    {
        ArgumentNullException.ThrowIfNull(parent);

        return ParseAll<T>(parent.Elements(elementName));
    }

    public static IReadOnlyList<T> ParseAll<T>(XElement parent, string elementName, Action<T, XElement> withChildren) where T : new()
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(withChildren);

        return [.. parent.Elements(elementName).Select(element =>
        {
            var parsed = Parse<T>(element);
            withChildren(parsed, element);

            return parsed;
        })];
    }

    public static T ParseSingle<T>(XElement parent, string elementName) where T : new()
    {
        ArgumentNullException.ThrowIfNull(parent);

        var element = parent.Element(elementName)
            ?? throw new InvalidOperationException($"<{parent.Name.LocalName}> carries no <{elementName}> element.");

        return Parse<T>(element);
    }

    public static T? ParseOptional<T>(XElement parent, string elementName) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(parent);

        var element = parent.Element(elementName);

        return element is null ? null : Parse<T>(element);
    }

    private static MappedProperty[] MapOf(Type type) => MappedProperties.GetOrAdd(type, Build);

    private static ChildProperty[] ChildrenOf(Type type) => ChildProperties.GetOrAdd(type, BuildChildren);

    private static ChildProperty[] BuildChildren(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(property => new { property, item = ItemTypeOf(property.PropertyType) })
            .Where(x => x.item is not null || !IsScalar(x.property.PropertyType))
            .Select(x => new ChildProperty(
                x.property,
                (x.item ?? x.property.PropertyType).Name,
                x.item))];

    private static Type? ItemTypeOf(Type type)
    {
        if (type == typeof(string) || !type.IsGenericType) { return null; }

        return typeof(IEnumerable).IsAssignableFrom(type) ? type.GetGenericArguments()[0] : null;
    }

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(Guid);
    }

    private static MappedProperty[] Build(Type type)
    {
        var nullability = new NullabilityInfoContext();

        return [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Where(property => !IsCollection(property.PropertyType))
            .Select(property => new MappedProperty(
                property,
                AttributeNameOf(property),
                Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType,
                IsRequired(property, nullability)))];
    }

    // An attribute binds to the property of the same name, unless that name is the element's own (MeterReaderRemarks
    // carries a MeterReaderRemarks attribute), which C# forbids as a member name; [XmlAttribute] names it then.
    private static string AttributeNameOf(PropertyInfo property) =>
        property.GetCustomAttribute<XmlAttributeAttribute>()?.AttributeName is { Length: > 0 } name ? name : property.Name;

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsRequired(PropertyInfo property, NullabilityInfoContext nullability) =>
        Nullable.GetUnderlyingType(property.PropertyType) is null
        && nullability.Create(property).WriteState is not NullabilityState.Nullable;

    private static object Convert(string value, Type targetType, XElement element, string attributeName)
    {
        try
        {
            if (targetType == typeof(string)) return value;
            if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);
            if (targetType == typeof(bool)) return value is "1" || (value is not "0" && bool.Parse(value));
            if (targetType == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(DateOnly)) return DateOnly.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(TimeOnly)) return TimeOnly.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
            if (targetType == typeof(Guid)) return Guid.Parse(value);

            return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"<{element.Name.LocalName}> attribute '{attributeName}' has value '{value}', which is not a valid {targetType.Name}.", exception);
        }
    }

    private sealed record MappedProperty(PropertyInfo Info, string AttributeName, Type ValueType, bool IsRequired);

    private sealed record ChildProperty(PropertyInfo Info, string ElementName, Type? ItemType);
}
