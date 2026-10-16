using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCrm.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundSettings");
        }
    }
}
