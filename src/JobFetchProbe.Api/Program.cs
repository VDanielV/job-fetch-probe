using System.Text.Json.Serialization;
using JobFetchProbe.Api.Handlers;
using JobFetchProbe.Data;
using JobFetchProbe.Data.Configuration;
using JobFetchProbe.Infrastructure;
using JobFetchProbe.Services;
using Microsoft.Extensions.Options;

// RESTful API over the jobs database. It reads the stored jobs and the journal, and can start an acquisition.
//   POST api/JobFetcher                      starts an acquisition in the background (202), or 409 if one is running
//   POST api/JobFetcher/stop                 stops the acquisition started here (202), or 409 if there is none
//   GET api/JobCatalog?page=1&pageSize=25   one page of jobs (JobSummaryPage)
//   GET api/JobCatalog/{jobId}               one job with its details and description (StoredJob)
//   GET api/Journal/runs?take=10             recent runs of both steps (journal)
//   GET api/Journal/job-listing/runs/{id}?after={entryId}   a step 1 run and its new entries
//   GET api/Journal/job-details/runs/{id}?after={entryId}   a step 2 run and its new entries
//   GET openapi/v1.json                      the API description (Development only)

var builder = WebApplication.CreateBuilder(args);

// Same Paths:DataRoot setting all apps use the same database file and input folder.
var dataRoot = DataPaths.AddDataRootDefaults(builder.Configuration);

// Enums (journal states, steps…) as their names, not numbers.
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<JobStoreUnavailableHandler>();
builder.Services.AddExceptionHandler<JournalUnavailableHandler>();
builder.Services.AddExceptionHandler<UnexpectedErrorHandler>(); // last: everything the two above leave

// The controllers use the service layer (IJobRetriever, IJournalService, IAcquisitionRunner). The data layer
// (the database, read-write: the acquisition stores jobs) and Infrastructure (HTTP client, saved pages, input folder)
// implement what the services need. This is the only place in the API that refers to Data and Infrastructure.
builder.Services
    .AddServices(builder.Configuration)
    .AddAcquisitionRunner()
    .AddJobRetriever()
    .AddJournal()
    .AddInfrastructure(builder.Configuration)
    .AddJobStore(builder.Configuration);

// The host logs a start-up failure with its stack trace before throwing it; a configuration error is reported below in
// one line instead. Other start-up failures (a port in use, say) still end the process with their error.
builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

var app = builder.Build();

// The input folders exist, and the database is created or updated before the first request.
if (dataRoot is not null)
{
    DataPaths.CreateFolders(dataRoot);
}
app.Logger.LogInformation("{Database}", await app.Services.InitializeDatabaseAsync());

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

try
{
    await app.RunAsync();
}
catch (OptionsValidationException ex)
{
    // A bad value in appsettings.json, e.g. FetchJobDetails:DelaySeconds 0. Checked at start-up, before any request.
    app.Logger.LogCritical("Configuration error: {Error}", ex.Message);
    return 2;
}
return 0;
