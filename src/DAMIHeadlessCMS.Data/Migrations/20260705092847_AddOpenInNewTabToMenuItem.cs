using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenInNewTabToMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OpenInNewTab",
                schema: "cms",
                table: "MenuItem",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpenInNewTab",
                schema: "cms",
                table: "MenuItem");
        }
    }
}
