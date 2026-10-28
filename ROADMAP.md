# Roadmap

Oopsling turns a crash in a .NET app into a GitHub issue the user files themselves, and sends nothing on its own. This file holds the decisions already made, what shipped, the milestones still to build, and what is out of scope.

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

## Decisions after 1.0 (2026-10-27)

- **Open work stays in this file,** the owner's call, not in GitHub issues, milestones or a Project board. Work goes from the next unchecked item, as before. This gives up a public board that contributors could follow.
- **The finished milestones are folded into Shipped.** M0 to M4's checklists, with the evidence for each "Done when", are in `git show v1.0.0:ROADMAP.md`. M4's notes from after the tag (the 1.0.0 publish, the fresh-install check against it, and Staypaw taking it) are in `git show 80f299f:ROADMAP.md`. Oopsling commits straight to `main`, so that hash stays.
- **The next milestones,** in this order, the owner's picks from Later: a contact line for users without a GitHub account, and the window's text in any language (1.1); Sweepling as the second host; native crashes on Windows (1.2). The order puts the small additions first, since Staypaw's users include gamers without GitHub accounts, and the large one last. It gives up starting on native crashes, which Staypaw may hit, until after Sweepling.
- **.NET 10 only stays.** .NET 8 and 9 both reach the end of their support in November 2026, so targeting them would serve hosts for a few weeks. This gives up hosts that can't move to .NET 10, such as CyberRadio Assistant on .NET 8.

## Decisions in M5 (2026-10-27)

- **Clicking the contact copies the report, then opens it,** the owner's pick: a mail draft to an email address with the report's title as its subject, or the link's page. A `mailto:` link can't carry a long report, so the draft is empty and the user pastes. If the copy fails nothing opens, as with long reports, and if nothing can open it, the window says the report is on the clipboard. This gives up a draft with the report already in it.
- **A contact is an email address or an `https://` link, and `Start` throws for anything else.** The OS shell opens it, so a `file:` path or a script would run, and a mistake should show on the first run rather than at a user's crash. An address holds only ASCII letters, digits and `. _ + -` before a dotted domain, so nothing can add a header or a recipient to the draft, or hide in the window as a right-to-left override would. This gives up `http://` links, app links such as `discord://`, and addresses with other characters or a non-ASCII domain.
- **Hosts write the window's text as whole sentences,** the owner's pick, with their app's name in them, in one `CrashReportStrings` record where each string left out stays English. The host already knows its name and contact, so there's no placeholder syntax to learn or check. The contact link follows the contact sentence, so a language that would put it mid-sentence ends the sentence with a colon instead. It's `CrashReportStrings`, not `CrashReportText`, since a report's `Text` is the report itself.
- **The contact and the strings come in a second `Start` overload.** Package validation now compares each pack with 1.0.0, and turning `Start(appName, repo)` into one method with optional parameters fails it with CP0002, since hosts built against 1.0 call the two-argument method. This gives up a single `Start`.

## Shipped

