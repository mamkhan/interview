-- One row per forecast version of a supply point: the daily kWh from the anchor date and every input that produced
-- them, as JSON. A recompute is a new version; exactly one version per supply point is current, and a recorded
-- reading triggers at most one version, so a redelivery finds the version it already made.
CREATE TABLE [ConsumptionForecast] (
    [SupplyPointId] varchar(16) NOT NULL,
    [ForecastVersion] bigint NOT NULL,
    [ForecastId] varchar(40) NOT NULL,
    [ComputedAtUtc] datetimeoffset(3) NOT NULL,
    [AnchorDate] date NOT NULL,
    [HorizonDays] smallint NOT NULL,
    [TotalKwh] decimal(18,6) NOT NULL,
    [UsageFactorSource] varchar(8) NOT NULL,
    [TriggerLedgerEntryId] varchar(200) NOT NULL,
    [IsCurrent] bit NOT NULL,
    [Daily] varchar(max) NOT NULL,
    [Inputs] varchar(max) NOT NULL,
    CONSTRAINT [PK_ConsumptionForecast] PRIMARY KEY ([SupplyPointId], [ForecastVersion])
);

CREATE UNIQUE INDEX [UQ_ConsumptionForecast_SupplyPointId_Current] ON [ConsumptionForecast] ([SupplyPointId])
    WHERE [IsCurrent] = 1;

CREATE UNIQUE INDEX [UQ_ConsumptionForecast_SupplyPointId_TriggerLedgerEntryId]
    ON [ConsumptionForecast] ([SupplyPointId], [TriggerLedgerEntryId]);

CREATE INDEX [IX_ConsumptionForecast_Superseded_ComputedAtUtc] ON [ConsumptionForecast] ([ComputedAtUtc])
    WHERE [IsCurrent] = 0;
