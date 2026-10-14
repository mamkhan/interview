-- The one table of a second database whose scripts share this assembly with the suite's own, so a test can
-- tell which database a script reached.
CREATE TABLE [SiblingRow] (
    [Id] int NOT NULL,
    CONSTRAINT [PK_SiblingRow] PRIMARY KEY ([Id])
);
