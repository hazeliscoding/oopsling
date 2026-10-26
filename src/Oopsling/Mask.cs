using System.Text.RegularExpressions;

namespace Oopsling;

/// <summary>
/// Hides the home folder, the user name and token-like strings before a report is written, since a report becomes
/// a public issue. It masks too much rather than too little.
/// </summary>
internal sealed partial class Mask(string home, string user)
{
    // Matched on its own, not only inside the home folder: paths reach messages in other shapes, like C--Users-<name>-project.
    private readonly Regex? _user = user.Length == 0
        ? null
        : new Regex($"(?<![A-Za-z0-9]){Regex.Escape(user)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Apply(string text)
    {
        if (home.Length > 0)
        {
            text = text
                .Replace(home, "<home>", StringComparison.OrdinalIgnoreCase)
                .Replace(home.Replace('\\', '/'), "<home>", StringComparison.OrdinalIgnoreCase);
        }

        if (_user is not null)
        {
            text = _user.Replace(text, "<user>");
        }

        text = SecretValue().Replace(text, "${name}${separator}<token>");
        text = KnownToken().Replace(text, "<token>");
        return LongOpaque().Replace(text, "<token>");
    }

    // The value after a name like --token=, api_key= or password. The patterns are Sweepling's CommandLineMask.
    [GeneratedRegex(@"(?<name>[\w-]*(token|secret|password|passwd|api[-_]?key|auth)[\w-]*)(?<separator>=|\s+)(?!<token>)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretValue();

    // GitHub, OpenAI and Anthropic, Slack and AWS key prefixes, and JWTs.
    [GeneratedRegex(@"(gh[pousr]_|github_pat_|sk-|xox[abpr]-|AKIA|eyJ)[A-Za-z0-9._\-]{8,}", RegexOptions.CultureInvariant)]
    private static partial Regex KnownToken();

    // A run of 32 or more letters and digits that has both, like a key or an ID.
    [GeneratedRegex(@"(?=[A-Za-z0-9+=_-]*\d)(?=[A-Za-z0-9+=_-]*[A-Za-z])[A-Za-z0-9+=_-]{32,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongOpaque();
}
