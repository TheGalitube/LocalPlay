#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
android_root="$(cd "${script_dir}/.." && pwd)"

if [[ -z "${ANDROID_HOME:-}" && -z "${ANDROID_SDK_ROOT:-}" && ! -f "${android_root}/local.properties" ]]; then
  echo "Android SDK nicht gefunden. Setze ANDROID_HOME oder öffne Android/ in Android Studio." >&2
  exit 2
fi

cd "${android_root}"
./gradlew testDebugUnitTest assembleDebug
