using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashFunctionToFieldDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHashFunction",
                schema: "cms",
                table: "FieldDefinition",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHashFunction",
                schema: "cms",
                table: "FieldDefinition");
        }
    }
}
