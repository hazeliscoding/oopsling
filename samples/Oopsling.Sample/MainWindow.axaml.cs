using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Oopsling.Sample;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    // For automated checks: press a Crash button once the window is up, as the dispatcher would deliver a click.
    internal void PressOnceOpened(bool backgroundThread) =>
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
            (backgroundThread ? BackgroundCrashButton : CrashButton).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));

    private void Crash(object? sender, RoutedEventArgs e) =>
        throw new InvalidOperationException("The Crash button was pressed.");

    private void CrashOnBackgroundThread(object? sender, RoutedEventArgs e) =>
        new Thread(() => throw new InvalidOperationException("The background thread's Crash button was pressed.")).Start();
}
