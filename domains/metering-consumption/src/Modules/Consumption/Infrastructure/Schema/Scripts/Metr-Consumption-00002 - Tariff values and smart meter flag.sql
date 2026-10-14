-- The tariff values the customer's tariff states (whether it has 48 rates, the curve that projects a smart
-- meter's usage, the load profile it assigns) and V1's smart meter flag, each group with the moment it was read
-- and who stated it. Every column is NULL until its group first arrives.
ALTER TABLE [SupplyPointProfile] ADD
    [IsTou] bit NULL,
    [TariffCalcType] varchar(16) NULL,
    [EffectiveLoadProfileCode] varchar(8) NULL,
    [TariffAsOf] datetimeoffset(3) NULL,
    [TariffSource] varchar(64) NULL,
    [IsNsmp] bit NULL,
    [SmartMeterFlagAsOf] datetimeoffset(3) NULL,
    [SmartMeterFlagSource] varchar(64) NULL;
