namespace Oopsling.Tests;

public class CrashReportTests
{
    private static readonly Mask Mask = new(@"C:\Users\Wren", "Wren");

    [Fact]
    public void The_issue_link_fills_in_the_title_and_the_rest_of_the_report()
    {
        var text = Report.Format(new InvalidOperationException("boom"), "Sample 1.0", "- App: Sample 1.0", Mask);

        var report = new CrashReport("Sample", "hazeliscoding/staypaw", text);

        Assert.True(report.FitsInLink);
        Assert.Equal("Sample 1.0 crashed: InvalidOperationException", report.Title);
        Assert.Equal(text, report.Text);
        Assert.StartsWith("https://github.com/hazeliscoding/staypaw/issues/new?", report.IssueLink);
        var parameters = Parameters(report.IssueLink);
        Assert.Equal(report.Title, parameters["title"]);
        Assert.Equal(text[(report.Title.Length + 2)..], parameters["body"]);
    }

    // GitHub turns the page into a 404 when the link sets something the user has no permission for, like labels.
    [Fact]
    public void The_issue_link_sets_only_the_title_and_body()
    {
        var text = Report.Format(new InvalidOperationException("boom"), "Sample 1.0", "- App: Sample 1.0", Mask);

        Assert.Equal(["title", "body"], Parameters(new CrashReport("Sample", "hazeliscoding/staypaw", text).IssueLink).Keys);
    }

    [Fact]
    public void A_report_too_long_for_a_link_keeps_the_top_of_the_stack_and_the_details()
    {
        var frames = string.Concat(Enumerable.Range(0, 400).Select(i => $"\n   at Sample.Deep.Frame{i}(Object, EventArgs)"));
        var text = Report.Format(new LongException("boom", frames), "Sample 1.0", "- App: Sample 1.0\n- OS: Made-up OS 1", Mask);

        var report = new CrashReport("Sample", "hazeliscoding/staypaw", text);

        Assert.False(report.FitsInLink);
        Assert.InRange(report.IssueLink.Length, CrashReport.MaxLinkLength - 200, CrashReport.MaxLinkLength);
        var body = Parameters(report.IssueLink)["body"];
        Assert.StartsWith("This report was too long for a link, so the end of its stack is missing here. The whole report is "
            + "on your clipboard: select all of this text and paste it in its place.\n\n", body);
        Assert.Contains("LongException: boom\n   at Sample.Deep.Frame0(", body);
        Assert.DoesNotContain("Frame399(", body);
        Assert.EndsWith("   ...\n```\n\n- App: Sample 1.0\n- OS: Made-up OS 1\n", body);
    }

    // An AggregateException can put hundreds of messages on one line, and a report with backticks in it has a longer fence.
    [Fact]
    public void A_report_whose_first_line_is_too_long_for_a_link_keeps_as_much_of_it_as_fits()
    {
        var text = Report.Format(new LongException("``` " + new string('x', 10_000), "\n   at Sample.Run()"), "Sample 1.0", "- App: Sample 1.0", Mask);

        var report = new CrashReport("Sample", "hazeliscoding/staypaw", text);

        Assert.InRange(report.IssueLink.Length, CrashReport.MaxLinkLength - 200, CrashReport.MaxLinkLength);
        var body = Parameters(report.IssueLink)["body"];
        Assert.Contains("LongException: ``` xxxxxxxxxx", body);
        Assert.EndsWith("x\n   ...\n````\n\n- App: Sample 1.0\n", body);
    }

    [Fact]
    public void A_cut_report_starts_with_the_hosts_note()
    {
        var frames = string.Concat(Enumerable.Range(0, 400).Select(i => $"\n   at Sample.Deep.Frame{i}(Object, EventArgs)"));
        var text = Report.Format(new LongException("boom", frames), "Sample 1.0", "- App: Sample 1.0", Mask);

        var report = new CrashReport("Sample", "hazeliscoding/staypaw", text, strings: new() { CutNote = "Der Bericht war zu lang." });

        Assert.InRange(report.IssueLink.Length, CrashReport.MaxLinkLength - 200, CrashReport.MaxLinkLength);
        Assert.StartsWith("Der Bericht war zu lang.\n\n```text\n", Parameters(report.IssueLink)["body"]);
    }

    [Fact]
    public void The_heading_names_the_app_in_English_unless_the_host_writes_its_own()
    {
        const string text = "Sample 1.0 crashed: InvalidOperationException\n\n```text\nboom\n```\n";

        Assert.Equal("Sample closed because of an error.", new CrashReport("Sample", "hazeliscoding/staypaw", text).Strings.Closed);
        Assert.Equal("Sample closed because of an error.", new CrashReport("Sample", "hazeliscoding/staypaw", text, strings: new() { Copy = "Kopieren" }).Strings.Closed);
        Assert.Equal("Sample ist abgestürzt.", new CrashReport("Sample", "hazeliscoding/staypaw", text, strings: new() { Closed = "Sample ist abgestürzt." }).Strings.Closed);
    }

    [Fact]
    public void An_email_contact_opens_a_draft_with_the_title_as_its_subject()
    {
        var text = Report.Format(new InvalidOperationException("boom"), "Sample 1.0", "- App: Sample 1.0", Mask);

        var report = new CrashReport("Sample", "hazeliscoding/staypaw", text, contact: "crashes@example.com");

        Assert.Equal("crashes@example.com", report.Contact);
        Assert.Equal("mailto:crashes@example.com?subject=Sample%201.0%20crashed%3A%20InvalidOperationException", report.ContactLink);
    }

    [Fact]
    public void A_link_contact_opens_as_given()
    {
        var text = Report.Format(new InvalidOperationException("boom"), "Sample 1.0", "- App: Sample 1.0", Mask);

        Assert.Equal("https://example.com/support", new CrashReport("Sample", "hazeliscoding/staypaw", text, contact: "https://example.com/support").ContactLink);
    }

    [Fact]
    public void Without_a_contact_there_is_nothing_to_open()
    {
        var report = new CrashReport("Sample", "hazeliscoding/staypaw", Report.Format(new InvalidOperationException("boom"), "Sample 1.0", "- App: Sample 1.0", Mask));

        Assert.Null(report.Contact);
        Assert.Null(report.ContactLink);
        Assert.Throws<InvalidOperationException>(report.OpenContact);
    }

    private static Dictionary<string, string> Parameters(string link) =>
        link[(link.IndexOf('?') + 1)..].Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    private sealed class LongException(string message, string frames) : Exception(message)
    {
        public override string ToString() => $"{GetType().FullName}: {Message}{frames}";
    }
}
