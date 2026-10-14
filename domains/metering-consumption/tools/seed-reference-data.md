# Seed dev's reference-data database

Until the process that owns the market reference data exists, dev's `reference-data` database is seeded by hand from
V1, for the dates a test or a run needs. Production is never seeded this way.

The forecast reads, for each load profile, one row per date and hour (`dbo.LoadProfileHourlyValue`) and, for each
DUoS group, one row per year (`dbo.AnnualAverageUsage`). The first two steps read V1's reporting server, read-only;
show each query to whoever owns V1 access before running it.

## 1. Export the hourly load-profile values from V1

Each hour is the sum of its four quarter-hours, cast to `decimal(12,10)` as V1 does. Export one row per profile, date
and hour, with the 24H, 00D and 00N values in that order, as comma-separated text. Set the date range to the forecast
window and horizon you need. A curve a profile does not have is written as an empty field, which `bcp` loads as NULL;
`sqlcmd` would otherwise write the text `NULL`, which `bcp` refuses for a decimal column.

```bash
sqlcmd -S <v1-reporting-server> -d <v1-database> -E -C -K ReadOnly -l 30 -t 600 -W -s "," -h -1 \
  -i load-profile-hours.sql -o load-profile-hours.csv
```

The smart profiles LP25, LP27 and LP29 have no 24H curve in V1: V1 blends their 01D, 01N and 01P curves. That blend
is not reproduced here; seed them only when a test needs them, from the same blend.

## 2. Export the annual average usage

Export one row per DUoS group for the year: the group, the year and the 24H, 00D and 00N annual average usage.

## 3. Load them into dev

Signed in as the dev deployer principal, which is the server's Entra admin:

```bash
bcp "dbo.LoadProfileHourlyValue" in load-profile-hours.csv -S acmdevmetrsqlmain.database.windows.net \
  -d reference-data -G -c -t "," -q
```

and insert the averages with an `INSERT ... VALUES` per DUoS group. Rows already there are kept; delete a profile's
dates first to replace them.
