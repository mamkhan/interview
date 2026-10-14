-- The column names the interpreter that reached the outcome, the word the Interpret feature uses for it.
-- Rows already written take the same words, so a filter on an outcome or an interpreter finds all of them.
EXEC sp_rename N'dbo.MarketMessage.Handler', N'Interpreter', N'COLUMN';
GO

UPDATE [MarketMessage] SET [Outcome] = 'no-interpreter' WHERE [Outcome] = 'no-handler';
UPDATE [MarketMessage] SET [Interpreter] = 'MM343Strategy' WHERE [Interpreter] = 'Mm343Handler';
