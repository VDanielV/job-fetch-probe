using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobFetchProbe.Data.Migrations
{
    /// <summary>
    /// Gives every job the app's own ID (Jobs.Id) and keeps where it comes from in Source and SourceJobId (the old JobId,
    /// unchanged). A plain number is a LinkedIn posting; anything else came from the Notion import. Every table that refers
    /// to a job is rebuilt to point at Jobs.Id, keeping every row. Written by hand: SQLite cannot change a primary or a
    /// foreign key in place, and the rows must be mapped from the old IDs to the new ones. See docs/spec-job-identity.md.
    /// </summary>
    public partial class UseOwnJobIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- 1. The new ID of every job: LinkedIn numbers first, in today's order (shortest, then lowest, first), then
                --    the Notion jobs in the order they were imported. The old ID stays as SourceJobId.
                CREATE TEMP TABLE "_JobIds" AS
                SELECT "JobId" AS "OldId",
                       ROW_NUMBER() OVER (ORDER BY
                           CASE WHEN "JobId" <> '' AND "JobId" NOT GLOB '*[^0-9]*' THEN 0 ELSE 1 END,
                           CASE WHEN "JobId" <> '' AND "JobId" NOT GLOB '*[^0-9]*' THEN length("JobId") END,
                           CASE WHEN "JobId" <> '' AND "JobId" NOT GLOB '*[^0-9]*' THEN "JobId" END,
                           "FirstSeenAt", "JobId") AS "NewId",
                       CASE WHEN "JobId" <> '' AND "JobId" NOT GLOB '*[^0-9]*' THEN 'linkedin' ELSE 'notion' END AS "Source"
                FROM "Jobs";

                -- 2. The new tables, under temporary names; the children point at the new jobs table.
                CREATE TABLE "Jobs_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Jobs" PRIMARY KEY AUTOINCREMENT,
                    "Source" TEXT NOT NULL,
                    "SourceJobId" TEXT NOT NULL,
                    "Url" TEXT NOT NULL,
                    "Title" TEXT NULL,
                    "Company" TEXT NULL,
                    "Location" TEXT NULL,
                    "Posted" TEXT NULL,
                    "EmploymentType" TEXT NULL,
                    "Description" TEXT NULL,
                    "ParsedFrom" TEXT NULL,
                    "RetrievedAt" TEXT NULL,
                    "Status" TEXT NOT NULL,
                    "LastError" TEXT NULL,
                    "LastAttemptAt" TEXT NULL,
                    "FirstSeenAt" TEXT NOT NULL
                );
                INSERT INTO "Jobs_new" ("Id", "Source", "SourceJobId", "Url", "Title", "Company", "Location", "Posted",
                    "EmploymentType", "Description", "ParsedFrom", "RetrievedAt", "Status", "LastError", "LastAttemptAt", "FirstSeenAt")
                SELECT m."NewId", m."Source", j."JobId", j."Url", j."Title", j."Company", j."Location", j."Posted",
                    j."EmploymentType", j."Description", j."ParsedFrom", j."RetrievedAt", j."Status", j."LastError", j."LastAttemptAt", j."FirstSeenAt"
                FROM "Jobs" j JOIN "_JobIds" m ON m."OldId" = j."JobId";

                CREATE TABLE "JobDetails_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_JobDetails" PRIMARY KEY AUTOINCREMENT,
                    "JobId" INTEGER NOT NULL,
                    "Position" INTEGER NOT NULL,
                    "Label" TEXT NOT NULL,
                    "Value" TEXT NOT NULL,
                    CONSTRAINT "FK_JobDetails_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs_new" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "JobDetails_new" ("Id", "JobId", "Position", "Label", "Value")
                SELECT d."Id", m."NewId", d."Position", d."Label", d."Value"
                FROM "JobDetails" d JOIN "_JobIds" m ON m."OldId" = d."JobId";

                CREATE TABLE "ListingJobs_new" (
                    "BatchId" TEXT NOT NULL,
                    "JobId" INTEGER NOT NULL,
                    "Position" INTEGER NOT NULL,
                    "Url" TEXT NOT NULL,
                    "Title" TEXT NULL,
                    "Company" TEXT NULL,
                    "Location" TEXT NULL,
                    "ListedAt" TEXT NULL,
                    CONSTRAINT "PK_ListingJobs" PRIMARY KEY ("BatchId", "JobId"),
                    CONSTRAINT "FK_ListingJobs_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs_new" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_ListingJobs_Listings_BatchId" FOREIGN KEY ("BatchId") REFERENCES "Listings" ("BatchId") ON DELETE CASCADE
                );
                INSERT INTO "ListingJobs_new" ("BatchId", "JobId", "Position", "Url", "Title", "Company", "Location", "ListedAt")
                SELECT l."BatchId", m."NewId", l."Position", l."Url", l."Title", l."Company", l."Location", l."ListedAt"
                FROM "ListingJobs" l JOIN "_JobIds" m ON m."OldId" = l."JobId";

                CREATE TABLE "ArchivedJobs_new" (
                    "JobId" INTEGER NOT NULL CONSTRAINT "PK_ArchivedJobs" PRIMARY KEY,
                    "ArchivedAt" TEXT NOT NULL,
                    "Reason" TEXT NOT NULL,
                    "Note" TEXT NULL,
                    CONSTRAINT "FK_ArchivedJobs_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs_new" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "ArchivedJobs_new" ("JobId", "ArchivedAt", "Reason", "Note")
                SELECT m."NewId", a."ArchivedAt", a."Reason", a."Note"
                FROM "ArchivedJobs" a JOIN "_JobIds" m ON m."OldId" = a."JobId";

                CREATE TABLE "Applications_new" (
                    "JobId" INTEGER NOT NULL CONSTRAINT "PK_Applications" PRIMARY KEY,
                    "AppliedAt" TEXT NOT NULL,
                    "Stage" TEXT NOT NULL,
                    "StageChangedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_Applications_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs_new" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "Applications_new" ("JobId", "AppliedAt", "Stage", "StageChangedAt")
                SELECT m."NewId", a."AppliedAt", a."Stage", a."StageChangedAt"
                FROM "Applications" a JOIN "_JobIds" m ON m."OldId" = a."JobId";

                CREATE TABLE "ApplicationNotes_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_ApplicationNotes" PRIMARY KEY AUTOINCREMENT,
                    "JobId" INTEGER NOT NULL,
                    "At" TEXT NOT NULL,
                    "Kind" TEXT NOT NULL,
                    "Text" TEXT NOT NULL,
                    CONSTRAINT "FK_ApplicationNotes_Applications_JobId" FOREIGN KEY ("JobId") REFERENCES "Applications_new" ("JobId") ON DELETE CASCADE
                );
                INSERT INTO "ApplicationNotes_new" ("Id", "JobId", "At", "Kind", "Text")
                SELECT n."Id", m."NewId", n."At", n."Kind", n."Text"
                FROM "ApplicationNotes" n JOIN "_JobIds" m ON m."OldId" = n."JobId";

                CREATE TABLE "JobFitAssessments_new" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_JobFitAssessments" PRIMARY KEY AUTOINCREMENT,
                    "JobId" INTEGER NOT NULL,
                    "AssessedAt" TEXT NOT NULL,
                    "Fit" INTEGER NOT NULL,
                    "Decision" TEXT NOT NULL,
                    "HardSkip" INTEGER NOT NULL,
                    "Reason" TEXT NOT NULL,
                    "MissingInformation" TEXT NOT NULL,
                    "Model" TEXT NULL,
                    "ProfileVersion" TEXT NOT NULL,
                    CONSTRAINT "FK_JobFitAssessments_Jobs_JobId" FOREIGN KEY ("JobId") REFERENCES "Jobs_new" ("Id") ON DELETE CASCADE
                );
                INSERT INTO "JobFitAssessments_new" ("Id", "JobId", "AssessedAt", "Fit", "Decision", "HardSkip", "Reason",
                    "MissingInformation", "Model", "ProfileVersion")
                SELECT f."Id", m."NewId", f."AssessedAt", f."Fit", f."Decision", f."HardSkip", f."Reason",
                    f."MissingInformation", f."Model", f."ProfileVersion"
                FROM "JobFitAssessments" f JOIN "_JobIds" m ON m."OldId" = f."JobId";

                -- 3. Every row was copied: the migration fails (and changes nothing) otherwise.
                CREATE TEMP TABLE "_Check" ("Ok" INTEGER NOT NULL CHECK ("Ok" = 1));
                INSERT INTO "_Check" ("Ok") SELECT
                    (SELECT count(*) FROM "Jobs") = (SELECT count(*) FROM "Jobs_new")
                    AND (SELECT count(*) FROM "JobDetails") = (SELECT count(*) FROM "JobDetails_new")
                    AND (SELECT count(*) FROM "ListingJobs") = (SELECT count(*) FROM "ListingJobs_new")
                    AND (SELECT count(*) FROM "ArchivedJobs") = (SELECT count(*) FROM "ArchivedJobs_new")
                    AND (SELECT count(*) FROM "Applications") = (SELECT count(*) FROM "Applications_new")
                    AND (SELECT count(*) FROM "ApplicationNotes") = (SELECT count(*) FROM "ApplicationNotes_new")
                    AND (SELECT count(*) FROM "JobFitAssessments") = (SELECT count(*) FROM "JobFitAssessments_new");

                -- 4. The old tables go, children first; the new ones take their names (the references follow the renames).
                DROP TABLE "ApplicationNotes";
                DROP TABLE "Applications";
                DROP TABLE "JobFitAssessments";
                DROP TABLE "ArchivedJobs";
                DROP TABLE "ListingJobs";
                DROP TABLE "JobDetails";
                DROP TABLE "Jobs";
                ALTER TABLE "Jobs_new" RENAME TO "Jobs";
                ALTER TABLE "JobDetails_new" RENAME TO "JobDetails";
                ALTER TABLE "ListingJobs_new" RENAME TO "ListingJobs";
                ALTER TABLE "ArchivedJobs_new" RENAME TO "ArchivedJobs";
                ALTER TABLE "Applications_new" RENAME TO "Applications";
                ALTER TABLE "ApplicationNotes_new" RENAME TO "ApplicationNotes";
                ALTER TABLE "JobFitAssessments_new" RENAME TO "JobFitAssessments";

                -- 5. The indexes, as the model names them.
                CREATE INDEX "IX_Jobs_Company" ON "Jobs" ("Company");
                CREATE INDEX "IX_Jobs_Status" ON "Jobs" ("Status");
                CREATE UNIQUE INDEX "IX_Jobs_Source_SourceJobId" ON "Jobs" ("Source", "SourceJobId");
                CREATE INDEX "IX_JobDetails_JobId_Position" ON "JobDetails" ("JobId", "Position");
                CREATE INDEX "IX_ListingJobs_JobId" ON "ListingJobs" ("JobId");
                CREATE INDEX "IX_Applications_Stage" ON "Applications" ("Stage");
                CREATE INDEX "IX_ApplicationNotes_JobId_At" ON "ApplicationNotes" ("JobId", "At");
                CREATE INDEX "IX_JobFitAssessments_JobId_AssessedAt" ON "JobFitAssessments" ("JobId", "AssessedAt");

                -- 6. No row points at a job that does not exist.
                INSERT INTO "_Check" ("Ok") SELECT (SELECT count(*) FROM pragma_foreign_key_check) = 0;

                DROP TABLE "_Check";
                DROP TABLE "_JobIds";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "UseOwnJobIds cannot be undone: restore the copy of jobs.db made before it was applied.");
        }
    }
}
