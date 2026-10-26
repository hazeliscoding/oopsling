using System.Text.RegularExpressions;

namespace Oopsling;

/// <summary>
/// Hides the home folder, the user, machine and domain names, and token-like strings before a report is written,
/// since a report becomes a public issue. It masks too much rather than too little.
/// </summary>
internal sealed partial class Mask(string home, string user, string machine = "", string domain = "")
{
    // The longer names go first, so a machine named after its user still reads as the machine.
    private readonly (Regex Pattern, string Placeholder)[] _names =
        [.. new[] { (machine, "<machine>"), (domain, "<domain>"), (user, "<user>") }
            .Where(name => name.Item1.Length > 0)
            .Select(name => (Name(name.Item1), name.Item2))];

    public string Apply(string text)
    {
        if (home.Length > 0)
        {
            text = text
                .Replace(home, "<home>", StringComparison.OrdinalIgnoreCase)
                .Replace(home.Replace('\\', '/'), "<home>", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var (pattern, placeholder) in _names)
        {
            text = pattern.Replace(text, placeholder);
        }

        text = UrlCredentials().Replace(text, "<token>@");
        text = SecretValue().Replace(text, "${name}${separator}<token>");
        text = KnownToken().Replace(text, "<token>");
        return LongOpaque().Replace(text, "<token>");
    }

    // Matched on its own, not only inside the home folder: paths reach messages in other shapes, like
    // C--Users-<name>-project, and URLs percent-encode the separators around it.
    private static Regex Name(string name) => new(
        $"(?:(?<![A-Za-z0-9])|(?<=%[0-9A-Fa-f]{{2}})){Regex.Escape(name)}(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // The user and password in https://user:password@host. A password can hold @, ?, # or / unescaped, so this runs to
    // the last @ before a space. It also masks the host of a URL with an @ further on, like an npm scope: too much
    // rather than too little.
    [GeneratedRegex(@"(?<=://)\S*@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredentials();

    // The value after a name like token=, api_key= or password:. Unlike Sweepling's CommandLineMask, a space alone
    // doesn't count, or every "CancellationToken cancellationToken" in a stack trace would be masked.
    [GeneratedRegex(@"(?<name>[\w-]*(token|secret|password|passwd|api[-_]?key|auth)[\w-]*)(?<separator>\s*[=:]\s*)(?!<token>)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretValue();

    // GitHub, OpenAI and Anthropic, Slack and AWS key prefixes, and JWTs.
    [GeneratedRegex(@"(gh[pousr]_|github_pat_|sk-|xox[abpr]-|AKIA|eyJ)[A-Za-z0-9._\-]{8,}", RegexOptions.CultureInvariant)]
    private static partial Regex KnownToken();

    // A run of 32 or more letters and digits that has both, like a key or an ID.
    [GeneratedRegex(@"(?=[A-Za-z0-9+=_-]*\d)(?=[A-Za-z0-9+=_-]*[A-Za-z])[A-Za-z0-9+=_-]{32,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongOpaque();
}
