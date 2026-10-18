using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "JobListingJournalRuns",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorType",
                table: "JobDetailsJournalRuns",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StillUnread",
                table: "JobDetailsJournalRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalRead",
                table: "JobDetailsJournalRuns",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorType",
                table: "JobListingJournalRuns");

            migrationBuilder.DropColumn(
                name: "ErrorType",
                table: "JobDetailsJournalRuns");

            migrationBuilder.DropColumn(
                name: "StillUnread",
                table: "JobDetailsJournalRuns");

            migrationBuilder.DropColumn(
                name: "TotalRead",
                table: "JobDetailsJournalRuns");
        }
    }
}
