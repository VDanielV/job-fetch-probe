using JobFetchProbe.Policy;

namespace JobFetchProbe.Tests;

public class RobotsPolicyParserTests
{
    private static string Sample() => File.ReadAllText(TestPaths.Fixture("robots-sample.txt"));

    [Fact]
    public void Sample_star_group_disallows_everything()
    {
        var report = RobotsPolicyParser.Analyze(Sample(), 200);

        Assert.True(report.GeneralClientDisallowsAll);
        Assert.True(report.JobPathsDisallowedForGeneralClient);
        Assert.Equal(new[] { "Disallow: /" }, report.GeneralClientRules);
    }

    [Fact]
    public void Sample_job_lines_come_from_named_bot_group()
    {
        var report = RobotsPolicyParser.Analyze(Sample(), 200);

        Assert.Equal(
            new[]
            {
                "[ExampleSearchBot] Disallow: /jobs-guest/",
                "[ExampleSearchBot] Disallow: /job-apply/",
                "[ExampleSearchBot] Allow: /jobs/view/",
            },
            report.JobRuleLines);
    }

    [Fact]
    public void Consecutive_user_agents_share_a_group_and_comments_are_ignored()
    {
        var groups = RobotsPolicyParser.ParseGroups(Sample());

        Assert.Equal(3, groups.Count);
        Assert.Equal(new[] { "OtherBot", "ThirdBot" }, groups[1].UserAgents);
        Assert.Equal(new[] { "Disallow: /private/" }, groups[1].Rules.Select(r => r.ToString()));
    }

    [Fact]
    public void Disallow_jobs_prefix_marks_job_paths_without_disallowing_all()
    {
        var report = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs\n", 200);

        Assert.False(report.GeneralClientDisallowsAll);
        Assert.True(report.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Disallow_guest_only_still_counts_as_job_path()
    {
        var report = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs-guest/\n", 200);

        Assert.True(report.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Unrelated_or_empty_disallow_does_not_mark_job_paths()
    {
        var report = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /private/\nDisallow:\n", 200);

        Assert.False(report.GeneralClientDisallowsAll);
        Assert.False(report.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Named_bot_rules_do_not_apply_to_general_client()
    {
        var report = RobotsPolicyParser.Analyze("User-agent: SomeBot\nDisallow: /jobs\n\nUser-agent: *\nAllow: /\n", 200);

        Assert.False(report.JobPathsDisallowedForGeneralClient);
        Assert.Equal(new[] { "[SomeBot] Disallow: /jobs" }, report.JobRuleLines);
    }

    [Fact]
    public void Wildcards_are_matched_literally()
    {
        var report = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs*\n", 200);

        Assert.False(report.JobPathsDisallowedForGeneralClient);
    }
}
