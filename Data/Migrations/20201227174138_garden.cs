using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace dm.YLD.Data.Migrations
{
    public partial class garden : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UniswapRFISupply",
                table: "Stats",
                newName: "UniswapRFIAmount");

            migrationBuilder.RenameColumn(
                name: "UniswapETHSupply",
                table: "Stats",
                newName: "UniswapETHAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "GardenETHSupply",
                table: "Stats",
                type: "decimal(25,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GardenRFISupply",
                table: "Stats",
                type: "decimal(25,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "GardenHolders",
                columns: table => new
                {
                    GardenHolderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pair = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FirstBlockNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstTimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GardenHolders", x => x.GardenHolderId);
                });

            migrationBuilder.CreateTable(
                name: "GardenTransactions",
                columns: table => new
                {
                    GardenTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeStamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    From = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    To = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pair = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GardenTransactions", x => x.GardenTransactionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GardenHolders_Pair",
                table: "GardenHolders",
                column: "Pair");

            migrationBuilder.CreateIndex(
                name: "IX_GardenHolders_Value",
                table: "GardenHolders",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_GardenTransactions_Pair",
                table: "GardenTransactions",
                column: "Pair");

            migrationBuilder.CreateIndex(
                name: "IX_GardenTransactions_TimeStamp",
                table: "GardenTransactions",
                column: "TimeStamp");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GardenHolders");

            migrationBuilder.DropTable(
                name: "GardenTransactions");

            migrationBuilder.DropColumn(
                name: "GardenETHSupply",
                table: "Stats");

            migrationBuilder.DropColumn(
                name: "GardenRFISupply",
                table: "Stats");

            migrationBuilder.RenameColumn(
                name: "UniswapRFIAmount",
                table: "Stats",
                newName: "UniswapRFISupply");

            migrationBuilder.RenameColumn(
                name: "UniswapETHAmount",
                table: "Stats",
                newName: "UniswapETHSupply");
        }
    }
}
