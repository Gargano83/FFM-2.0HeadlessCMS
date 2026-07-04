using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCmsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.CreateTable(
                name: "EntityDefinition",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "dbo"),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryKeyColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menu",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Page",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Page", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Page_Page_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "cms",
                        principalTable: "Page",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldDefinition",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SqlDataType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    IsNullable = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimaryKey = table.Column<bool>(type: "bit", nullable: false),
                    IsForeignKey = table.Column<bool>(type: "bit", nullable: false),
                    ForeignKeyTargetEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ForeignKeyDisplayColumn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EditorType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShowInList = table.Column<bool>(type: "bit", nullable: false),
                    ShowInForm = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDefinition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldDefinition_EntityDefinition_EntityDefinitionId",
                        column: x => x.EntityDefinitionId,
                        principalSchema: "cms",
                        principalTable: "EntityDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldDefinition_EntityDefinition_ForeignKeyTargetEntityId",
                        column: x => x.ForeignKeyTargetEntityId,
                        principalSchema: "cms",
                        principalTable: "EntityDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItem",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItem_MenuItem_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "cms",
                        principalTable: "MenuItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MenuItem_Menu_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "cms",
                        principalTable: "Menu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntityDefinition_SchemaName_TableName",
                schema: "cms",
                table: "EntityDefinition",
                columns: new[] { "SchemaName", "TableName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinition_EntityDefinitionId_ColumnName",
                schema: "cms",
                table: "FieldDefinition",
                columns: new[] { "EntityDefinitionId", "ColumnName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinition_ForeignKeyTargetEntityId",
                schema: "cms",
                table: "FieldDefinition",
                column: "ForeignKeyTargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Menu_Name",
                schema: "cms",
                table: "Menu",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItem_MenuId",
                schema: "cms",
                table: "MenuItem",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItem_ParentId",
                schema: "cms",
                table: "MenuItem",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Page_ParentId",
                schema: "cms",
                table: "Page",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Page_Slug",
                schema: "cms",
                table: "Page",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldDefinition",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MenuItem",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Page",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "EntityDefinition",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Menu",
                schema: "cms");
        }
    }
}
