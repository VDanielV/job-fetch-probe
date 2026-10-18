using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveJournalStarter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite drops the column in place: every row and its other columns stay as they are. EF's DropColumn
            // would rebuild both tables outside the migration's transaction instead.
            migrationBuilder.Sql("ALTER TABLE \"JobListingJournalRuns\" DROP COLUMN \"StartedBy\";");
            migrationBuilder.Sql("ALTER TABLE \"JobDetailsJournalRuns\" DROP COLUMN \"StartedBy\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StartedBy",
                table: "JobListingJournalRuns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Api");

            migrationBuilder.AddColumn<string>(
                name: "StartedBy",
                table: "JobDetailsJournalRuns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Api");
        }
    }
}
