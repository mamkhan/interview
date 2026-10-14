using AwesomeAssertions;
using BuildingBlocks.Commands;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.Contracts.Queries;
using MeteringConsumption.SupplyPoints.Application.Extensions;
using MeteringConsumption.SupplyPoints.Application.Features.Provide;
using MeteringConsumption.SupplyPoints.Application.Features.Provide.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Reload;
using MeteringConsumption.SupplyPoints.Application.Features.Reload.Abstraction;
using MeteringConsumption.SupplyPoints.Application.Features.Store;
using MeteringConsumption.SupplyPoints.Application.Features.Store.Abstraction;
using MeteringConsumption.SupplyPoints.Domain.ValueObjects;
using MeteringConsumption.SupplyPoints.Infrastructure.Consumers;
using MeteringConsumption.SupplyPoints.Infrastructure.Extensions;
using MeteringConsumption.SupplyPoints.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Contracts.Events.V1;

namespace MeteringConsumption.SupplyPoints.UnitTests.Composition;

// A host adds the roles it runs and gets exactly those consumers. The composed service collection is the
// evidence, read before anything is resolved.
public class WhenComposingAHost
{
    [Fact]
    public void TheBaseRegistersTheEntityNamesEveryConsumerResolvesItsTopicThrough()
    {
        var builder = Composed();

        builder.Services.Should().Contain(d => d.ServiceType == typeof(EntityNames));
        HostedServicesOf(builder).Should().BeEmpty();
    }

    [Fact]
    public void StoreRegistersItsOneConsumerAndItsHandler()
    {
        var builder = Composed();
        builder.Services.AddStoreApplication();
        builder.Services.AddStoreInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().Equal(typeof(MeterConfigurationChangeReceivedEventConsumer));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(MeterConfigurationChangeReceivedEventHandler));
    }

    [Fact]
    public void StoreRunsItsCommandsOnTheDomainDatabase()
    {
        var builder = Composed();
        builder.Services.AddStoreApplication();
        builder.Services.AddStoreInfrastructure(builder.Configuration);

        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(IUnitOfWork));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(CommandRunner));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ISupplyPointRepository));
        builder.Services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ICommandHandler<MeterConfigurationChangeReceived, IReadOnlyList<RecordOutcomeKind>>));
    }

    [Fact]
    public void ReloadRegistersItsOneConsumerAndItsHandler()
    {
        var builder = Composed();
        builder.Services.AddReloadApplication();
        builder.Services.AddReloadInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().Equal(typeof(SupplyPointLoadedFromV1EventConsumer));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(SupplyPointLoadedFromV1EventHandler));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ISupplyPointReloadRepository));
        builder.Services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ICommandHandler<SupplyPointLoadedFromV1, ReloadOutcome>));
    }

    [Fact]
    public void StoreAndReloadRunTheirCommandsOnOneUnitOfWork()
    {
        var builder = Composed();
        builder.Services.AddStoreApplication();
        builder.Services.AddStoreInfrastructure(builder.Configuration);
        builder.Services.AddReloadApplication();
        builder.Services.AddReloadInfrastructure(builder.Configuration);

        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(IUnitOfWork));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(CommandRunner));
        HostedServicesOf(builder).Should().Equal(
            typeof(MeterConfigurationChangeReceivedEventConsumer), typeof(SupplyPointLoadedFromV1EventConsumer));
    }

    [Fact]
    public void ProvideRegistersTheReadOtherModulesCallAndNoConsumer()
    {
        var builder = Composed();
        builder.Services.AddProvideApplication();
        builder.Services.AddProvideInfrastructure(builder.Configuration);

        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(IMeterPointConfiguration)
            && d.ImplementationType == typeof(MeterPointConfigurationQuery));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ISupplyPointReader));
        HostedServicesOf(builder).Should().BeEmpty();
    }

    [Fact]
    public void StoreRefusesAHostWithNoDomainDatabase()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        var compose = () => builder.Services.AddStoreInfrastructure(builder.Configuration);

        compose.Should().Throw<InvalidOperationException>().WithMessage("*metering-consumption*");
    }

    private static HostApplicationBuilder Composed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(
            [new($"ConnectionStrings:{SupplyPointSchema.ConnectionName}", "Server=.;Database=x;Encrypt=True")]);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        return builder;
    }

    private static IEnumerable<Type> HostedServicesOf(HostApplicationBuilder builder) =>
        builder.Services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType ?? d.ImplementationInstance?.GetType() ?? typeof(object));
}
