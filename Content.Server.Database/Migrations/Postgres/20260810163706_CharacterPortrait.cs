using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CharacterPortrait : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "construction_favorites",
                table: "preference");

            migrationBuilder.AddColumn<string>(
                name: "character_portrait_url",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "character_portrait_url",
                table: "profile");

            migrationBuilder.AddColumn<List<string>>(
                name: "construction_favorites",
                table: "preference",
                type: "text[]",
                nullable: false);
        }
    }
}
