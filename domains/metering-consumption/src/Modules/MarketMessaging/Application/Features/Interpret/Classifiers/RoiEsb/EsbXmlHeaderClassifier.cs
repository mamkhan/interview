using System.Xml;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers.RoiEsb;

public sealed class ESBXmlHeaderClassifier : IMessageClassifier
{
    public string Classify(Stream content)
    {
        try
        {
            using var reader = ESBMessageParser.OpenReader(content);
            if (!reader.ReadToFollowing("MessageHeader"))
            {
                throw new MessageClassificationException("The file has no MessageHeader element.");
            }

            var code = reader.GetAttribute("MessageTypeCode");
            return string.IsNullOrWhiteSpace(code)
                ? throw new MessageClassificationException("MessageHeader carries no MessageTypeCode.")
                : code;
        }
        catch (XmlException exception)
        {
            throw new MessageClassificationException($"The file is not readable XML: {exception.Message}");
        }
    }
}
