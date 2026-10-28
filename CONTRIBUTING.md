# Contributing

Thanks for helping. Issues are the most useful thing you can send, above all from people who ship apps without telemetry: what you'd need in a crash report, and what must never be in one.

- **A bug:** open an issue with the bug form.
- **A crash report that held something personal:** don't open an issue. [SECURITY.md](SECURITY.md) says how to send it privately.
- **A bigger change:** open an issue first, so we can agree on it before you write the code. [ROADMAP.md](ROADMAP.md) has the decisions already made and what's out of scope.

## Building

You need the .NET 10 SDK.

```sh
dotnet build Oopsling.slnx
dotnet test --solution Oopsling.slnx
dotnet format Oopsling.slnx
```

`samples/Oopsling.Sample` is a small Avalonia app with a Crash button. Run it, crash it, and start it again to see the window. Start it with `--repo <owner>/<name>` to open the issue link for a repo of your own, `--contact <address>` to show a contact under the buttons, and `--lang de` to see the window in German.

## The rules

[AGENTS.md](AGENTS.md) holds the rules for everyone who changes the code, people included, each with its reason. The ones that matter most:

- **No network.** Neither package may reference networking code, and a test fails if one does.
- **Nothing personal in a report.** Masking changes come with tests, written first, using made-up names and folders. Never a real one.
- **No new dependencies** in either package without asking first, since every app that uses Oopsling inherits them.
- **NativeAOT and trim safe.** Warnings are errors.

Commits follow [Conventional Commits](https://www.conventionalcommits.org/), such as `fix(mask): …`. A pull request needs CI to pass on Windows, Linux and macOS.

By contributing, you agree that your work is licensed under Apache-2.0, like the rest of the project.
