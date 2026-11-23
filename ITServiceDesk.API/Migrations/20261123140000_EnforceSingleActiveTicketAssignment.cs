using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITServiceDesk.API.Migrations
{
    public partial class EnforceSingleActiveTicketAssignment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketAssignments_TicketId",
                table: "TicketAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_Active_TicketId",
                table: "TicketAssignments",
                column: "TicketId",
                unique: true,
                filter: "[UnassignedAt] IS NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketAssignments_Active_TicketId",
                table: "TicketAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_TicketId",
                table: "TicketAssignments",
                column: "TicketId");
        }
    }
}
