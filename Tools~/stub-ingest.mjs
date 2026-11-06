#!/usr/bin/env node
// A local stand-in for Fload's attribution ingest, for running the example app
// against your own machine. Never point an example at production.
//
//   node Tools~/stub-ingest.mjs [--port 3999]
//
// It checks each request against the wire contract (the two routes, the
// `X-Fload-Write-Key` header, the fields and their limits) and answers like the
// real ingest: 202 {accepted:true}, 400 invalid_request, 401 for an unknown key,
// 410 for a revoked key. Keys: anything starting `fload_pk_` is accepted, except
// `fload_pk_unknown` (401) and `fload_pk_revoked` (410).
//
// GET /__requests returns every request it received (the token shortened), and
// DELETE /__requests clears them, so a test can assert what the SDK sent.
// No dependencies; Node 18 or later.
import { createServer } from 'node:http';

const port = Number(argValue('--port') ?? process.env.PORT ?? 3999);
const received = [];

const APPLE_STATUSES = ['token', 'token_expired', 'unsupported_os', 'token_error'];
const PLAY_STATUSES = ['ok', 'unavailable', 'not_supported', 'error'];
const INSTANT = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/;
const PHONE_ID = /^[A-Za-z0-9._:-]{8,128}$/;

const server = createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', 'http://stub');
  if (url.pathname === '/__requests') {
    if (req.method === 'DELETE') received.length = 0;
    return send(res, 200, received);
  }
  if (req.method !== 'POST' || !['/attribution/v1/installs', '/attribution/v1/identify'].includes(url.pathname)) {
    return send(res, 404, { error: 'not_found' });
  }
  const raw = await readBody(req);
  const key = req.headers['x-fload-write-key'];
  const route = url.pathname.endsWith('installs') ? 'installs' : 'identify';
  let body;
  try {
    body = JSON.parse(raw.toString('utf8'));
  } catch {
    body = undefined;
  }
  const problems = raw.length > 2048 ? ['body over 2 KB'] : body === undefined ? ['body is not JSON'] : validate(route, body);
  const status = statusFor(key, problems);
  received.push({
    at: new Date().toISOString(),
    route,
    status,
    keyPrefix: typeof key === 'string' ? key.slice(0, 9) : null,
    userAgent: req.headers['user-agent'] ?? null,
    problems,
    body: redact(body),
  });
  console.log(`${new Date().toISOString()} ${route} -> ${status}${problems.length ? ` (${problems.join('; ')})` : ''} ${summary(body)}`);
  if (status === 202) return send(res, 202, { accepted: true });
  if (status === 400) return send(res, 400, { error: 'invalid_request', message: problems.join('; ') });
  if (status === 413) return send(res, 413, { error: 'too_large' });
  return send(res, status, { error: status === 410 ? 'revoked' : 'unauthorized' });
});

server.listen(port, '0.0.0.0', () => console.log(`stub ingest on http://0.0.0.0:${port}`));

function statusFor(key, problems) {
  if (typeof key !== 'string' || !key.startsWith('fload_pk_') || key === 'fload_pk_unknown') return 401;
  if (key === 'fload_pk_revoked') return 410;
  if (problems.includes('body over 2 KB')) return 413;
  return problems.length ? 400 : 202;
}

function validate(route, b) {
  const p = [];
  if (typeof b !== 'object' || b === null || Array.isArray(b)) return ['body is not an object'];
  id(p, b, 'requestId');
  id(p, b, 'installReference');
  if (!['ios', 'android'].includes(b.platform)) p.push('platform must be ios or android');
  if (route === 'identify') {
    text(p, b, 'appUserId', 256, true);
    return p;
  }
  text(p, b, 'appId', 256, true);
  for (const f of ['appVersion', 'appBuild', 'osVersion', 'deviceModel', 'locale']) text(p, b, f, 64, false);
  if (typeof b.sdk !== 'object' || b.sdk === null) p.push('sdk is required');
  else {
    text(p, b.sdk, 'name', 64, true, 'sdk.');
    text(p, b.sdk, 'version', 64, true, 'sdk.');
  }
  instant(p, b, 'firstLaunchAt', true);
  text(p, b, 'appUserId', 256, false);
  if (b.platform === 'ios') {
    if (!b.appleAds || b.playReferrer) p.push('iOS sends appleAds, Android sends playReferrer');
    else {
      const a = b.appleAds;
      if (!APPLE_STATUSES.includes(a.status)) p.push('appleAds.status is not a known status');
      if ((a.status === 'token') !== Boolean(a.token)) p.push('token is sent exactly when status is "token"');
      if (a.token !== undefined && (typeof a.token !== 'string' || a.token.length > 4096 || !/^[A-Za-z0-9+/=_-]+$/.test(a.token))) p.push('appleAds.token is not base64');
      instant(p, a, 'tokenCreatedAt', false, 'appleAds.');
    }
  }
  if (b.platform === 'android') {
    if (!b.playReferrer || b.appleAds) p.push('iOS sends appleAds, Android sends playReferrer');
    else {
      const r = b.playReferrer;
      if (!PLAY_STATUSES.includes(r.status)) p.push('playReferrer.status is not a known status');
      if (r.referrer !== undefined && (typeof r.referrer !== 'string' || r.referrer.length > 1024)) p.push('playReferrer.referrer is too long');
      for (const f of ['referrerClickAt', 'installBeginAt', 'referrerClickServerAt', 'installBeginServerAt']) instant(p, r, f, false, 'playReferrer.');
      text(p, r, 'installVersion', 64, false, 'playReferrer.');
      if (r.googlePlayInstant !== undefined && typeof r.googlePlayInstant !== 'boolean') p.push('playReferrer.googlePlayInstant must be a boolean');
    }
  }
  return p;
}

function id(p, o, f) {
  if (typeof o[f] !== 'string' || !PHONE_ID.test(o[f])) p.push(`${f} must be 8 to 128 of [A-Za-z0-9._:-]`);
}
function text(p, o, f, max, required, prefix = '') {
  if (o[f] === undefined) {
    if (required) p.push(`${prefix}${f} is required`);
    return;
  }
  if (typeof o[f] !== 'string' || o[f].trim().length < 1 || o[f].trim().length > max) p.push(`${prefix}${f} must be 1 to ${max} characters`);
}
function instant(p, o, f, required, prefix = '') {
  if (o[f] === undefined) {
    if (required) p.push(`${prefix}${f} is required`);
    return;
  }
  if (typeof o[f] !== 'string' || !INSTANT.test(o[f])) p.push(`${prefix}${f} must be an ISO 8601 instant`);
}

function redact(body) {
  if (!body?.appleAds?.token) return body;
  return { ...body, appleAds: { ...body.appleAds, token: `<${body.appleAds.token.length} chars>` } };
}
function summary(b) {
  if (!b || typeof b !== 'object') return '';
  const source = b.appleAds ? `appleAds=${b.appleAds.status}` : b.playReferrer ? `playReferrer=${b.playReferrer.status}` : '';
  return [b.platform, b.appId, b.sdk ? `${b.sdk.name}/${b.sdk.version}` : '', source, b.appUserId ? 'appUserId=set' : ''].filter(Boolean).join(' ');
}
function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', (c) => chunks.push(c));
    req.on('end', () => resolve(Buffer.concat(chunks)));
    req.on('error', reject);
  });
}
function send(res, status, json) {
  res.writeHead(status, { 'content-type': 'application/json' });
  res.end(JSON.stringify(json));
}
function argValue(name) {
  const i = process.argv.indexOf(name);
  return i >= 0 ? process.argv[i + 1] : undefined;
}
