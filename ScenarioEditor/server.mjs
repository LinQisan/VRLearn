// Scenario editor server: serves the web editor, the exported map and the scenario files in
// ../Scenarios, and saves edits back there. No dependencies; `node server.mjs`.
//
//   GET    /api/map                    map.json of the Hikone map
//   GET    /api/scenarios              list of scenario files (+ validation summary)
//   GET    /api/scenario?path=…        one file (path relative to Scenarios/)
//   PUT    /api/scenarios/:id          save Scenarios/<id>.json (drafts with errors are allowed)
//   DELETE /api/scenarios/:id          move Scenarios/<id>.json to Scenarios/.trash/
//   POST   /api/play                   {path} → Scenarios/.play-request.json (Unity plays it)
//   GET    /api/play                   { pending } — whether Unity has not picked it up yet
//   GET    /api/quest                  adb path, app package, connected headsets
//   POST   /api/quest/sync             {serial?} → copy every valid scenario to the headset(s) (mirror)

import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { normalize, serialize, validate, ID_PATTERN, HIKONE_MAP } from './web/scenario.js';

const here = path.dirname(fileURLToPath(import.meta.url));
export const SCENARIOS = path.resolve(here, '..', 'Scenarios');
const WEB = path.join(here, 'web');
const MAP_DIR = path.join(SCENARIOS, 'maps', HIKONE_MAP);
const PLAY_REQUEST = path.join(SCENARIOS, '.play-request.json');
const PROJECT = path.resolve(here, '..');
const run = promisify(execFile);

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.png': 'image/png', '.svg': 'image/svg+xml'
};

let mapCache = null;
async function loadMap() {
  if (!mapCache) mapCache = JSON.parse(await fs.readFile(path.join(MAP_DIR, 'map.json'), 'utf8'));
  return mapCache;
}

/** Resolves a path inside a root folder; null for anything that escapes it. */
function inside(root, relative) {
  const full = path.resolve(root, relative);
  return full === root || full.startsWith(root + path.sep) ? full : null;
}

async function listScenarios() {
  const map = await loadMap().catch(() => null);
  const result = [];
  for (const [folder, template] of [['', false], ['templates', true]]) {
    const dir = path.join(SCENARIOS, folder);
    let names = [];
    try { names = (await fs.readdir(dir)).filter(n => n.endsWith('.json') && !n.startsWith('.') && !n.endsWith('.schema.json')); }
    catch { continue; }
    for (const name of names.sort()) {
      const rel = folder ? `${folder}/${name}` : name;
      const entry = { path: rel, template };
      try {
        const scenario = normalize(JSON.parse(await fs.readFile(path.join(dir, name), 'utf8')));
        const problems = validate(scenario, map);
        Object.assign(entry, {
          id: scenario.id, name: scenario.name, playerMode: scenario.playerMode, setting: scenario.setting,
          errors: problems.filter(p => p.level === 'error').length,
          warnings: problems.filter(p => p.level === 'warning').length
        });
      } catch (error) {
        Object.assign(entry, { id: name.replace(/\.json$/, ''), name: '(読めません)', errors: 1, warnings: 0, unreadable: String(error.message) });
      }
      result.push(entry);
    }
  }
  return result;
}

async function readBody(req) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > 2_000_000) throw Object.assign(new Error('body too large'), { status: 413 });
    chunks.push(chunk);
  }
  return Buffer.concat(chunks).toString('utf8');
}

function send(res, status, body, type = 'application/json; charset=utf-8') {
  res.writeHead(status, { 'Content-Type': type, 'Cache-Control': 'no-store' });
  res.end(typeof body === 'string' || Buffer.isBuffer(body) ? body : JSON.stringify(body));
}

async function serveFile(res, file) {
  try {
    send(res, 200, await fs.readFile(file), TYPES[path.extname(file)] ?? 'application/octet-stream');
  } catch {
    send(res, 404, { error: 'not found' });
  }
}


// ------------------------------------------------------------------ Quest (adb)

