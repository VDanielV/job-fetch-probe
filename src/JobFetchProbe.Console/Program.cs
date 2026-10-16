using JobFetchProbe.Console;
using JobFetchProbe.Infrastructure;
using JobFetchProbe.Infrastructure.Configuration;
using JobFetchProbe.Infrastructure.Database;
using JobFetchProbe.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// job-fetch-probe runs one flow and exits:
//   Step 1  saved search pages in <DataRoot>\input → job links in the database → pages moved to input\archived
//   Step 2  every job without a description → read its page, one at a time → description and status in the database
//   Summary the totals
// There are no commands or arguments. Exit code: 0 nothing left to do, 1 something left for the next run,
// 2 configuration error, 3 database error.

// Content root = the folder the app was built to, so appsettings.json is found however the app is started.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
});

// The input folders and the database file are placed under Paths:DataRoot (see DataPaths), unless set one by one.
var dataRoot = DataPaths.AddDataRootDefaults(builder.Configuration);

builder.Services
    .AddServices()
    .AddInfrastructure(builder.Configuration);

// EF Core logs failed commands itself; the app reports database errors in one line instead (exit code 3).
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);

builder.Services.AddSingleton<InboxConsole>();
builder.Services.AddSingleton<DetailsConsole>();
builder.Services.AddSingleton<PipelineConsole>();

using var host = builder.Build();

try
{
    if (dataRoot is not null)
    {
        DataPaths.CreateFolders(dataRoot);
        ConsoleLog.Info($"Data folder: {dataRoot}");
    }

    // Creates the database on the first run, and applies new migrations after an update.
    ConsoleLog.Info(await host.Services.InitializeDatabaseAsync());

    return await host.Services.GetRequiredService<PipelineConsole>().RunAsync();
}
catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or Microsoft.EntityFrameworkCore.DbUpdateException)
{
    // The database file is locked, read-only, damaged, or not a database.
    Console.Error.WriteLine($"Database error: {ex.GetBaseException().Message}");
    return 3;
}
catch (OptionsValidationException ex)
{
    // A bad value in appsettings.json, e.g. an empty Inbox:Folder.
    Console.Error.WriteLine($"Configuration error: {ex.Message}");
    return 2;
}
