using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupNameToEntityDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupName",
                schema: "cms",
                table: "EntityDefinition",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupName",
                schema: "cms",
                table: "EntityDefinition");
        }
    }
}
