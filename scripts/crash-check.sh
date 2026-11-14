#!/usr/bin/env bash
# Crashes a build of the sample on the UI thread, then on a background thread, and checks that each crash left one
# report that names the method that threw, says whether the build is NativeAOT, and holds no trace of this machine's
# user or host name. This is M1's done line. On Windows it then crashes the sample natively, and checks that the next
# launch reports it.
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

  # On Windows the folder also remembers where in Windows' log the last look stopped.
  [ "$(ls "$folder" | grep -c '^crash-')" -eq 1 ] || fail "$thread: expected one report in $folder"
  report="$folder/$(ls "$folder" | grep '^crash-')"
  method="MainWindow.Crash"
  [ "$thread" = background ] && method="CrashOnBackgroundThread"

  grep -q "$method" "$report" || fail "$thread: the report doesn't name $method"
  grep -qx -- "- NativeAOT: $aot" "$report" || fail "$thread: the report doesn't say NativeAOT: $aot"
  if grep -qiw -- "$user" "$report"; then fail "$thread: the report holds the user name"; fi
  if grep -qiw -- "$host" "$report"; then fail "$thread: the report holds the host name"; fi
  echo "$thread: ok, $(head -1 "$report")"
done

# On Windows, a native crash leaves no report of its own, and the next launch reads Windows' record of it. This is M7's
# done line. The window shows whatever report it's given, and its own tests cover that.
case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*)
    rm -rf "$folder"
    if launch "$app" --crash native; then
      fail "native: the sample exited normally instead of crashing"
    fi

    if ls "$folder" 2>/dev/null | grep -q '^crash-'; then fail "native: the native crash left a managed report"; fi

    # Windows logs the crash about a second after the process ends.
    report="$(mktemp)"
    for _ in $(seq 30); do
      "$app" --print-report "$(cygpath -w "$report")"
      [ -s "$report" ] && break
      sleep 1
    done

    head -1 "$report" | grep -q "crashed: access violation (0xC0000005)" || fail "native: the next launch didn't report the native crash"
    if grep -qiw -- "$user" "$report"; then fail "native: the report holds the user name"; fi
    if grep -qiw -- "$host" "$report"; then fail "native: the report holds the host name"; fi
    echo "native: ok, $(head -1 "$report")"
    ;;
esac
