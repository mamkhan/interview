using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using BuildingBlocks.Messaging;
using BuildingBlocks.TestsShared;
using Dapper;
using MeteringConsumption.Contracts;
using MeteringConsumption.Contracts.Events.MarketMessaging;
using MeteringConsumption.MarketMessaging.Application.Features.Audit.Abstraction;
using MeteringConsumption.MarketMessaging.Infrastructure.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MeteringConsumption.MarketMessaging.IntegrationTests.Scenarios;

namespace MeteringConsumption.MarketMessaging.IntegrationTests;

// The Audit role booted through the module's own registration: a receipt published on the bus becomes a row. That the
// same receipt delivered again stays one row is the repository's and the handler's to show.
public sealed class WhenAReceiptArrivesOnTheBus(ReceiptArrivedOnTheBusScenario arrived)
    : IClassFixture<ReceiptArrivedOnTheBusScenario>
{
    [Fact]
    public void WritesTheReceiptRow()
    {
        arrived.Row.Should().NotBeNull();
        arrived.Row!.Hash.Should().Be("5bb7202821a6c2e2e6ea6baec50e0e8408c6670fc94cba9c838a888bed67c724");
        arrived.Row.FileName.Should().Be("DSO_MIM_343_1782352213415_3805737.xml");
        arrived.Row.Outcome.Should().BeNull();
    }
}
