namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

// One key's history and current rows held in memory, recorded by the same decision as the tables.
public sealed class KeyRows<TValues>
    where TValues : class, ICarriedValues<TValues>
{
    private readonly SortedDictionary<DateOnly, TValues> _history = [];
    private readonly SortedDictionary<DateOnly, TValues> _current = [];

    public IReadOnlyList<Dated<TValues>> History => [.. _history.Select(row => new Dated<TValues>(row.Key, row.Value))];

    public IReadOnlyList<Dated<TValues>> Current => [.. _current.Select(row => new Dated<TValues>(row.Key, row.Value))];

    public Recorded<TValues> Record(Dated<TValues> statement, DateOnly today)
    {
        var recorded = Recording.Decide(statement, today, FactsAround(statement.EffectiveDate));
        Apply(recorded);
        return recorded;
    }

    // The whole history either side of the date: more rows than the repository reads, which changes no decision.
    public KeyFacts<TValues> FactsAround(DateOnly date) => new()
    {
        Current = Current,
        HistoryOnDate = _history.TryGetValue(date, out var onDate) ? new Dated<TValues>(date, onDate) : null,
        StatedOnOrBefore = [.. History.Where(row => row.EffectiveDate <= date)],
        StatedAfter = [.. History.Where(row => row.EffectiveDate > date)],
    };

    public void Apply(Recorded<TValues> recorded)
    {
        if (recorded.History is { } history)
        {
            _history[history.EffectiveDate] = history.Values;
        }

        foreach (var write in recorded.Current)
        {
            if (write.Kind == CurrentWriteKind.Delete)
            {
                _current.Remove(write.Row.EffectiveDate);
            }
            else
            {
                _current[write.Row.EffectiveDate] = write.Row.Values;
            }
        }
    }
}