| Version | Released | What it brought |
|---|---|---|
| [0.1.0-preview.1](https://www.nuget.org/packages/Oopsling.Avalonia/0.1.0-preview.1) | 2026-10-27 | M0 to M3: the masked report written while dying, the window that shows it once, the filled-in issue link, the release workflow, and Staypaw as the first host. |
| [1.0.0](https://github.com/hazeliscoding/oopsling/releases/tag/v1.0.0) | 2026-10-27 | M4: install steps, a quick start and a demo in the README, the fresh-install check on each OS, `SECURITY.md` and `CONTRIBUTING.md`, and error messages that name what they refused. |
| [1.1.0](https://github.com/hazeliscoding/oopsling/releases/tag/v1.1.0) | 2026-10-27 | M5's code: a contact for users without a GitHub account, the window's text in the host's language, and each pack checked against 1.0.0's API. |

## M4: 1.0, finishing

Everything else in M4 shipped as 1.0.0.

- [ ] A Staypaw release with Oopsling in it has shipped. Staypaw's `main` takes 1.0.0 for v1.2; when it ships is the owner's call.

**Done when:** that Staypaw release is out.

## M5: 1.1, reports without GitHub, and the window in any language

- [x] A `Start` overload takes a contact, an email address or a link, and the window shows "No GitHub account? Send the report to …" under its buttons. Existing `Start(appName, repo)` calls don't change. Clicking the contact copies the report, then opens it (see the decisions above).
- [x] A host can give the window its own text for every string it shows, in any language, with English as the default. The note at the top of a cut report, which reaches the issue, takes the host's text too. Oopsling ships no translations of its own.
- [ ] Staypaw takes 1.1 with a contact the owner picks.

**Done when:** the sample shows the contact line and a German window from host-given text; a test proves `Start(appName, repo)` behaves as in 1.0; and the fresh-install check passes against 1.1.0 on each OS.

**Checked on 2026-10-27,** before the release:

- The sample's Release build, crashed with `--crash ui` and started again with `--contact crashes@example.com --lang de`, showed the window in German with the contact line under its buttons. That run is also the only check that `Start` passes the contact and strings on: a test can't call `Start` without hooking the test host's own crashes. A throwaway headless render, not kept, showed the German heading and contact line wrapping at the narrowest width.
- `Start(appName, repo)` behaves as in 1.0: tests pin every English string 1.0 showed, word for word, and the window without a contact or strings shows exactly 1.0's text. Package validation against 1.0.0 passes, and failed with CP0002 when `Start(appName, repo)` was given an optional contact instead.
- Positive controls: accepting `http://`, spaces or `?` in a contact each fails a test, and so do a contact click that doesn't copy the report and one that opens before it copies. The branch where the copy fails isn't tested, since the headless clipboard can't be made to fail.
- A review agent read the change. Its fixes: a blank heading falls back to English, the contact sentence wraps, addresses are an allow-list, and the README example says to give every string.
- 1.1.0 is on nuget.org, and the fresh-install check passed against it on Windows, macOS and Linux: [run 40627254210](https://github.com/hazeliscoding/oopsling/actions/runs/40627254210).
- Left: Staypaw takes 1.1 with the contact `staypaw.crashes.happier582@passinbox.com`, an alias the owner made for it.

## M6: Sweepling, the second host

- [ ] Sweepling takes `Oopsling.Avalonia` from nuget.org, and its no-network guard covers Oopsling's assemblies, as Staypaw's does.
- [ ] Anything Sweepling needs that only suited Staypaw is fixed in Oopsling first, with a dated decision.

**Done when:** a Sweepling build made to crash shows the window at its next launch and opens a filled-in issue page for Sweepling's repo, and a Sweepling release with Oopsling in it has shipped.

## M7: 1.2, native crashes on Windows

- [ ] A dated decision on how to read a crash that left no managed exception, at the next launch: Windows' Application log, where Windows Error Reporting and the .NET runtime record a crashed process, read through `wevtapi` with source-generated interop, or a package, which needs the owner's approval. It stays NativeAOT safe and lives in a Windows-only package, so the core stays as it is.
- [ ] A dated decision on what such a report holds, since "What a report holds" lists only a managed exception's fields. Likely the exception code, the faulting module and its offset, the app's version, and the OS. It's masked like any report.
- [ ] It reads the log only when the window asks for reports, never at startup, so a launch still costs one handler and one folder check.

**Done when:** on Windows, the sample made to crash natively leaves no managed report, yet its next launch shows a report of the native crash; a masking test covers the paths an event carries; and the core's guard tests still pass.

## How we'll know it works

The dogfooding log in `docs/dogfooding.md`: crashes reported through Oopsling, and any report that held something it shouldn't have. That last count must stay zero. Public signals: NuGet downloads, issues, pull requests, and other apps using it.

## Later

- Line numbers in NativeAOT stacks, for example from a map shipped beside the app, if reports without them prove hard to act on.
- An unclean-exit marker ("closed unexpectedly last time"), if it can tell a crash from being ended in Task Manager or a forced reboot.
- WPF and WinForms windows. Until then, those apps use the core with their own dialog.
- A window for Avalonia 11, if a host on it asks. Azure Key Vault Explorer was on 11.3 when Oopsling was suggested to it. Avalonia 12 broke APIs the window uses, so it would be a build of its own.
- Filling in an issue form's fields, not only a blank issue's title and body, for hosts whose repos turn blank issues off. GitHub fills a form's fields from parameters named after them.
- `ShowUnshown` telling the host whether a window opened. It can't know when the owner hasn't opened yet, so it would need a callback or an event.
- More hosts after Sweepling, one at a time: Shybun, Tattlebun and Overglance as each one ships, then Gil Sweep, XIV Vault's desktop app, Prompuff and Carmine Workbench. Each needs a public repo with issues turned on, or the issue link opens a 404.

## Not planned

- Sending anything: no network code, no server, no email, no "send automatically" option.
- Minidumps, logs, breadcrumbs, screenshots or environment variables in a report.
- Catching stack overflows, `Environment.FailFast` or native crashes inside the process. M7 reads Windows' record of a native crash at the next launch instead.
- Unobserved task exceptions.
- Marking a crash handled to keep the app running.
- .NET 8 and 9, which reach the end of their support in November 2026.
