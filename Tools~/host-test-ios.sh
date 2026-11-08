#!/bin/sh
# Host test of the iOS bridge without Unity or Xcode: compiles the vendored
# Swift SDK and FloadAttributionUnityBridge.swift as a module named
# UnityFramework (as Unity's Xcode project does), compiles
# FloadAttributionBridge.mm against the generated UnityFramework-Swift.h, links
# a C program that calls the bridge's C functions like [DllImport] does, runs it
# against the local stub ingest, and checks what the stub received.
# Needs only the Command Line Tools (swiftc, clang) and Node.
set -eu
cd "$(dirname "$0")/.."
out="$(mktemp -d)"
port=$((40000 + $$ % 10000))
cleanup() {
  if [ -n "${stub:-}" ]; then kill "$stub" 2>/dev/null || true; wait "$stub" 2>/dev/null || true; fi
  rm -rf "$out"
}
trap cleanup EXIT
# The wrapper name and version Runtime/FloadAttribution.cs passes to the native
# side, and the package version they must match.
wrapper_name="$(sed -n 's/.*const string WrapperName = "\([^"]*\)".*/\1/p' Runtime/FloadAttribution.cs)"
wrapper_version="$(sed -n 's/.*const string Version = "\([^"]*\)".*/\1/p' Runtime/FloadAttribution.cs)"
package_version="$(node -p 'require("./package.json").version')"

swiftc -wmo -parse-as-library -module-name UnityFramework -c \
  Plugins/iOS/FloadAttribution/*.swift Plugins/iOS/FloadAttributionUnityBridge.swift \
  -emit-objc-header -emit-objc-header-path "$out/UnityFramework-Swift.h" \
  -o "$out/swift.o"
clang++ -std=c++17 -fobjc-arc -fmodules -Werror -Wall \
  -DFLOAD_SWIFT_HEADER='"UnityFramework-Swift.h"' -I "$out" \
  -c Plugins/iOS/FloadAttributionBridge.mm -o "$out/bridge.o"
clang -Werror -Wall -c "Tests~/HostTests/bridge-host-test.c" -o "$out/main.o"
swiftc "$out/swift.o" "$out/bridge.o" "$out/main.o" -lc++ -o "$out/bridge-host-test"

node Tools~/stub-ingest.mjs --port "$port" > "$out/stub.log" 2>&1 &
stub=$!
sleep 1

# The SDK keeps its state in Application Support; give it a throwaway home.
mkdir -p "$out/home"
CFFIXED_USER_HOME="$out/home" "$out/bridge-host-test" "http://127.0.0.1:$port" "unity-host-user-1" "$wrapper_name" "$wrapper_version"

curl -s "http://127.0.0.1:$port/__requests" > "$out/requests.json"
node - "$out/requests.json" "$package_version" <<'JS'
const requests = JSON.parse(require('fs').readFileSync(process.argv[2], 'utf8'));
const packageVersion = process.argv[3];
const installs = requests.filter((r) => r.route === 'installs');
const identifies = requests.filter((r) => r.route === 'identify');
let failed = 0;
const check = (ok, name) => { console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}`); if (!ok) failed++; };
check(installs.length === 1, `one install sent (got ${installs.length})`);
const install = installs[0];
check(install && install.status === 202, 'the stub accepted the install (contract valid)');
check(install && install.body.platform === 'ios', 'platform ios');
check(install && install.body.sdk.name === 'fload-attribution-ios', 'sdk name is the Swift SDK');
check(install && install.keyPrefix === 'fload_pk_', 'write key header sent');
const wrapper = install && install.body.sdk.wrapper;
check(wrapper && wrapper.name === 'unity' && wrapper.version === packageVersion && Object.keys(wrapper).length === 2, `sdk.wrapper is {"unity", "${packageVersion}"} (package.json)`);
const linked = (install && install.body.appUserId === 'unity-host-user-1') || identifies.some((r) => r.status === 202 && r.body.appUserId === 'unity-host-user-1');
check(linked, 'user id set before configure reached Fload');
const identify = identifies.find((r) => r.body.appUserId === 'unity-host-user-2');
check(identify && identify.status === 202, 'user id set after the install went out as an accepted identify');
check(identify && install && identify.body.installReference === install.body.installReference, 'identify names the same install');
console.log(`sdk: ${JSON.stringify(install && install.body.sdk)}  appleAds: ${JSON.stringify(install && install.body.appleAds)}`);
process.exit(failed ? 1 : 0);
JS
echo "stub log:"; cat "$out/stub.log"
