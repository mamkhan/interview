using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Classifiers;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.MessageStrategies;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MeteringConsumption.MarketMessaging.UnitTests.Composition;

// Every ESB message type the platform reads, as the builder writes it, through the Interpret role's own registrations:
// the classifier it registers for the market names the file's type, and the strategies it registers hold the one for
// that type. The expected names are the strategies a host runs, written out, not derived from the code.
public class WhenDispatchingEveryMessageType
{
    [Theory]
    [InlineData("101", "MM101Strategy")]
    [InlineData("105", "MM105Strategy")]
    [InlineData("300", "MM300Strategy")]
    [InlineData("300S", "MM300SStrategy")]
    [InlineData("301", "MM301Strategy")]
    [InlineData("306", "MM306Strategy")]
    [InlineData("307", "MM307Strategy")]
    [InlineData("310", "MM310Strategy")]
    [InlineData("320", "MM320Strategy")]
    [InlineData("331", "MM331Strategy")]
    [InlineData("332", "MM332Strategy")]
    [InlineData("343", "MM343Strategy")]
    public void ReachesTheStrategyForItsType(string messageTypeCode, string strategy)
    {
        using var services = ComposeTheInterpretRole();
        using var scope = services.CreateScope();
        using var payload = new MemoryStream(new MarketMessageXmlBuilder().WithMessageTypeCode(messageTypeCode).BuildBytes());

        var messageType = scope.ServiceProvider.GetRequiredService<MessageClassifiers>().For("roi-esb")!.Classify(payload);
        var interpreter = scope.ServiceProvider.GetRequiredService<MessageStrategy>().Find("roi-esb", messageType);

        interpreter!.GetType().Name.Should().Be(strategy);
    }

    [Fact]
    public void FindsNoStrategyForATypeThePlatformDoesNotRead()
    {
        using var services = ComposeTheInterpretRole();
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<MessageStrategy>().Find("roi-esb", "017").Should().BeNull();
    }

    // The clock and the client are what a host adds before the role; nothing here sends, so the client never connects.
    private static ServiceProvider ComposeTheInterpretRole()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new ServiceBusClient(
            "Endpoint=sb://localhost;SharedAccessKeyName=unused;SharedAccessKey=unused;UseDevelopmentEmulator=true;"));
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        builder.Services.AddInterpretApplication();
        builder.Services.AddInterpretInfrastructure(builder.Configuration);
        return builder.Services.BuildServiceProvider();
    }
}
