using System.Text.Json;
using JobFetchProbe.Application.Dtos;
using JobFetchProbe.Infrastructure.Reporting;

namespace JobFetchProbe.Infrastructure.Tests;

public class JsonReportWriterTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jfp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ProbeReport Report(string command, PolicySummary? policy = null) => new()
    {
        RunId = "20261014T110600Z",
        Command = command,
        StartedAt = new DateTime(2026, 10, 14, 11, 6, 0, DateTimeKind.Utc),
        Policy = policy,
    };

    [Fact]
    public void Write_overwrites_report_and_appends_log()
    {
        var dir = NewTempDir();
        var writer = new JsonReportWriter(new InfrastructureOptions { OutDir = dir });

        writer.Write(Report("policy"));
        writer.Write(Report("extract"));

        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, JsonReportWriter.ReportFile))).RootElement;
        Assert.Equal("extract", json.GetProperty("command").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("posting").ValueKind);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(dir, JsonReportWriter.LogFile)).Length);
    }

    [Fact]
    public void Find_last_policy_reads_latest_policy_line()
    {
        var dir = NewTempDir();
        var writer = new JsonReportWriter(new InfrastructureOptions { OutDir = dir });

        Assert.Null(writer.FindLastPolicy());

        writer.Write(Report("policy", new PolicySummary { GeneralClientDisallowsAll = false }));
        writer.Write(Report("policy", new PolicySummary { GeneralClientDisallowsAll = true, JobPathsDisallowedForGeneralClient = true }));
        writer.Write(Report("fetch"));

        var policy = writer.FindLastPolicy();
        Assert.NotNull(policy);
        Assert.True(policy!.GeneralClientDisallowsAll);
        Assert.True(policy.JobPathsDisallowedForGeneralClient);
    }
}
