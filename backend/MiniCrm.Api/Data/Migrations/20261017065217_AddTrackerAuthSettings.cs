using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCrm.Api.Data.Migrations
{
    /// <inheritdoc />
    // Edited by hand. EF wanted to delete the endpoint rows that HasData used to seed (the
    // defaults now come from .env at startup), which would wipe the saved endpoints. They stay.
    // The auth row is created at startup from .env too (TrackerAuthService.EnsureSettings).
    public partial class AddTrackerAuthSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackerAuthSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    HeaderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeaderTemplate = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackerAuthSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackerAuthSettings");
        }
    }
}
