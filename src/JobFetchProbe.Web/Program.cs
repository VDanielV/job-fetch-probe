using JobFetchProbe.Infrastructure;
using JobFetchProbe.Infrastructure.Configuration;

// A read-only web page over the jobs database the console app fills:
//   /             all jobs, newest first, 25 per page
//   /jobs/{id}    one job: its details and description
// It never writes to the database and never creates it; run the console app first.

var builder = WebApplication.CreateBuilder(args);

// Same Paths:DataRoot setting as the console app, so both use the same database file.
DataPaths.AddDataRootDefaults(builder.Configuration);

builder.Services.AddRazorPages();
builder.Services.AddJobCatalog(builder.Configuration);

var app = builder.Build();
app.MapRazorPages();
app.Run();
