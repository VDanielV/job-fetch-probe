using JobFetchProbe.Console.Cli;

namespace JobFetchProbe.Console.Tests;

public class CommandLineSplitterTests
{
    [Fact]
    public void Splits_on_whitespace()
    {
        Assert.Equal(new[] { "extract", "--html", "a.html" }, CommandLineSplitter.Split("  extract   --html\ta.html "));
    }

    [Fact]
    public void Quotes_keep_spaces_and_backslashes()
    {
        Assert.Equal(
            new[] { "extract", "--html", @"C:\My Files\job.html" },
            CommandLineSplitter.Split(@"extract --html ""C:\My Files\job.html"""));
        Assert.Equal(new[] { "a b" }, CommandLineSplitter.Split("'a b'"));
    }

    [Fact]
    public void Empty_line_gives_no_arguments()
    {
        Assert.Empty(CommandLineSplitter.Split("   "));
    }

    [Fact]
    public void Unclosed_quote_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CommandLineSplitter.Split("extract --html \"C:\\x"));
    }
}
