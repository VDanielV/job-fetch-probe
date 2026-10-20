using JobFetchProbe.Web.Api;
using Microsoft.Extensions.Options;

// A web page over the jobs, read from the API (JobFetchProbe.Api, which must be running):
//   /             all jobs, newest first, 25 per page       ← GET api/JobCatalog?page=&pageSize=
//   /?job={id}    the same, with that job in the details pane ← GET api/JobCatalog/{jobId}
//                 archive the ticked jobs, or the one in the pane ← POST api/ArchivedJobs (?archived=1 shows them)
//                 "Check fit" scores the job in the pane with the local model ← POST api/JobFit (kept: GET api/JobFit/{id})
//                 ?sort=fit lists the best latest fit first
//                 "I applied" moves the job in the pane to Applications ← POST api/Applications (Undo: DELETE)
//   /applications the jobs applied to: stage, follow-up flags, notes ← GET/PUT/POST/DELETE api/Applications
//                 (?closed=1 the closed ones; the menu shows the open and flagged counts ← GET api/Applications/summary)
//   /acquire      explains the acquisition and starts it   ← POST api/JobFetcher
//                 (?since=…) its progress, refreshed every 15 s  ← GET api/Journal/runs, api/Journal/<step>/runs/{id}
//   /runs         past acquisitions, newest first          ← GET api/Runs
//   /runs/{id}    one of them: Summary, Whole run          ← GET api/Runs/{id}, api/Journal/<step>/runs/{id}
//                 and Job links: new or known, what step 2 did ← GET api/Runs/{id}/job-links
//   /jobs/{id}    old address of a job: redirects to /?job={id}
//   /chat         one message to the local model          ← POST api/Chat
// It never touches the database itself. The chat answer is not stored.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddOptions<JobCatalogApiSettings>()
    .Bind(builder.Configuration.GetSection(JobCatalogApiSettings.SectionName))
    .Validate(s => Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _), "JobCatalogApi:BaseUrl must be an absolute URL")
    .Validate(s => s.TimeoutSeconds is >= 1 and <= 120, "JobCatalogApi:TimeoutSeconds must be 1-120")
    .Validate(s => s.LocalModelTimeoutSeconds is >= 1 and <= 900, "JobCatalogApi:LocalModelTimeoutSeconds must be 1-900")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

// Typed clients through IHttpClientFactory: pooled handlers, nothing to dispose. All eight call the same API.
// Chat and job fit wait longer than the others (below): one local answer can take minutes, and the catalog must still fail fast.
builder.Services.AddHttpClient<IJobCatalogClient, JobCatalogClient>(ConfigureApi);
builder.Services.AddHttpClient<IJobFetcherClient, JobFetcherClient>(ConfigureApi);
builder.Services.AddHttpClient<IJournalClient, JournalClient>(ConfigureApi);
builder.Services.AddHttpClient<IJobArchiveClient, JobArchiveClient>(ConfigureApi);
builder.Services.AddHttpClient<IApplicationClient, ApplicationClient>(ConfigureApi);
builder.Services.AddHttpClient<IRunsClient, RunsClient>(ConfigureApi);
// Job fit and chat wait for the local model: JobCatalogApi:LocalModelTimeoutSeconds, a little longer than the API's
// Ollama:TimeoutSeconds.
builder.Services.AddHttpClient<IJobFitClient, JobFitClient>((services, client) =>
{
    ConfigureApi(services, client);
    client.Timeout = LocalModelTimeout(services);
});
builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>((services, client) =>
{
    ConfigureApi(services, client);
    client.Timeout = LocalModelTimeout(services);
});

var app = builder.Build();
app.MapStaticAssets();
app.MapRazorPages();
app.Run();

static void ConfigureApi(IServiceProvider services, HttpClient client)
{
    var s = services.GetRequiredService<IOptions<JobCatalogApiSettings>>().Value;
    client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}

static TimeSpan LocalModelTimeout(IServiceProvider services)
{
    var s = services.GetRequiredService<IOptions<JobCatalogApiSettings>>().Value;
    return TimeSpan.FromSeconds(s.LocalModelTimeoutSeconds);
}
