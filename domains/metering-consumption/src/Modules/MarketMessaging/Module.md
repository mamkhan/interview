# MarketMessaging

business description... needs update.

## Diagram

LEGEND   ──▶ data or event flow     sub = Service Bus subscription    

═════════════════════════════════════════════════════════════════════════════════════════════════════
 OPERATOR  (ops-owned, no platform code)
═════════════════════════════════════════════════════════════════════════════════════════════════════

  ESB EMMA ──▶ GoAnywhere MFT ──upload──┐
                                        │
═════════════════════════════════════════════════════════════════════════════════════════════════════
 AZURE · metering-consumption domain
═════════════════════════════════════════════════════════════════════════════════════════════════════
                                        ▼
                          Blob container  esb-drop
                                        │ BlobCreated, sent by an Event Grid system topic
                                        ▼
                          Queue  metr.receive-blob-created
                                        │
                                        ▼
  ┌─ Receive  (host MarketMessaging.Receiver) ──────────────────┐
  │  1. read the file from esb-drop                             │
  │  2. store it once, named by its content hash ───────────────┼──▶ Blob  market-message-archive
  │  3. publish MarketMessageReceived                           │        {market}/{hash}.xml
  │  4. delete the file from esb-drop                           │        write-once, kept forever
  └──────────────────────────────┬──────────────────────────────┘
                                 ▼
                    Topic  metr.market-message-received
                                 │
          ┌──────────────────────┴───────────────────────────────┐
          │ sub metr.interpret-market-message-received           │ sub metr.audit-market-message-received
          ▼                                                      ▼
  ┌─ Interpret  (host MeteringConsumption.Host) ─┐       ┌─ Audit  (host MeteringConsumption.Host) ─┐
  │  read the file from the archive              │       │  insert one row per received file        │
  │  classify it by its header → message type    │       │  write the outcome onto that row         │
  │  pick the interpreter for (market, type)     │       └──────────▲──────────────────────┬────────┘
  │    (roi-esb, 343) → MM343Strategy            │                  │                      │ SQL
  │    (roi-esb, 320) → MM320Strategy            │                  │                      │
  │    (roi-esb, 332) → MM332Strategy            │                  │                      │
  │    other types and markets                   │                  │                      ▼
  │  publish the events, then one outcome        │                  │     Azure SQL  metering-consumption
  └──────┬──────────────────────────────┬────────┘                  │       table MarketMessage
         │ events                       │ outcome                   │
         │                              ▼                           │
         │         Topic  metr.market-message-processed  ───────────┤ sub metr.audit-market-message-processed
         │         Topic  metr.market-message-failed     ───────────┘ sub metr.audit-market-message-failed
         │           outcome: processed, no-interpreter or failed
         ▼
  Topic  metr.interval-reading-received       event per MPRN-day, from 343
         │ sub metr.record-interval-reading-received
  Topic  metr.usage-factors-received          event per MPRN, from 320 and 332
         │ sub metr.profile-usage-factors-received
  Topic  metr.meter-configuration-received    event per MPRN, from 320 and 332
         │ sub metr.profile-meter-configuration-received
         ▼
  Consumption module

  Topic  metr.meter-configuration-change-received   each file's whole configuration, from every
         │                                          configuration type and 331
         │ sub metr.meter-configuration-change-received
         ▼
  SupplyPoints module
