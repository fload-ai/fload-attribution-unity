# Shared helpers for the iOS simulator checks (sourced, not run).
# Needs Xcode with an iOS simulator runtime, Node and curl.

# Boots the simulator named in $FLOAD_SIM_UDID, or the first available iPhone,
# and remembers whether we booted it so finish() only shuts down our own.
sim_boot() {
  if [ -n "${FLOAD_SIM_UDID:-}" ]; then
    udid="$FLOAD_SIM_UDID"
  else
    udid="$(xcrun simctl list devices available -j | node -e '
      const d = JSON.parse(require("fs").readFileSync(0, "utf8")).devices;
      const ios = Object.keys(d).filter((k) => k.includes("iOS")).sort().reverse();
      for (const k of ios) { const p = d[k].find((x) => x.name.startsWith("iPhone")); if (p) { console.log(p.udid); break; } }')"
  fi
  [ -n "$udid" ] || { echo "FAIL no iPhone simulator available"; exit 1; }
  state="$(xcrun simctl list devices -j | node -e "
    const d = JSON.parse(require('fs').readFileSync(0, 'utf8')).devices;
    for (const k in d) for (const x of d[k]) if (x.udid === '$udid') console.log(x.state);")"
  booted_here=0
  if [ "$state" != "Booted" ]; then
    xcrun simctl boot "$udid"
    booted_here=1
  fi
  xcrun simctl bootstatus "$udid" -b >/dev/null
  echo "simulator $udid"
}

# Runs a command with its output in $work/<name>.log; on failure prints the
# log's tail and stops, so a failed build can never read as a pass.
run_logged() { # name command...
  name="$1"; shift
  if "$@" > "$work/$name.log" 2>&1; then
    echo "ok   $name"
  else
    code=$?
    echo "FAIL $name (exit $code); last lines of its log:"
    tail -60 "$work/$name.log"
    exit 1
  fi
}

stub_start() { # port
  stub_port="$1"
  node "$stub_script" --port "$stub_port" > "$work/stub.log" 2>&1 &
  stub_pid=$!
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    curl -sf "http://127.0.0.1:$stub_port/__requests" >/dev/null && return 0
    sleep 0.5
  done
  echo "FAIL stub ingest did not start"; cat "$work/stub.log"; exit 1
}

# Waits up to $2 seconds for the stub to hold an accepted install from app id $1.
stub_wait_install() { # appId seconds
  i=0
  while [ "$i" -lt "$2" ]; do
    if curl -sf "http://127.0.0.1:$stub_port/__requests" | node -e "
      const r = JSON.parse(require('fs').readFileSync(0, 'utf8'));
      process.exit(r.some((x) => x.route === 'installs' && x.body && x.body.appId === '$1') ? 0 : 1);"; then
      return 0
    fi
    sleep 1; i=$((i + 1))
  done
  return 1
}

# Asserts what the stub received from app id $1 with user id $2; prints ok/FAIL lines.
stub_assert() { # appId userId
  curl -sf "http://127.0.0.1:$stub_port/__requests" > "$work/requests.json"
  node - "$work/requests.json" "$1" "$2" <<'JS'
const [file, appId, userId] = process.argv.slice(2);
const all = JSON.parse(require('fs').readFileSync(file, 'utf8'));
const installs = all.filter((r) => r.route === 'installs' && r.body && r.body.appId === appId);
const install = installs[0];
const ref = install && install.body.installReference;
const identifies = all.filter((r) => r.route === 'identify' && r.body && r.body.installReference === ref);
let failed = 0;
const check = (ok, name) => { console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}`); if (!ok) failed++; };
check(installs.length === 1, `one install from ${appId} (got ${installs.length})`);
check(install && install.status === 202, 'the stub accepted it (contract valid)');
check(install && install.body.platform === 'ios', 'platform ios');
check(install && install.body.sdk && install.body.sdk.name === 'fload-attribution-ios', 'sent by the Swift SDK');
check(install && install.keyPrefix === 'fload_pk_', 'write key header');
const linked = (install && install.body.appUserId === userId) || identifies.some((r) => r.status === 202 && r.body.appUserId === userId);
check(linked, `user id ${userId} reached the stub`);
console.log(`appleAds: ${JSON.stringify(install && install.body.appleAds)}  sdk: ${JSON.stringify(install && install.body.sdk)}`);
process.exit(failed ? 1 : 0);
JS
}

finish() {
  if [ -n "${stub_pid:-}" ]; then kill "$stub_pid" 2>/dev/null || true; wait "$stub_pid" 2>/dev/null || true; fi
  if [ "${booted_here:-0}" = 1 ] && [ -n "${udid:-}" ]; then xcrun simctl shutdown "$udid" 2>/dev/null || true; fi
}
