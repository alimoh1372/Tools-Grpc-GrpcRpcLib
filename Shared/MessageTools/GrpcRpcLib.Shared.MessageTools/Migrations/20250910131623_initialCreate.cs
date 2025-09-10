using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrpcRpcLib.Shared.MessageTools.Migrations
{
    /// <inheritdoc />
    public partial class initialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageEnvelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyTo = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAddresses",
                columns: table => new
                {
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAddresses", x => x.ServiceName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageEnvelopes");

            migrationBuilder.DropTable(
                name: "ServiceAddresses");
        }
    }
}
