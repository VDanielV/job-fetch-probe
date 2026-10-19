using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobFitAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobFitAssessments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Fit = table.Column<int>(type: "INTEGER", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    HardSkip = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    MissingInformation = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobFitAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobFitAssessments_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobFitAssessments_JobId_AssessedAt",
                table: "JobFitAssessments",
                columns: new[] { "JobId", "AssessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobFitAssessments");
        }
    }
}
