namespace Oopsling.Tests;

public class MaskTests
{
    // Made-up home folders and user names: no real one reaches a test.
    private static readonly Mask Windows = new(@"C:\Users\Wren", "Wren");
    private static readonly Mask Linux = new("/home/wren", "wren");

    // Token-shaped values are built at run time, so no literal ever reaches the history.
    private static readonly string GitHubToken = "gh" + "p_" + new string('x', 36);

    [Theory]
    [InlineData(@"Could not find file 'C:\Users\Wren\AppData\x.json'.", @"Could not find file '<home>\AppData\x.json'.")]
    [InlineData("Could not find file 'C:/Users/Wren/AppData/x.json'.", "Could not find file '<home>/AppData/x.json'.")]
    [InlineData(@"c:\users\wren\x", @"<home>\x")]
    public void Masks_the_home_folder(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Fact]
    public void Masks_a_Linux_home_folder() => Assert.Equal("<home>/.config/app", Linux.Apply("/home/wren/.config/app"));

    [Theory]
    [InlineData("C--Users-Wren-project", "C--Users-<user>-project")]
    [InlineData("Access denied for WREN.", "Access denied for <user>.")]
    [InlineData(@"\\server\share\wren\notes.txt", @"\\server\share\<user>\notes.txt")]
    public void Masks_the_user_name_wherever_it_appears(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    // A stack trace full of mangled method names would be no use to anyone.
    [Fact]
    public void Leaves_a_word_that_contains_the_user_name() => Assert.Equal("at Tools.Wrench.Tighten()", Windows.Apply("at Tools.Wrench.Tighten()"));

    [Fact]
    public void Masks_a_token_with_a_known_prefix() => Assert.Equal("Bad credentials: <token>", Windows.Apply($"Bad credentials: {GitHubToken}"));

    [Theory]
    [InlineData("api_key=abc123", "api_key=<token>")]
    [InlineData("Invalid password hunter2", "Invalid password <token>")]
    public void Masks_the_value_after_a_secret_name(string text, string expected) => Assert.Equal(expected, Windows.Apply(text));

    [Fact]
    public void Masks_a_long_opaque_value() => Assert.Equal("key <token>", Windows.Apply("key " + string.Concat(Enumerable.Repeat("a1", 20))));
}
