using JobFetchProbe.Console.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace JobFetchProbe.Console;

/// <summary>
/// Prompt loop for typing commands by hand. Each command runs exactly as in one-shot mode.
/// The plan's per-run limits apply to the whole session: one policy request and one fetch request.
/// </summary>
public sealed class InteractiveShell(
    TextReader input,
    TextWriter stdout,
    TextWriter stderr,
    Action<IServiceCollection>? configureServices = null)
{
    public const string Prompt = "probe> ";

    public const string Help =
        """
        commands:
          policy                                      read robots.txt (one request per session)
          extract --html <path>                       extract a job from a local HTML file (no network)
          fetch --url <job-view-url> --live           one GET of one job URL (one request per session)
          limits                                      show what this session has used
          help                                        show this text
          exit                                        leave

        shared flags: --out <dir>  --timeout-seconds <5-30>
        quote paths that contain spaces: extract --html "C:\My Files\job.html"
        """;

    private bool _policyUsed;
    private bool _fetchUsed;

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        stdout.WriteLine("JobFetchProbe interactive mode. Type 'help' for commands, 'exit' to quit.");
        stdout.WriteLine("Session limits: one policy request and one fetch request.");

        while (!ct.IsCancellationRequested)
        {
            stdout.Write(Prompt);
            stdout.Flush();

            var line = await input.ReadLineAsync(ct);
            if (line is null)
            {
                stdout.WriteLine();
                break; // end of input (Ctrl+Z / Ctrl+D)
            }

            IReadOnlyList<string> args;
            try
            {
                args = CommandLineSplitter.Split(line);
            }
            catch (ArgumentException ex)
            {
                stderr.WriteLine($"error: {ex.Message}");
                continue;
            }

            if (args.Count == 0)
            {
                continue;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "exit" or "quit":
                    return ExitCodes.Ok;
                case "help" or "?":
                    stdout.WriteLine(Help);
                    continue;
                case "limits":
                    stdout.WriteLine($"policy: {(_policyUsed ? "used" : "available")}  fetch: {(_fetchUsed ? "used" : "available")}");
                    continue;
                case "policy" when _policyUsed:
                    stderr.WriteLine("error: the policy request for this session is already used; the result is in the report directory");
                    continue;
                case "fetch" when _fetchUsed:
                    stderr.WriteLine("error: the one job URL allowed for this session is already used; do not refetch to get a different outcome");
                    continue;
            }

            var exit = await ProbeApp.RunAsync([.. args], stdout, stderr, configureServices, ct);

            // Exit code 2 means arguments were rejected before any request; anything else used the request.
            if (exit != ExitCodes.InvalidArguments)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "policy":
                        _policyUsed = true;
                        break;
                    case "fetch":
                        _fetchUsed = true;
                        break;
                }
            }

            stdout.WriteLine($"(exit code {exit})");
        }

        return ExitCodes.Ok;
    }
}
