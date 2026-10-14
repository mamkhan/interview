using MeteringConsumption.Contracts.Events.DTO;

namespace MeteringConsumption.Contracts.Events.Abstraction;

/// <summary>An event about one meter point: what a publisher needs to key it on the transport, by MPRN session
/// and reading identity, and to trace it to the market file or other source it came from.</summary>
public interface IEvent
{
    string MPRN { get; }

    string ReadingId { get; }

    string CorrelationId { get; }

    ReadingProvenance Provenance { get; }
}
