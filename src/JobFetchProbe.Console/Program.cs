using JobFetchProbe.Console;
using JobFetchProbe.Infrastructure;
using JobFetchProbe.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Usage:
//   job-fetch-probe                       ask for links or saved files one at a time
//   job-fetch-probe <job-link | file>     read one and exit

// Content root = the folder the app was built to, so appsettings.json is found however the app is started.
// Command-line arguments are not passed to configuration, so a link or path is never mistaken for a setting.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services
    .AddJobServices()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<JobConsole>();

using var host = builder.Build();
var app = host.Services.GetRequiredService<JobConsole>();

try
{
    return args.Length > 0
        ? await app.RunOnceAsync(args[0])
        : await app.RunInteractiveAsync();
}
catch (OptionsValidationException ex)
{
    // A bad value in appsettings.json, e.g. an empty Output:Folder.
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 2;
}
