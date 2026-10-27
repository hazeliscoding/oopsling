# Roadmap

Oopsling turns a crash in a .NET app into a GitHub issue the user files themselves, and sends nothing on its own. This file tracks the decisions already made, what gets built in what order, and what is out of scope.

## Architecture characteristics

What the design protects, in order:

1. **Privacy.** Nothing leaves the PC unless the user reads it and presses Submit on GitHub. A report holds nothing the user wouldn't post in public, because a GitHub issue is public.
2. **Works while dying.** The handler runs in a process that is already broken, so it writes one file and does nothing else: it never throws, never waits on a lock, and never shows UI or calls the host's services. Showing and filing happen at the next launch.
3. **Free until a crash.** NativeAOT and trim safe, with no reflection. At startup Oopsling hooks one handler and checks one folder. It needs nothing beyond the .NET base library, and Avalonia for the window.

Given up for them: the diagnostics other crash reporters collect (minidumps, logs, breadcrumbs, the environment), a "send now?" dialog at the moment of the crash, and line numbers in NativeAOT stacks. Readable reports come fourth: a report must name the method that threw, NativeAOT or not.

## Decisions (2026-10-25)

- **Name: Oopsling,** a little one who says "oops" and owns it. Free and open source (Apache-2.0, like Staypaw and Sweepling), in the public repo `hazeliscoding/oopsling`, published to nuget.org. On 2026-10-24 the name had no GitHub repos, no NuGet package, no web hits and no DNS for `oopsling.com`, `oopsling.app` or `oopsling.dev`. Trademarks aren't checked. It gives up Ouchling, the runner-up.
- **Two packages.** `Oopsling` holds the catching, the report, the masking and the issue link, with no UI, so any .NET app can use it. `Oopsling.Avalonia` adds the window that shows the last crash. The core never references Avalonia. Every app the owner ships is Avalonia. On 2026-10-25 no package was found that keeps a report local, masks it and hands it to GitHub: MaksIT.Core.UI shows a local crash window for Avalonia with Copy, as one of its makers' shared windows, and Avalonia apps such as PixiEditor and UVtools each build their own issue link. This gives up installing a single package.
- **Windows, macOS and Linux, on `net10.0`.** The core needs nothing OS-specific: home-folder masking, the OS description and opening the browser work on all three, and Prompuff ships on macOS and Linux. CI tests on all three. .NET 10 only, since .NET 8 and 9 both reach end of support on 2026-11-10, and Avalonia 12, like the owner's apps. This gives up Windows-only extras in the core, such as reading the Event Log; one would need its own package.
- **Unhandled managed exceptions only.** Oopsling hooks `AppDomain.CurrentDomain.UnhandledException`. Avalonia rethrows a UI-thread exception out of `Main`, so that one hook should see it too. M1 checks this, and `Oopsling.Avalonia` adds a dispatcher hook if it doesn't. Oopsling records the crash and lets the app die: it never marks an exception handled, because a broken app kept running can damage the user's data. Unobserved task exceptions aren't crashes and are left out. This gives up stack overflows, `Environment.FailFast` and native crashes, which end the process before any handler runs. *Settled in M1: the one hook sees UI-thread crashes on all three OSes, so there's no dispatcher hook. See "Decisions in M1".*
- **Masked exception messages go in the report.** A message is often the most useful line ("Could not find file …"), and the user sees the whole report twice before anything is public: in the window, and in GitHub's form, where they can still edit it. Home folders, the user name and token-like strings are masked. The user name is matched on its own, not only inside the home folder's path, because Sweepling's masking missed it in Claude Code's `C--Users-<name>-…` paths. This gives up a guarantee that nothing personal ever slips through; the user's review is the safety net. *Extended in M1 to machine and domain names and URL credentials. See "Decisions in M1".*
- **What a report holds:** for the exception and each inner one, its type, masked message and stack; then the app's version, the OS description, the .NET version, the process architecture, and whether the app is NativeAOT. Nothing else: no environment variables, command lines, logs, machine name or screenshots. A new field needs a dated decision here.
- **A report is a Markdown file: the issue body as it will be filed.** One file per crash in a `crashes` folder under `Environment.SpecialFolder.LocalApplicationData`/`<app>`, which puts Staypaw's beside its focus log, and the newest 10 are kept. What the user reads is what reaches GitHub, and the dying process writes one format. Masks are worked out at start, so at crash time Oopsling only replaces strings and writes the file. This gives up a machine-readable report. *Refined in M1: the file's first line is the issue's title. See "Decisions in M1".*
- **Shown once, at the next launch.** The Avalonia window opens when there's a report it hasn't shown. Report on GitHub opens the filled-in issue, Copy puts the report on the clipboard, and Dismiss closes the window. It follows the host's theme. This gives up a prompt at the moment of the crash.
- **The issue link** is the repo's `issues/new` page with `title` and `body` filled in, opened through the OS shell, so the user's own browser and GitHub sign-in do the rest and Oopsling sends nothing. Only `title` and `body`: GitHub's docs say a parameter the user has no permission for, such as `labels`, turns the page into a 404, and someone filing a crash usually can't label. When a report doesn't fit in a URL, the link carries the top of the stack and asks the user to paste the rest, which the window has put on the clipboard. No limit is documented: GitHub answers 414 past one it doesn't state, and the 2083 characters often quoted for `ShellExecute` come from WinINET and an old blog post. M2 measures the real limits. This gives up users without a GitHub account, who can still Copy the report. *Settled in M2: links stop at 6,000 characters, and a longer report asks the user to paste the whole report over the top of it. See "Decisions in M2".*
- **First host: Staypaw.** It's shipped (1.1.1), so a report from a stranger is the real proof, and its `GuardrailTests` already fail if its code references a networking assembly. A sample app in this repo, with a Crash button, drives the tests and the README demo. This gives up Sweepling, Shybun and Tattlebun until after the first release.
- **Brand: option 1, "Bandage",** a round coral critter with a crossed bandage on its head and a sheepish smile, picked from four takes on the claude.ai design canvas "Oopsling logo". It says "oops" and owns it without explanation, and the bandage cross still reads at 16 px. The wordmark is outlined Fredoka SemiBold, like Staypaw's and Sweepling's. Assets are in `docs/brand/`. This gives up Note, which held up a report card, Wobble and Peek.
- **Releases** go to nuget.org through trusted publishing from a protected `release` environment, so no API key is stored. A `v*` tag publishes both packages at the same version.

