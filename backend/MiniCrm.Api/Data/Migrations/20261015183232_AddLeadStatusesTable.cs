using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MiniCrm.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadStatusesTable : Migration
    {
        private const string LeadStatusesTable = "LeadStatuses";
        private const string TrackerEndpointsTable = "TrackerEndpoints";
        private const string LeadsTable = "Leads";
        private const string NameColumn = "Name";
        private const string TriggerStatusColumn = "TriggerStatus";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: LeadStatusesTable,
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadStatuses", x => x.Name);
                });

            migrationBuilder.InsertData(
                table: LeadStatusesTable,
                columns: new[] { NameColumn, "SortOrder" },
                values: new object[,]
                {
                    { "contacted", 2 },
                    { "converted", 4 },
                    { "lost", 5 },
                    { "new", 1 },
                    { "qualified", 3 }
                });

            // Before, statuses were saved as enum names ("Converted"). The table uses
            // lower case ("converted"), so convert existing rows before adding the keys.
            migrationBuilder.Sql("UPDATE Leads SET Status = LOWER(Status)");
            migrationBuilder.Sql("UPDATE TrackerEndpoints SET TriggerStatus = LOWER(TriggerStatus)");

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEndpoints_TriggerStatus",
                table: TrackerEndpointsTable,
                column: TriggerStatusColumn);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status",
                table: LeadsTable,
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_LeadStatuses_Status",
                table: LeadsTable,
                column: "Status",
                principalTable: LeadStatusesTable,
                principalColumn: NameColumn,
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TrackerEndpoints_LeadStatuses_TriggerStatus",
                table: TrackerEndpointsTable,
                column: TriggerStatusColumn,
                principalTable: LeadStatusesTable,
                principalColumn: NameColumn,
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_LeadStatuses_Status",
                table: LeadsTable);

            migrationBuilder.DropForeignKey(
                name: "FK_TrackerEndpoints_LeadStatuses_TriggerStatus",
                table: TrackerEndpointsTable);

            migrationBuilder.DropTable(
                name: LeadStatusesTable);

            // Back to enum names: "converted" -> "Converted".
            migrationBuilder.Sql("UPDATE Leads SET Status = UPPER(LEFT(Status, 1)) + SUBSTRING(Status, 2, 20)");

            migrationBuilder.DropIndex(
                name: "IX_TrackerEndpoints_TriggerStatus",
                table: TrackerEndpointsTable);

            migrationBuilder.DropIndex(
                name: "IX_Leads_Status",
                table: LeadsTable);

            migrationBuilder.UpdateData(
                table: TrackerEndpointsTable,
                keyColumn: "Id",
                keyValue: 2,
                column: TriggerStatusColumn,
                value: "Converted");
        }
    }
}
