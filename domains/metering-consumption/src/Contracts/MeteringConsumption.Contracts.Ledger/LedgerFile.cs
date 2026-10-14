using Parquet;
using Parquet.Serialization;

namespace MeteringConsumption.Contracts.Ledger;

/// <summary>A ledger file's rows written and read with the one set of Parquet options every writer uses, so a read
/// date stays a DATE.</summary>
public static class LedgerFile
{
    private static readonly ParquetSerializerOptions Options = new()
    {
        ParquetOptions = new ParquetOptions { UseDateOnlyTypeForDates = true },
    };

    public static async Task WriteAsync(IEnumerable<LedgerRow> rows, Stream stream, CancellationToken cancellationToken) =>
        await ParquetSerializer.SerializeAsync(rows, stream, Options, cancellationToken);

    public static async Task<IList<LedgerRow>> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        await ParquetSerializer.DeserializeAsync<LedgerRow>(stream, Options, cancellationToken);
}
