using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StageChangedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_Applications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationNotes_Applications_JobId",
                        column: x => x.JobId,
                        principalTable: "Applications",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationNotes_JobId_At",
                table: "ApplicationNotes",
                columns: new[] { "JobId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Stage",
                table: "Applications",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationNotes");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
