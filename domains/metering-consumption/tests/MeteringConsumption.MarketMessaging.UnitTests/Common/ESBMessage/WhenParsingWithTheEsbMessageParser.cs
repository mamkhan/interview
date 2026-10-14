using System.Text;
using System.Xml.Serialization;
using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;

namespace MeteringConsumption.MarketMessaging.UnitTests.Common.ESBMessage;

// The parser's own binding rules over types that exist only here, so a rename in the ESB schema cannot pass or
// fail these: attributes bind to scalar properties of the same name, a complex property to the one child element
// named after its type, a list to every child element named after its item type; nullable means optional.
public class WhenParsingWithTheEsbMessageParser
{
    [Fact]
    public void BindsEachAttributeToThePropertyOfTheSameName()
    {
        var parsed = Parse<Reading>("""<Reading Code="KWH" Count="48" Amount="17.441" Day="2026-06-24"/>""");

        parsed.Code.Should().Be("KWH");
        parsed.Count.Should().Be(48);
        parsed.Amount.Should().Be(17.441m);
        parsed.Day.Should().Be(new DateOnly(2026, 6, 24));
    }

    [Fact]
    public void KeepsTheOffsetATimestampIsStatedIn() =>
        Parse<Reading>("""<Reading At="2026-06-24T00:30:00+01:00"/>""").At
            .Should().Be(new DateTimeOffset(2026, 6, 24, 0, 30, 0, TimeSpan.FromHours(1)));

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ReadsAFlagAsTheOperatorWritesIt(string value, bool expected) =>
        Parse<Reading>($"""<Reading Flag="{value}"/>""").Flag.Should().Be(expected);

    [Fact]
    public void LeavesAnAbsentOptionalAttributeNull() =>
        Parse<Reading>("<Reading/>").Count.Should().BeNull();

    [Fact]
    public void TreatsAnEmptyAttributeAsAbsent() =>
        Parse<Reading>("""<Reading Count=""/>""").Count.Should().BeNull();

    [Fact]
    public void IgnoresAttributesAndElementsItDoesNotMap() =>
        Parse<Reading>("""<Reading Code="KWH" Unmapped="x"><Unmapped/></Reading>""").Code.Should().Be("KWH");

    [Fact]
    public void BindsAComplexPropertyToTheChildElementNamedAfterItsType() =>
        Parse<Reading>("""<Reading><Meter SerialNumber="32300001"/></Reading>""").Meter!.SerialNumber.Should().Be("32300001");

    [Fact]
    public void LeavesAnAbsentChildElementNull() =>
        Parse<Reading>("<Reading/>").Meter.Should().BeNull();

    [Fact]
    public void BindsAListToEveryChildElementNamedAfterItsItemTypeInDocumentOrder() =>
        Parse<Reading>("""<Reading><Period Value="0.14"/><Period Value="0.069"/></Reading>""").Periods!
            .Select(p => p.Value).Should().Equal(0.14m, 0.069m);

    [Fact]
    public void GivesAnEmptyListWhenNoChildElementIsPresent() =>
        Parse<Reading>("<Reading/>").Periods.Should().BeEmpty();

    [Fact]
    public void BindsNestedChildrenAtEveryLevel() =>
        Parse<Reading>("""<Reading><Meter SerialNumber="32300001"><Period Value="1.5"/></Meter></Reading>""").Meter!.Periods!
            .Single().Value.Should().Be(1.5m);

    [Fact]
    public void BindsAnAttributeToThePropertyItsXmlAttributeNames() =>
        Parse<Remark>("""<Remark Remark="Meter behind locked gate"/>""").Text.Should().Be("Meter behind locked gate");

    [Fact]
    public void IgnoresThePropertysOwnNameOnceXmlAttributeRenamesIt() =>
        Parse<Remark>("""<Remark Text="ignored"/>""").Text.Should().BeNull();

    [Fact]
    public void RefusesAMissingRequiredAttributeNamingIt()
    {
        var parse = () => Parse<Required>("<Required/>");

        parse.Should().Throw<InvalidOperationException>().WithMessage("*'Name'*required*");
    }

    [Fact]
    public void RefusesAValueThatIsNotTheDeclaredTypeNamingTheAttributeAndValue()
    {
        var parse = () => Parse<Reading>("""<Reading Count="many"/>""");

        parse.Should().Throw<InvalidOperationException>().WithMessage("*'Count'*'many'*Int32*");
    }

    private static T Parse<T>(string xml) where T : class
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return ESBMessageParser.ParseDocument<T>(stream);
    }

    private sealed class Reading
    {
        public string? Code { get; set; }

        public int? Count { get; set; }

        public decimal? Amount { get; set; }

        public DateOnly? Day { get; set; }

        public DateTimeOffset? At { get; set; }

        public bool? Flag { get; set; }

        public Meter? Meter { get; set; }

        public IReadOnlyList<Period>? Periods { get; set; }
    }

    private sealed class Meter
    {
        public string? SerialNumber { get; set; }

        public IReadOnlyList<Period>? Periods { get; set; }
    }

    private sealed class Period
    {
        public decimal? Value { get; set; }
    }

    // The element and its attribute share a name, which C# forbids for a member, as MeterReaderRemarks does in the XSD.
    private sealed class Remark
    {
        [XmlAttribute("Remark")]
        public string? Text { get; set; }
    }

    private sealed class Required
    {
        public string Name { get; set; } = "";
    }
}
