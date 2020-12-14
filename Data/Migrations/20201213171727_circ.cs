using Microsoft.EntityFrameworkCore.Migrations;

namespace dm.YLD.Data.Migrations
{
    public partial class circ : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Circulation",
                table: "Stats",
                newName: "HolderCirculation");

            migrationBuilder.AddColumn<decimal>(
                name: "FullCirculation",
                table: "Stats",
                type: "decimal(25,18)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullCirculation",
                table: "Stats");

            migrationBuilder.RenameColumn(
                name: "HolderCirculation",
                table: "Stats",
                newName: "Circulation");
        }
    }
}
