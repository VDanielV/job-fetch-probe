using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JobFetchProbe.Application.Abstractions;
using JobFetchProbe.Application.Dtos;

namespace JobFetchProbe.Infrastructure.Reporting;

/// <summary>Writes <c>probe-report.json</c> (overwritten each run) and <c>probe-log.jsonl</c> (one line appended per run).</summary>
public sealed class JsonReportWriter(InfrastructureOptions options) : IReportWriter
{
    public const string ReportFile = "probe-report.json";
    public const string LogFile = "probe-log.jsonl";

    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Write(ProbeReport report)
    {
        Directory.CreateDirectory(options.OutDir);
        File.WriteAllText(Path.Combine(options.OutDir, ReportFile), JsonSerializer.Serialize(report, Pretty) + Environment.NewLine);
        File.AppendAllText(Path.Combine(options.OutDir, LogFile), JsonSerializer.Serialize(report, Compact) + "\n");
    }

    public PolicySummary? FindLastPolicy()
    {
        var path = Path.Combine(options.OutDir, LogFile);
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(path).Reverse())
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if ((string?)node?["command"] != "policy" || node["policy"] is not JsonObject policy)
            {
                continue;
            }

            return new PolicySummary
            {
                GeneralClientDisallowsAll = (bool?)policy["generalClientDisallowsAll"] ?? false,
                JobPathsDisallowedForGeneralClient = (bool?)policy["jobPathsDisallowedForGeneralClient"] ?? false,
                Source = $"{LogFile} run {(string?)node["runId"]}",
            };
        }

        return null;
    }
}
