-- Creates the host's database user for its user-assigned managed identity and grants the data
-- roles. The SID form (client id as binary) avoids the Entra directory lookup that a
-- service-principal admin is not permitted to make. Idempotent: safe to re-run every deploy.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE [name] = '$(IdentityName)')
BEGIN
    CREATE USER [$(IdentityName)] WITH SID = $(IdentitySid), TYPE = E;
END;

IF IS_ROLEMEMBER('db_datareader', '$(IdentityName)') = 0
    ALTER ROLE db_datareader ADD MEMBER [$(IdentityName)];

IF IS_ROLEMEMBER('db_datawriter', '$(IdentityName)') = 0
    ALTER ROLE db_datawriter ADD MEMBER [$(IdentityName)];
