-- SupplyPoints' tables sit in their own schema of the domain database. One row per supply point, and under it a
-- sequence per kind of fact ESB states: the meter point's codes, and each timeslot's usage factors. A row runs from
-- StartDate to EndDate, both days included; the open row has no EndDate. A meter point row keeps the networks
-- reference number of the message that stated it. Lengths and decimals follow the ESB XSD 14.00.00.
CREATE SCHEMA [supply];
GO

CREATE TABLE [supply].[SupplyPoint] (
    [SupplyPointId] bigint IDENTITY(1, 1) NOT NULL,
    [XPRN] varchar(11) NOT NULL,
    [Market] varchar(16) NOT NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_SupplyPoint_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_SupplyPoint] PRIMARY KEY ([SupplyPointId]),
    CONSTRAINT [UQ_SupplyPoint_XPRN] UNIQUE ([XPRN])
);

CREATE TABLE [supply].[MeterPoint] (
    [MeterPointId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [ReadDate] date NULL,
    [MeterConfigurationCode] varchar(10) NULL,
    [LoadProfileCode] varchar(3) NULL,
    [DuosGroup] varchar(4) NULL,
    [MeterPointStatusCode] varchar(2) NULL,
    [NetworksReferenceNumber] varchar(35) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterPoint_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterPoint_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeterPoint] PRIMARY KEY ([MeterPointId]),
    CONSTRAINT [FK_MeterPoint_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_MeterPoint_SupplyPointId_StartDate] UNIQUE ([SupplyPointId], [StartDate]),
    CONSTRAINT [CK_MeterPoint_EndDate] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);

-- The open row is the one in effect from now on, so a supply point has at most one.
CREATE UNIQUE INDEX [UQ_MeterPoint_SupplyPointId_Open] ON [supply].[MeterPoint] ([SupplyPointId]) WHERE [EndDate] IS NULL;

CREATE TABLE [supply].[UsageFactor] (
    [UsageFactorId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [TimeslotCode] varchar(10) NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [EstimatedUsageFactor] decimal(15,8) NULL,
    [ActualUsageFactor] decimal(15,8) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_UsageFactor_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_UsageFactor_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_UsageFactor] PRIMARY KEY ([UsageFactorId]),
    CONSTRAINT [FK_UsageFactor_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_UsageFactor_SupplyPointId_TimeslotCode_StartDate] UNIQUE ([SupplyPointId], [TimeslotCode], [StartDate]),
    CONSTRAINT [CK_UsageFactor_EndDate] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);

-- The same per timeslot: each timeslot is its own sequence with at most one open row.
CREATE UNIQUE INDEX [UQ_UsageFactor_SupplyPointId_TimeslotCode_Open] ON [supply].[UsageFactor] ([SupplyPointId], [TimeslotCode]) WHERE [EndDate] IS NULL;
