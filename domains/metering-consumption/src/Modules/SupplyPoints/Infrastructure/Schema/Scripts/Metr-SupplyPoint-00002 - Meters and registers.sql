-- The meters in place at each supply point and their registers, each a sequence of rows like the meter point's: a
-- meter's row runs from the date it was put in place, or first known, until it is removed; a register's row keeps the
-- read it opened with. A register names its meter by serial number, and is told from the meter's other registers by
-- RegisterKey: its sequence number, or its register type and unit for a smart meter's channel. Lengths and decimals
-- follow the ESB XSD 14.00.00.
CREATE TABLE [supply].[Meter] (
    [MeterId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [MeterCategoryCode] varchar(15) NULL,
    [MeterLocationCode] varchar(3) NULL,
    [ExchangedMeterReference] varchar(18) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Meter_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_Meter_UpdatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Meter] PRIMARY KEY ([MeterId]),
    CONSTRAINT [FK_Meter_SupplyPoint] FOREIGN KEY ([SupplyPointId]) REFERENCES [supply].[SupplyPoint] ([SupplyPointId]),
    CONSTRAINT [UQ_Meter_SupplyPointId_SerialNumber_StartDate] UNIQUE ([SupplyPointId], [SerialNumber], [StartDate]),
    CONSTRAINT [CK_Meter_EndDate] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);

-- The open row is the meter in place now, so a meter has at most one.
CREATE UNIQUE INDEX [UQ_Meter_SupplyPointId_SerialNumber_Open] ON [supply].[Meter] ([SupplyPointId], [SerialNumber]) WHERE [EndDate] IS NULL;

CREATE TABLE [supply].[RegisterLevelInfo] (
    [RegisterLevelInfoId] bigint IDENTITY(1, 1) NOT NULL,
    [SupplyPointId] bigint NOT NULL,
    [SerialNumber] varchar(9) NOT NULL,
    [RegisterKey] varchar(8) NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
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
    CONSTRAINT [UQ_RegisterLevelInfo_SupplyPointId_SerialNumber_RegisterKey_StartDate]
        UNIQUE ([SupplyPointId], [SerialNumber], [RegisterKey], [StartDate]),
    CONSTRAINT [CK_RegisterLevelInfo_EndDate] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate])
);

-- The same per register of a meter: each is its own sequence with at most one open row.
CREATE UNIQUE INDEX [UQ_RegisterLevelInfo_SupplyPointId_SerialNumber_RegisterKey_Open]
    ON [supply].[RegisterLevelInfo] ([SupplyPointId], [SerialNumber], [RegisterKey]) WHERE [EndDate] IS NULL;
