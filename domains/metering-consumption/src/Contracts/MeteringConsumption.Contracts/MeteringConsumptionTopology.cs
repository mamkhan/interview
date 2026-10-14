using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts;

/// <summary>The names another module needs to consume this domain's published events, identical in every
/// environment. Subscriptions belong to the consuming module, never to the publisher's contracts.</summary>
public static class MeteringConsumptionTopology
{
    public static class Topics
    {
        public const string IntervalReadingReceived = "metr.interval-reading-received";

        public const string UsageFactorsReceived = "metr.usage-factors-received";

        public const string MeterConfigurationReceived = "metr.meter-configuration-received";

        public const string MeterConfigurationChangeReceived = "metr.meter-configuration-change-received";

        public const string ConsumptionRecorded = "metr.consumption-recorded";

        public const string ConsumptionForecastUpdated = "metr.consumption-forecast-updated";
    }

    /// <summary>Stores another component writes or reads besides the module that owns them.</summary>
    public static class Containers
    {
        /// <summary>The consumption ledger; its file format is MeteringConsumption.Contracts.Ledger.</summary>
        public const string ConsumptionLedger = "consumption-ledger";
    }

    /// <summary>Message properties promoted onto the transport so a subscription can route on a
    /// correlation filter without opening the body.</summary>
    public static class MessageProperties
    {
        public const string Market = "market";
    }

    /// <summary>Identity tags for the market transports this domain captures: the values of
    /// <see cref="ReadingProvenance.Market"/>.</summary>
    public static class Markets
    {
        public const string RoiEsb = "roi-esb";

        public const string RoiGni = "roi-gni";

        public const string NiNie = "ni-nie";

        public const string NiGas = "ni-gas";

        /// <summary>The fuel a market transport carries, or null for a market this domain does not know.</summary>
        public static string? FuelOf(string market) => market switch
        {
            RoiEsb or NiNie => Fuels.Electricity,
            RoiGni or NiGas => Fuels.Gas,
            _ => null,
        };
    }

    public static class Fuels
    {
        public const string Electricity = "electricity";

        public const string Gas = "gas";
    }
}
