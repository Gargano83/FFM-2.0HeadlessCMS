using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocalizationSource",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentSchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContentTableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContentIdColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LanguageIdColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TextColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RowIdColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LanguageSchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LanguageTableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LanguageIdColumnInLanguageTable = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LanguageCodeColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LanguageNameColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DefaultLanguageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizationSource", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinition_LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition",
                column: "LocalizationSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldDefinition_LocalizationSource_LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition",
                column: "LocalizationSourceId",
                principalSchema: "cms",
                principalTable: "LocalizationSource",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FieldDefinition_LocalizationSource_LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition");

            migrationBuilder.DropTable(
                name: "LocalizationSource",
                schema: "cms");

            migrationBuilder.DropIndex(
                name: "IX_FieldDefinition_LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition");

            migrationBuilder.DropColumn(
                name: "LocalizationSourceId",
                schema: "cms",
                table: "FieldDefinition");
        }
    }
}
