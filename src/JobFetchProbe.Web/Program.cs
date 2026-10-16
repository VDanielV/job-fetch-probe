using JobFetchProbe.Web.Api;
using Microsoft.Extensions.Options;

// A read-only web page over the jobs, read from the API (JobFetchProbe.Api, which must be running):
//   /             all jobs, newest first, 25 per page       ← GET api/JobCatalog?page=&pageSize=
//   /?job={id}    the same, with that job in the details pane ← GET api/JobCatalog/{jobId}
//   /acquire      how jobs are acquired (explanation only)
//   /jobs/{id}    old address of a job: redirects to /?job={id}
// It never touches the database itself.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddOptions<JobCatalogApiSettings>()
    .Bind(builder.Configuration.GetSection(JobCatalogApiSettings.SectionName))
    .Validate(s => Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _), "JobCatalogApi:BaseUrl must be an absolute URL")
    .Validate(s => s.TimeoutSeconds is >= 1 and <= 120, "JobCatalogApi:TimeoutSeconds must be 1-120")
    .ValidateOnStart();

// Typed client through IHttpClientFactory: pooled handlers, nothing to dispose.
builder.Services.AddHttpClient<IJobCatalogClient, JobCatalogClient>((sp, client) =>
{
    var s = sp.GetRequiredService<IOptions<JobCatalogApiSettings>>().Value;
    client.BaseAddress = new Uri(s.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

var app = builder.Build();
app.MapStaticAssets();
app.MapRazorPages();
app.Run();
