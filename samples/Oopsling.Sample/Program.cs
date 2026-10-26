using Avalonia;

namespace Oopsling.Sample;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Checks pass --repo to file in a scratch repo, so trying the link never touches Oopsling's own issues.
        CrashReports.Start("Oopsling Sample", args is ["--repo", var repo, ..] ? repo : "hazeliscoding/oopsling");
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
