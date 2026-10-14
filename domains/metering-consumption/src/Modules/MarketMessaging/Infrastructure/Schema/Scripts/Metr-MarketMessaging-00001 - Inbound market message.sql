-- The inbound index: one row per market message the gateway has received, carrying the outcome its
-- handler reached. The receipt id is the natural key, derived from the file, so a retry lands on the row
-- the first attempt created.
CREATE TABLE [InboundMarketMessage] (
    [ReceiptId] varchar(64) NOT NULL,
    [Market] varchar(16) NOT NULL,
    [Fuel] varchar(16) NOT NULL,
    [FileName] nvarchar(260) NOT NULL,
    [ReceivedAt] datetimeoffset(3) NOT NULL,
    [Hash] varchar(64) NOT NULL,
    [BlobPath] varchar(400) NOT NULL,
    [CorrelationId] varchar(100) NOT NULL,
    [MessageType] varchar(16) NULL,
    [Outcome] varchar(16) NULL,
    [DuplicateOfReceiptId] varchar(64) NULL,
    [Handler] varchar(200) NULL,
    [ReadingsPublished] int NULL,
    [FailureReason] nvarchar(1000) NULL,
    [ProcessedAt] datetimeoffset(3) NULL,
    CONSTRAINT [PK_InboundMarketMessage] PRIMARY KEY ([ReceiptId])
);

-- The hash is how an operator finds the other arrivals of one file; no query path depends on it.
CREATE INDEX [IX_InboundMarketMessage_Hash] ON [InboundMarketMessage] ([Hash]);

CREATE INDEX [IX_InboundMarketMessage_Market_MessageType_ReceivedAt] ON [InboundMarketMessage] ([Market], [MessageType], [ReceivedAt]);

CREATE INDEX [IX_InboundMarketMessage_Outcome_ReceivedAt] ON [InboundMarketMessage] ([Outcome], [ReceivedAt]);
