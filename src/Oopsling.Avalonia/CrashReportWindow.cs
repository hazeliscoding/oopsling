using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Oopsling.Avalonia;

/// <summary>
/// Shows the newest crash report at the next launch, with Report on GitHub, Copy and Dismiss, in the host's theme.
/// </summary>
public sealed class CrashReportWindow : Window
{
    private readonly CrashReport _report;
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };

    internal CrashReportWindow(CrashReport report)
    {
        _report = report;
        var strings = report.Strings;
        // CrashReport fills in the English heading when the host left it out.
        var closed = strings.Closed!;
        Title = closed.TrimEnd('.');
        Width = 760;
        Height = 560;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var reportOnGitHub = new Button { Content = strings.ReportOnGitHub, Classes = { "accent" } };
        var copy = new Button { Content = strings.Copy };
        var dismiss = new Button { Content = strings.Dismiss, IsCancel = true };
        reportOnGitHub.Click += ReportOnGitHub;
        copy.Click += Copy;
        dismiss.Click += (_, _) => Close();

        var text = new TextBox
        {
            Text = report.Text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace"),
        };
        AutomationProperties.SetName(text, strings.ReportName);
        ScrollViewer.SetHorizontalScrollBarVisibility(text, ScrollBarVisibility.Auto);

        var intro = new StackPanel
        {
            Spacing = 8,
            Margin = new(0, 0, 0, 12),
            Children =
            {
                // Wraps too, since a host's language can make it longer than the narrowest window.
                new TextBlock { Text = closed, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = strings.Intro, TextWrapping = TextWrapping.Wrap },
            },
        };
        if (!report.FitsInLink)
        {
            intro.Children.Add(new TextBlock { Text = strings.TooLongForLink, TextWrapping = TextWrapping.Wrap });
        }

        intro.Children.Add(_status);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new(0, 12, 0, 0),
            Children = { reportOnGitHub, copy, dismiss },
        };
        DockPanel.SetDock(intro, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        Content = new DockPanel { Margin = new(20), Children = { intro, buttons, text } };
    }

    /// <summary>
    /// Once <paramref name="owner"/> is open, shows the newest report the user hasn't seen over it. Does nothing when the
    /// app hasn't crashed since a report was last shown. Call it where the main window is created. An app without one,
    /// such as a tray app, passes no owner, and the report shows at once in the middle of the screen. Such an app has to
    /// keep running when its last window closes, with <c>ShutdownMode.OnExplicitShutdown</c>, or Dismiss would end it.
    /// </summary>
    public static void ShowUnshown(Window? owner = null) => ShowUnshown(owner, CrashReports.TakeUnshown);

    // Takes the report through take, so tests don't need CrashReports.Start's process-wide handler.
    internal static void ShowUnshown(Window? owner, Func<CrashReport?> take)
    {
        // The report is read after the main window is up, so it costs the launch nothing.
        if (owner is null || owner.IsVisible)
        {
            Show();
        }
        else
        {
            // Opened is raised again each time a hidden window is shown, and the reports are read once.
            owner.Opened += Opened;
        }

        void Opened(object? sender, EventArgs e)
        {
            owner.Opened -= Opened;
            Show();
        }

        void Show()
        {
            if (take() is not { } report)
            {
                return;
            }

            var window = new CrashReportWindow(report);
            if (owner is null)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Show();
            }
            else
            {
                window.Show(owner);
            }
        }
    }

    private async void ReportOnGitHub(object? sender, RoutedEventArgs e)
    {
        if (!_report.FitsInLink)
        {
            try
            {
                await SetClipboard();
            }
            catch (Exception)
            {
                // The link holds only part of a long report, and the user would paste whatever the clipboard held before.
                Say(_report.Strings.CopyFailedBeforeGitHub);
                return;
            }
        }

        try
        {
            _report.OpenIssue();
        }
        catch (Exception)
        {
            // The host is just starting: a missing browser mustn't take it down.
            Say(_report.Strings.NoBrowser);
        }
    }

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SetClipboard();
            Say(_report.Strings.Copied);
        }
        catch (Exception)
        {
            Say(_report.Strings.CopyFailed);
        }
    }

    private async Task SetClipboard()
    {
        if (Clipboard is not { } clipboard)
        {
            throw new InvalidOperationException("No clipboard.");
        }

        await clipboard.SetTextAsync(_report.Text);
    }

    private void Say(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }
}
