#!/bin/sh
# The iOS bridge on a real iOS SDK, without Unity:
#   1. compiles the vendored Swift SDK and FloadAttributionUnityBridge.swift as a
#      module named UnityFramework (as Unity's Xcode project does), and
#      FloadAttributionBridge.mm against the generated UnityFramework-Swift.h,
#      for an iOS 15 device (compile only) and for the simulator;
#   2. links the C driver (Tests~/HostTests/bridge-host-test.c, which calls the
#      bridge's C functions the way [DllImport("__Internal")] does) for the
#      simulator, runs it there with `simctl spawn`, and checks what the local
#      stub ingest received.
# Needs Xcode with an iOS simulator and Node. Shuts down the simulator if it booted it.
# Usage: sh Tools~/ios-simulator-check.sh   (FLOAD_SIM_UDID=… to pick one)
set -eu
cd "$(dirname "$0")/.."
root="$PWD"
work="$(mktemp -d)"
stub_script="$root/Tools~/stub-ingest.mjs"
. "$root/Tools~/sim-common.sh"
trap 'finish; rm -rf "$work"' EXIT

swift_sources="Plugins/iOS/FloadAttribution/*.swift Plugins/iOS/FloadAttributionUnityBridge.swift"

build() { # sdk target outdir
  sdk="$1"; target="$2"; out="$3"; mkdir -p "$out"
  # shellcheck disable=SC2086
  run_logged "swift-$sdk" xcrun -sdk "$sdk" swiftc -target "$target" -wmo -parse-as-library \
    -module-name UnityFramework -c $swift_sources \
    -emit-objc-header -emit-objc-header-path "$out/UnityFramework-Swift.h" -o "$out/swift.o"
  run_logged "mm-$sdk" xcrun -sdk "$sdk" clang++ -target "$target" -std=c++17 -fobjc-arc -fmodules -Werror -Wall \
    -DFLOAD_SWIFT_HEADER='"UnityFramework-Swift.h"' -I "$out" \
    -c Plugins/iOS/FloadAttributionBridge.mm -o "$out/bridge.o"
}

echo "== device build (iOS 15, compile only)"
build iphoneos arm64-apple-ios15.0 "$work/device"

echo "== simulator build and run"
build iphonesimulator arm64-apple-ios15.0-simulator "$work/sim"
run_logged c-driver xcrun -sdk iphonesimulator clang -target arm64-apple-ios15.0-simulator -Werror -Wall \
  -c "Tests~/HostTests/bridge-host-test.c" -o "$work/sim/main.o"
run_logged link xcrun -sdk iphonesimulator swiftc -target arm64-apple-ios15.0-simulator \
  "$work/sim/swift.o" "$work/sim/bridge.o" "$work/sim/main.o" -lc++ -o "$work/sim/bridge-host-test"

sim_boot
stub_start 3999
mkdir -p "$work/home"
# A throwaway home, so every run is a first launch.
SIMCTL_CHILD_CFFIXED_USER_HOME="$work/home" \
  xcrun simctl spawn "$udid" "$work/sim/bridge-host-test" "http://127.0.0.1:3999" "unity-host-user-1"

curl -sf "http://127.0.0.1:3999/__requests" > "$work/requests.json"
node - "$work/requests.json" <<'JS'
const requests = JSON.parse(require('fs').readFileSync(process.argv[2], 'utf8'));
const installs = requests.filter((r) => r.route === 'installs');
const identifies = requests.filter((r) => r.route === 'identify');
let failed = 0;
const check = (ok, name) => { console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}`); if (!ok) failed++; };
const install = installs[0];
check(installs.length === 1, `one install sent (got ${installs.length})`);
check(install && install.status === 202, 'the stub accepted the install (contract valid)');
check(install && install.body.platform === 'ios', 'platform ios');
check(install && install.body.sdk.name === 'fload-attribution-ios', 'sent by the Swift SDK');
const linked = (install && install.body.appUserId === 'unity-host-user-1') || identifies.some((r) => r.status === 202 && r.body.appUserId === 'unity-host-user-1');
check(linked, 'user id set before configure reached the stub');
const later = identifies.find((r) => r.body.appUserId === 'unity-host-user-2');
check(later && later.status === 202 && install && later.body.installReference === install.body.installReference, 'user id set after the install went out as an identify for the same install');
console.log(`appleAds: ${JSON.stringify(install && install.body.appleAds)}  osVersion: ${install && install.body.osVersion}  deviceModel: ${install && install.body.deviceModel}`);
process.exit(failed ? 1 : 0);
JS
echo "stub log:"; cat "$work/stub.log"
