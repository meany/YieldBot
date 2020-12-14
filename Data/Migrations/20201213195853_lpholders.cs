using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dm.YLD.Data.Migrations
{
    public partial class lpholders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LPHolders",
                columns: table => new
                {
                    LPHolderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pair = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FirstBlockNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LPHolders", x => x.LPHolderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LPHolders_Pair",
                table: "LPHolders",
                column: "Pair");

            migrationBuilder.CreateIndex(
                name: "IX_LPHolders_Value",
                table: "LPHolders",
                column: "Value");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LPHolders");
        }
    }
}
