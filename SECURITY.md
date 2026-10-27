# Security

Oopsling's promise is that nothing personal reaches a crash report, and that nothing leaves the user's PC unless they file it. Anything that breaks that promise is a security problem.

## Tell me privately

Use GitHub's [private vulnerability reporting](https://github.com/hazeliscoding/oopsling/security/advisories/new). Please don't open a public issue: if a crash report held something personal, an issue would publish it again.

Send it privately when:

- a crash report held a user name, a home folder, a machine or domain name, a token, or anything else personal, however it was written;
- Oopsling sent anything anywhere, or referenced networking code;
- the crash handler hung the app, changed how it exits, or kept a crashed app running.

Describe what got through by its shape, with made-up values, such as `C:\Users\<name>~1\` or `token: abc…`. Include the Oopsling version and the OS.

## Versions

Only the latest release gets fixes. Oopsling is maintained by one person, so I'll reply as soon as I can, usually within a week.
