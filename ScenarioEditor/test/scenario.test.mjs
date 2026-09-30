// Shared model: templates, validation parity with CustomScenario.Validate, map checks, timing.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as S from '../web/scenario.js';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', 'Scenarios');
const map = JSON.parse(fs.readFileSync(path.join(root, 'maps', 'hikone-kyobashi', 'map.json'), 'utf8'));
const template = id => S.normalize(JSON.parse(fs.readFileSync(path.join(root, 'templates', id + '.json'), 'utf8')));
const templates = fs.readdirSync(path.join(root, 'templates')).filter(n => n.endsWith('.json')).map(n => n.replace('.json', ''));
const errors = s => S.validate(s, map).filter(p => p.level === 'error').map(p => p.message);

test('every built-in template is valid on the map', () => {
  assert.equal(templates.length, 10);
  for (const id of templates) assert.deepEqual(errors(template(id)), [], id);
});

test('07/08 cross the sidewalk at the parking exit: a warning, not an error', () => {
  const warnings = S.validate(template('builtin-07'), map).filter(p => p.level === 'warning').map(p => p.message);
  assert.ok(warnings.some(m => m.includes('歩道の上を通ります')), warnings.join('\n'));
});

test('structural errors use the same messages as Unity', () => {
  const s = template('builtin-01');
  s.name = ''; s.id = 'Bad Id'; s.vehicles[0].speedKmh = 200; s.vehicles[1].route = [s.vehicles[1].route[0]];
  const m = errors(s).join('\n');
  for (const part of ['name（場面の名前）がありません。', 'id は英小文字', 'speedKmh は 5〜80 です。', 'route には 2 点以上が必要です。'])
    assert.ok(m.includes(part), part);
  const csharp = fs.readFileSync(path.resolve(root, '..', 'Assets', '_Project', 'Scripts', 'Scenario', 'CustomScenario.cs'), 'utf8');
  for (const part of ['name（場面の名前）がありません。', 'speedKmh は 5〜80 です。', 'route には 2 点以上が必要です。',
    'trigger を使うには trigger エリアが必要です。', 'spawn と goal は 3 m 以上離してください。'])
    assert.ok(csharp.includes(part), 'C# has the same text: ' + part);
});

test('map checks: points in the moat or on buildings are errors', () => {
  const s = template('builtin-01');
  s.vehicles[2].route[3] = { x: 0, z: 55 };       // the moat north of Route 25
  assert.ok(errors(s).some(m => m.includes('点 4 が道路の上にありません')));
  s.spawn = { x: 0, z: 55 };
  assert.ok(errors(s).some(m => m.includes('出発点が道路・歩道の上にありません')));
});

test('serialize keeps Unity field order and centimetres; round trip is stable', () => {
  const s = template('builtin-01');
  s.spawn.x = 40.123456;
  const text = S.serialize(s);
  assert.match(text, /"x": 40.12,/);
  assert.equal(S.serialize(S.normalize(JSON.parse(text))), text);
  assert.deepEqual(Object.keys(JSON.parse(text)), ['format', 'version', 'id', 'name', 'nameEn', 'setting', 'learningGoal',
    'situation', 'point', 'map', 'playerMode', 'spawn', 'goal', 'trigger', 'vehicles']);
});

test('timing: accident car of 01 appears 6 s after the trigger and crosses the participant line', () => {
  const s = template('builtin-01');
  const accident = s.vehicles.find(v => v.accident);
  assert.deepEqual(S.spawnTimes(accident, 5), [11]);
  assert.deepEqual(S.spawnTimes(accident, null), []);
  const right = s.vehicles.find(v => v.stopOnTrigger);
  assert.ok(S.spawnTimes(right, 12).every(t => t < 12), 'stopped at the trigger');
  const c = S.conflictPoint(s, accident);
  assert.ok(c && Math.abs(c.x - 40) < 0.5, 'crosses x = 40 (the crosswalk)');
  assert.ok(S.carsAt(s, 11.5, 5).some(car => s.vehicles[car.index].accident));
});

test('acceleration matches CarController (8 m/s² to cruise speed)', () => {
  assert.equal(S.distanceAt(1, 10), 4);                      // 0.5 * 8 * 1²
  assert.ok(Math.abs(S.timeToDistance(S.distanceAt(3.7, 10), 10) - 3.7) < 1e-9);
});

test('rotated areas follow Unity yaw (clockwise from +z)', () => {
  const area = { x: 0, z: 0, width: 4, depth: 1, yaw: 90 };
  assert.ok(S.pointInArea([0, 1.9], area), 'width now runs along z');
  assert.ok(!S.pointInArea([1.9, 0], area));
});
