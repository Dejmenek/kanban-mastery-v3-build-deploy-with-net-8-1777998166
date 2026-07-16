using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOnDeleteRestrictToColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Columns_ColumnId",
                table: "Cards");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Columns_ColumnId",
                table: "Cards",
                column: "ColumnId",
                principalTable: "Columns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Columns_ColumnId",
                table: "Cards");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Columns_ColumnId",
                table: "Cards",
                column: "ColumnId",
                principalTable: "Columns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
