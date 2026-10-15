using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MiniCrm.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackerEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackerEndpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TriggerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UrlTemplate = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventIdTemplate = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackerEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackerEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    TrackerEndpointId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SimulateServerError = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackerEvents_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrackerEvents_TrackerEndpoints_TrackerEndpointId",
                        column: x => x.TrackerEndpointId,
                        principalTable: "TrackerEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrackerAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrackerEventId = table.Column<int>(type: "int", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackerAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackerAttempts_TrackerEvents_TrackerEventId",
                        column: x => x.TrackerEventId,
                        principalTable: "TrackerEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "TrackerEndpoints",
                columns: new[] { "Id", "BodyTemplate", "EventIdTemplate", "IsActive", "Method", "Name", "TriggerStatus", "UrlTemplate" },
                values: new object[,]
                {
                    { 1, null, "", true, "GET", "ping", null, "https://bipro2interface.sseku.com/api/candidate-tracker/ping" },
                    { 2, "{\n  \"event_id\": \"{event_id}\",\n  \"event_name\": \"purchase\",\n  \"email\": \"{email}\",\n  \"phone\": \"{phone}\",\n  \"lead_id\": \"{lead_id}\",\n  \"amount\": {amount},\n  \"currency\": \"{currency}\",\n  \"occurred_at\": \"{occurred_at}\"\n}", "conv_{lead_id}", true, "POST", "purchase", "Converted", "https://bipro2interface.sseku.com/api/candidate-tracker/conversions" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackerAttempts_TrackerEventId",
                table: "TrackerAttempts",
                column: "TrackerEventId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEndpoints_Name",
                table: "TrackerEndpoints",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEvents_EventId",
                table: "TrackerEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEvents_LeadId_TrackerEndpointId",
                table: "TrackerEvents",
                columns: new[] { "LeadId", "TrackerEndpointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEvents_Status_NextAttemptAt",
                table: "TrackerEvents",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackerEvents_TrackerEndpointId",
                table: "TrackerEvents",
                column: "TrackerEndpointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackerAttempts");

            migrationBuilder.DropTable(
                name: "TrackerEvents");

            migrationBuilder.DropTable(
                name: "TrackerEndpoints");
        }
    }
}
