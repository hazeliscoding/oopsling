using System.Runtime.InteropServices;

namespace Oopsling.Tests;

// Start is only ever called here with arguments it refuses, so the test host's own crashes are never recorded.
public class CrashReportsTests
{
    [Theory]
    [InlineData("staypaw")]
    [InlineData("hazeliscoding/")]
    [InlineData("hazeliscoding/staypaw/issues")]
    public void Start_wants_the_repo_as_owner_and_name(string repo) =>
        Assert.Throws<ArgumentException>(() => CrashReports.Start("Sample", repo));

    [Fact]
    public void Start_wants_an_app_name_it_can_use_as_a_folder() =>
        Assert.Throws<ArgumentException>(() => CrashReports.Start("Sample/../elsewhere", "hazeliscoding/staypaw"));

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