## Decisions in M1 (2026-10-25)

- **The public API is `CrashReports.Start(appName, repo)`.** `Oopsling.Start` would put a class in a namespace of the same name, which makes lookups ambiguous. It uses the glossary's words. It gives up `Oops.Start`, the playful option. It throws for an app name that can't be a folder name or a repo that isn't `owner/name`, since both come from the developer and a mistake should show on the first run.
- **A UI-thread crash needs no dispatcher hook.** On Windows the exception crosses Avalonia's `WndProc` and `DispatchMessage` back out to `Main`, and CI found the same result on Linux and macOS. The check posts a press of the Crash button to the dispatcher. It doesn't send real OS input, which reaches the handler through the same message loop. This gives up nothing.
- **A report's first line is the issue's title:** "\<app\> \<version\> crashed: \<exception type\>", with a blank line before the body. The title names the exception inside a `TargetInvocationException`, because Avalonia calls Click handlers through reflection and every UI-thread crash arrives wrapped. The version in the title drops the commit after `+`; the details keep it.
- **Only the exception's text is masked.** The title and details come from names and versions Oopsling reads itself, and masking them turned the version's commit hash into `<token>`.
- **The mask also covers the machine and domain names and the credentials in a URL, and it finds the user name between percent-escapes.** These came from an automated security review of the first mask. A domain name can name the user's employer. Machine, domain and user names are each matched on their own, longest first. URL credentials run to the last `@` before a space, since a password can hold `@`, `?`, `#` or `/` unescaped. This gives up the host of a URL with an `@` further along, such as an npm scope, which gets masked too.
- **A secret's value is masked only after `=` or `:`.** Sweepling's pattern also took the word after a space, which turned every `CancellationToken cancellationToken` in a stack trace into `<token>`. This gives up masking "password hunter2" written with only a space; known token prefixes and long opaque runs are still masked anywhere.
- **The NativeAOT line asks whether anything was JIT-compiled.** `RuntimeFeature.IsDynamicCodeSupported` is off in the JIT build of any `PublishAot` project, so the sample's JIT build reported "NativeAOT: yes".
- **NativeAOT stacks are readable enough:** each frame keeps its method name, with an offset in place of a line number, as in `MainWindow.Crash(Object, RoutedEventArgs) + 0x2b`. Frames without metadata show as `Oopsling.Sample!<BaseAddress>+0x7d40ed`. Line numbers stay under Later.
- **The sample takes `--crash ui` or `--crash background`,** which presses that Crash button once the window is up, so `scripts/crash-check.sh` can run M1's done line on every OS in CI. Linux runs it on a virtual display through `xvfb-run`.

