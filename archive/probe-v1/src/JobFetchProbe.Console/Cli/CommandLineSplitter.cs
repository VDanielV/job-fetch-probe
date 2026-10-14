using System.Text;

namespace JobFetchProbe.Console.Cli;

/// <summary>
/// Splits a typed line into arguments. Whitespace separates arguments; "double" or 'single' quotes
/// keep spaces together (for paths like "C:\My Files\job.html"). Backslashes are kept as typed.
/// </summary>
public static class CommandLineSplitter
{
    /// <exception cref="ArgumentException">A quote is not closed.</exception>
    public static IReadOnlyList<string> Split(string line)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inToken = false;
        char? quote = null;

        foreach (var c in line)
        {
            if (quote is not null)
            {
                if (c == quote)
                {
                    quote = null;
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                inToken = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inToken)
                {
                    args.Add(current.ToString());
                    current.Clear();
                    inToken = false;
                }
            }
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        if (quote is not null)
        {
            throw new ArgumentException($"missing closing {quote} quote");
        }
        if (inToken)
        {
            args.Add(current.ToString());
        }

        return args;
    }
}