/** adb: $ADB, then PATH, the Android SDK, then the adb bundled with any installed Unity editor. */
export async function findAdb() {
  if (process.env.ADB) return process.env.ADB;
  const exe = process.platform === 'win32' ? 'adb.exe' : 'adb';
  try { await run(exe, ['version']); return exe; } catch { /* not on PATH */ }
  const home = process.env.HOME ?? process.env.USERPROFILE ?? '';
  const candidates = [path.join(home, 'Library', 'Android', 'sdk', 'platform-tools', exe),
    path.join(home, 'AppData', 'Local', 'Android', 'Sdk', 'platform-tools', exe)];
  for (const hub of ['/Applications/Unity/Hub/Editor', 'C:\\Program Files\\Unity\\Hub\\Editor']) {
    const versions = await fs.readdir(hub).catch(() => []);
    for (const v of versions.sort().reverse()) {
      candidates.push(path.join(hub, v, 'PlaybackEngines', 'AndroidPlayer', 'SDK', 'platform-tools', exe));
      candidates.push(path.join(hub, v, 'Editor', 'Data', 'PlaybackEngines', 'AndroidPlayer', 'SDK', 'platform-tools', exe));
    }
  }
  for (const c of candidates) if (await fs.access(c).then(() => true, () => false)) return c;
  return null;
}

/** Android package id of the app (ProjectSettings), e.g. com.moxuanxuerain.vrlearn. */
export async function packageId() {
  const text = await fs.readFile(path.join(PROJECT, 'ProjectSettings', 'ProjectSettings.asset'), 'utf8');
  return text.match(/applicationIdentifier:\s*\n(?:\s+\S+:.*\n)*?\s+Android:\s*(\S+)/)?.[1] ?? null;
}

async function adb(adbPath, args) {
  const { stdout } = await run(adbPath, args, { timeout: 20000 });
  return stdout;
}

export async function listDevices(adbPath) {
  const out = await adb(adbPath, ['devices', '-l']);
  return out.split('\n').slice(1).map(l => l.trim()).filter(Boolean).map(line => {
    const [serial, state, ...rest] = line.split(/\s+/);
    const model = rest.find(r => r.startsWith('model:'))?.slice(6).replace(/_/g, ' ') ?? '';
    return { serial, state, model };
  });
}

async function questStatus() {
  const adbPath = await findAdb();
  const pkg = await packageId().catch(() => null);
  if (!adbPath) return { adb: null, package: pkg, devices: [], error: 'adb が見つかりません（Unity の Android サポートか Android SDK を入れてください）。' };
  try {
    return { adb: adbPath, package: pkg, devices: await listDevices(adbPath) };
  } catch (error) {
    return { adb: adbPath, package: pkg, devices: [], error: 'adb を実行できません: ' + error.message };
  }
}

/**
 * Makes the headset's scenario folder match the PC: pushes every valid scenario in Scenarios/
 * (templates excluded) and removes headset files that no longer exist on the PC.
 */
async function syncQuest(serial) {
  const status = await questStatus();
  if (!status.adb) throw Object.assign(new Error(status.error), { status: 409 });
  if (!status.package) throw Object.assign(new Error('アプリの package id を ProjectSettings から読めません。'), { status: 500 });
  const targets = status.devices.filter(d => d.state === 'device' && (!serial || d.serial === serial));
  if (!targets.length) throw Object.assign(new Error('接続されている Quest がありません（USB かワイヤレス adb で接続し、ヘッドセットで USB デバッグを許可してください）。'), { status: 409 });

  const list = (await listScenarios()).filter(e => !e.template);
  const valid = list.filter(e => e.errors === 0);
  const skipped = list.filter(e => e.errors > 0).map(e => ({ id: e.id, errors: e.errors }));
  const remote = `/sdcard/Android/data/${status.package}/files/Scenarios`;
  const results = [];
  for (const device of targets) {
    const result = { serial: device.serial, model: device.model, pushed: [], removed: [], error: null };
    try {
      const installed = await adb(status.adb, ['-s', device.serial, 'shell', 'pm', 'path', status.package]);
      if (!installed.trim()) throw new Error(`アプリ（${status.package}）がこの Quest に入っていません。`);
      await adb(status.adb, ['-s', device.serial, 'shell', 'mkdir', '-p', remote]);
      const existing = (await adb(status.adb, ['-s', device.serial, 'shell', 'ls', remote]).catch(() => ''))
        .split(/\s+/).filter(n => /^[a-z0-9][a-z0-9_-]{0,39}\.json$/.test(n));
      for (const e of valid) {
        await adb(status.adb, ['-s', device.serial, 'push', path.join(SCENARIOS, e.path), `${remote}/${e.path}`]);
        result.pushed.push(e.id);
      }
      const keep = new Set(valid.map(e => e.path));
      for (const name of existing.filter(n => !keep.has(n))) {
        await adb(status.adb, ['-s', device.serial, 'shell', 'rm', '-f', `${remote}/${name}`]);
        result.removed.push(name.replace(/\.json$/, ''));
      }
    } catch (error) {
      result.error = String(error.message ?? error);
    }
    results.push(result);
  }
  return { remote, skipped, results };
}

