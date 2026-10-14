-- Each kind of fact under a supply point gets two tables keyed by the date a value takes effect, in place of one table of
-- non-overlapping periods. A history row holds only the values one statement stated, a value it left out being NULL. A
-- current row holds every value as in effect from its date, each the one the latest-dated statement stating it gave;
-- the current table keeps the row in effect today and every later row, and a period ends the day before the next row's
-- date. The networks reference number is a meter point value like its codes, and a register's read is one of its values.
-- The periods' rows are dropped, not converted: they cannot tell a value a statement stated from one it carried. Lengths
-- and decimals follow the ESB XSD 14.00.00.
DROP TABLE [supply].[RegisterLevelInfo];
DROP TABLE [supply].[Meter];
DROP TABLE [supply].[UsageFactor];
DROP TABLE [supply].[MeterPoint];

CREATE TABLE [supply].[MeterPointHistory] (
    [MeterPointHistoryId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [EffectiveDate] date NOT NULL,
    [MeterConfigurationCode] varchar(10) NULL,
    [LoadProfileCode] varchar(3) NULL,
    [DuosGroup] varchar(4) NULL,
    [MeterPointStatusCode] varchar(2) NULL,
    [NetworksReferenceNumber] varchar(35) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterPointHistory_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeterPointHistory] PRIMARY KEY ([MeterPointHistoryId]),
    CONSTRAINT [FK_MeterPointHistory_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_MeterPointHistory_SupplyPointId_EffectiveDate] UNIQUE ([SupplyPointId], [EffectiveDate])
);

CREATE TABLE [supply].[MeterPoint] (
    [MeterPointId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [EffectiveDate] date NOT NULL,
    [MeterConfigurationCode] varchar(10) NULL,
    [LoadProfileCode] varchar(3) NULL,
    [DuosGroup] varchar(4) NULL,
    [MeterPointStatusCode] varchar(2) NULL,
    [NetworksReferenceNumber] varchar(35) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterPoint_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterPoint_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeterPoint] PRIMARY KEY ([MeterPointId]),
    CONSTRAINT [FK_MeterPoint_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_MeterPoint_SupplyPointId_EffectiveDate] UNIQUE ([SupplyPointId], [EffectiveDate])
);

CREATE TABLE [supply].[UsageFactorHistory] (
    [UsageFactorHistoryId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [TimeslotCode] varchar(10) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [EstimatedUsageFactor] decimal(15,8) NULL,
    [ActualUsageFactor] decimal(15,8) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_UsageFactorHistory_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_UsageFactorHistory] PRIMARY KEY ([UsageFactorHistoryId]),
    CONSTRAINT [FK_UsageFactorHistory_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_UsageFactorHistory_SupplyPointId_TimeslotCode_EffectiveDate] UNIQUE ([SupplyPointId], [TimeslotCode], [EffectiveDate])
);

CREATE TABLE [supply].[UsageFactor] (
    [UsageFactorId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [TimeslotCode] varchar(10) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [EstimatedUsageFactor] decimal(15,8) NULL,
    [ActualUsageFactor] decimal(15,8) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_UsageFactor_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_UsageFactor_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_UsageFactor] PRIMARY KEY ([UsageFactorId]),
    CONSTRAINT [FK_UsageFactor_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_UsageFactor_SupplyPointId_TimeslotCode_EffectiveDate] UNIQUE ([SupplyPointId], [TimeslotCode], [EffectiveDate])
);

-- A register names its meter by serial number, and is told from the meter's other registers by RegisterKey: its
-- sequence number, or its register type and unit for a smart meter's channel. Every statement of a register carries its
-- type and unit, so a history row always holds them.
CREATE TABLE [supply].[MeterHistory] (
    [MeterHistoryId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [MeterCategoryCode] varchar(15) NULL,
    [MeterLocationCode] varchar(3) NULL,
    [ExchangedMeterReference] varchar(18) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_MeterHistory_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_MeterHistory] PRIMARY KEY ([MeterHistoryId]),
    CONSTRAINT [FK_MeterHistory_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_MeterHistory_SupplyPointId_SerialNumber_EffectiveDate] UNIQUE ([SupplyPointId], [SerialNumber], [EffectiveDate])
);

CREATE TABLE [supply].[Meter] (
    [MeterId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [MeterCategoryCode] varchar(15) NULL,
    [MeterLocationCode] varchar(3) NULL,
    [ExchangedMeterReference] varchar(18) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Meter_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Meter_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Meter] PRIMARY KEY ([MeterId]),
    CONSTRAINT [FK_Meter_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_Meter_SupplyPointId_SerialNumber_EffectiveDate] UNIQUE ([SupplyPointId], [SerialNumber], [EffectiveDate])
);

CREATE TABLE [supply].[RegisterLevelInfoHistory] (
    [RegisterLevelInfoHistoryId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [RegisterKey] varchar(8) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [TimeslotCode] varchar(10) NULL,
    [RegisterTypeCode] varchar(2) NOT NULL,
    [UOM_Code] varchar(3) NOT NULL,
    [MeterMultiplier] decimal(12,5) NULL,
    [MeteringInterval] int NULL,
    [PreDecimalDetails] varchar(2) NULL,
    [PostDecimalDetails] varchar(1) NULL,
    [ReadingValue] decimal(15,3) NULL,
    [ReadTypeCode] varchar(2) NULL,
    [ReadReasonCode] varchar(2) NULL,
    [ReadStatusCode] varchar(4) NULL,
    [PreviousReadDate] date NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_RegisterLevelInfoHistory_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_RegisterLevelInfoHistory] PRIMARY KEY ([RegisterLevelInfoHistoryId]),
    CONSTRAINT [FK_RegisterLevelInfoHistory_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_RegisterLevelInfoHistory_SupplyPointId_SerialNumber_RegisterKey_EffectiveDate]
        UNIQUE ([SupplyPointId], [SerialNumber], [RegisterKey], [EffectiveDate])
);

CREATE TABLE [supply].[RegisterLevelInfo] (
    [RegisterLevelInfoId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [RegisterKey] varchar(8) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [TimeslotCode] varchar(10) NULL,
    [RegisterTypeCode] varchar(2) NOT NULL,
    [UOM_Code] varchar(3) NOT NULL,
    [MeterMultiplier] decimal(12,5) NULL,
    [MeteringInterval] int NULL,
    [PreDecimalDetails] varchar(2) NULL,
    [PostDecimalDetails] varchar(1) NULL,
    [ReadingValue] decimal(15,3) NULL,
    [ReadTypeCode] varchar(2) NULL,
    [ReadReasonCode] varchar(2) NULL,
    [ReadStatusCode] varchar(4) NULL,
    [PreviousReadDate] date NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_RegisterLevelInfo_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_RegisterLevelInfo_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_RegisterLevelInfo] PRIMARY KEY ([RegisterLevelInfoId]),
    CONSTRAINT [FK_RegisterLevelInfo_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_RegisterLevelInfo_SupplyPointId_SerialNumber_RegisterKey_EffectiveDate]
        UNIQUE ([SupplyPointId], [SerialNumber], [RegisterKey], [EffectiveDate])
);
