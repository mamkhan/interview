-- The one row table a test handler writes to, so the transaction boundary is proved on a table rather
-- than on a fixture. It stands in for a module's own schema in these suites.
CREATE TABLE [WrittenRow] (
    [ContentHash] varchar(64) NOT NULL,
    CONSTRAINT [PK_WrittenRow] PRIMARY KEY ([ContentHash])
);
