using Microsoft.EntityFrameworkCore.Migrations;

namespace dm.YLD.Data.Migrations
{
    public partial class uniswapsupplies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UniswapETHSupply",
                table: "Stats",
                type: "decimal(25,18)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UniswapRFISupply",
                table: "Stats",
                type: "decimal(25,18)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UniswapETHSupply",
                table: "Stats");

            migrationBuilder.DropColumn(
                name: "UniswapRFISupply",
                table: "Stats");
        }
    }
}
