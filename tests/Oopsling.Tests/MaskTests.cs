using System.Diagnostics;

namespace Oopsling.Tests;

public class MaskTests
{
    // Made-up home folders and user names: no real one reaches a test.
    private static readonly Mask Windows = new(@"C:\Users\Wren", "Wren", "WRENS-PC", "BRIGHTCORP");
    private static readonly Mask Linux = new("/home/wren", "wren");

    // Token-shaped values are built at run time, so no literal ever reaches the history.
    private static readonly string GitHubToken = "gh" + "p_" + new string('x', 36);

    [Theory]
    [InlineData(@"Could not find file 'C:\Users\Wren\AppData\x.json'.", @"Could not find file '<home>\AppData\x.json'.")]
    [InlineData("Could not find file 'C:/Users/Wren/AppData/x.json'.", "Could not find file '<home>/AppData/x.json'.")]
    [InlineData(@"c:\users\wren\x", @"<home>\x")]
    [InlineData(@"Could not read C:\\Users\\Wren\\x.json", @"Could not read <home>\\x.json")]
    public void Masks_the_home_folder(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Fact]
    public void Masks_a_Linux_home_folder() => Assert.Equal("<home>/.config/app", Linux.Apply("/home/wren/.config/app"));

    [Theory]
    [InlineData("C--Users-Wren-project", "C--Users-<user>-project")]
    [InlineData("Access denied for WREN.", "Access denied for <user>.")]
    [InlineData(@"\\server\share\wren\notes.txt", @"\\server\share\<user>\notes.txt")]
    [InlineData("C%3A%5CUsers%5CWren%5Cnotes.txt", "C%3A%5CUsers%5C<user>%5Cnotes.txt")]
    public void Masks_the_user_name_wherever_it_appears(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Theory]
    // A renamed account keeps its old folder, whose name isn't the user name.
    [InlineData(@"C:\Users\Wren", "someone", "C--Users-Wren-project", "C--Users-<user>-project")]
    [InlineData(@"C:\Users\Wren", "someone", "C%3A%5CUsers%5CWren%5Cnotes", "C%3A%5CUsers%5C<user>%5Cnotes")]
    // An 8.3 short name, or another user's folder.
    [InlineData(@"C:\Users\Wren", "Wren", @"C:\Users\WRENEX~1\AppData", @"C:\Users\<user>\AppData")]
    [InlineData(@"C:\Users\Wren", "Wren", @"C:\Users\Wrenna\AppData", @"C:\Users\<user>\AppData")]
    [InlineData("/home/wren", "wren", "/home/someone/.config", "/home/<user>/.config")]
    // A name with a dot or a space, written with other punctuation in its place.
    [InlineData(@"C:\Users\wren.example", "wren.example", "C--Users-wren-example-project", "C--Users-<user>-project")]
    [InlineData(@"C:\Users\Wren Example", "wren", "file:///C:/Users/Wren%20Example/notes.txt", "file:///C:/Users/<user>/notes.txt")]
    public void Masks_a_users_folder_in_any_shape(string home, string user, string text, string expected) =>
        Assert.Equal(expected, new Mask(home, user).Apply(text));

    // Windows cuts a machine's NetBIOS name to 15 characters, and host names still carry the whole of it.
    [Fact]
    public void Masks_the_whole_of_a_machine_name_that_Windows_cut_short() =>
        Assert.Equal("Could not reach <machine>.local", new Mask(@"C:\Users\Wren", "Wren", "WRENS-GAMING-DE").Apply("Could not reach wrens-gaming-desktop.local"));

    [Fact]
    public void Leaves_paths_alone_when_the_home_folder_is_the_root() =>
        Assert.Equal("/usr/lib/libSkiaSharp.so", new Mask("/", "wren").Apply("/usr/lib/libSkiaSharp.so"));

    [Theory]
    [InlineData(@"Access to \\WRENS-PC\share was denied.", @"Access to \\<machine>\share was denied.")]
    [InlineData(@"Could not sign in as BRIGHTCORP\someone.", @"Could not sign in as <domain>\someone.")]
    public void Masks_the_machine_and_domain_names(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Theory]
    [InlineData("Could not reach https://deploy:hunter2@example.com/feed", "Could not reach https://<token>@example.com/feed")]
    [InlineData("Could not reach https://deploy:p@ss@example.com/feed", "Could not reach https://<token>@example.com/feed")]
    [InlineData("Could not reach https://deploy:p?s#s@example.com/feed", "Could not reach https://<token>@example.com/feed")]
    [InlineData("Could not reach https://deploy:p/ss@example.com/feed", "Could not reach https://<token>@example.com/feed")]
    public void Masks_credentials_in_a_URL(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    // Parameter names like these fill .NET stack traces.
    [Fact]
    public void Leaves_a_token_parameter_in_a_stack_trace() =>
        Assert.Equal("at Feed.Load(CancellationToken cancellationToken)", Windows.Apply("at Feed.Load(CancellationToken cancellationToken)"));

    // A stack trace full of mangled method names would be no use to anyone.
    [Fact]
    public void Leaves_a_word_that_contains_the_user_name() => Assert.Equal("at Tools.Wrench.Tighten()", Windows.Apply("at Tools.Wrench.Tighten()"));

    [Fact]
    public void Masks_a_token_with_a_known_prefix() => Assert.Equal("Bad credentials: <token>", Windows.Apply($"Bad credentials: {GitHubToken}"));

    [Theory]
    [InlineData("api_key=abc123", "api_key=<token>")]
    [InlineData("Login failed, password: hunter2", "Login failed, password: <token>")]
    public void Masks_the_value_after_a_secret_name(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Fact]
    public void Masks_a_long_opaque_value() => Assert.Equal("key <token>", Windows.Apply("key " + string.Concat(Enumerable.Repeat("a1", 20))));

    // The mask runs in a dying process on up to 256 KB, so no message may keep it busy. Runs of these a hundredth this
    // long took seconds once.
    [Theory]
    [InlineData("auth", 65_536)]
    [InlineData("a", 262_144)]
    [InlineData("://", 87_381)]
    public void Masks_a_pathological_message_quickly(string part, int count)
    {
        var text = string.Concat(Enumerable.Repeat(part, count));
        Windows.Apply("warm up");

        var time = Stopwatch.StartNew();
        Windows.Apply(text);

        Assert.True(time.Elapsed < TimeSpan.FromSeconds(1), $"Took {time.Elapsed}.");
    }
}
