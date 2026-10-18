using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobFetchProbe.Data;

/// <summary>
/// Used only by "dotnet ef migrations add …", so the tools do not have to start the app (which would run the acquisition).
/// The file name here is never opened by the app.
/// </summary>
public sealed class DesignTimeJobsDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    public JobsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JobsDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
