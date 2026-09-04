#!/bin/bash
# ============================================================================
# CROSSROADS Android development APK - local build wrapper (task 15).
#
# Prerequisites (dev machine):
#   - Unity 6 LTS (6000.0.23f1) installed via Unity Hub
#   - Android Build Support module (Android SDK/NDK + OpenJDK) installed
#     (Unity Hub -> Installs -> Add modules -> Android Build Support)
#
# Usage:
#   bash scripts/build_android_apk.sh /path/to/Unity/Editor/Unity
#
# Output:
#   Builds/CrossroadsDev.apk  (development build: debuggable, profiler-capable)
#
# Install on a device (developer mode + USB debugging on):
#   adb install -r Builds/CrossroadsDev.apk
# then launch "CROSSROADS" and verify the checklist in ANDROID_BUILD.md.
# ============================================================================
set -euo pipefail

UNITY="${1:-}"
if [ -z "$UNITY" ]; then
  # common Unity Hub locations
  for CAND in \
    "$(ls "$HOME/Unity/Hub/Editor"/6*/Unity 2>/dev/null | tail -1)" \
    "/Applications/Unity/Hub/Editor/6000.0.23f1/Unity.app/Contents/MacOS/Unity" \
    "/usr/bin/unity-editor"; do
    if [ -x "$CAND" ]; then UNITY="$CAND"; break; fi
  done
fi
if [ -z "$UNITY" ] || [ ! -x "$UNITY" ]; then
  echo "ERROR: Unity 6 editor executable not found." >&2
  echo "Pass it explicitly: bash scripts/build_android_apk.sh /path/to/Unity" >&2
  exit 1
fi

cd "$(dirname "$0")/.."
mkdir -p Builds

echo "== Building CROSSROADS dev APK with: $UNITY"
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$(pwd)" \
  -executeMethod Crossroads.EditorTools.AndroidDevBuild.BuildDevApk \
  -buildTarget Android \
  -logFile Builds/android-build.log

STATUS=$?
tail -5 Builds/android-build.log || true
if [ $STATUS -ne 0 ]; then
  echo "== BUILD FAILED - see Builds/android-build.log" >&2
  exit $STATUS
fi
echo "== DONE: Builds/CrossroadsDev.apk"
echo "   adb install -r Builds/CrossroadsDev.apk"
