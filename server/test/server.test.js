import assert from 'node:assert/strict';
import test from 'node:test';
import WebSocket from 'ws';
import { createGameServer } from '../src/server.js';
import { isServiceCredential } from '../src/supabase.js';

const USER = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CHARACTER = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

function fakeJwt(role) {
  const payload = Buffer.from(JSON.stringify({ role, sub: USER })).toString('base64url');
  return `eyJhbGciOiJub25lIn0.${payload}.sig`;
}

function track(ws) {
  const pending = [];
  const waiters = [];
  ws.on('message', (buf) => {
    const msg = JSON.parse(buf.toString());
    if (waiters.length) waiters.shift()(msg);
    else pending.push(msg);
  });
  return {
    next(timeout = 2000) {
      if (pending.length) return Promise.resolve(pending.shift());
      return new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('timeout')), timeout);
        waiters.push((msg) => {
          clearTimeout(timer);
          resolve(msg);
        });
      });
    },
    async until(type, timeout = 2000) {
      const deadline = Date.now() + timeout;
      while (Date.now() < deadline) {
        const msg = await this.next(Math.max(1, deadline - Date.now()));
        if (msg.t === 'error') throw new Error(msg.msg);
        if (msg.t === type) return msg;
      }
      throw new Error(`timeout waiting for ${type}`);
    },
  };
}

function connect(port) {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const box = track(ws);
  return new Promise((resolve, reject) => {
    ws.once('open', () => resolve({ ws, ...box }));
    ws.once('error', reject);
  });
}

test('service credentials are recognized and never look like the anon key', () => {
  assert.equal(isServiceCredential('sb_secret_abc', 'other'), true);
  assert.equal(isServiceCredential(`Bearer ${fakeJwt('service_role')}`, 'other'), true);
  assert.equal(isServiceCredential(fakeJwt('anon'), 'other'), false);
  assert.equal(isServiceCredential('eyJhbGciOiJub25lIn0.not-json.sig', 'other'), false);
});

test('health, public config and proxy hide the service role', async () => {
  const calls = [];
  const supabase = {
    url: 'https://example.supabase.co',
    anonKey: 'anon-public-key',
    serviceKey: 'service-secret-key',
  };
  const server = createGameServer({
    supabase,
    fetch: async (url, init) => {
      calls.push({ url, init });
      return new Response(JSON.stringify({ ok: true }), { status: 200, headers: { 'content-type': 'application/json' } });
    },
  });
  const address = await server.listen();
  try {
    const health = await fetch(`http://127.0.0.1:${address.port}/health`);
    assert.equal(health.status, 200);
    const healthBody = await health.json();
    assert.equal(healthBody.ok, true);
    assert.equal(healthBody.supabase, true);

    const config = await fetch(`http://127.0.0.1:${address.port}/api/public-config`);
    const configBody = await config.json();
    assert.equal(configBody.anonKey, 'anon-public-key');
    assert.equal(configBody.supabaseUrl, `http://127.0.0.1:${address.port}/supabase`);
    assert.equal(configBody.wsUrl, `ws://127.0.0.1:${address.port}/ws`);
    assert.equal(JSON.stringify(configBody).includes('service-secret-key'), false);

    const blocked = await fetch(`http://127.0.0.1:${address.port}/supabase/rest/v1/rpc/record_match_result`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', apikey: 'anon-public-key' },
      body: '{}',
    });
    assert.equal(blocked.status, 403);

    const stolen = await fetch(`http://127.0.0.1:${address.port}/supabase/rest/v1/characters`, {
      headers: { apikey: 'service-secret-key', authorization: 'Bearer service-secret-key' },
    });
    assert.equal(stolen.status, 403);
    assert.equal(calls.length, 0);

    const forwarded = await fetch(`http://127.0.0.1:${address.port}/supabase/auth/v1/token?grant_type=password`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        authorization: 'Bearer user-token',
        apikey: 'client-tried-something-else',
      },
      body: JSON.stringify({ email: 'a@b.c', password: 'secret' }),
    });
    assert.equal(forwarded.status, 200);
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, 'https://example.supabase.co/auth/v1/token?grant_type=password');
    assert.equal(calls[0].init.headers.apikey, 'anon-public-key');
    assert.equal(calls[0].init.headers.authorization, 'Bearer user-token');
    assert.equal(JSON.stringify(calls[0].init.headers).includes('service-secret-key'), false);
  } finally {
    await server.close();
  }
});

