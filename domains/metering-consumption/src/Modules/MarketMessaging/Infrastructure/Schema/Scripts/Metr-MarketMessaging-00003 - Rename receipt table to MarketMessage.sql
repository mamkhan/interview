-- The table is named for what it holds, one row per market message the module has received, rather than
-- for the direction it arrived from.
EXEC sp_rename N'dbo.InboundMarketMessage', N'MarketMessage';
EXEC sp_rename N'dbo.PK_InboundMarketMessage', N'PK_MarketMessage', N'OBJECT';
EXEC sp_rename N'dbo.MarketMessage.IX_InboundMarketMessage_Hash', N'IX_MarketMessage_Hash', N'INDEX';
EXEC sp_rename N'dbo.MarketMessage.IX_InboundMarketMessage_Market_MessageType_ReceivedAt', N'IX_MarketMessage_Market_MessageType_ReceivedAt', N'INDEX';
EXEC sp_rename N'dbo.MarketMessage.IX_InboundMarketMessage_Outcome_ReceivedAt', N'IX_MarketMessage_Outcome_ReceivedAt', N'INDEX';
