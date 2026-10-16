using JobFetchProbe.Api.Handlers;
using JobFetchProbe.Data;
using JobFetchProbe.Data.Configuration;
using JobFetchProbe.Services;

// RESTful API over the jobs database the console app fills. Read-only.
//   GET api/JobCatalog?page=1&pageSize=25   one page of jobs (JobSummaryPage)
//   GET api/JobCatalog/{jobId}               one job with its details and description (StoredJob)
//   GET openapi/v1.json                      the API description (Development only)

var builder = WebApplication.CreateBuilder(args);

// Same Paths:DataRoot setting as the console app, so all apps use the same database file.
DataPaths.AddDataRootDefaults(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<JobStoreUnavailableHandler>();

// The controllers use the service layer (IJobRetriever); the data layer provides what it reads (IJobCatalog).
// This is the only place in the API that refers to the data layer.
builder.Services.AddJobRetriever();
builder.Services.AddJobCatalog(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.Run();
