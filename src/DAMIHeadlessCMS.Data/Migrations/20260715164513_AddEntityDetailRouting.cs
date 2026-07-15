using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAMIHeadlessCMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityDetailRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailRoutePrefix",
                schema: "cms",
                table: "EntityDefinition",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntityDefinition_DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition",
                column: "DetailKeyFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_EntityDefinition_DetailRoutePrefix",
                schema: "cms",
                table: "EntityDefinition",
                column: "DetailRoutePrefix",
                unique: true,
                filter: "[DetailRoutePrefix] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EntityDefinition_FieldDefinition_DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition",
                column: "DetailKeyFieldId",
                principalSchema: "cms",
                principalTable: "FieldDefinition",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntityDefinition_FieldDefinition_DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition");

            migrationBuilder.DropIndex(
                name: "IX_EntityDefinition_DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition");

            migrationBuilder.DropIndex(
                name: "IX_EntityDefinition_DetailRoutePrefix",
                schema: "cms",
                table: "EntityDefinition");

            migrationBuilder.DropColumn(
                name: "DetailKeyFieldId",
                schema: "cms",
                table: "EntityDefinition");

            migrationBuilder.DropColumn(
                name: "DetailRoutePrefix",
                schema: "cms",
                table: "EntityDefinition");
        }
    }
}