## Decisions in M2 (2026-10-26)

- **The URL limits, measured.** GitHub sets the limit on every OS:
  - **GitHub, signed in:** an 8,000-character link opened the new-issue page with the title and body filled in. From 8,210 characters GitHub answers 414, a request target of 8,192.
  - **GitHub, signed out:** GitHub first redirects to sign in. Links up to 6,366 characters always got the redirect. From about 6,600 some got a 500 instead, and from 6,900 all did. Percent-escapes made no difference.
  - **`ShellExecute`,** through `Process.Start` on Windows 11: 32,000 characters reached a URL protocol handler intact, and 32,767 failed, the command line's limit. The often-quoted 2,083 doesn't apply.
  - **`xdg-open`** 1.2.1 on Ubuntu, in CI: 131,000 characters arrived intact, and 131,072 failed, Linux's limit for one argument.
  - **`open`** on macOS, in CI: a 65,000-character `http://127.0.0.1` link reached a local listener through Safari intact, the most that listener accepts. `open` took a 262,144-character argument and failed at 1,048,576, macOS's limit for a command's arguments.
- **Links stop at 6,000 characters,** so a signed-out user's redirect to sign in still works, with a margin for GitHub's own headers. This gives up about 2,200 characters of report for users who are signed in. The sample's UI-thread crash, with Avalonia's frames, is 3.7 KB and fits.
- **A report too long for a link keeps the top of the stack and the details, and the clipboard gets the whole report.** A note at the top of the issue asks the user to select everything and paste over it. Pasting only the rest, as first planned, needs the cursor at the right spot and splits the code fence between two pieces. A user who doesn't paste still files a useful issue: the frames nearest the throw, the details, and a note that the stack was cut. This gives up a link that fills in the whole report.
- **The core's API:** `CrashReports.TakeUnshown()` returns the newest report not yet shown as a `CrashReport`, with its title, its text, its issue link, whether it fits in the link, and `OpenIssue()`, and marks it shown. A `shown` file in the crashes folder remembers the newest name shown, so an older report is never shown: the newer crash already told the user. `OpenIssue()` uses `Process.Start` with the shell, the path measured above.
- **The window:** `CrashReportWindow.ShowUnshown(mainWindow)` reads the report only once the main window is open, so it costs the launch nothing. The window opens over the main window without blocking it. This gives up showing a crash before the main window exists.
- **Copy copies the whole report, title line first,** and so does Report on GitHub for a long report. The pasted issue then repeats its title in the first line. This gives up a body without the title line, for one text the user can also paste anywhere else.
- **The sample takes `--repo owner/name` as its first arguments,** so checks open the link for the private scratch repo `hazeliscoding/oopsling-scratch`, never for Oopsling's own issues.
- **The window's tests render it with `Avalonia.Headless`,** a test-only package, so they run on every OS in CI.

