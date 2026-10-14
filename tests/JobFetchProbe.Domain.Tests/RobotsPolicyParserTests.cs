using JobFetchProbe.Domain.Rules;

namespace JobFetchProbe.Domain.Tests;

public class RobotsPolicyParserTests
{
    private static string Sample() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "robots-sample.txt"));

    [Fact]
    public void Sample_star_group_disallows_everything()
    {
        var policy = RobotsPolicyParser.Analyze(Sample());

        Assert.True(policy.GeneralClientDisallowsAll);
        Assert.True(policy.JobPathsDisallowedForGeneralClient);
        Assert.Equal(new[] { "Disallow: /" }, policy.GeneralClientRules);
    }

    [Fact]
    public void Sample_job_lines_come_from_named_bot_group()
    {
        var policy = RobotsPolicyParser.Analyze(Sample());

        Assert.Equal(
            new[]
            {
                "[ExampleSearchBot] Disallow: /jobs-guest/",
                "[ExampleSearchBot] Disallow: /job-apply/",
                "[ExampleSearchBot] Allow: /jobs/view/",
            },
            policy.JobRuleLines);
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
        var policy = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs\n");

        Assert.False(policy.GeneralClientDisallowsAll);
        Assert.True(policy.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Disallow_guest_only_still_counts_as_job_path()
    {
        var policy = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs-guest/\n");

        Assert.True(policy.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Unrelated_or_empty_disallow_does_not_mark_job_paths()
    {
        var policy = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /private/\nDisallow:\n");

        Assert.False(policy.GeneralClientDisallowsAll);
        Assert.False(policy.JobPathsDisallowedForGeneralClient);
    }

    [Fact]
    public void Named_bot_rules_do_not_apply_to_general_client()
    {
        var policy = RobotsPolicyParser.Analyze("User-agent: SomeBot\nDisallow: /jobs\n\nUser-agent: *\nAllow: /\n");

        Assert.False(policy.JobPathsDisallowedForGeneralClient);
        Assert.Equal(new[] { "[SomeBot] Disallow: /jobs" }, policy.JobRuleLines);
    }

    [Fact]
    public void Wildcards_are_matched_literally()
    {
        var policy = RobotsPolicyParser.Analyze("User-agent: *\nDisallow: /jobs*\n");

        Assert.False(policy.JobPathsDisallowedForGeneralClient);
    }
}
