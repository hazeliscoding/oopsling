using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Oopsling.Tests;

public class ReportTests
{
    private static readonly Mask Mask = new(@"C:\Users\Wren", "Wren");
    private const string Details = "- App: Sample 1.2.3+abc\n- OS: Made-up OS 1.0";

    [Fact]
    public void The_first_line_is_the_issue_title() =>
        Assert.Equal("Sample 1.2.3 crashed: InvalidOperationException", Format(Thrown(new InvalidOperationException("boom"))).Split('\n')[0]);

    // Avalonia calls Click handlers through reflection, so a UI-thread crash arrives wrapped.
    [Fact]
    public void The_title_names_the_exception_inside_a_TargetInvocationException() =>
        Assert.Equal(
            "Sample 1.2.3 crashed: InvalidOperationException",
            Format(new TargetInvocationException(new InvalidOperationException("boom"))).Split('\n')[0]);

    // Oopsling builds the details itself, and a commit hash in the version would look like a key to the mask.
    [Fact]
    public void Leaves_the_details_as_given()
    {
        const string details = "- App: Sample 1.2.3+0123456789abcdef0123456789abcdef01234567";

        Assert.Contains(details, Report.Format(new InvalidOperationException("boom"), "Sample 1.2.3", details, Mask));
    }

    [Fact]
    public void Names_the_method_that_threw() => Assert.Contains(nameof(ThrowFromHere), Format(Thrown(new InvalidOperationException("boom"))));

    [Fact]
    public void Holds_each_inner_exception_with_its_message() =>
        Assert.Contains("System.FormatException: inner detail", Format(Thrown(new InvalidOperationException("outer", new FormatException("inner detail")))));

    [Fact]
    public void Ends_with_the_details() => Assert.EndsWith(Details, Format(Thrown(new InvalidOperationException("boom"))).TrimEnd());

    [Fact]
    public void Leaves_no_trace_of_a_made_up_user_or_home_folder()
    {
        var report = Format(Thrown(new IOException(@"Could not open 'C:\Users\Wren\notes.txt' or C--Users-Wren-notes")));

        Assert.Contains("<home>", report);
        Assert.DoesNotContain("Wren", report, StringComparison.OrdinalIgnoreCase);
    }

    // Masking a huge message would keep the dying process busy, and no issue could hold it anyway.
    [Fact]
    public void Cuts_a_huge_message_before_masking_it()
    {
        var report = Format(new InvalidOperationException(string.Concat(Enumerable.Repeat("line\n", 200_000))));

        Assert.InRange(report.Length, 60_000, 70_000);
        Assert.Contains("line\n   ...\n```", report);
    }

    // The cut before masking can split a name the mask then misses, and masking can shrink what's left below the limit,
    // so the end of the masked text has to go.
    [Fact]
    public void Drops_a_name_that_the_cut_before_masking_split()
    {
        const string Prefix = "System.InvalidOperationException: ";
        var token = string.Concat(Enumerable.Repeat("a1", 32)) + " ";
        var length = 262_144 - Prefix.Length - "Wren Exa".Length;
        var filler = string.Concat(Enumerable.Repeat(token, length / token.Length)).PadRight(length);

        var report = Report.Format(new InvalidOperationException(filler + "Wren Example and more"), "Sample 1.0", Details, new Mask(@"C:\Users\Wren Example", "someone"));

        Assert.DoesNotContain("Wren", report, StringComparison.OrdinalIgnoreCase);
    }

    // A message holding ``` would end the code block, and the rest would read as Markdown, where an @mention notifies
    // someone when the issue is filed.
    [Fact]
    public void Keeps_a_message_with_backticks_inside_the_code_block()
    {
        var report = Format(new InvalidOperationException("```\n@someone"));

        Assert.Contains("\n````text\n", report);
        Assert.Contains("@someone", report);
        Assert.EndsWith("\n````\n\n" + Details, report.TrimEnd());
    }

    [Fact]
    public void Survives_an_exception_whose_ToString_throws() => Assert.Contains(nameof(BrokenException), Format(new BrokenException()));

    [Fact]
    public void A_native_crash_is_titled_by_its_exception_code_and_module() =>
        Assert.Equal("Sample 0.7.0.0 crashed: access violation (0xC0000005) in ntdll.dll", Format(Native).Split('\n')[0]);

    [Fact]
    public void A_native_crash_with_a_code_Oopsling_has_no_name_for_is_titled_by_the_number() =>
        Assert.Equal(
            "Sample 0.7.0.0 crashed: exception 0xE0000001 in KERNELBASE.dll",
            Format(Native with { Code = 0xE0000001, Module = "KERNELBASE.dll" }).Split('\n')[0]);

    // The developer looks the offset up in the module's symbols, so it's written as a debugger takes it.
    [Fact]
    public void A_native_report_holds_the_offset_in_the_module_and_the_modules_version()
    {
        var report = Format(Native);

        Assert.Contains("Access violation (0xC0000005) at ntdll.dll+0x168405", report);
        Assert.Contains(@"Module: C:\WINDOWS\SYSTEM32\ntdll.dll, version 10.0.26100.1", report);
    }

    [Fact]
    public void A_native_report_holds_the_runtimes_message_and_stack() =>
        Assert.Contains(
            "Message: gave up\nStack:\n   at Sample.Program.Main()",
            Format(Native, "Description: The application requested process termination through System.Environment.FailFast.\r\nMessage: gave up\r\nStack:\r\n   at Sample.Program.Main()"));

    // An app installed for one user runs from their home folder, and a JIT build's stack names its source files.
    [Fact]
    public void A_native_report_leaves_no_trace_of_a_made_up_user_in_the_paths_an_event_carries()
    {
        var report = Format(
            Native with { Module = "Sample.exe", ModulePath = @"C:\Users\Wren\AppData\Local\Sample\current\Sample.exe" },
            "Description: The process was terminated due to an unhandled exception.\nStack:\n   at Sample.Program.Main() in C:\\Users\\Wren\\src\\Program.cs:line 19");

        Assert.Contains(@"<home>\AppData\Local\Sample\current\Sample.exe", report);
        Assert.DoesNotContain("Wren", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_native_report_ends_with_the_details() => Assert.EndsWith(Details, Format(Native).TrimEnd());

    private static readonly NativeCrash Native = new(
        Record: 7,
        Time: new(2026, 11, 14, 7, 0, 1, DateTimeKind.Utc),
        Started: new(2026, 11, 14, 7, 0, 0, DateTimeKind.Utc),
        ProcessId: 4242,
        AppVersion: "0.7.0.0",
        Module: "ntdll.dll",
        ModuleVersion: "10.0.26100.1",
        ModulePath: @"C:\WINDOWS\SYSTEM32\ntdll.dll",
        Code: 0xC0000005,
        Offset: 0x168405);

    private static string Format(NativeCrash crash, string? runtime = null) => Report.Format(crash, runtime, "Sample 0.7.0.0", Details, Mask);

    private static string Format(Exception exception) => Report.Format(exception, "Sample 1.2.3", Details, Mask);

    private static Exception Thrown(Exception exception)
    {
        try
        {
            ThrowFromHere(exception);
        }
        catch (Exception caught)
        {
            return caught;
        }

        throw new UnreachableException();
    }

    // Kept out of line so Release builds keep its frame in the stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromHere(Exception exception) => throw exception;

    private sealed class BrokenException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("ToString is broken.");
    }
}
