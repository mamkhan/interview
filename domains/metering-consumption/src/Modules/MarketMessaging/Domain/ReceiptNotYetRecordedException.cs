namespace MeteringConsumption.MarketMessaging.Domain;

// Not a DomainException: the outcome overtaking its receipt is transient, so this abandons the message for
// redelivery. Deriving it from DomainException would dead-letter the outcome on first delivery instead.
public sealed class ReceiptNotYetRecordedException(string receiptId)
    : Exception($"Receipt {receiptId} has no row yet; the outcome is retried until the receipt is recorded.");
