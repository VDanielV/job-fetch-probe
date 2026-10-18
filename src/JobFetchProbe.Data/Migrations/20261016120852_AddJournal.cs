using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobDetailsJournalRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedBy = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    ToRead = table.Column<int>(type: "INTEGER", nullable: false),
                    Fetched = table.Column<int>(type: "INTEGER", nullable: false),
                    Failed = table.Column<int>(type: "INTEGER", nullable: false),
                    Requests = table.Column<int>(type: "INTEGER", nullable: false),
                    CircuitState = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CircuitOpenUntil = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Openings = table.Column<int>(type: "INTEGER", nullable: false),
                    StopAfterOpenings = table.Column<int>(type: "INTEGER", nullable: false),
                    EtaSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDetailsJournalRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobListingJournalRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedBy = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Pages = table.Column<int>(type: "INTEGER", nullable: false),
                    PagesRead = table.Column<int>(type: "INTEGER", nullable: false),
                    PagesKept = table.Column<int>(type: "INTEGER", nullable: false),
                    JobsStored = table.Column<int>(type: "INTEGER", nullable: false),
                    NewJobs = table.Column<int>(type: "INTEGER", nullable: false),
                    ListingId = table.Column<string>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobListingJournalRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobDetailsJournalEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Gone = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    WaitSeconds = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobDetailsJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobDetailsJournalEntries_JobDetailsJournalRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "JobDetailsJournalRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobListingJournalEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<long>(type: "INTEGER", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false),
                    File = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Jobs = table.Column<int>(type: "INTEGER", nullable: false),
                    NewJobs = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobListingJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobListingJournalEntries_JobListingJournalRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "JobListingJournalRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobDetailsJournalEntries_RunId_Id",
                table: "JobDetailsJournalEntries",
                columns: new[] { "RunId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_JobDetailsJournalRuns_State",
                table: "JobDetailsJournalRuns",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_JobListingJournalEntries_RunId_Id",
                table: "JobListingJournalEntries",
                columns: new[] { "RunId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_JobListingJournalRuns_State",
                table: "JobListingJournalRuns",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobDetailsJournalEntries");

            migrationBuilder.DropTable(
                name: "JobListingJournalEntries");

            migrationBuilder.DropTable(
                name: "JobDetailsJournalRuns");

            migrationBuilder.DropTable(
                name: "JobListingJournalRuns");
        }
    }
}
