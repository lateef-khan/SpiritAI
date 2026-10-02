/*
================================================================================
 SpiritAI logins and roles on Sage 100 (SPIRITSRV-021, database MAS_SFC)

 Run as a sysadmin. sqlcmd reads the three passwords from environment variables, so
 they never appear on a command line:
   $env:DabPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_dab')).Password
   $env:ManagerPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_manager')).Password
   $env:AdminPassword = [Net.NetworkCredential]::new('', (Read-Host -AsSecureString 'spiritai_admin')).Password
   sqlcmd -S SPIRITSRV-021 -E -C -b -i logins.sql
   Remove-Item Env:DabPassword, Env:ManagerPassword, Env:AdminPassword
 Safe to run again: a second run sets each password to the value given, unlocks
 the login, and denies any cost column added since.

 spiritai_dab      DAB's login. EXECUTE on schema agent and nothing else; the
                   procedures read the tables through ownership chaining.
 spiritai_manager  the manager agent's shell. Reads every table except the
                   cost columns.
 spiritai_admin    the admin agent's shell. Reads every table.

 spiritai_reader   member of db_datareader.
 spiritai_no_cost  DENY SELECT on every column whose name contains Cost, and on
                   every view or table-valued function that reads one, because
                   a view can rename a column.
 AgentWorker is Cursor's login on SPIRITSRV-024 and is not touched here. Sage owns
 every other object in MAS_SFC; we add only the schemas core and agent.
================================================================================
*/

USE [master];
GO

IF SUSER_ID(N'spiritai_dab') IS NULL
    CREATE LOGIN spiritai_dab WITH PASSWORD = N'$(DabPassword)',
        CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [MAS_SFC];
ELSE
    ALTER LOGIN spiritai_dab WITH PASSWORD = N'$(DabPassword)' UNLOCK;
GO

IF SUSER_ID(N'spiritai_manager') IS NULL
    CREATE LOGIN spiritai_manager WITH PASSWORD = N'$(ManagerPassword)',
        CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [MAS_SFC];
ELSE
    ALTER LOGIN spiritai_manager WITH PASSWORD = N'$(ManagerPassword)' UNLOCK;
GO

IF SUSER_ID(N'spiritai_admin') IS NULL
    CREATE LOGIN spiritai_admin WITH PASSWORD = N'$(AdminPassword)',
        CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [MAS_SFC];
ELSE
    ALTER LOGIN spiritai_admin WITH PASSWORD = N'$(AdminPassword)' UNLOCK;
GO

USE [MAS_SFC];
GO

IF USER_ID(N'spiritai_dab') IS NULL     CREATE USER spiritai_dab FOR LOGIN spiritai_dab;
IF USER_ID(N'spiritai_manager') IS NULL CREATE USER spiritai_manager FOR LOGIN spiritai_manager;
IF USER_ID(N'spiritai_admin') IS NULL   CREATE USER spiritai_admin FOR LOGIN spiritai_admin;
GO

IF DATABASE_PRINCIPAL_ID(N'spiritai_reader') IS NULL  CREATE ROLE spiritai_reader AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'spiritai_no_cost') IS NULL CREATE ROLE spiritai_no_cost AUTHORIZATION dbo;
GO

ALTER ROLE db_datareader ADD MEMBER spiritai_reader;
ALTER ROLE spiritai_reader ADD MEMBER spiritai_manager;
ALTER ROLE spiritai_no_cost ADD MEMBER spiritai_manager;
ALTER ROLE spiritai_reader ADD MEMBER spiritai_admin;
GO

IF SCHEMA_ID(N'core') IS NULL  EXEC (N'CREATE SCHEMA core AUTHORIZATION dbo');
IF SCHEMA_ID(N'agent') IS NULL EXEC (N'CREATE SCHEMA agent AUTHORIZATION dbo');
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name IN (N'core', N'agent') AND principal_id <> USER_ID(N'dbo'))
    THROW 50000, N'Schema core or agent is not owned by dbo. Ownership chaining would not reach the tables.', 1;
GO

GRANT EXECUTE ON SCHEMA::agent TO spiritai_dab;
DENY SELECT ON SCHEMA::core TO spiritai_no_cost;
GO

/* public holds SELECT, EXECUTE and a few writes on legacy objects, and every user is a member
   of public. These schema DENYs take those back from spiritai_dab. The agent procedures still
   read their tables: an unbroken ownership chain (all dbo) skips the permission check on the
   tables it reaches, DENY included. */
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'DENY SELECT, EXECUTE, INSERT, UPDATE, DELETE ON SCHEMA::'
            + QUOTENAME(s.name) + N' TO spiritai_dab;' + NCHAR(10)
FROM sys.schemas AS s
WHERE s.name NOT IN (N'agent', N'sys', N'INFORMATION_SCHEMA', N'guest')
  AND s.principal_id < 16384;
EXEC (@sql);
GO

DECLARE @sql nvarchar(max) = N'', @columns int;
SELECT @sql += N'DENY SELECT ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(o.name)
             + N' (' + QUOTENAME(c.name) + N') TO spiritai_no_cost;' + NCHAR(10)
FROM sys.columns AS c
JOIN sys.objects AS o ON o.object_id = c.object_id
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
  AND c.name LIKE N'%cost%';
SET @columns = (SELECT COUNT(*) FROM sys.columns AS c
                JOIN sys.objects AS o ON o.object_id = c.object_id
                WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0 AND c.name LIKE N'%cost%');
EXEC sys.sp_executesql @sql;
PRINT CONCAT(N'Cost columns denied to spiritai_no_cost: ', @columns);
GO

DECLARE @name nvarchar(600), @id int, @objects int = 0, @added int = 1;
DECLARE @denied TABLE (object_id int PRIMARY KEY);
WHILE @added > 0
BEGIN
    SET @added = 0;
    DECLARE readers CURSOR LOCAL STATIC FOR
        SELECT o.object_id, QUOTENAME(s.name) + N'.' + QUOTENAME(o.name)
        FROM sys.objects AS o
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        WHERE o.type IN ('V', 'IF', 'TF') AND o.is_ms_shipped = 0
          AND o.object_id NOT IN (SELECT object_id FROM @denied);
    OPEN readers;
    FETCH NEXT FROM readers INTO @id, @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            IF EXISTS (SELECT 1 FROM sys.dm_sql_referenced_entities(@name, N'OBJECT') AS r
                       WHERE r.referenced_minor_name LIKE N'%cost%'
                          OR r.referenced_id IN (SELECT object_id FROM @denied))
            BEGIN
                EXEC (N'DENY SELECT ON ' + @name + N' TO spiritai_no_cost;');
                INSERT @denied VALUES (@id);
                SET @objects += 1;
                SET @added += 1;
            END
        END TRY
        BEGIN CATCH
            PRINT CONCAT(N'Could not read what ', @name, N' depends on, so it is denied: ', ERROR_MESSAGE());
            EXEC (N'DENY SELECT ON ' + @name + N' TO spiritai_no_cost;');
            INSERT @denied VALUES (@id);
            SET @objects += 1;
            SET @added += 1;
        END CATCH
        FETCH NEXT FROM readers INTO @id, @name;
    END
    CLOSE readers;
    DEALLOCATE readers;
END
PRINT CONCAT(N'Views and functions that read a cost column, denied whole: ', @objects);
GO
