using Growth.LocalGraph;
using MeteringConsumption.LocalGraph;
using Platform.LocalGraph;

// The whole platform: the shared fixtures, then every domain's graph composed from that domain's own
// LocalGraph methods. Nothing is wired here that a domain AppHost does not wire the same way.
var builder = DistributedApplication.CreateBuilder(args);

var platform = builder.AddPlatformFixtures();

var growth = builder.AddGrowthFixtures(platform);

var metering = builder.AddMeteringConsumptionFixtures(platform, growth);
builder.AddReceiverHost<Projects.MarketMessaging_Receiver>(metering);
builder.AddMeteringConsumptionHost<Projects.MeteringConsumption_Host>(metering);
builder.AddEsbDropBlobEvents<Projects.LocalBlobEvents>(metering);


builder.Build().Run();
