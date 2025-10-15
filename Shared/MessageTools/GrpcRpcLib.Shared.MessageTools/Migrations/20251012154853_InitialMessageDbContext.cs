using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrpcRpcLib.Shared.MessageTools.Migrations
{
    /// <inheritdoc />
    public partial class InitialMessageDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CentPub_MessageEnvelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ReplyTo = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Payload = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentPub_MessageEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CentPub_ServiceAddresses",
                columns: table => new
                {
                    ServiceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CentPub_ServiceAddresses", x => x.ServiceName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CentPub_MessageEnvelopes");

            migrationBuilder.DropTable(
                name: "CentPub_ServiceAddresses");
        }
    }
}
