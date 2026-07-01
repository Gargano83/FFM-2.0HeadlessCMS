using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCms.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsIdentityToFieldDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIdentity",
                schema: "cms",
                table: "FieldDefinition",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsIdentity",
                schema: "cms",
                table: "FieldDefinition");
        }
    }
}
