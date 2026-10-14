using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers.RoiEsb;
using MeteringConsumption.MarketMessaging.Domain;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

public class WhenClassifyingAnESBFile
{
    private readonly ESBXmlHeaderClassifier _classifier = new();

    [Fact]
    public void ReadsTheTypeFromTheMessageHeaderTheOperatorWrote()
    {
        var code = Classify(
            """<?xml version="1.0"?><ieXMLDocument><MessageHeader MessageTypeCode="343" TxRefNbr="1"/></ieXMLDocument>""");

        code.Should().Be("343");
    }

    [Fact]
    public void IgnoresTheFileNameWhenClassifying()
    {
        // The rail's skeleton read the family out of the file name; a naming convention is not a guarantee
        // and the operator states the type inside the document.
        var code = Classify(
            """<ieXMLDocument><MessageHeader MessageTypeCode="332"/></ieXMLDocument>""");

        code.Should().Be("332");
    }

    [Fact]
    public void RefusesAFileWithNoMessageHeader()
    {
        var classify = () => Classify("<ieXMLDocument><Something/></ieXMLDocument>");

        classify.Should().Throw<MessageClassificationException>().WithMessage("*MessageHeader*");
    }

    [Fact]
    public void RefusesAHeaderWithNoTypeCode()
    {
        var classify = () => Classify("<ieXMLDocument><MessageHeader TxRefNbr=\"1\"/></ieXMLDocument>");

        classify.Should().Throw<MessageClassificationException>().WithMessage("*MessageTypeCode*");
    }

    [Fact]
    public void RefusesAFileThatIsNotXml()
    {
        var classify = () => Classify("this is not xml");

        classify.Should().Throw<MessageClassificationException>();
    }

    [Fact]
    public void RefusesADocumentDeclaringAnExternalEntityWithoutResolvingIt()
    {
        var classify = () => Classify(
            """<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x SYSTEM "file:///etc/passwd">]><r>&x;</r>""");

        classify.Should().Throw<MessageClassificationException>();
    }

    private string Classify(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return _classifier.Classify(stream);
    }
}
