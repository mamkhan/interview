-- The gateway no longer decides that an arrival repeats an earlier one, so no outcome can name another
-- receipt. Two arrivals of the same bytes are two processed rows sharing one hash, and the consumer
-- discards the repeat by reading identity.
ALTER TABLE [InboundMarketMessage] DROP COLUMN [DuplicateOfReceiptId];
