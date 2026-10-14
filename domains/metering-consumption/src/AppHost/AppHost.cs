using Growth.LocalGraph;
using MeteringConsumption.LocalGraph;
using Platform.LocalGraph;

// This domain alone: the team's daily `aspire run`. The platform AppHost composes the same graph
// methods beside every other domain's, so what runs here is exactly what runs there.
var builder = DistributedApplication.CreateBuilder(args);

var platform = builder.AddPlatformFixtures();
var growth = builder.AddGrowthFixtures(platform);
var metering = builder.AddMeteringConsumptionFixtures(platform, growth);

builder.AddReceiverHost<Projects.MarketMessaging_Receiver>(metering);
builder.AddMeteringConsumptionHost<Projects.MeteringConsumption_Host>(metering);
builder.AddEsbDropBlobEvents<Projects.LocalBlobEvents>(metering);

builder.Build().Run();
