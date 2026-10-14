using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

// What one statement writes, so the repository writes exactly that: the history row it inserts or overwrites (none when
// unchanged), and the current rows it inserts, updates or deletes.
public sealed record Recorded<TValues>(RecordOutcomeKind Kind, Dated<TValues>? History, IReadOnlyList<CurrentWrite<TValues>> Current);
