namespace Oopsling.Tests;

// Runs against this machine's own Application log, whose events come from other apps, so the interop reads real events.
// The sample's native crash in scripts/crash-check.sh reads one of its own.
public class ApplicationLogTests
{
    [Fact]
    public void Finds_no_crash_for_an_app_that_never_ran()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows has an Application log.");
            return;
        }

        var (crashes, _) = new ApplicationLog(@"C:\Made Up\Never Ran.exe").Crashes(0, DateTime.UtcNow.AddDays(-7));

        Assert.Empty(crashes);
    }
}
