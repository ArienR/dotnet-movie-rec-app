using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieRecApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbFieldsToMovie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TmdbId",
                table: "Movies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Movies");
        }
    }
}
