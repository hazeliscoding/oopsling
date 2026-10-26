#!/usr/bin/env bash
# Crashes a build of the sample on the UI thread, then on a background thread, and checks that each crash left one
# report that names the method that threw, says whether the build is NativeAOT, and holds no trace of this machine's
# user or host name. This is M1's done line.
#
# Usage, from the repo root: scripts/crash-check.sh <the sample's executable> <yes|no: is it a NativeAOT build>
# It deletes the sample's own reports folder first, and nothing else.
set -euo pipefail

app="$1"
aot="$2"

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) folder="$(cygpath "$LOCALAPPDATA")/Oopsling Sample/crashes" ;;
  Darwin) folder="$HOME/Library/Application Support/Oopsling Sample/crashes" ;;
  *) folder="${XDG_DATA_HOME:-$HOME/.local/share}/Oopsling Sample/crashes" ;;
esac

user="${USER:-$USERNAME}"
host="$(hostname | cut -d. -f1)"

# Linux runners have no display, so the window opens on a virtual one.
launch() {
  if [ "$(uname -s)" = Linux ] && [ -z "${DISPLAY:-}" ]; then xvfb-run -a "$@"; else "$@"; fi
}

fail() {
  echo "$1"
  exit 1
}

for thread in ui background; do
  rm -rf "$folder"
  if launch "$app" --crash "$thread"; then
    fail "$thread: the sample exited normally instead of crashing"
  fi

  [ "$(ls "$folder" | wc -l)" -eq 1 ] || fail "$thread: expected one report in $folder"
  report="$folder/$(ls "$folder")"
  method="MainWindow.Crash"
  [ "$thread" = background ] && method="CrashOnBackgroundThread"

  grep -q "$method" "$report" || fail "$thread: the report doesn't name $method"
  grep -qx -- "- NativeAOT: $aot" "$report" || fail "$thread: the report doesn't say NativeAOT: $aot"
  if grep -qiw -- "$user" "$report"; then fail "$thread: the report holds the user name"; fi
  if grep -qiw -- "$host" "$report"; then fail "$thread: the report holds the host name"; fi
  echo "$thread: ok, $(head -1 "$report")"
done
