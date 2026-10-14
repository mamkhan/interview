namespace Platform.Contracts.Events.V1;

/// <summary>V1's meter point codes and usage factors for one supply point, read by the V1 reader to replace what the
/// platform holds for them. <c>OccurredAtUtc</c> is when the reader's run started.</summary>
public sealed record SupplyPointLoadedFromV1 : EventEnvelope
{
    public required string MPRN { get; init; }

    /// <summary>The market the supply point belongs to, as Metering &amp; Consumption names it (<c>roi-esb</c>).</summary>
    public required string Market { get; init; }

    /// <summary>The date V1 holds for the codes and the estimated usage factors: that of the message or file it took them from.</summary>
    public required DateOnly CodesFrom { get; init; }

    public string? MeterConfigurationCode { get; init; }

    public string? LoadProfileCode { get; init; }

    public string? DuosGroup { get; init; }

    /// <summary>One entry per timeslot and date: the estimates from <see cref="CodesFrom"/>, the actuals from their own dates.</summary>
    public required IReadOnlyList<LoadedUsageFactor> UsageFactors { get; init; }
}

/// <summary>A timeslot's usage factors from the date they take effect. A null factor means V1 holds none.</summary>
public sealed record LoadedUsageFactor(string TimeslotCode, DateOnly EffectiveFromDate, decimal? EstimatedUsageFactor, decimal? ActualUsageFactor);
