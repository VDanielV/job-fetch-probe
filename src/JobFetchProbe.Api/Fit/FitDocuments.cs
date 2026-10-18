using JobFetchProbe.Services.Fit;

namespace JobFetchProbe.Api.Fit;

/// <summary>Reads the three Fit files from the API content folder. They are copied next to the app when it is built.</summary>
public sealed class FitDocuments(IHostEnvironment environment) : IFitDocuments
{
    public FitPack Load()
    {
        var folder = Path.Combine(environment.ContentRootPath, "Fit");
        return new FitPack(
            Read(folder, "candidate-profile.json"),
            Read(folder, "decision-policy.json"),
            Read(folder, "scoring-rubric.json"));
    }

    private static string Read(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The fit file is missing: {path}");
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"The fit file is empty: {path}");
        }

        return text;
    }
}
