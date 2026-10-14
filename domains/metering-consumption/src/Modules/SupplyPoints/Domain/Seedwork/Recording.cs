using MeteringConsumption.SupplyPoints.Domain.ValueObjects;

namespace MeteringConsumption.SupplyPoints.Domain.Seedwork;

public static class Recording
{
    // Nothing is written when every value the statement states is already in effect on its date. Otherwise history keeps
    // what it stated on a row of its date, and current gets a row of that date only when it is on or after today's row
    // (the latest on or before today): a backdated statement reaches current only through the later rows that took its
    // values from earlier statements. Rows older than today's row then leave current.
    public static Recorded<TValues> Decide<TValues>(Dated<TValues> statement, DateOnly today, KeyFacts<TValues> facts)
        where TValues : class, ICarriedValues<TValues>
    {
        var date = statement.EffectiveDate;

        // A first statement on or before its date is kept even when it states no value: the key exists from then.
        var inEffect = InEffectOn(date, facts.Current, facts.StatedOnOrBefore);
        if (inEffect is not null && statement.Values.CarriedFrom(inEffect).Equals(inEffect))
        {
            return new Recorded<TValues>(RecordOutcomeKind.Unchanged, null, []);
        }

        var rows = facts.Current.ToDictionary(row => row.EffectiveDate, row => row.Values);
        if (TodaysRowDate(rows, today) is not { } todays || date >= todays)
        {
            rows[date] = statement.Values.CarriedFrom(inEffect);
        }

        foreach (var later in rows.Keys.Where(rowDate => rowDate > date).ToList())
        {
            rows[later] = statement.Values.NotStatedIn(StatedBetween(facts.StatedAfter, date, later)).CarriedFrom(rows[later]);
        }

        if (TodaysRowDate(rows, today) is { } todaysRow)
        {
            foreach (var older in rows.Keys.Where(rowDate => rowDate < todaysRow).ToList())
            {
                rows.Remove(older);
            }
        }

        // One history row per date: a second statement for it overwrites the values it states, the last one processed wins.
        return facts.HistoryOnDate is { } onDate
            ? new Recorded<TValues>(RecordOutcomeKind.Replaced, new Dated<TValues>(date, statement.Values.CarriedFrom(onDate.Values)),
                Writes(facts.Current, rows))
            : new Recorded<TValues>(RecordOutcomeKind.Added, statement, Writes(facts.Current, rows));
    }

    // From current when a current row starts on or before the day; otherwise each value is the latest history stated on
    // or before it. Null when nothing was stated by then.
    public static TValues? InEffectOn<TValues>(DateOnly day, IReadOnlyList<Dated<TValues>> current,
        IReadOnlyList<Dated<TValues>> statedOnOrBefore)
        where TValues : class, ICarriedValues<TValues>
    {
        if (current.Where(row => row.EffectiveDate <= day).MaxBy(row => row.EffectiveDate) is { } row)
        {
            return row.Values;
        }

        TValues? values = null;
        foreach (var stated in statedOnOrBefore.Where(row => row.EffectiveDate <= day).OrderByDescending(row => row.EffectiveDate))
        {
            values = values?.CarriedFrom(stated.Values) ?? stated.Values;
        }

        return values;
    }

    // The values some statement after the first date and on or before the second stated itself; a later row keeps those.
    private static TValues StatedBetween<TValues>(IReadOnlyList<Dated<TValues>> statedAfter, DateOnly after, DateOnly onOrBefore)
        where TValues : class, ICarriedValues<TValues>
    {
        var stated = TValues.None;
        foreach (var row in statedAfter.Where(row => row.EffectiveDate > after && row.EffectiveDate <= onOrBefore))
        {
            stated = row.Values.CarriedFrom(stated);
        }

        return stated;
    }

    private static DateOnly? TodaysRowDate<TValues>(Dictionary<DateOnly, TValues> rows, DateOnly today) =>
        rows.Keys.Where(rowDate => rowDate <= today).Select(rowDate => (DateOnly?)rowDate).Max();

    private static List<CurrentWrite<TValues>> Writes<TValues>(IReadOnlyList<Dated<TValues>> before, Dictionary<DateOnly, TValues> after)
        where TValues : class
    {
        var writes = new List<CurrentWrite<TValues>>();
        var was = before.ToDictionary(row => row.EffectiveDate, row => row.Values);
        foreach (var (rowDate, values) in after.OrderBy(row => row.Key))
        {
            if (!was.TryGetValue(rowDate, out var old))
            {
                writes.Add(new CurrentWrite<TValues>(CurrentWriteKind.Insert, new Dated<TValues>(rowDate, values)));
            }
            else if (!old.Equals(values))
            {
                writes.Add(new CurrentWrite<TValues>(CurrentWriteKind.Update, new Dated<TValues>(rowDate, values)));
            }
        }

        foreach (var row in before.Where(row => !after.ContainsKey(row.EffectiveDate)))
        {
            writes.Add(new CurrentWrite<TValues>(CurrentWriteKind.Delete, row));
        }

        return writes;
    }
}
