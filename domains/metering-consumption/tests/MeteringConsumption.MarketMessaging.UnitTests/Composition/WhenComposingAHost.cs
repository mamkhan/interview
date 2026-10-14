using MeteringConsumption.MarketMessaging.Application.Extensions;
using MeteringConsumption.MarketMessaging.Infrastructure.Extensions;
using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MeteringConsumption.MarketMessaging.Infrastructure.Consumers;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Receive.Abstraction;
using MeteringConsumption.MarketMessaging.Application.Features.Interpret.Abstraction;

namespace MeteringConsumption.MarketMessaging.UnitTests.Composition;

// A host adds the capabilities it runs and gets exactly those consumers. The composed service collection is the
// evidence, read before anything is resolved.
public class WhenComposingAHost
{
    [Fact]
    public void ReceiveRegistersItsBlobCreatedConsumerAndNoRepository()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.Services.AddBaseApplication();

        builder.Services.AddBaseInfrastructure(builder.Configuration);

        builder.Services.AddReceiveApplication();

        builder.Services.AddReceiveInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().Equal(typeof(ReceiveBlobCreatedEventConsumer));
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(IMarketMessageRepository));
    }

    [Fact]
    public void TheBaseNamesTheTopicSupplyPointsReadsAWholeConfigurationChangeFrom()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);

        using var services = builder.Services.BuildServiceProvider();

        services.GetRequiredService<EventTopicMap>().TopicFor(typeof(MeterConfigurationChangeReceived))
            .Should().Be("metr.meter-configuration-change-received");
    }

    [Fact]
    public void InterpretAndAuditRegisterTheirFourConsumers()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(
            [new($"ConnectionStrings:{MarketMessagingSchema.ConnectionName}", "Server=.;Database=x;Encrypt=True")]);

        builder.Services.AddBaseApplication();

        builder.Services.AddBaseInfrastructure(builder.Configuration);

        builder.Services.AddInterpretApplication();

        builder.Services.AddInterpretInfrastructure(builder.Configuration);

        builder.Services.AddAuditApplication();

        builder.Services.AddAuditInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().BeEquivalentTo(
        [
            typeof(AuditMarketMessageReceivedEventConsumer),
            typeof(InterpretMarketMessageReceivedEventConsumer),
            typeof(AuditMarketMessageProcessedEventConsumer),
            typeof(AuditMarketMessageFailedEventConsumer),
        ]);
        builder.Services.Where(d => d.ServiceType == typeof(IMarketMessageRepository)).Should().ContainSingle();
        builder.Services.Should().NotContain(d =>
            d.ServiceType.Name.Contains("UnitOfWork", StringComparison.Ordinal)
            || d.ServiceType.Name.Contains("OutboxRelay", StringComparison.Ordinal));
    }

    [Fact]
    public void AuditRegistersNothingThatNeedsBlobStorage()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(
            [new($"ConnectionStrings:{MarketMessagingSchema.ConnectionName}", "Server=.;Database=x;Encrypt=True")]);

        builder.Services.AddBaseApplication();

        builder.Services.AddBaseInfrastructure(builder.Configuration);

        builder.Services.AddAuditApplication();

        builder.Services.AddAuditInfrastructure(builder.Configuration);

        builder.Services.Should().NotContain(d =>
            d.ServiceType == typeof(Application.Features.Receive.Abstraction.IReceiveArchiveBlobStore) || d.ServiceType == typeof(IInterpretArchiveBlobStore) || d.ServiceType == typeof(IDropBlobStore));
    }

    [Fact]
    public void InterpretAloneRegistersItsConsumerAndNoRepository()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.Services.AddBaseApplication();

        builder.Services.AddBaseInfrastructure(builder.Configuration);

        builder.Services.AddInterpretApplication();

        builder.Services.AddInterpretInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().Equal(typeof(InterpretMarketMessageReceivedEventConsumer));
        builder.Services.Should().NotContain(d => d.ServiceType == typeof(IMarketMessageRepository));
    }

    private static IEnumerable<Type> HostedServicesOf(HostApplicationBuilder builder) =>
        builder.Services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType ?? d.ImplementationInstance?.GetType() ?? typeof(object));
}
