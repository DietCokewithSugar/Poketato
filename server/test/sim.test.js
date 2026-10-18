import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { createMatch } from '../src/match.js';
import { capFor, missionReward } from '../src/rewards.js';

const sql = readFileSync(new URL('../../supabase/migrations/20261018120000_extraction_backend.sql', import.meta.url), 'utf8');

test('mission rewards and party caps match the design', () => {
  assert.equal(missionReward('foggy-forest', 'extracted', 10), 90);
  assert.equal(missionReward('foggy-forest', 'extracted_early', 10), 50);
  assert.equal(missionReward('foggy-forest', 'died', 10), 0);
  assert.equal(missionReward('temporal-tower', 'extracted', 0), 400);
  assert.equal(missionReward('guild-basement', 'extracted', 0), 300);
  assert.equal(missionReward('northern-desert', 'extracted', 12), 12);
  assert.equal(capFor('extraction'), 4);
  assert.equal(capFor('boss'), 8);
  assert.equal(capFor('guild'), 8);
  assert.equal(capFor('world'), 20);
  assert.ok(sql.includes("('foggy-forest', '雾林小径', 'extraction', 4, 1, 8, 80)"));
  assert.ok(sql.includes("('temporal-tower', '时空之塔', 'boss', 8, 28, 35, 400)"));
  assert.ok(sql.includes("('guild-basement', '公会地下', 'guild', 8, 20, 30, 300)"));
  assert.ok(sql.includes("('northern-desert', '北方争霸', 'world', 20, 15, 40, 0)"));
  assert.ok(sql.includes('when p_outcome = \'extracted\' then m.bonus + greatest(coalesce(p_run_gold, 0), 0)'));
  assert.ok(sql.includes('when p_outcome = \'extracted_early\' then m.bonus / 2 + greatest(coalesce(p_run_gold, 0), 0)'));
});

test('ten ticks record fire-0 and fire-1, and their damage sums to the total', () => {
  const match = createMatch({
    mode: 'boss',
    seed: 2,
    durationMs: 1000,
    players: [{ id: 1, name: '喷火龙', species: 'charizard', dex: 6 }],
  });
  for (let i = 0; i < 10; i++) match.step();
  const player = match.final.players[0];
  const basic = player.skills.find((s) => s.skillId === 'fire-0');
  const opener = player.skills.find((s) => s.skillId === 'fire-1');
  assert.equal(player.outcome, 'failed');
  assert.equal(basic.hits, 2);
  assert.equal(basic.damage, 24);
  assert.equal(opener.hits, 1);
  assert.equal(opener.damage, 18);
  assert.equal(player.damage, basic.damage + opener.damage);
});

test('extraction keeps the run only after reaching the zone', () => {
  const match = createMatch({
    mode: 'extraction',
    seed: 3,
    durationMs: 2000,
    players: [{ id: 1, name: '喷火龙', species: 'charizard', dex: 6 }],
  });
  match.setInput(1, 1, 1);
  for (let i = 0; i < 4; i++) match.step();
  assert.equal(match.extract(1).ok, false);
  match.step();
  assert.equal(match.extract(1).ok, true);
  assert.equal(match.final.players[0].outcome, 'extracted');
  assert.ok(match.final.players[0].damage > 0);
});

test('dying outside the zone drops the run', () => {
  const match = createMatch({
    mode: 'extraction',
    seed: 4,
    durationMs: 10_000,
    players: [{ id: 1, name: '喷火龙', species: 'charizard', dex: 6, x: 0, y: 0 }],
  });
  let guard = 0;
  while (!match.ended && guard++ < 80) match.step();
  assert.equal(match.final.players[0].outcome, 'died');
  assert.ok(match.final.players[0].damage > 0);
});

test('world war keeps both sides and their skill damage', () => {
  const match = createMatch({
    mode: 'world',
    seed: 5,
    durationMs: 1000,
    players: [
      { id: 1, name: '喷火龙', species: 'charizard', dex: 6 },
      { id: 2, name: '水箭龟', species: 'blastoise', dex: 9 },
    ],
  });
  for (let i = 0; i < 10; i++) match.step();
  const [a, b] = match.final.players;
  assert.equal(a.damage, 42);
  assert.equal(b.damage, 36);
  assert.equal(a.outcome, 'extracted');
  assert.equal(b.outcome, 'extracted');
  assert.equal(a.skills.reduce((sum, s) => sum + s.damage, 0), a.damage);
  assert.equal(b.skills.find((s) => s.skillId === 'water-1').damage, 12);
});
