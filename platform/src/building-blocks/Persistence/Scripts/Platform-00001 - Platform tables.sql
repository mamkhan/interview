-- The tables every module's unit of work relies on, created in the module's own database ahead of the
-- module's own scripts: the outbox, the inbox and the client-request log.
CREATE TABLE [OutboxMessage] (
    [Id] uniqueidentifier NOT NULL,
    [EventType] nvarchar(400) NOT NULL,
    [Payload] nvarchar(max) NOT NULL,
    [CorrelationId] nvarchar(100) NOT NULL,
    [MessageId] nvarchar(200) NOT NULL,
    [SessionId] nvarchar(128) NULL,
    [OccurredAtUtc] datetimeoffset(3) NOT NULL,
    [State] int NOT NULL,
    [PublishedAtUtc] datetimeoffset(3) NULL,
    CONSTRAINT [PK_OutboxMessage] PRIMARY KEY ([Id])
);

-- The relay reads pending rows oldest first, so the state predicate is the one that has to be indexed.
CREATE INDEX [IX_OutboxMessage_State] ON [OutboxMessage] ([State]);

CREATE TABLE [ProcessedMessage] (
    [Consumer] nvarchar(200) NOT NULL,
    [MessageId] nvarchar(200) NOT NULL,
    [ProcessedAtUtc] datetimeoffset(3) NOT NULL,
    CONSTRAINT [PK_ProcessedMessage] PRIMARY KEY ([Consumer], [MessageId])
);

CREATE TABLE [ClientRequest] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [ReceivedAtUtc] datetimeoffset(3) NOT NULL,
    [ResultJson] nvarchar(max) NULL,
    CONSTRAINT [PK_ClientRequest] PRIMARY KEY ([Id])
);
