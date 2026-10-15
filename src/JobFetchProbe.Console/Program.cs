using JobFetchProbe.Console;
using JobFetchProbe.Infrastructure;
using JobFetchProbe.Infrastructure.Database;
using JobFetchProbe.Infrastructure.Details;
using JobFetchProbe.Infrastructure.Inbox;
using JobFetchProbe.Infrastructure.Search;
using JobFetchProbe.Services;
using JobFetchProbe.Services.Details;
using JobFetchProbe.Services.Inbox;
using JobFetchProbe.Services.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Usage:
//   job-fetch-probe                       ask for links or saved files one at a time
//   job-fetch-probe <job-link | file>     read one and exit
//   job-fetch-probe search [<link | file>]  list the jobs in one search results page and exit
//                                         (no argument: Search:DefaultUrl from appsettings.json)
//   job-fetch-probe inbox                 run the whole process once and exit:
//                                         inbox pages -> listing -> job descriptions -> summary
//   job-fetch-probe details [<listing>]   read the job descriptions of a listing (default: the newest) and exit
//
// When Inbox:ProcessOnStart is true, the whole process runs first on every start (except for the inbox and details commands).

// Content root = the folder the app was built to, so appsettings.json is found however the app is started.
// Command-line arguments are not passed to configuration, so a link or path is never mistaken for a setting.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

// Every folder and the database file are placed under Paths:DataRoot (see DataPaths), unless set one by one.
var dataRoot = DataPaths.AddDataRootDefaults(builder.Configuration);

builder.Services
    .AddJobServices()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddSearchServices()
    .AddSearchInfrastructure(builder.Configuration);
builder.Services
    .AddInboxServices()
    .AddInboxInfrastructure(builder.Configuration);
builder.Services
    .AddDetailsServices()
    .AddDetailsInfrastructure(builder.Configuration);

// Storage:Provider = Json (files) or Sqlite (one database file). Must come after the registrations above.
builder.Services.AddStorage(builder.Configuration);

// EF Core logs failed commands itself; the app reports database errors in one line instead (exit code 3).
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

builder.Services.AddSingleton<JobConsole>();
builder.Services.AddSingleton<SearchConsole>();
builder.Services.AddSingleton<InboxConsole>();
builder.Services.AddSingleton<DetailsConsole>();
builder.Services.AddSingleton<PipelineConsole>();

using var host = builder.Build();
var app = host.Services.GetRequiredService<JobConsole>();

try
{
    if (dataRoot is not null)
    {
        DataPaths.CreateFolders(dataRoot);
        ConsoleLog.Info($"Data folder: {dataRoot}");
    }

    // Creates the database or applies new migrations (Sqlite only).
    ConsoleLog.Info(await host.Services.InitializeStorageAsync());

    var inbox = host.Services.GetRequiredService<InboxConsole>();
    var pipeline = host.Services.GetRequiredService<PipelineConsole>();
    if (args.Length > 0 && InboxConsole.IsInbox(args[0]))
    {
        return await pipeline.RunAsync() ? 0 : 1;
    }
    if (args.Length > 0 && DetailsConsole.IsDetails(args[0]))
    {
        var result = await host.Services.GetRequiredService<DetailsConsole>().RunAsync(args.Length > 1 ? args[1] : null);
        PipelineConsole.PrintSummary(null, result);
        return result is { Failed: 0, Pending: 0 } ? 0 : 1;
    }
    if (inbox.ProcessOnStart)
    {
        await pipeline.RunAsync(quietWhenEmpty: true);
    }

    if (args.Length > 0 && args[0].Equals(SearchConsole.Command, StringComparison.OrdinalIgnoreCase))
    {
        var search = host.Services.GetRequiredService<SearchConsole>();
        return await search.HandleAsync(args.Length > 1 ? args[1] : null) ? 0 : 1;
    }

    return args.Length > 0
        ? await app.RunOnceAsync(args[0])
        : await app.RunInteractiveAsync();
}
catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or Microsoft.EntityFrameworkCore.DbUpdateException)
{
    // The database file is locked, read-only, damaged, or not a database.
    Console.Error.WriteLine($"Database error: {ex.GetBaseException().Message}");
    return 3;
}
catch (OptionsValidationException ex)
{
    // A bad value in appsettings.json, e.g. an empty Output:Folder.
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 2;
}
