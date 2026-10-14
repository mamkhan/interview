namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

// What one key holds around a statement's date: its current rows, and from its history the row on that date, the rows
// that give each value its latest statement on or before the date, and those that give each value its first statement
// after it. More history rows than those change no decision.
public sealed record KeyFacts<TValues>
{
    public required IReadOnlyList<Dated<TValues>> Current { get; init; }

    public Dated<TValues>? HistoryOnDate { get; init; }

    public required IReadOnlyList<Dated<TValues>> StatedOnOrBefore { get; init; }

    public required IReadOnlyList<Dated<TValues>> StatedAfter { get; init; }
}
