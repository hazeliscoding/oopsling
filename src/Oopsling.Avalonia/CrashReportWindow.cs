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
        Title = $"{report.App} closed because of an error";
        Width = 760;
        Height = 560;
        MinWidth = 420;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var reportOnGitHub = new Button { Content = "Report on GitHub", Classes = { "accent" } };
        var copy = new Button { Content = "Copy" };
        var dismiss = new Button { Content = "Dismiss", IsCancel = true };
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
        AutomationProperties.SetName(text, "Crash report");
        ScrollViewer.SetHorizontalScrollBarVisibility(text, ScrollBarVisibility.Auto);

        var intro = new StackPanel
        {
            Spacing = 8,
            Margin = new(0, 0, 0, 12),
            Children =
            {
                new TextBlock { Text = $"{report.App} closed because of an error.", FontSize = 18, FontWeight = FontWeight.SemiBold },
                new TextBlock
                {
                    Text = "This is the report it made, and nothing has been sent. Report on GitHub opens it as a new issue "
                        + "in your browser, where you can read and edit it before you submit it.",
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };
        if (!report.FitsInLink)
        {
            intro.Children.Add(new TextBlock
            {
                Text = "The report is too long for a link, so Report on GitHub also copies it. Paste it into the issue in "
                    + "place of the text there.",
                TextWrapping = TextWrapping.Wrap,
            });
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
    /// app hasn't crashed since a report was last shown. Call it where the main window is created.
    /// </summary>
    public static void ShowUnshown(Window owner)
    {
        // The report is read after the main window is up, so it costs the launch nothing.
        if (owner.IsVisible)
        {
            Show(owner);
        }
        else
        {
            owner.Opened += (_, _) => Show(owner);
        }

        static void Show(Window owner)
        {
            if (CrashReports.TakeUnshown() is { } report)
            {
                new CrashReportWindow(report).Show(owner);
            }
        }
    }

    private async void ReportOnGitHub(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!_report.FitsInLink)
            {
                await SetClipboard();
            }

            _report.OpenIssue();
        }
        catch (Exception)
        {
            // The host is just starting: a missing browser or clipboard mustn't take it down.
            Say("No browser opened. Copy the report and file it on GitHub yourself.");
        }
    }

    private async void Copy(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SetClipboard();
            Say("The report is on the clipboard.");
        }
        catch (Exception)
        {
            Say("The report couldn't be copied. Select it above and copy it from there.");
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