export async function handle(req, res) {
  const url = new URL(req.url, 'http://localhost');
  const p = decodeURIComponent(url.pathname);
  try {
    if (p === '/api/map' && req.method === 'GET') return send(res, 200, await loadMap());
    if (p === '/api/scenarios' && req.method === 'GET') return send(res, 200, await listScenarios());
    if (p === '/api/scenario' && req.method === 'GET') {
      const file = inside(SCENARIOS, url.searchParams.get('path') ?? '');
      if (!file || !file.endsWith('.json')) return send(res, 400, { error: 'bad path' });
      return serveFile(res, file);
    }
    const match = p.match(/^\/api\/scenarios\/([^/]+)$/);
    if (match && req.method === 'PUT') {
      const id = match[1];
      if (!ID_PATTERN.test(id)) return send(res, 400, { error: 'id は英小文字・数字・-・_ の 1〜40 文字にしてください。' });
      if (id.startsWith('builtin-')) return send(res, 400, { error: '"builtin-" で始まる id はひな形用です。別の id にしてください。' });
      const scenario = normalize(JSON.parse(await readBody(req)));
      if (scenario.id !== id) return send(res, 400, { error: 'id がファイル名と一致しません。' });
      const text = serialize(scenario);
      await fs.writeFile(path.join(SCENARIOS, `${id}.json`), text, 'utf8');
      const problems = validate(scenario, await loadMap().catch(() => null));
      return send(res, 200, { path: `${id}.json`, problems });
    }
    if (match && req.method === 'DELETE') {
      const id = match[1];
      if (!ID_PATTERN.test(id)) return send(res, 400, { error: 'bad id' });
      const trash = path.join(SCENARIOS, '.trash');
      await fs.mkdir(trash, { recursive: true });
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      await fs.rename(path.join(SCENARIOS, `${id}.json`), path.join(trash, `${id}-${stamp}.json`));
      return send(res, 200, { trashed: true });
    }
    if (p === '/api/play' && req.method === 'POST') {
      const { path: rel } = JSON.parse(await readBody(req));
      const file = inside(SCENARIOS, rel ?? '');
      if (!file || !file.endsWith('.json')) return send(res, 400, { error: 'bad path' });
      await fs.access(file);
      await fs.writeFile(PLAY_REQUEST, JSON.stringify({ path: rel, requestedAt: new Date().toISOString() }), 'utf8');
      return send(res, 200, { requested: rel });
    }
    if (p === '/api/play' && req.method === 'GET') {
      const pending = await fs.access(PLAY_REQUEST).then(() => true, () => false);
      return send(res, 200, { pending });
    }
    if (p === '/api/quest' && req.method === 'GET') return send(res, 200, await questStatus());
    if (p === '/api/quest/sync' && req.method === 'POST') {
      const body = await readBody(req);
      const { serial } = body ? JSON.parse(body) : {};
      return send(res, 200, await syncQuest(serial));
    }
    if (p.startsWith('/maps/')) {
      const file = inside(path.join(SCENARIOS, 'maps'), p.slice('/maps/'.length));
      return file ? serveFile(res, file) : send(res, 400, { error: 'bad path' });
    }
    if (req.method === 'GET') {
      const file = inside(WEB, p === '/' ? 'index.html' : p.slice(1));
      return file ? serveFile(res, file) : send(res, 400, { error: 'bad path' });
    }
    send(res, 405, { error: 'method not allowed' });
  } catch (error) {
    send(res, error.status ?? 500, { error: String(error.message ?? error) });
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const port = Number(process.env.PORT ?? 8765);
  const host = process.env.HOST ?? '127.0.0.1';
  http.createServer(handle).listen(port, host, () => {
    console.log(`シナリオエディタ: http://${host === '0.0.0.0' ? 'localhost' : host}:${port}/`);
    console.log(`場面ファイル: ${SCENARIOS}`);
  });
}
