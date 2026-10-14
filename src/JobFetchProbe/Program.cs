using JobFetchProbe;
using JobFetchProbe.Extract;
using JobFetchProbe.Fetch;
using JobFetchProbe.Policy;
using JobFetchProbe.Report;

return await ProbeRunner.RunAsync(args, HttpPageFetcher.Create, Console.Out, Console.Error);

namespace JobFetchProbe
{
    /// <summary>
    /// Command dispatch and exit codes. The fetcher factory is only invoked by commands
    /// that are allowed to touch the network, after argument validation has passed.
    /// </summary>
    public static class ProbeRunner
    {
        public const int ExitOk = 0;
        public const int ExitInvalidArgs = 2;
        public const int ExitStopped = 3;
        public const int ExitTransport = 4;

        public static async Task<int> RunAsync(
            string[] args,
            Func<int, IPageFetcher> fetcherFactory,
            TextWriter stdout,
            TextWriter stderr,
            Func<DateTime>? clock = null)
        {
            var now = clock ?? (() => DateTime.UtcNow);

            if (args is ["--help"] or ["-h"] or ["help"])
            {
                stdout.WriteLine(ProbeOptions.Usage);
                return ExitOk;
            }

            ProbeOptions options;
            try
            {
                options = ProbeOptions.Parse(args);
            }
            catch (ArgumentException ex)
            {
                stderr.WriteLine($"error: {ex.Message}");
                stderr.WriteLine(ProbeOptions.Usage);
                return ExitInvalidArgs;
            }

            try
            {
                return options.Command switch
                {
                    ProbeCommand.Extract => RunExtract(options, stdout, now),
                    ProbeCommand.Policy => await RunPolicyAsync(options, fetcherFactory(options.TimeoutSeconds), stdout, now),
                    ProbeCommand.Fetch => await RunFetchAsync(options, fetcherFactory(options.TimeoutSeconds), stdout, stderr, now),
                    _ => ExitInvalidArgs,
                };
            }
            catch (FormatException ex)
            {
                stderr.WriteLine($"error: {ex.Message}");
                return ExitInvalidArgs;
            }
        }

        private static ProbeReport NewReport(string command, Func<DateTime> now)
        {
            var started = Truncate(now());
            return new ProbeReport
            {
                RunId = started.ToString("yyyyMMdd'T'HHmmss'Z'"),
                Command = command,
                StartedAt = started,
            };
        }

        private static int Finish(ProbeReport report, int exitCode, string outDir, Func<DateTime> now)
        {
            report.FinishedAt = Truncate(now());
            report.ExitCode = exitCode;
            ReportWriter.Write(report, outDir);
            return exitCode;
        }

        private static int RunExtract(ProbeOptions o, TextWriter stdout, Func<DateTime> now)
        {
            var report = NewReport("extract", now);
            IJobExtractor extractor = new FixtureJobExtractor();
            var posting = extractor.Extract(o.HtmlPath!);
            report.Posting = posting;

            stdout.WriteLine(
                $"extract source={posting.SourcePath} title=\"{posting.Title}\" " +
                $"company=\"{posting.Company}\" location=\"{posting.Location}\" description-chars={posting.Description.Length}");
            return Finish(report, ExitOk, o.OutDir, now);
        }

        private static async Task<int> RunPolicyAsync(ProbeOptions o, IPageFetcher fetcher, TextWriter stdout, Func<DateTime> now)
        {
            var report = NewReport("policy", now);
            var (fetch, policy) = await new RobotsPolicyClient(fetcher).ReadAsync();
            ApplyFetch(report, fetch);

            if (fetch.TransportError is not null)
            {
                report.Note = fetch.TransportError;
                stdout.WriteLine($"policy status=0 error={fetch.TransportError}");
                return Finish(report, ExitTransport, o.OutDir, now);
            }

            if (policy is null)
            {
                report.Note = $"robots.txt returned status {fetch.Status}; no policy parsed";
                stdout.WriteLine($"policy status={fetch.Status} parsed=no");
                return Finish(report, ExitStopped, o.OutDir, now);
            }

            report.Policy = new PolicySummary
            {
                GeneralClientDisallowsAll = policy.GeneralClientDisallowsAll,
                JobPathsDisallowedForGeneralClient = policy.JobPathsDisallowedForGeneralClient,
                GeneralClientRules = policy.GeneralClientRules,
                JobRuleLines = policy.JobRuleLines,
            };

            stdout.WriteLine(
                $"policy general-client={(policy.GeneralClientDisallowsAll ? "disallow-all" : "not-disallow-all")} " +
                $"job-paths={(policy.JobPathsDisallowedForGeneralClient ? "disallowed" : "not-disallowed")}");

            var disallowed = policy.GeneralClientDisallowsAll || policy.JobPathsDisallowedForGeneralClient;
            if (disallowed)
            {
                report.Note = "job paths are disallowed for a general client; phase 2 stays off unless the operator passes --live after reading this";
            }
            return Finish(report, disallowed ? ExitStopped : ExitOk, o.OutDir, now);
        }

        private static async Task<int> RunFetchAsync(
            ProbeOptions o, IPageFetcher fetcher, TextWriter stdout, TextWriter stderr, Func<DateTime> now)
        {
            var report = NewReport("fetch", now);
            report.Policy = ReportWriter.LastPolicy(o.OutDir);
            if (report.Policy is null)
            {
                stderr.WriteLine("note: no earlier policy run in the report directory; the lab note should record phase 0 first");
            }

            var fetch = await fetcher.GetAsync(o.Url!, "text/html");
            ApplyFetch(report, fetch);

            var transportFailed = fetch.TransportError is not null;
            var visible = transportFailed ? string.Empty : HtmlText.Visible(fetch.Body);
            var hasH1 = !transportFailed && HtmlText.HasH1(fetch.Body);
            // The body goes no further than this method.

            var result = ResponseClassifier.Classify(new ClassifierInput(
                fetch.Status, fetch.FinalUrl, visible, hasH1, transportFailed));

            report.Classification = result.Class.ToString();
            report.ClassificationReason = result.Reason;
            report.VisibleTextLength = visible.Length;
            report.Note = fetch.TransportError ?? fetch.StopReason;

            stdout.WriteLine($"fetch host={fetch.FinalUrl.Host} status={fetch.Status} class={result.Class}");

            var exit = result.Class switch
            {
                FetchClassification.DescriptionPresent => ExitOk,
                FetchClassification.Error when transportFailed => ExitTransport,
                _ => ExitStopped,
            };
            return Finish(report, exit, o.OutDir, now);
        }

        private static void ApplyFetch(ProbeReport report, FetchResult fetch)
        {
            report.RequestsSent = fetch.RequestsSent;
            report.LiveRequestSent = fetch.RequestsSent > 0;
            report.HttpStatus = fetch.Status == 0 ? null : fetch.Status;
            report.ContentType = fetch.ContentType;
            report.BodyLength = fetch.TransportError is null ? fetch.BodyLength : null;
            report.FinalUrlHost = fetch.FinalUrl.Host;
            report.Redirects = fetch.Hops;
        }

        private static DateTime Truncate(DateTime t)
        {
            var utc = t.ToUniversalTime();
            return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        }
    }
}
