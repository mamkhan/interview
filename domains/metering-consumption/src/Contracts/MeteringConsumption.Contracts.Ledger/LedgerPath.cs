using System.Globalization;

namespace MeteringConsumption.Contracts.Ledger;

/// <summary>The ledger's file names: one folder per supply point, a file per read date and version. The underscore
/// ends a day's prefix, so listing one day never returns another.</summary>
public static class LedgerPath
{
    // On the hierarchical namespace a listing walks every folder it meets, so a folder per day would slow every read
    // of a supply point's history as it grows.
    public static string File(string supplyPointId, DateOnly readDate, int rrvn) =>
        string.Create(CultureInfo.InvariantCulture, $"{DayPrefix(supplyPointId, readDate)}{rrvn}.parquet");

    public static string DayPrefix(string supplyPointId, DateOnly readDate) =>
        string.Create(CultureInfo.InvariantCulture, $"{supplyPointId}/{readDate:yyyy-MM-dd}_");

    /// <summary>The folder that holds every entry of a supply point.</summary>
    public static string SupplyPointFolder(string supplyPointId) => $"{supplyPointId}/";

    /// <summary>The prefix every entry of a supply point read in that month starts with; dates are fixed width.</summary>
    public static string MonthPrefix(string supplyPointId, DateOnly month) =>
        string.Create(CultureInfo.InvariantCulture, $"{supplyPointId}/{month:yyyy-MM}-");

    /// <summary>The read date and version a file's name states; false for a name this layout does not produce.</summary>
    public static bool TryParse(string name, out DateOnly readDate, out int rrvn)
    {
        readDate = default;
        rrvn = 0;
        var file = name[(name.LastIndexOf('/') + 1)..];
        var separator = file.IndexOf('_', StringComparison.Ordinal);
        return separator > 0
            && file.EndsWith(".parquet", StringComparison.Ordinal)
            && DateOnly.TryParseExact(file[..separator], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out readDate)
            && int.TryParse(file[(separator + 1)..^".parquet".Length], NumberStyles.None, CultureInfo.InvariantCulture, out rrvn);
    }
}
