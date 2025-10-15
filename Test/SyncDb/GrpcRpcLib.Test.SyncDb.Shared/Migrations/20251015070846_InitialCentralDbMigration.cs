using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrpcRpcLib.Test.SyncDb.Shared.Migrations
{
    /// <inheritdoc />
    public partial class InitialCentralDbMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normal tables
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    Attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessorInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReplayJobs",
                columns: table => new
                {
                    JobId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestorService = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AggregateId = table.Column<int>(type: "int", nullable: false),
                    FromSequence = table.Column<long>(type: "bigint", nullable: false),
                    ToSequence = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReplayJobs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Sku", "Title" },
                values: new object[,]
                {
                    { 1, "P-001", "Prod A" },
                    { 2, "P-002", "Prod B" },
                    { 3, "P-003", "Prod C" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "FullName", "Username" },
                values: new object[,]
                {
                    { 1, "User One", "u1" },
                    { 2, "User Two", "u2" },
                    { 3, "User Three", "u3" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateType_AggregateId_SequenceNumber",
                table: "Events",
                columns: new[] { "AggregateType", "AggregateId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status",
                table: "Events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReplayJobs_Status",
                table: "ReplayJobs",
                column: "Status");

            #region AggregateSequenceMigration

            if (migrationBuilder.ActiveProvider.Contains("SqlServer"))
            {
                // 1) create filegroup if not exists (outside transaction)
                migrationBuilder.Sql(@"
BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.filegroups WHERE name = 'InMemoryFG')
    BEGIN
        ALTER DATABASE CURRENT ADD FILEGROUP [InMemoryFG] CONTAINS MEMORY_OPTIMIZED_DATA;
    END
END TRY
BEGIN CATCH
    PRINT('Warning: could not create memory-optimized filegroup: ' + ERROR_MESSAGE());
END CATCH
", suppressTransaction: true);

                // 2) add file if not exists (outside transaction)
                migrationBuilder.Sql(@"
BEGIN TRY
    IF NOT EXISTS (SELECT * FROM sys.database_files WHERE name = 'InMemoryFile')
    BEGIN
        DECLARE @defaultPath NVARCHAR(260) = CONVERT(nvarchar(260), SERVERPROPERTY('InstanceDefaultDataPath'));
        IF (@defaultPath IS NULL OR LEN(@defaultPath)=0)
        BEGIN
            SELECT TOP(1) @defaultPath = SUBSTRING(physical_name, 1, LEN(physical_name)-CHARINDEX('\', REVERSE(physical_name))+1)
            FROM sys.master_files WHERE database_id = DB_ID() AND type_desc='ROWS' AND file_id=1;
        END
        DECLARE @fileName NVARCHAR(400) = @defaultPath + N'\' + DB_NAME() + N'_InMemory.ndf';
        DECLARE @sql NVARCHAR(MAX) = N'ALTER DATABASE CURRENT ADD FILE (NAME = N''InMemoryFile'', FILENAME = N''' + REPLACE(@fileName, '''','''''') + N''') TO FILEGROUP [InMemoryFG];';
        EXEC sp_executesql @sql;
    END
END TRY
BEGIN CATCH
    PRINT('Warning: cannot add in-memory file: ' + ERROR_MESSAGE());
END CATCH
", suppressTransaction: true);

                // 3) create memory-optimized table only if it does NOT exist (outside transaction)
                migrationBuilder.Sql(@"
BEGIN TRY
    IF OBJECT_ID('dbo.AggregateSequences','U') IS NULL
    BEGIN
        CREATE TABLE dbo.AggregateSequences
        (
            AggregateType NVARCHAR(100) NOT NULL,
            AggregateId INT NOT NULL,
            LastSequence BIGINT NOT NULL DEFAULT 0,
            CONSTRAINT PK_AggregateSequences PRIMARY KEY NONCLUSTERED HASH (AggregateType, AggregateId) WITH (BUCKET_COUNT = 8192)
        )
        WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);

        PRINT('Created memory-optimized AggregateSequences table.');
    END
    ELSE
    BEGIN
        PRINT('AggregateSequences already exists - skipping creation.');
    END
END TRY
BEGIN CATCH
    PRINT('Memory-optimized creation failed: ' + ERROR_MESSAGE());
    PRINT('Falling back to disk-based AggregateSequences table.');

    IF OBJECT_ID('dbo.AggregateSequences','U') IS NULL
    BEGIN
        CREATE TABLE dbo.AggregateSequences
        (
            AggregateType NVARCHAR(100) NOT NULL,
            AggregateId INT NOT NULL,
            LastSequence BIGINT NOT NULL DEFAULT 0,
            CONSTRAINT PK_AggregateSequences PRIMARY KEY (AggregateType, AggregateId)
        );
        CREATE INDEX IX_AggregateSequences_Agg ON dbo.AggregateSequences (AggregateType, AggregateId);
    END
END CATCH
", suppressTransaction: true);

                // 4) create natively compiled stored procedure if memory-optimized table exists and proc not exists (outside transaction)
                migrationBuilder.Sql(@"
BEGIN TRY
    -- only create native proc if AggregateSequences is memory-optimized
    IF EXISTS (SELECT 1 FROM sys.tables t WHERE t.name = 'AggregateSequences' AND t.is_memory_optimized = 1)
    BEGIN
        IF OBJECT_ID(N'dbo.usp_GetNextAggregateSequence','P') IS NOT NULL
            DROP PROCEDURE dbo.usp_GetNextAggregateSequence;
        
        EXEC(N'
CREATE PROCEDURE dbo.usp_GetNextAggregateSequence
    @AggregateType NVARCHAR(100),
    @AggregateId INT,
    @NewSeq BIGINT OUTPUT
WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N''us_english'')
    DECLARE @found INT = 0;

    SELECT TOP(1) @found = 1
    FROM dbo.AggregateSequences
    WHERE AggregateType = @AggregateType AND AggregateId = @AggregateId;

    IF @found = 1
    BEGIN
        UPDATE dbo.AggregateSequences
        SET LastSequence = LastSequence + 1
        WHERE AggregateType = @AggregateType AND AggregateId = @AggregateId;

        SELECT @NewSeq = LastSequence
        FROM dbo.AggregateSequences
        WHERE AggregateType = @AggregateType AND AggregateId = @AggregateId;
    END
    ELSE
    BEGIN
        INSERT dbo.AggregateSequences (AggregateType, AggregateId, LastSequence)
        VALUES (@AggregateType, @AggregateId, 1);

        SELECT @NewSeq = LastSequence
        FROM dbo.AggregateSequences
        WHERE AggregateType = @AggregateType AND AggregateId = @AggregateId;
    END
END
');
    END
END TRY
BEGIN CATCH
    PRINT('Warning: could not create native proc: ' + ERROR_MESSAGE());
END CATCH
", suppressTransaction: true);
            }
            else
            {
                // non sql server (no-op here)
                migrationBuilder.CreateTable(
                    name: "AggregateSequences",
                    columns: table => new
                    {
                        AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                        AggregateId = table.Column<int>(type: "int", nullable: false),
                        LastSequence = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_AggregateSequences", x => new { x.AggregateType, x.AggregateId });
                    });
                migrationBuilder.CreateIndex(
                    name: "IX_AggregateSequences_Agg",
                    table: "AggregateSequences",
                    columns: new[] { "AggregateType", "AggregateId" });
            }

            #endregion
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop normal tables first (transactional)
            migrationBuilder.DropTable(name: "Events");
            migrationBuilder.DropTable(name: "Products");
            migrationBuilder.DropTable(name: "ReplayJobs");
            migrationBuilder.DropTable(name: "Users");

            // ---------- AggregateSequences cleanup (SQL Server specific) ----------
            if (migrationBuilder.ActiveProvider.Contains("SqlServer"))
            {
                // DROP native proc if exists (outside transaction)
                migrationBuilder.Sql(@"
BEGIN TRY
    IF OBJECT_ID(N'dbo.usp_GetNextAggregateSequence','P') IS NOT NULL
        DROP PROCEDURE dbo.usp_GetNextAggregateSequence;
END TRY
BEGIN CATCH
    PRINT('Warning: could not drop native proc: ' + ERROR_MESSAGE());
END CATCH
", suppressTransaction: true);

                // 1) DROP the memory-optimized table OUTSIDE transaction 
                migrationBuilder.Sql(@"
BEGIN TRY
    IF OBJECT_ID('dbo.AggregateSequences','U') IS NOT NULL
    BEGIN
        DROP TABLE dbo.AggregateSequences;
    END
END TRY
BEGIN CATCH
    PRINT('Warning dropping AggregateSequences: ' + ERROR_MESSAGE());
END CATCH
", suppressTransaction: true);

                // 2) Attempt robust removal of the InMemory file and filegroup with retries and logging.
                migrationBuilder.Sql(@"
-- Safe removal script for InMemoryFile / InMemoryFG
SET NOCOUNT ON;

DECLARE @FileLogicalName SYSNAME = N'InMemoryFile';
DECLARE @FilegroupName SYSNAME = N'InMemoryFG';
DECLARE @MaxAttempts INT = 8;
DECLARE @DelaySeconds INT = 8;

IF OBJECT_ID('dbo.InMemoryMigrationLog','U') IS NULL
BEGIN
    CREATE TABLE dbo.InMemoryMigrationLog
    (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        LogTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Action NVARCHAR(200),
        Message NVARCHAR(MAX),
        Attempt INT,
        Success BIT
    );
END

DECLARE @Attempt INT = 1;
DECLARE @RemovedFile BIT = 0;
DECLARE @RemovedFG BIT = 0;
DECLARE @wait NVARCHAR(8);

WHILE @Attempt <= @MaxAttempts AND (@RemovedFile = 0 OR @RemovedFG = 0)
BEGIN
    BEGIN TRY
        INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
        VALUES('StartAttempt', 'Starting attempt to remove file/filegroup if safe.', @Attempt, 0);

        IF EXISTS (SELECT 1 FROM sys.tables WHERE is_memory_optimized = 1)
        BEGIN
            INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
            VALUES('Abort', 'User memory-optimized tables still exist; aborting removal.', @Attempt, 0);
            BREAK;
        END

        IF EXISTS (SELECT 1 FROM sys.internal_tables it WHERE it.object_id IS NOT NULL)
        BEGIN
            INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
            VALUES('AbortInternal', 'Internal XTP tables exist; aborting removal to avoid corruption.', @Attempt, 0);
            BREAK;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.database_files WHERE name = @FileLogicalName)
        BEGIN
            INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
            VALUES('Info', CONCAT('File ', @FileLogicalName, ' not present; marking removed.'), @Attempt, 1);
            SET @RemovedFile = 1;
        END
        ELSE
        BEGIN
            BEGIN TRY
                DECLARE @sql NVARCHAR(MAX) = N'ALTER DATABASE CURRENT REMOVE FILE ' + QUOTENAME(@FileLogicalName) + N';';
                EXEC(@sql);

                INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                VALUES('RemoveFile', CONCAT('ALTER DATABASE REMOVE FILE executed for ', @FileLogicalName), @Attempt, 1);

                SET @RemovedFile = 1;
            END TRY
            BEGIN CATCH
                INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                VALUES('RemoveFileFailed', ERROR_MESSAGE(), @Attempt, 0);

                CHECKPOINT;
                SET @wait = '00:00:' + RIGHT('00' + CAST(@DelaySeconds AS VARCHAR(3)), 2);
                WAITFOR DELAY @wait;
            END CATCH;
        END

        IF @RemovedFile = 1
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.filegroups WHERE name = @FilegroupName)
            BEGIN
                DECLARE @dsid INT = (SELECT data_space_id FROM sys.filegroups WHERE name = @FilegroupName);

                IF NOT EXISTS (SELECT 1 FROM sys.database_files WHERE data_space_id = @dsid)
                BEGIN
                    BEGIN TRY
                        DECLARE @sql2 NVARCHAR(MAX) = N'ALTER DATABASE CURRENT REMOVE FILEGROUP ' + QUOTENAME(@FilegroupName) + N';';
                        EXEC(@sql2);

                        INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                        VALUES('RemoveFilegroup', CONCAT('ALTER DATABASE REMOVE FILEGROUP executed for ', @FilegroupName), @Attempt, 1);

                        SET @RemovedFG = 1;
                    END TRY
                    BEGIN CATCH
                        INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                        VALUES('RemoveFilegroupFailed', ERROR_MESSAGE(), @Attempt, 0);

                        SET @wait = '00:00:' + RIGHT('00' + CAST(@DelaySeconds AS VARCHAR(3)), 2);
                        WAITFOR DELAY @wait;
                    END CATCH;
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                    VALUES('FilegroupHasFiles', CONCAT('Filegroup ', @FilegroupName, ' still has files; will retry later.'), @Attempt, 0);

                    SET @wait = '00:00:' + RIGHT('00' + CAST(@DelaySeconds AS VARCHAR(3)), 2);
                    WAITFOR DELAY @wait;
                END
            END
            ELSE
            BEGIN
                INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
                VALUES('FilegroupNotFound', CONCAT('Filegroup ', @FilegroupName, ' not found; marking removed.'), @Attempt, 1);
                SET @RemovedFG = 1;
            END
        END
    END TRY
    BEGIN CATCH
        INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
        VALUES('Unhandled', ERROR_MESSAGE(), @Attempt, 0);

        SET @wait = '00:00:' + RIGHT('00' + CAST(@DelaySeconds AS VARCHAR(3)), 2);
        WAITFOR DELAY @wait;
    END CATCH;

    SET @Attempt = @Attempt + 1;
END

IF @RemovedFile = 1 AND @RemovedFG = 1
BEGIN
    INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
    VALUES('Completed', 'File and filegroup removed (or were not present).', @Attempt-1, 1);
END
ELSE
BEGIN
    INSERT INTO dbo.InMemoryMigrationLog(Action, Message, Attempt, Success)
    VALUES('Incomplete', CONCAT('Removal incomplete after ', @MaxAttempts, ' attempts. FileRemoved=', @RemovedFile, ', FilegroupRemoved=', @RemovedFG), @Attempt-1, 0);
END
", suppressTransaction: true);
            }
            else
            {
                // Non-SQL Server providers: simple drop
                migrationBuilder.DropTable(name: "AggregateSequences");
            }
        }
    }
}



