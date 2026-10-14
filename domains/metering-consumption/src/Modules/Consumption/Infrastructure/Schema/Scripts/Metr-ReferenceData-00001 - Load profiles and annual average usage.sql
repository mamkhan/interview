-- The market reference data a forecast reads, in its own database: another process writes the rows.
-- One hour of one load profile on one day, as a share of a typical year's use: H24 for the whole day, D00 and N00 for
-- the day and night bands. A smart profile (LP25, LP27, LP29) carries its blended curve in H24 and no bands. Names
-- are V1's (LP1, LP25).
CREATE TABLE [LoadProfileHourlyValue] (
    [LoadProfileCode] varchar(8) NOT NULL,
    [ValueDate] date NOT NULL,
    [Hour] tinyint NOT NULL,
    [H24] decimal(12,10) NULL,
    [D00] decimal(12,10) NULL,
    [N00] decimal(12,10) NULL,
    CONSTRAINT [PK_LoadProfileHourlyValue] PRIMARY KEY ([LoadProfileCode], [ValueDate], [Hour]),
    CONSTRAINT [CK_LoadProfileHourlyValue_Hour] CHECK ([Hour] BETWEEN 0 AND 23)
);

-- A DUoS group's average annual use, in kWh, for each timeslot in one year.
CREATE TABLE [AnnualAverageUsage] (
    [DuosGroup] varchar(8) NOT NULL,
    [Year] smallint NOT NULL,
    [Aau24H] decimal(18,6) NULL,
    [Aau00D] decimal(18,6) NULL,
    [Aau00N] decimal(18,6) NULL,
    CONSTRAINT [PK_AnnualAverageUsage] PRIMARY KEY ([DuosGroup], [Year])
);
