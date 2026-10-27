# AGENTS.md

These are the working rules for agents in this repo. Oopsling (Apache-2.0) is a .NET library that turns a crash into a GitHub issue the user files themselves, and sends nothing on its own. It is two NuGet packages on .NET 10: `Oopsling`, with no UI, and `Oopsling.Avalonia`, the window. Its first host is [Staypaw](https://github.com/hazeliscoding/staypaw).

## Sources of truth

- `README.md`: what Oopsling does and its promise.
- `ROADMAP.md`: the architecture characteristics, decisions already made, the milestones, and what is out of scope. Work from the next unchecked item, tick it off when done, and record new decisions there, dated, with why and what they gave up. When a decision changes, mark the old one superseded instead of deleting it. Respect existing decisions unless the owner reopens them, and don't build past the current milestone without asking.

## Hard rules

Each rule has its reason, so it can be applied to cases it didn't foresee. When one becomes code, give it a test that fails when the rule is broken.

- **Oopsling never touches the network.** The whole promise is that the user decides what leaves the PC, in their own browser. Neither package references a networking assembly, and opening a URL through the OS shell is the only way out. Hosts such as Staypaw and Sweepling enforce the same rule in their own guard tests, so a networking reference here breaks them too.
- **Nothing goes in a report that the user wouldn't post in public.** A GitHub issue is public. A report holds only the fields `ROADMAP.md` lists under "What a report holds"; a new field needs a dated decision there. Mask before anything reaches the disk.
- **Mask the user name wherever it appears, not only inside the home folder's path.** Paths reach messages in other shapes: Claude Code writes `C:\Users\<name>\project` as `C--Users-<name>-project`, which got past Sweepling's profile-path masking.
- **Never put a real token, user name or home folder in a test or fixture.** Build token-shaped values at run time and use made-up names, so no real value reaches the history and gitleaks stays quiet for the right reason.
- **The crash handler does as little as it can.** It runs in a broken process: no UI, no lock it might wait on, no host services, no network, and no work that start could have done instead. It catches its own failures, so the original crash still ends the app as it would have, and Oopsling never changes how the app exits.
- **Never mark a crash handled.** Oopsling records the crash and lets the app die. A broken app kept running can damage the user's data.
- **The core never references Avalonia.** It serves any .NET app, and a test enforces it.
- **NativeAOT and trim safe, with no reflection.** Hosts are NativeAOT apps that count megabytes of idle memory. Trim and AOT warnings are errors in every project. If serialization is ever needed, it is source-generated.
- **Startup stays at one handler and one folder check.** The host pays it on every launch, crash or not. Reading and formatting a report waits until the window asks for it.
- **The tag and the nuget.org publish wait for the owner's go-ahead.** A version on nuget.org can't be deleted, only unlisted, so a mistake there stays public.

## Glossary

One meaning per term. Code, docs, UI copy and agents use these words and no synonyms.

- **Crash:** an unhandled managed exception that ends the process. Nothing else counts.
- **Report:** the masked Markdown file Oopsling writes for one crash. It is the issue body as it will be filed.
- **Host:** an app that uses Oopsling, such as Staypaw.
- **Mask:** replace personal text with a placeholder before anything is written, such as the home folder, the user name or a token.
- **Issue link:** the host repo's GitHub new-issue URL with a report's title and body filled in.
- **Shown:** a report the window has displayed once. It isn't shown again.

## Dependencies

- `Oopsling` depends on nothing beyond the .NET base library, and `Oopsling.Avalonia` adds only Avalonia. Whatever either package depends on becomes every host's dependency, with its size and idle memory, so never add a package to either without asking.
- Tests, the sample and tools may add a special-purpose library on their own, such as a test helper, when it is NativeAOT-compatible where it ships in the sample. Give the reason in the commit.
- A general-purpose helper only after checking that nothing in the project or the .NET base library already does the job.
- Never a framework without asking.

## Commands

- `dotnet build Oopsling.slnx` and `dotnet test --solution Oopsling.slnx` from the repo root.
- `dotnet format Oopsling.slnx --verify-no-changes` is the CI style check. Run `dotnet format Oopsling.slnx` before committing.
- `dotnet pack Oopsling.slnx -c Release -o artifacts/packages` builds both packages.
- `dotnet publish samples/Oopsling.Sample -c Release -r win-x64 -o artifacts/publish` makes the sample's NativeAOT build; use `linux-x64` or `osx-arm64` on those OSes. On Windows it needs Visual Studio's C++ workload. If linking fails with "'vswhere.exe' is not recognized", put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on PATH: VS 18's `vcvarsall.bat` calls `vswhere.exe` by bare name, and the error text lands in the linker path.
- To prove a guard test, add a throwaway file that breaks its rule, such as a core class that creates an `HttpClient`, check that exactly that test fails, then delete the file. The file has to compile, since warnings are errors.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/), atomic, with a scope when it helps (`feat(mask): …`).
- **No AI attribution** in commits or PRs: no `Co-Authored-By` trailers, no "Generated with" lines, no session links.
- **Tests first for masking and the report.** Both are pure functions of their inputs, so tests give them made-up home folders, user names and exceptions.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. A check that tests for an absence needs a positive control: prove a CI guardrail with a throwaway pull request that must fail, and a guard test by changing the code it protects so the test fails.
- **Never file a real issue to test the link.** Open the filled-in page for the private scratch repo, by starting the sample with `--repo hazeliscoding/oopsling-scratch`, and stop before Submit. A filed test issue lands in someone's tracker. Leave the tabs it opens for the owner to close: the browser is theirs, with their own tabs in it.
- **Validation:** evidence comes from the dogfooding log and public signals (downloads, issues, PRs). Don't plan interviews or outreach. One exception, the owner's call: a reply pointing to Oopsling on another repo's open issue that asks for this is fine. Draft each one and post it only after the owner approves it, since it's public and under the owner's name.
- **Docs:** short. Prefer editing `ROADMAP.md` over new planning documents.
- **Code comments:** explain why, not what. No boilerplate XML docs, no commented-out code.
- **UI copy:** calm, short, declarative. No exclamation marks or emoji. The window says what happened in words ("Staypaw closed because of an error") before it shows the report.
