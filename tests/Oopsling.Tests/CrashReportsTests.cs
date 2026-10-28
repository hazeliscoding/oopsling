using System.Runtime.InteropServices;

namespace Oopsling.Tests;

// Start is only ever called here with arguments it refuses, so the test host's own crashes are never recorded.
public class CrashReportsTests
{
    [Theory]
    [InlineData("staypaw")]
    [InlineData("hazeliscoding/")]
    [InlineData("hazeliscoding/staypaw/issues")]
    [InlineData("hazeliscoding/staypaw?labels=bug")]
    [InlineData("hazeliscoding/staypaw#x")]
    [InlineData(" hazeliscoding / staypaw ")]
    [InlineData("hazeliscoding/staypaw\n")]
    [InlineData("../evil")]
    [InlineData("hazeliscoding/..")]
    public void Start_wants_the_repo_as_owner_and_name(string repo) =>
        Assert.Contains($"\"{repo}\"", Assert.Throws<ArgumentException>(() => CrashReports.Start("Sample", repo)).Message);

    [Theory]
    [InlineData("hazeliscoding/staypaw")]
    [InlineData("some-owner/my.repo_name-2")]
    public void A_repo_is_an_owner_and_a_name(string repo) => Assert.True(CrashReports.IsRepo(repo));

    [Fact]
    public void Start_wants_the_contact_as_an_email_address_or_an_https_link() =>
        Assert.Contains("\"example.com\"", Assert.Throws<ArgumentException>(() => CrashReports.Start("Sample", "hazeliscoding/staypaw", "example.com")).Message);

    // The contact becomes a link the shell opens, so only these two kinds get through.
    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("crashes@example")]
    [InlineData("crashes@@example.com")]
    [InlineData("crashes @example.com")]
    [InlineData("crashes@example.com?subject=x")]
    [InlineData("a@example.com,b@example.com")]
    [InlineData("mailto:crashes@example.com")]
    [InlineData("http://example.com/support")]
    [InlineData("https://")]
    [InlineData("https://example.com/a b")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    public void Anything_else_is_not_a_contact(string contact) => Assert.False(CrashReports.IsContact(contact));

    [Theory]
    [InlineData("crashes@example.com")]
    [InlineData("first.last+app@mail.example.org")]
    [InlineData("https://example.com/support")]
    [InlineData("https://discord.gg/abc123")]
    public void A_contact_is_an_email_address_or_an_https_link(string contact) => Assert.True(CrashReports.IsContact(contact));

    [Fact]
    public void Start_wants_an_app_name_it_can_use_as_a_folder() =>
        Assert.Contains("\"Sample/../elsewhere\"", Assert.Throws<ArgumentException>(() => CrashReports.Start("Sample/../elsewhere", "hazeliscoding/staypaw")).Message);

    [Fact]
    public void TakeUnshown_wants_Start_first() => Assert.Throws<InvalidOperationException>(CrashReports.TakeUnshown);

    [Fact]
    public void Details_name_the_app_runtime_and_architecture()
    {
        var details = CrashReports.Details("Sample", "1.2.3+abc");

        Assert.Contains("- App: Sample 1.2.3+abc", details);
        Assert.Contains(RuntimeInformation.OSDescription, details);
        Assert.Contains(RuntimeInformation.FrameworkDescription, details);
        Assert.Contains(RuntimeInformation.ProcessArchitecture.ToString(), details);
        Assert.Contains("- NativeAOT: no", details);
    }
}
