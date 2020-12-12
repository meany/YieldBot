using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dm.YLD.Data.Migrations
{
    public partial class holders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Holders",
                columns: table => new
                {
                    HolderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FirstBlockNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holders", x => x.HolderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holders_Value",
                table: "Holders",
                column: "Value");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holders");
        }
    }
}
