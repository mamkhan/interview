namespace MeteringConsumption.Consumption.Domain;

// One supply point's latest meter configuration and usage factors, each figure with the date it took effect
// and the file it came from, and the latest tariff values and smart meter flag with the moment they were read. A statement never removes a figure; Apply reports whether anything changed, so a
// repeat of a message already applied writes nothing.
public sealed class SupplyPointProfile
{
    private readonly Dictionary<UsageFactorSlot, UsageFactorStatement> _usageFactors = [];

    private SupplyPointProfile(string supplyPointId, string market)
    {
        SupplyPointId = supplyPointId;
        Market = market;
    }

    public string SupplyPointId { get; }

    public string Market { get; }

    public MeterConfigurationStatement? Configuration { get; private set; }

    public IReadOnlyDictionary<UsageFactorSlot, UsageFactorStatement> UsageFactors => _usageFactors;

    public TariffStatement? Tariff { get; private set; }

    public SmartMeterFlagStatement? SmartMeterFlag { get; private set; }

    public static SupplyPointProfile New(string supplyPointId, string market) => new(supplyPointId, market);

    // A profile as it was stored: every figure already passed the rules when it was applied.
    public static SupplyPointProfile Restore(
        string supplyPointId,
        string market,
        MeterConfigurationStatement? configuration,
        IEnumerable<UsageFactorStatement> usageFactors,
        TariffStatement? tariff = null,
        SmartMeterFlagStatement? smartMeterFlag = null)
    {
        var profile = new SupplyPointProfile(supplyPointId, market)
        {
            Configuration = configuration,
            Tariff = tariff,
            SmartMeterFlag = smartMeterFlag,
        };
        foreach (var factor in usageFactors)
        {
            profile._usageFactors[factor.Slot] = factor;
        }

        return profile;
    }

    // Not every message carries all four codes (an MM300 has no meter configuration code, an MM301 no load profile
    // or status), so each code a statement leaves empty keeps the one the profile has while the others move on.
    public bool Apply(MeterConfigurationStatement statement)
    {
        Require(statement.Source, "source");

        statement = statement with
        {
            MeterConfigurationCode = OrStored(statement.MeterConfigurationCode, Configuration?.MeterConfigurationCode),
            LoadProfileCode = OrStored(statement.LoadProfileCode, Configuration?.LoadProfileCode),
            DuosGroup = OrStored(statement.DuosGroup, Configuration?.DuosGroup),
            MeterPointStatusCode = OrStored(statement.MeterPointStatusCode, Configuration?.MeterPointStatusCode),
        };

        if (!Replaces(statement, Configuration, s => s.EffectiveFrom))
        {
            return false;
        }

        Configuration = statement;
        return true;
    }

    // A reading at the same moment or later replaces the tariff only when its values differ, so a nightly repeat
    // writes nothing and the stored AsOf stays the moment the current values first arrived. Empty values replace
    // stored ones: a supply point V1 holds no tariff for must show none.
    public bool Apply(TariffStatement statement)
    {
        if (Tariff is not null && (statement.AsOf < Tariff.AsOf || statement.StatesTheSameValuesAs(Tariff)))
        {
            return false;
        }

        Tariff = statement;
        return true;
    }

    // As for the tariff: a reading at the same moment or later replaces the flag only when it says otherwise.
    public bool Apply(SmartMeterFlagStatement statement)
    {
        if (SmartMeterFlag is not null && (statement.AsOf < SmartMeterFlag.AsOf || statement.IsNsmp == SmartMeterFlag.IsNsmp))
        {
            return false;
        }

        SmartMeterFlag = statement;
        return true;
    }

    public bool Apply(IReadOnlyCollection<UsageFactorStatement> statements)
    {
        var conflict = statements
            .Distinct()
            .GroupBy(statement => (statement.Slot, statement.EffectiveFrom))
            .FirstOrDefault(group => group.Count() > 1);
        if (conflict is not null)
        {
            throw new ProfileStatementRejectedException(
                $"The message for {SupplyPointId} states {conflict.Key.Slot.Timeslot} {conflict.Key.Slot.Kind} " +
                $"effective {conflict.Key.EffectiveFrom:yyyy-MM-dd} with {conflict.Count()} different values.");
        }

        var changed = false;
        foreach (var statement in statements)
        {
            if (!Replaces(statement, _usageFactors.GetValueOrDefault(statement.Slot), s => s.EffectiveFrom))
            {
                continue;
            }

            _usageFactors[statement.Slot] = statement;
            changed = true;
        }

        return changed;
    }

    // The latest version of a figure is the one with the latest effective date, whatever order or date the
    // files carry; on the same date the one applied last wins, so a re-sent correction replaces the original.
    private static bool Replaces<T>(T incoming, T? stored, Func<T, DateOnly> effectiveFrom)
        where T : class =>
        stored is null || (effectiveFrom(incoming) >= effectiveFrom(stored) && !incoming.Equals(stored));

    private static string? OrStored(string? stated, string? stored) => string.IsNullOrWhiteSpace(stated) ? stored : stated;

    private void Require(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProfileStatementRejectedException($"The statement for {SupplyPointId} has no {field}.");
        }
    }
}
