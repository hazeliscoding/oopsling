#!/usr/bin/env bash
# Follows the README in a new Avalonia app: makes one from Avalonia's own template, adds Oopsling.Avalonia from nuget.org,
# and pastes in the README's quick start lines. Then it crashes the app, checks the report, starts the app again, and
# checks that the window took the report without crashing. This is M4's fresh-install gate.
#
# Usage, from the repo root: scripts/fresh-install-check.sh <a version on nuget.org>
# It builds the app in a new temporary folder, and deletes the app's own reports folder first, and nothing else.
set -euo pipefail

version="$1"
readme="$PWD/README.md"
py="$(command -v python3 || command -v python)"

fail() {
  echo "$1"
  exit 1
}

# Taken from the README, so the check fails when the README stops matching the package.
grep -q '^dotnet add package Oopsling.Avalonia' "$readme" || fail "The README doesn't say to add Oopsling.Avalonia"
start="$(grep -m1 -E '^[[:space:]]*CrashReports\.Start\(' "$readme" | sed -E 's/^[[:space:]]+//' || true)"
show="$(grep -m1 -E '^[[:space:]]*CrashReportWindow\.ShowUnshown\(desktop\.MainWindow\);' "$readme" | sed -E 's/^[[:space:]]+//' || true)"
[ -n "$start" ] && [ -n "$show" ] || fail "The README's quick start lines are missing"
name="$(echo "$start" | sed -E 's/.*Start\("([^"]+)".*/\1/')"

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) folder="$(cygpath "$LOCALAPPDATA")/$name/crashes" ;;
  Darwin) folder="$HOME/Library/Application Support/$name/crashes" ;;
  *) folder="${XDG_DATA_HOME:-$HOME/.local/share}/$name/crashes" ;;
esac

user="${USER:-$USERNAME}"
host="$(hostname | cut -d. -f1)"

# Linux runners have no display, so the window opens on a virtual one.
launch() {
  if [ "$(uname -s)" = Linux ] && [ -z "${DISPLAY:-}" ]; then xvfb-run -a "$@"; else "$@"; fi
}

work="$(mktemp -d)"
# On Windows the killed app can still hold its files for a moment; a leftover temp folder mustn't fail the check.
trap 'rm -rf "$work" || true' EXIT
dotnet new install Avalonia.Templates --debug:custom-hive "$work/hive" > /dev/null
dotnet new avalonia.app -o "$work/FreshApp" -n FreshApp --debug:custom-hive "$work/hive" > /dev/null
cd "$work/FreshApp"
dotnet add package Oopsling.Avalonia --version "$version" > /dev/null

START="$start" SHOW="$show" "$py" - << 'EOF'
import os, re

p = "Program.cs"
s = open(p, encoding="utf-8-sig").read()
s, n = re.subn(
    r"public static void Main\(string\[\] args\) => BuildAvaloniaApp\(\)\s*\.StartWithClassicDesktopLifetime\(args\);",
    "public static void Main(string[] args)\n    {\n        " + os.environ["START"]
    + "\n        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);\n    }",
    s)
assert n == 1, "Program.cs isn't the template's"
open(p, "w", encoding="utf-8").write("using Oopsling;\n" + s)

p = "App.axaml.cs"
s = open(p, encoding="utf-8-sig").read()
old = "desktop.MainWindow = new MainWindow();"
assert s.count(old) == 1, "App.axaml.cs isn't the template's"
# The README's line, then the check's own crash on demand, which isn't in the README.
s = s.replace(old, old + "\n            " + os.environ["SHOW"]
    + '\n            if (System.Environment.GetEnvironmentVariable("FRESH_APP_CRASH") == "1") throw new System.InvalidOperationException("A crash on purpose");')
open(p, "w", encoding="utf-8").write("using Oopsling.Avalonia;\n" + s)
EOF

dotnet build --configuration Release --output "$work/out" > "$work/build.log" || { cat "$work/build.log"; fail "The app doesn't build"; }
exe="$work/out/FreshApp"
[ -f "$exe.exe" ] && exe="$exe.exe"

rm -rf "$folder"
if FRESH_APP_CRASH=1 launch "$exe"; then
  fail "The app exited normally instead of crashing"
fi

[ "$(ls "$folder" | wc -l)" -eq 1 ] || fail "Expected one report in $folder"
report="$(ls "$folder")"
grep -q "^$name .* crashed: InvalidOperationException" "$folder/$report" || fail "The report's title isn't the app's crash"
grep -q "A crash on purpose" "$folder/$report" || fail "The report doesn't hold the exception's message"
if grep -qiw -- "$user" "$folder/$report"; then fail "The report holds the user name"; fi
if grep -qiw -- "$host" "$folder/$report"; then fail "The report holds the host name"; fi

# The next launch takes the report once the main window opens, then shows it. A failure there would crash the app and
# leave a second report.
launch "$exe" &
for _ in $(seq 1 60); do
  [ -f "$folder/shown" ] && break
  sleep 1
done
sleep 3
case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) taskkill //F //IM FreshApp.exe > /dev/null 2>&1 || true ;;
  *) pkill -f "$work/out/FreshApp" || true ;;
esac
wait || true

[ "$(cat "$folder/shown" 2> /dev/null)" = "$report" ] || fail "The next launch didn't take the report"
[ "$(ls "$folder"/crash-*.md | wc -l)" -eq 1 ] || fail "Showing the report crashed the app"
echo "ok: $(head -1 "$folder/$report"), shown at the next launch"
