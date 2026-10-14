using AwesomeAssertions;
using BuildingBlocks.Messaging;
using MeteringConsumption.Consumption.Application.Extensions;
using MeteringConsumption.Consumption.Application.Features.Profile;
using MeteringConsumption.Consumption.Application.Features.Profile.Abstraction;
using MeteringConsumption.Consumption.Application.Features.Record;
using MeteringConsumption.Consumption.Application.Features.Record.Abstraction;
using MeteringConsumption.Consumption.Infrastructure.Configuration;
using MeteringConsumption.Consumption.Infrastructure.Consumers;
using MeteringConsumption.Consumption.Infrastructure.Extensions;
using MeteringConsumption.Consumption.Infrastructure.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MeteringConsumption.Consumption.UnitTests.Composition;

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
    public void ProfileRegistersItsThreeConsumersTheirHandlersAndTheRepository()
    {
        var builder = Composed();
        builder.Services.AddProfileApplication();
        builder.Services.AddProfileInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().BeEquivalentTo(
        [
            typeof(ProfileMeterConfigurationReceivedEventConsumer),
            typeof(ProfileUsageFactorsReceivedEventConsumer),
            typeof(ProfileTariffResolvedEventConsumer),
        ]);
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ProfileMeterConfigurationReceivedEventHandler));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ProfileUsageFactorsReceivedEventHandler));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ProfileTariffResolvedEventHandler));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(ISupplyPointProfileRepository));
    }

    // A full V1 run fills every Profile session at once. By default the two V1-fed consumers take 6 sessions each, so
    // with usage factors' 8 and Market Messaging's 4 and 4 the host sends at most 28 requests to the database at
    // once, within the 30 that dev's Basic tier accepts.
    [Fact]
    public void ProfileKeepsItsDatabaseRequestsWithinTheSmallestTierByDefault()
    {
        var options = ProfileOptionsOf(Composed());

        options.MaxConcurrentSessions.Should().Be(6);
        options.SessionIdleTimeout.Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void ProfileTakesItsSessionLimitsFromConfiguration()
    {
        var builder = Composed();
        builder.Configuration.AddInMemoryCollection(
            [new("Profile:MaxConcurrentSessions", "16"), new("Profile:SessionIdleTimeout", "00:00:00.5")]);

        var options = ProfileOptionsOf(builder);

        options.MaxConcurrentSessions.Should().Be(16);
        options.SessionIdleTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RecordRegistersItsConsumerItsHandlerAndTheLedgerStore()
    {
        var builder = Composed();
        builder.Services.AddRecordApplication();
        builder.Services.AddRecordInfrastructure(builder.Configuration);

        HostedServicesOf(builder).Should().Equal(typeof(RecordIntervalReadingReceivedEventConsumer));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(RecordIntervalReadingReceivedEventHandler));
        builder.Services.Should().ContainSingle(d => d.ServiceType == typeof(IConsumptionLedgerStore));
    }

    [Fact]
    public void RecordPublishesDirectlyWithNoOutbox()
    {
        var builder = Composed();
        builder.Services.AddRecordApplication();
        builder.Services.AddRecordInfrastructure(builder.Configuration);

        builder.Services.Should().NotContain(d => d.ServiceType.Name.Contains("Outbox", StringComparison.Ordinal)
            || (d.ImplementationType != null && d.ImplementationType.Name.Contains("Outbox", StringComparison.Ordinal)));
        builder.Services.Should().Contain(d => d.ServiceType == typeof(IEventPublisher));
    }

    private static ProfileOptions ProfileOptionsOf(HostApplicationBuilder builder)
    {
        builder.Services.AddProfileInfrastructure(builder.Configuration);
        using var services = builder.Services.BuildServiceProvider();
        return services.GetRequiredService<IOptions<ProfileOptions>>().Value;
    }

    private static HostApplicationBuilder Composed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(
            [new($"ConnectionStrings:{ConsumptionSchema.ConnectionName}", "Server=.;Database=x;Encrypt=True")]);
        builder.Services.AddBaseApplication();
        builder.Services.AddBaseInfrastructure(builder.Configuration);
        return builder;
    }

    private static IEnumerable<Type> HostedServicesOf(HostApplicationBuilder builder) =>
        builder.Services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType ?? d.ImplementationInstance?.GetType() ?? typeof(object));
}
