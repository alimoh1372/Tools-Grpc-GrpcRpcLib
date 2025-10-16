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

			// AggregateSequences - disk-based, idempotent creation
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

			// seeds
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

			// indexes for events/replayjobs
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
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Drop in reverse order
			migrationBuilder.DropTable(name: "AggregateSequences");
			migrationBuilder.DropTable(name: "Events");
			migrationBuilder.DropTable(name: "Products");
			migrationBuilder.DropTable(name: "ReplayJobs");
			migrationBuilder.DropTable(name: "Users");
		}
	}
}



