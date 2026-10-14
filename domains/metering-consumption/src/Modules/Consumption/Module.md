# Consumption

The consumption side of Metering & Consumption: what each supply point uses, and what it is expected to use.

Record keeps every version of every meter-day's half-hourly readings, in full, in an append-only ledger in the data
lake. A later version cancels the one it replaces and posts itself in full, so a half-hour's rows always sum to the
latest version; a repeated or older version posts nothing. Profile keeps one row per supply point with the latest
meter configuration and usage factors a market message states, each keeping the file it came from, and the latest
tariff values and V1's smart meter flag, each keeping when it was read. Forecast recomputes a supply point's 62-day
forecast at each recorded reading from its recent ledger days, what SupplyPoints states is in effect, the tariff
values and the market reference data, keeping each version with what it was made from. Owned facts: the consumption
ledger; the profile and forecast tables in `metering-consumption`. Read only: `reference-data`. Published contracts:
each recorded version with its cancellation, each forecast, and the ledger's file format, through which the V1 reader
writes V1's history days, create-only. Non-goals: parsing market messages, deciding a customer's tariff or which
supply points are in scope, the supply window, loading reference data, estimating missing days, ledger read models.
