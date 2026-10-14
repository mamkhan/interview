using System.Xml;
using AwesomeAssertions;
using MeteringConsumption.MarketMessaging.Common.ESBMessage;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;
using MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

namespace MeteringConsumption.MarketMessaging.UnitTests.Interpret;

// The path every strategy parses through refuses a DTD outright, as the classifier does, so an entity in an
// operator file is never expanded or resolved; the Interpret handler turns the XmlException into a failed outcome.
public class WhenParsingADocumentThatDeclaresADtd
{
    [Fact]
    public void RefusesAnExternalEntityWithoutResolvingIt()
    {
        var parse = () => Parse("""<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x SYSTEM "file:///etc/passwd">]><r>&x;</r>""");

        parse.Should().Throw<XmlException>();
    }

    [Fact]
    public void RefusesAnInternalEntity()
    {
        var parse = () => Parse("""<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x "expanded">]><r>&x;</r>""");

        parse.Should().Throw<XmlException>();
    }

    private static Message<MIM320_ValidatedCoSReading> Parse(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return ESBMessageParser.ParseDocument<Message<MIM320_ValidatedCoSReading>>(stream);
    }
}
