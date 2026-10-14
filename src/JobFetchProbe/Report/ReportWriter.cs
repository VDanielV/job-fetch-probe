using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JobFetchProbe.Report;

public static class ReportWriter
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

    /// <summary>Overwrites probe-report.json and appends one line to probe-log.jsonl.</summary>
    public static string Write(ProbeReport report, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var reportPath = Path.Combine(outDir, ReportFile);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, Pretty) + Environment.NewLine);
        File.AppendAllText(Path.Combine(outDir, LogFile), JsonSerializer.Serialize(report, Compact) + "\n");
        return reportPath;
    }

    /// <summary>The two booleans from the most recent successful policy run in the log, if any.</summary>
    public static PolicySummary? LastPolicy(string outDir)
    {
        var path = Path.Combine(outDir, LogFile);
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
