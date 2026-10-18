using JobFetchProbe.Web.Api;
using Microsoft.Extensions.Options;

// A web page over the jobs, read from the API (JobFetchProbe.Api, which must be running):
//   /             all jobs, newest first, 25 per page       ← GET api/JobCatalog?page=&pageSize=
//   /?job={id}    the same, with that job in the details pane ← GET api/JobCatalog/{jobId}
//                 archive the ticked jobs, or the one in the pane ← POST api/ArchivedJobs (?archived=1 shows them)
//                 "Check fit" scores the job in the pane with the local model ← POST api/JobFit (not stored)
//   /acquire      explains the acquisition and starts it   ← POST api/JobFetcher
//                 (?since=…) its progress, refreshed every 15 s  ← GET api/Journal/runs, api/Journal/<step>/runs/{id}
//   /jobs/{id}    old address of a job: redirects to /?job={id}
//   /chat         one message to the local model          ← POST api/Chat
// It never touches the database itself. The chat answer is not stored.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddOptions<JobCatalogApiSettings>()
    .Bind(builder.Configuration.GetSection(JobCatalogApiSettings.SectionName))
    .Validate(s => Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _), "JobCatalogApi:BaseUrl must be an absolute URL")
    .Validate(s => s.TimeoutSeconds is >= 1 and <= 120, "JobCatalogApi:TimeoutSeconds must be 1-120")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

// Typed clients through IHttpClientFactory: pooled handlers, nothing to dispose. All six call the same API.
// Chat waits longer than the others: one local answer can take most of a minute, and the catalog must still fail fast.
builder.Services.AddHttpClient<IJobCatalogClient, JobCatalogClient>(ConfigureApi);
builder.Services.AddHttpClient<IJobFetcherClient, JobFetcherClient>(ConfigureApi);
builder.Services.AddHttpClient<IJournalClient, JournalClient>(ConfigureApi);
builder.Services.AddHttpClient<IJobArchiveClient, JobArchiveClient>(ConfigureApi);
// Job fit waits as long as chat: the local model scores one job in up to about two minutes.
builder.Services.AddHttpClient<IJobFitClient, JobFitClient>((services, client) =>
{
    ConfigureApi(services, client);
    client.Timeout = TimeSpan.FromSeconds(JobFitClient.TimeoutSeconds);
});
builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>((services, client) =>
{
    ConfigureApi(services, client);
    client.Timeout = TimeSpan.FromSeconds(ChatApiClient.TimeoutSeconds);
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
