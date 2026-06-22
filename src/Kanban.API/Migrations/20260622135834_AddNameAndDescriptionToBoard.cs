using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNameAndDescriptionToBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Boards",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Boards",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Boards");
        }
    }
}
