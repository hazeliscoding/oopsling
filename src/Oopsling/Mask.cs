using System.Text.RegularExpressions;

namespace Oopsling;

/// <summary>
/// Hides the home folder, the user, machine and domain names, and token-like strings before a report is written,
/// since a report becomes a public issue. It masks too much rather than too little.
/// </summary>
internal sealed partial class Mask
{
    // Stands in for a dot or a space in a name, which can come back as other punctuation or percent-encoded.
    private const string Punctuation = "(?:[^\\p{L}\\p{N}]|%[0-9A-Fa-f]{2}){1,3}";

    private readonly Regex? _home;
    private readonly (Regex Pattern, string Placeholder)[] _names;

    public Mask(string home, string user, string machine = "", string domain = "")
    {
        // A home folder that is the root, as some service accounts have, would mask every path.
        var trimmed = home.TrimEnd('\\', '/');
        var isRoot = trimmed.Length == 0 || (trimmed.Length == 2 && trimmed[1] == ':');

        // Either slash, doubled in JSON or percent-encoded in a URL, and not the start of a longer name like C:\Users\Wrenna.
        _home = isRoot ? null : new(
            string.Join(@"(?:\\{1,2}|/|%5C|%2F)", trimmed.Split('\\', '/').Select(Regex.Escape)) + "(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // A renamed account keeps its old folder, so the folder's name is masked as well as the user name. The longer
        // names go first, so a machine named after its user still reads as the machine, and the folder "Wren Example"
        // isn't half masked as the user "wren".
        var folder = isRoot ? "" : trimmed[(trimmed.LastIndexOfAny(['\\', '/']) + 1)..];
        _names = [.. new[] { (machine, "<machine>"), (domain, "<domain>"), (user, "<user>"), (folder, "<user>") }
            .Where(name => NamePunctuation().Replace(name.Item1, "").Length > 0)
            .OrderByDescending(name => name.Item1.Length)
            .Select(name => (Name(name.Item1, cutShort: name.Item2 == "<machine>" && name.Item1.Length == 15), name.Item2))];
    }

    public string Apply(string text)
    {
        if (_home is not null)
        {
            text = _home.Replace(text, "<home>");
        }

        foreach (var (pattern, placeholder) in _names)
        {
            text = pattern.Replace(text, placeholder);
        }

        text = UsersFolder().Replace(text, "<user>");
        text = UrlCredentials().Replace(text, "<token>@");
        text = SecretValue().Replace(text, "${name}${separator}${scheme}<token>");
        text = KnownToken().Replace(text, "<token>");
        return LongOpaque().Replace(text, "<token>");
    }

    // Matched on its own, not only inside the home folder: paths reach messages in other shapes, like
    // C--Users-<name>-project, and URLs percent-encode the separators around it. Windows cuts a machine's NetBIOS name
    // to 15 characters, so one that long may be the start of a longer host name.
    private static Regex Name(string name, bool cutShort)
    {
        var parts = NamePunctuation().Split(name).Where(part => part.Length > 0).Select(Regex.Escape);
        return new(
            $"(?:(?<![A-Za-z0-9])|(?<=%[0-9A-Fa-f]{{2}})){string.Join(Punctuation, parts)}{(cutShort ? "[A-Za-z0-9-]*" : "")}(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NamePunctuation();

    // Whatever follows Users\ or home/ is someone's folder, even when it isn't this user's: an 8.3 short name like
    // WRENEX~1, or another account.
    [GeneratedRegex(@"(?<=(?:^|[^A-Za-z0-9]|%[0-9A-Fa-f]{2})(?:Users|home)(?:\\{1,2}|/|%5C|%2F))[^\\/%\s'""<>|:*?]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsersFolder();

    // The patterns below run in a dying process, so none may backtrack on a long run: each starts only where a run starts,
    // or looks a bounded distance ahead.

    // The user and password in https://user:password@host. A password can hold @, ?, # or / unescaped, so this runs to
    // the last @ before a space, up to 256 characters on. It also masks the host of a URL with an @ further on, like an
    // npm scope: too much rather than too little.
    [GeneratedRegex(@"(?<=://)\S{0,256}@", RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredentials();

    // The value after a name like token=, Pwd=, AccountKey= or password:, and after Bearer or Basic in an Authorization
    // header. Unlike Sweepling's CommandLineMask, a space alone doesn't count, or every "CancellationToken
    // cancellationToken" in a stack trace would be masked. Nor does an exception's type, whose colon comes before its
    // message, as in "AuthenticationException: The remote certificate is invalid".
    [GeneratedRegex(@"(?<![\w-])(?<name>(?=[\w-]*?(token|secret|password|passwd|pwd|auth|api[-_]?key|keys?(?![\w-])))(?>[\w-]+))(?<!Exception)(?<separator>\s*[=:]\s*)(?<scheme>(Bearer|Basic|Token)\s+)?(?!<token>)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretValue();

    // GitHub, OpenAI and Anthropic, Slack and AWS key prefixes, and JWTs.
    [GeneratedRegex(@"(gh[pousr]_|github_pat_|sk-|xox[abpr]-|AKIA|eyJ)[A-Za-z0-9._\-]{8,}", RegexOptions.CultureInvariant)]
    private static partial Regex KnownToken();

    // A run of 32 or more letters and digits that has both, like a key or an ID.
    [GeneratedRegex(@"(?<![A-Za-z0-9+=_-])(?=[A-Za-z0-9+=_-]*\d)(?=[A-Za-z0-9+=_-]*[A-Za-z])[A-Za-z0-9+=_-]{32,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongOpaque();
}
