namespace Growth.Contracts.Events;

/// <summary>The tariff values that decide how a supply point's consumption is forecast, as the customer's tariff
/// states them. A null value means the tariff states none.</summary>
public sealed record TariffResolved : EventEnvelope
{
    public required string MPRN { get; init; }

    /// <summary>The market the supply point belongs to, as Metering &amp; Consumption names it (<c>roi-esb</c>).</summary>
    public required string Market { get; init; }

    /// <summary>Whether the tariff has 48 half-hourly rates.</summary>
    public bool? IsTou { get; init; }

    /// <summary>One of <see cref="TariffCalcTypes"/>.</summary>
    public string? TariffCalcType { get; init; }

    /// <summary>The load profile the tariff assigns, <c>LP1</c> to <c>LP6</c>.</summary>
    public string? EffectiveLoadProfileCode { get; init; }

    /// <summary>Who resolved the values, for example <c>v1-reader/tariff-values</c>.</summary>
    public required string Source { get; init; }
}