## Decisions in M3 (2026-10-26)

- **Releasing:** pushing a `v*` tag on `main` runs `release.yml`, which builds, tests and packs in one job and publishes in another. Only the publish job can get a nuget.org key, through trusted publishing: GitHub's OIDC token buys a key that lasts an hour, so there's no long-lived secret. That job runs in the `release` environment, which takes only `v*` tags and waits for the owner's approval. Running the workflow by hand packs a `0.0.0-dryrun.N` version and publishes nothing. This gives up a one-step release; the approval is the owner's go-ahead in code.
- **The window package depends on Avalonia 12.0.0,** the oldest 12.x, so it never makes a host upgrade Avalonia. Built against 12.1.4, it would have made Staypaw, on 12.1.3, upgrade to take it. Tests and the sample run the newest 12.x. This gives up building against anything newer than 12.0, and testing against 12.0 itself.
- **A host without a main window passes no owner.** Staypaw is a tray app, so `ShowUnshown()` with no owner shows the report at once in the middle of the screen. The host has to keep running when its last window closes, as a tray app does.
- **Both packages share one README,** `src/README.md`, and an icon rendered from the mark, since nuget.org takes neither relative links nor SVG.
- **A host tries a local build before a version is published:** Staypaw took a `0.1.0-local.1` package from a folder first, since a nuget.org version can't be deleted. That's how the missing owner showed up.
- **A review of both packages before the first publish** found that the masks could hang the dying process and that the user name got past them in several shapes. What changed:
  - **Masking stays linear.** Each pattern starts only where a run starts, or looks a bounded distance ahead. At 256 KB, the inputs that once took minutes take a fifth of a second.
  - **A report holds at most 64 KB of the exception's text,** cut after masking at a space or a line's end. Text past 256 KB is dropped before masking, and the end of what's left goes too, since a name that cut split could only be there. This gives up the rest of a huge message.
  - **A user's folder is masked in every shape seen so far:** with either slash, doubled in JSON or percent-encoded. The folder's own name is masked too, for a renamed account, and so is whatever follows `Users\` or `home/`, for 8.3 names and other accounts. A dot or a space in a name matches whatever punctuation replaced it. A 15-character machine name, which Windows may have cut short, matches the rest of the host name. This gives up some text that only looks like a name, such as `/users/42` in a URL.
  - **The code fence is longer than any run of backticks in the message,** so the message can't end the block. Outside it, an `@mention` would notify someone when the issue is filed.
  - **A new report is always named after the newest one already there,** so a clock set back can't get it pruned, and a clock once set forward can't hide later crashes.
  - **Only the first crash is recorded.** This gives up a second thread crashing at the same moment.
  - **`Start` takes the repo only as `owner/name`,** in GitHub's own characters, and a second call throws. A user, machine or domain name the OS can't give goes unmasked by name, rather than stopping the host in `Main`; the home folder is still masked.
  - **`CrashReport.App` is now `AppName`,** after `Start`'s parameter.

## Decisions in M4 (2026-10-27)

- **Issue templates don't break the link while blank issues stay on.** On the scratch repo with an issue form, the link opened the blank form with the title and body filled in. With blank issues off, it still did for the repo's owner, but GitHub may show other users the template picker, and that wasn't checked. So the README tells hosts to keep blank issues on, and Oopsling's own tracker does.
- **A crash report that held something personal goes to private vulnerability reporting,** not an issue, since an issue would publish it again. This gives up discussing it in the open.
- **The README's quick start is checked by running it:** the fresh-install check takes its lines from the README, so the two can't drift apart. This gives up checking the README's prose.
- **The demo GIF is recorded on a Windows runner,** whose desktop belongs to nobody, since the recording moves the real mouse. The runner draws no pointer, and has no GitHub sign-in, so the GIF ends on the window, not the filled-in issue. This gives up showing the browser.

## M0: Placeholder

- [x] .NET 10 solution with central package versions: `Oopsling`, `Oopsling.Avalonia`, the tests, and a sample Avalonia 12 app with a Crash button that does nothing yet.
- [x] Both packages are `IsAotCompatible`, trim and AOT warnings are errors, and the sample publishes with NativeAOT.
- [x] CI on Windows, macOS and Linux: build, test, pack, and the sample's NativeAOT publish.
- [x] Secret scanning: the gitleaks release binary, checked against its published checksum, run with `--log-opts="--full-history HEAD"`.
- [x] A test that fails when either package references a networking assembly, with a positive control.
- [x] A test that fails when the core references Avalonia, with a positive control.

**Done when:** CI passes on all three OSes, a throwaway pull request with a fake token fails the gitleaks check, and each guard test fails when the code it protects is changed to break it.

**Done on 2026-10-25:**

- CI passed on `main` on Windows, Linux and macOS, with the pack and the sample's NativeAOT publish on each: [run 40251443175](https://github.com/hazeliscoding/oopsling/actions/runs/40251443175).
- Throwaway pull request #1 planted a random fake GitHub token. Its secrets job failed on the history scan while the builds passed: [run 40251563026](https://github.com/hazeliscoding/oopsling/actions/runs/40251563026). It was closed without merging.
- Each guard test failed when the code it protects was broken, and no other test did. A core class creating an `HttpClient` failed `Neither_package_references_a_networking_assembly`, and so did the same class in `Oopsling.Avalonia`. Adding Avalonia to the core and using `Avalonia.Point` failed `The_core_does_not_reference_Avalonia`.
- Trim and AOT warnings fail the build: a `Type.GetType(string)` call in the core stopped it with `error IL2057`.
- The sample's NativeAOT build on Windows is a 16.5 MB exe. It started, showed its window, and closed with exit code 0.

## M1: Write the report

- [x] One call at the top of the host's `Main` hooks the handler and works out the report folder and the masks. Settle the public API's names here.
- [x] The report: every field in the decision above, masked, as Markdown.
- [x] Masking: the home folder, the user name in any form (with `-`, `/` or `\` around it), and token-like strings. Tests build token-shaped values at run time.
- [x] Keep the newest 10 reports.
- [x] Check that a UI-thread exception in Avalonia reaches the handler. If it doesn't, add the dispatcher hook to `Oopsling.Avalonia`.
- [x] The handler never throws. A full disk, a folder it can't write to, or an exception whose `ToString` throws still leaves the original crash to end the app as it would have.

**Done when:** the sample's JIT and NativeAOT builds, made to crash on the UI thread and on a background thread, each leave a report that names the method that threw, and a test with a made-up user name and home folder finds no trace of either in the report.

**Done on 2026-10-25:**

- `scripts/crash-check.sh` crashed both builds of the sample on both threads on Windows, Linux and macOS in CI, 12 crashes in all. Each left one report that names the method that threw and says whether the build is NativeAOT, with no trace of the runner's user or host name: [run 40255386872](https://github.com/hazeliscoding/oopsling/actions/runs/40255386872). The same four crashes passed locally on Windows 11.
- `ReportTests.Leaves_no_trace_of_a_made_up_user_or_home_folder` puts a made-up home folder and user name in an exception's message, as a path and as `C--Users-<name>-…`, and finds neither in the report.
- The crashed app's exit code stayed the runtime's own, `0xE0434352` on Windows: Oopsling doesn't change how the app exits.
- `RecorderTests.Never_throws_when_the_folder_cannot_be_written` failed when the recorder's `catch (Exception)` was narrowed to `catch (FormatException)`, so it tests what it claims. `ReportTests.Survives_an_exception_whose_ToString_throws` covers the other case. A full disk wasn't simulated; it fails the same write, inside the same catch.

## M2: Show and file

- [x] The core's next-launch API: the newest report not yet shown, its issue title and its issue link.
- [x] Measure the URL limits of GitHub's new-issue page, `ShellExecute`, `xdg-open` and `open`, and record them here.
- [x] Long reports: the link carries the top of the stack and asks the user to paste the rest from the clipboard. *Refined: the user pastes the whole report over the top of it. See "Decisions in M2".*
- [x] The Avalonia window: the report, Report on GitHub, Copy and Dismiss, in the host's theme, shown once per report.

**Done when:** on Windows, the sample's next launch after a crash shows the report once, and Report on GitHub opens a filled-in new-issue page for a scratch repo in the default browser, with nothing filed. The link tests pass on all three OSes, including a report too long for a URL.

**Done on 2026-10-26:**

- On Windows 11, the sample crashed on the UI thread, and at its next launch the window showed the report. Report on GitHub opened the new-issue page of `hazeliscoding/oopsling-scratch` in the default browser, Brave, with the title and the report filled in. Nothing was filed. The launch after that showed no window. The NativeAOT build showed the window too, and its exe grew from 16.5 MB to 18.6 MB.
- The link tests, including a 400-frame report cut to fit, and the headless window tests passed on Windows, Linux and macOS: [run 40370147390](https://github.com/hazeliscoding/oopsling/actions/runs/40370147390).
- Each window test failed when the code it covers was broken. Copying only the title failed `Copy_puts_the_whole_report_on_the_clipboard`, and showing the long-report note for a short report failed `Says_when_the_report_is_too_long_for_a_link`. The first version of the Copy test passed anyway: its async lambda went to a `Dispatch` overload that never awaits it, so its assertions never ran.
- The limits above came from `curl` against GitHub, a throwaway URL scheme on Windows, and a throwaway branch's workflow on Linux and macOS: [run 40356713787](https://github.com/hazeliscoding/oopsling/actions/runs/40356713787) for `xdg-open`, [run 40372160706](https://github.com/hazeliscoding/oopsling/actions/runs/40372160706) for `open`. The branch was deleted afterwards.
- Not checked: a signed-out user signing in and landing back on the filled-in page, a repo with issue templates, and the window outside headless tests on macOS and Linux.

## M3: Preview and Staypaw

- [x] A release workflow: a `v*` tag packs both packages and publishes them to nuget.org through trusted publishing.
- [x] `0.1.0-preview.1` on nuget.org.
- [x] Staypaw takes `Oopsling.Avalonia` from nuget.org, with its `GuardrailTests` still passing.

**Done when:** a Staypaw build made to crash shows Oopsling's window at its next launch and opens a filled-in issue page for Staypaw's repo, and Staypaw's CI is green.

**Done on 2026-10-27:**

- A dry run of the release workflow packed both packages ([run 40445453285](https://github.com/hazeliscoding/oopsling/actions/runs/40445453285)). The `v0.1.0-preview.1` tag published both, with their symbols, once the owner approved it ([run 40451880159](https://github.com/hazeliscoding/oopsling/actions/runs/40451880159)). The first try failed before pushing anything: the nuget.org policy's environment read `release.`, with a stray full stop.
- On Windows 11, a Staypaw build with the published package, made to throw on the UI thread, left one report. The home folder was masked, and so was the user name in a `C--Users-<user>-…` path. At the next launch the NativeAOT build showed the window, and Report on GitHub opened `hazeliscoding/staypaw/issues/new` in the default browser with the title and report filled in. Nothing was filed. The test copy ran while the owner's Staypaw was stopped, and the test report was deleted afterwards.
- Staypaw's [pull request 19](https://github.com/hazeliscoding/staypaw/pull/19) takes the package from nuget.org, with a guard test that Oopsling's assemblies use no network, a dated decision, and a line in its trust contract saying where a crash report goes. Its CI passed ([run 40453275984](https://github.com/hazeliscoding/staypaw/actions/runs/40453275984)), and it's merged into `main` for v1.2.
- Not checked: the window on macOS and Linux outside headless tests, a repo with issue templates, and a signed-out user signing in and landing back on the filled-in page.

## M4: 1.0

- [x] README: install, a demo GIF from the sample, and the comparison with alternatives checked again.
- [x] `CONTRIBUTING.md` and `SECURITY.md`.
- [x] The OSS launch gates:
  - [x] the core journey works end to end: crash, next launch, report, filled-in issue;
  - [x] a fresh install from nuget.org works in a new Avalonia app on each OS, following the README;
  - [x] the README quick start is accurate;
  - [x] CI is green and there are no known critical bugs;
  - [x] error messages are understandable;
  - [x] the issue tracker is ready for reports.

**The gates, checked on 2026-10-27:**

- **The core journey:** on Windows, through the sample in M2 and through Staypaw in M3, from the crash to the filled-in issue page. On Linux and macOS, the fresh-install check below crashes a new app, and its next launch takes the report and opens the window without crashing. The link opens there through `xdg-open` and `open`, measured in M2. Signed out, GitHub sends the user to `/login` with the whole link as `return_to`, title and body intact, so signing in lands back on the filled-in page; signing in itself wasn't tried.
- **A fresh install, following the README:** `fresh-install.yml` makes an app from Avalonia's own template on Windows, Linux and macOS. It adds `Oopsling.Avalonia` from nuget.org and pastes in the README's quick start lines, then checks the crash, the report and the next launch. It passed with `0.1.0-preview.1` ([run 40514630382](https://github.com/hazeliscoding/oopsling/actions/runs/40514630382)). As positive controls, it failed when the README lost a quick start line and when a line couldn't compile. It runs after every release.
- **CI** passed on all three OSes ([run 40515631065](https://github.com/hazeliscoding/oopsling/actions/runs/40515631065)), and no issues are open. The pre-release review's findings are fixed (see "Decisions in M3").
- **Error messages:** each one `Start` throws names the value it refused, and a missing `Start` says how to fix it. The window's messages say what happened and what to do instead.
- **The issue tracker:** a bug form, blank issues kept on so filled-in links work, and GitHub's private vulnerability reporting for a crash report that held something personal, as `SECURITY.md` says.

**Done when:** every gate above is ticked, and a Staypaw release with Oopsling in it has shipped.

## How we'll know it works

A dogfooding log in `docs/dogfooding.md`, kept from M3 on: crashes reported through Oopsling, and any report that held something it shouldn't have. That last count must stay zero. After 1.0, public signals: NuGet downloads, issues, pull requests, and other apps using it.

## Later

- Line numbers in NativeAOT stacks, for example from a map shipped beside the app, if reports without them prove hard to act on.
- A Windows package that reads the Event Log at the next launch, for crashes that leave no managed exception.
- An unclean-exit marker ("closed unexpectedly last time"), if it can tell a crash from being ended in Task Manager or a forced reboot.
- WPF and WinForms windows.
- More hosts, one at a time. Sweepling comes first, because it makes the same no-network promise. Next come Shybun, Tattlebun and Overglance as each one ships. After them come Gil Sweep, XIV Vault's desktop app, Prompuff and Carmine Workbench. Each host needs a public repo with issues turned on, or the issue link opens a 404.
- The window's text in other languages.
- A way for users without a GitHub account to send a report. One option is a contact line the host passes to `Start`, which the window shows as "No GitHub account? Copy the report and send it to …". A `mailto:` link is the other, but mail apps limit a link's length more strictly and less predictably than browsers do. Users who couldn't file never appear in the dogfooding log, so the host's audience decides when this is needed: before any host whose users are mostly not developers.

## Not planned

- Sending anything: no network code, no server, no email, no "send automatically" option.
- Minidumps, logs, breadcrumbs, screenshots or environment variables in a report.
- Catching stack overflows, `Environment.FailFast` or native crashes inside the process.
- Unobserved task exceptions.
- Marking a crash handled to keep the app running.