test('four players fill a raid and the fifth waits', async () => {
  const server = createGameServer({
    matchDurationMs: 5000,
    queueWaitMs: 5000,
    disconnectGraceMs: 0,
    requireAuth: false,
  });
  const address = await server.listen();
  const sockets = [];
  try {
    const players = [];
    for (let i = 0; i < 4; i++) players.push(await connect(address.port));
    sockets.push(...players);
    await Promise.all(players.map((p) => p.until('hello')));
    for (const p of players) {
      p.ws.send(JSON.stringify({ t: 'queue', mode: 'extraction', species: 'charmander', name: '红' }));
    }
    const starts = await Promise.all(players.map((p) => p.until('start')));
    assert.equal(new Set(starts.map((s) => s.matchId)).size, 1);
    assert.equal(starts[0].players.length, 4);
    assert.equal(starts[0].mode, 'extraction');

    const fifth = await connect(address.port);
    sockets.push(fifth);
    await fifth.until('hello');
    fifth.ws.send(JSON.stringify({ t: 'queue', mode: 'extraction', species: 'pikachu', name: '黄' }));
    await fifth.until('queued');
    await assert.rejects(() => fifth.until('start', 250), /timeout/);
  } finally {
    await server.close();
    for (const s of sockets) s.ws.terminate();
  }
});

test('a logged-in match writes per-skill damage with the service role', async () => {
  const calls = [];
  const server = createGameServer({
    matchDurationMs: 600,
    queueWaitMs: 20,
    disconnectGraceMs: 0,
    supabase: {
      url: 'https://example.supabase.co',
      anonKey: 'anon-public-key',
      serviceKey: 'service-secret-key',
    },
    fetch: async (url, init) => {
      calls.push({ url, init });
      if (url.endsWith('/auth/v1/user')) {
        return new Response(JSON.stringify({ id: USER, email: 'a@example.com' }), { status: 200 });
      }
      if (url.includes('/rest/v1/characters')) {
        return new Response(JSON.stringify([{ id: CHARACTER, dex: 6, level: 30 }]), { status: 200 });
      }
      if (url.includes('/rest/v1/species')) {
        return new Response(JSON.stringify([{ slug: 'charizard', name: '喷火龙', types: ['fire', 'flying'] }]), { status: 200 });
      }
      if (url.includes('/rest/v1/guild_members')) {
        return new Response(JSON.stringify([]), { status: 200 });
      }
      if (url.endsWith('/rest/v1/rpc/settle_run')) {
        return new Response(JSON.stringify(CHARACTER), { status: 200 });
      }
      return new Response(JSON.stringify({ message: 'unexpected ' + url }), { status: 500 });
    },
  });
  const address = await server.listen();
  let socket;
  try {
    socket = await connect(address.port);
    await socket.until('hello');
    socket.ws.send(JSON.stringify({
      t: 'queue',
      mode: 'boss',
      missionId: 'temporal-tower',
      token: 'user-token',
      characterId: CHARACTER,
    }));
    const end = await socket.until('end', 3000);
    assert.equal(end.persisted, true);
    assert.equal(end.mode, 'boss');
    const basic = end.players[0].skills.find((s) => s.skillId === 'fire-0');
    assert.ok(basic.damage > 0);
    assert.equal(end.players[0].species, 'charizard');
    const rpc = calls.find((c) => c.url.endsWith('/rpc/settle_run'));
    const body = JSON.parse(rpc.init.body);
    assert.equal(body.payload.mode, 'boss');
    assert.equal(body.payload.mission_id, 'temporal-tower');
    assert.equal(body.payload.report.kind, 'boss');
    assert.equal(body.payload.players[0].user_id, USER);
    assert.equal(body.payload.players[0].character_id, CHARACTER);
    const reported = body.payload.report.participants[0];
    assert.equal(reported.skills[0].skillId, 'fire-0');
    assert.equal(reported.damage, reported.skills.reduce((sum, s) => sum + s.damage, 0));
    assert.equal(rpc.init.headers.apikey, 'service-secret-key');
    assert.equal(JSON.stringify(rpc.init.headers).includes('anon-public-key'), false);
  } finally {
    await server.close();
    socket?.ws.terminate();
  }
});
