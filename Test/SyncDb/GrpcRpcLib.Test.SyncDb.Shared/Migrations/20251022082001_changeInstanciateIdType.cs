using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrpcRpcLib.Test.SyncDb.Shared.Migrations
{
    /// <inheritdoc />
    public partial class changeInstanciateIdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
	        // 1. ابتدا یک ستون موقت اضافه می‌کنیم
	        migrationBuilder.AddColumn<string>(
		        name: "ProcessorInstanceId_Temp",
		        table: "Events",
		        type: "nvarchar(max)",
		        nullable: false,
		        defaultValue: "");

	        // 2. داده‌ها را از ستون قدیمی به جدید کپی می‌کنیم
	        migrationBuilder.Sql(@"
            UPDATE Events 
            SET ProcessorInstanceId_Temp = CAST(ProcessorInstanceId AS nvarchar(50))
            WHERE ProcessorInstanceId IS NOT NULL
        ");

	        // 3. ستون قدیمی را حذف می‌کنیم
	        migrationBuilder.DropColumn(
		        name: "ProcessorInstanceId",
		        table: "Events");

	        // 4. ستون موقت را به نام اصلی تغییر می‌دهیم
	        migrationBuilder.RenameColumn(
		        name: "ProcessorInstanceId_Temp",
		        table: "Events",
		        newName: "ProcessorInstanceId");
			
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
	        // 1. ابتدا یک ستون موقت از نوع Guid اضافه می‌کنیم
	        migrationBuilder.AddColumn<Guid>(
		        name: "ProcessorInstanceId_Temp",
		        table: "Events",
		        type: "uniqueidentifier",
		        nullable: true);

	        // 2. داده‌ها را برمی‌گردانیم (فقط مقادیر معتبر)
	        migrationBuilder.Sql(@"
            UPDATE Events 
            SET ProcessorInstanceId_Temp = TRY_CAST(ProcessorInstanceId AS uniqueidentifier)
            WHERE ProcessorInstanceId IS NOT NULL AND ProcessorInstanceId != ''
        ");
	        // 3. ستون string را حذف می‌کنیم
	        migrationBuilder.DropColumn(
		        name: "ProcessorInstanceId",
		        table: "Events");

	        // 4. ستون موقت را به نام اصلی تغییر می‌دهیم
	        migrationBuilder.RenameColumn(
		        name: "ProcessorInstanceId_Temp",
		        table: "Events",
		        newName: "ProcessorInstanceId");
			
        }
    }
}
