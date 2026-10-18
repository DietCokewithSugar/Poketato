import http from 'node:http';
import { randomUUID } from 'node:crypto';
import { WebSocketServer } from 'ws';
import { MODES, TICK_MS, createMatch } from './match.js';
import { buildSettle } from './payload.js';
import { MISSIONS, capFor as modeCap } from './rewards.js';
import {
  BLOCKED_RPC,
  createSupabase,
  isServiceCredential,
  isUuid,
} from './supabase.js';

const SPECIES = /^[a-z0-9_]{1,32}$/;

export function cleanName(value) {
  const text = String(value || '').replace(/[\u0000-\u001f]/g, '').trim().slice(0, 16);
  return text || '训练家';
}

function allowOrigin(req) {
  const configured = (process.env.WEBGL_ORIGIN || '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean);
  if (configured.length === 0 || configured.includes('*')) return '*';
  const origin = req.headers.origin;
  if (origin && configured.includes(origin)) return origin;
  return configured[0];
}

function publicBase(req) {
  const configured = process.env.PUBLIC_URL?.trim().replace(/\/+$/, '');
  if (configured) return configured;
  const proto = String(req.headers['x-forwarded-proto'] || 'http').split(',')[0].trim();
  const host = String(req.headers['x-forwarded-host'] || req.headers.host || 'localhost');
  return `${proto}://${host}`;
}

function toWs(httpUrl) {
  const base = httpUrl.replace(/\/+$/, '');
  if (base.startsWith('https://')) return `wss://${base.slice('https://'.length)}/ws`;
  if (base.startsWith('http://')) return `ws://${base.slice('http://'.length)}/ws`;
  return `${base}/ws`;
}

function send(client, msg) {
  if (client.ws.readyState === 1) client.ws.send(JSON.stringify(msg));
}

function readBody(req, limit) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;
    req.on('data', (chunk) => {
      size += chunk.length;
      if (size > limit) {
        reject(Object.assign(new Error('too large'), { status: 413 }));
        req.destroy();
        return;
      }
      chunks.push(chunk);
    });
    req.on('end', () => resolve(Buffer.concat(chunks)));
    req.on('error', reject);
  });
}

export function createGameServer(opts = {}) {
  const host = opts.host || '127.0.0.1';
  const port = opts.port ?? 0;
  const fetchImpl = opts.fetch || fetch;
  const matchDurationMs = opts.matchDurationMs ?? 180_000;
  const queueWaitMs = opts.queueWaitMs ?? 20_000;
  const disconnectGraceMs = opts.disconnectGraceMs ?? 60_000;
  const supabase = opts.supabase ? createSupabase(opts.supabase, fetchImpl) : null;
  const requireAuth = opts.requireAuth ?? Boolean(supabase);

  const clients = new Set();
  const rooms = new Set();
  const queues = new Map();
  const graceTimers = new Set();
  let closing = false;
  let seq = 0;

  const defaultMission = {
    extraction: 'foggy-forest',
    boss: 'temporal-tower',
    guild: 'guild-basement',
    world: 'northern-desert',
  };

  function queueOf(key, mode) {
    let bucket = queues.get(key);
    if (!bucket) {
      bucket = { key, mode, players: [], timer: null };
      queues.set(key, bucket);
    }
    return bucket;
  }

  function capFor(mode, missionId) {
    return MISSIONS[missionId]?.cap || modeCap(mode);
  }

  function scheduleDeath(room, playerId) {
    if (closing || room.finished) return;
    const drop = () => {
      if (room.finished) return;
      room.sim.disconnect(playerId);
      if (room.sim.ended) void finishRoom(room);
    };
    if (disconnectGraceMs <= 0) {
      drop();
      return;
    }
    const timer = setTimeout(() => {
      graceTimers.delete(timer);
      drop();
    }, disconnectGraceMs);
    graceTimers.add(timer);
  }

  function removeQueued(client) {
    for (const bucket of queues.values()) {
      const before = bucket.players.length;
      bucket.players = bucket.players.filter((p) => p !== client);
      if (before !== bucket.players.length && bucket.players.length === 0 && bucket.timer) {
        clearTimeout(bucket.timer);
        bucket.timer = null;
      }
    }
    client.queued = false;
  }

  function broadcast(room, msg) {
    const data = JSON.stringify(msg);
    for (const member of room.members) {
      if (member.ws.readyState === 1) member.ws.send(data);
    }
  }

  async function finishRoom(room) {
    if (room.finished) return;
    room.finished = true;
    clearInterval(room.timer);
    rooms.delete(room);
    for (const member of room.members) {
      member.room = null;
      member.queued = false;
    }
    const result = room.sim.final;
    if (!result) return;
    const endedAt = new Date().toISOString();
    let persisted = false;
    let persistError = null;
    const canSave = supabase && result.players.length > 0
      && result.players.every((p) => isUuid(p.userId) && isUuid(p.characterId));
    if (canSave) {
      try {
        await supabase.settleRun(buildSettle({
          id: room.id,
          mode: room.mode,
          seed: room.seed,
          missionId: room.missionId,
          guildId: room.guildId,
          startedAt: room.startedAt,
          endedAt,
          durationMs: Math.max(1, Date.now() - Date.parse(room.startedAt)),
        }, result));
        persisted = true;
      } catch (err) {
        persistError = err.message || String(err);
        console.error('[poketato] settle_run failed:', persistError);
      }
    }
    broadcast(room, {
      t: 'end',
      matchId: room.id,
      mode: room.mode,
      missionId: room.missionId,
      reason: result.reason,
      persisted,
      persistError,
      players: result.players.map((p) => ({
        id: p.id,
        name: p.name,
        species: p.species,
        dex: p.dex,
        characterId: p.characterId,
        outcome: p.outcome,
        damage: p.damage,
        taken: p.taken,
        skills: p.skills,
      })),
    });
  }

  function begin(mode, members) {
    const seed = Math.floor(Math.random() * 1_000_000_000) + 1;
    const room = {
      id: randomUUID(),
      mode,
      seed,
      missionId: members[0].missionId,
      guildId: mode === 'guild' ? (members[0].guildId || null) : null,
      startedAt: new Date().toISOString(),
      members,
      finished: false,
      timer: null,
      sim: createMatch({
        mode,
        seed,
        durationMs: matchDurationMs,
        players: members.map((member) => ({
          id: member.id,
          name: member.name,
          species: member.species,
          dex: member.dex,
          userId: member.userId,
          characterId: member.characterId,
        })),
      }),
    };
    for (const member of members) {
      member.queued = false;
      member.room = room;
    }
    rooms.add(room);
    broadcast(room, {
      t: 'start',
      matchId: room.id,
      mode,
      seed: room.seed,
      durationMs: matchDurationMs,
      tickMs: TICK_MS,
      selfId: null,
      players: members.map((member) => ({
        id: member.id,
        name: member.name,
        species: member.species,
        characterId: member.characterId,
      })),
    });
    for (const member of members) {
      send(member, { t: 'self', id: member.id });
    }
    room.timer = setInterval(() => {
      if (room.finished) return;
      room.sim.step();
      if (!room.sim.ended) {
        const state = room.sim.state();
        broadcast(room, { t: 'snap', matchId: room.id, ...state });
        return;
      }
      void finishRoom(room);
    }, TICK_MS);
  }

  function tryStart(bucket) {
    const needsOfficer = bucket.mode === 'guild' && Boolean(supabase);
    const hasOfficer = bucket.players.some((p) => p.guildRole === 'leader' || p.guildRole === 'officer');
    if (needsOfficer && !hasOfficer) return;
    const cap = capFor(bucket.mode, bucket.players[0]?.missionId);
    while (bucket.players.length >= cap) {
      const group = bucket.players.splice(0, cap);
      if (bucket.players.length === 0 && bucket.timer) {
        clearTimeout(bucket.timer);
        bucket.timer = null;
      }
      begin(bucket.mode, group);
    }
    if (bucket.players.length > 0 && !bucket.timer) {
      bucket.timer = setTimeout(() => {
        bucket.timer = null;
        if (bucket.players.length === 0) return;
        const group = bucket.players.splice(0, cap);
        begin(bucket.mode, group);
        if (bucket.players.length > 0) tryStart(bucket);
      }, queueWaitMs);
    }
  }

  async function onQueue(client, msg) {
    if (client.closed || client.room || client.queued) {
      send(client, { t: 'error', msg: '已经在队列或对局中' });
      return;
    }
    if (!MODES.includes(msg.mode)) {
      send(client, { t: 'error', msg: '模式无效' });
      return;
    }
    const missionId = typeof msg.missionId === 'string' && MISSIONS[msg.missionId]?.mode === msg.mode
      ? msg.missionId
      : defaultMission[msg.mode];
    if (!missionId) {
      send(client, { t: 'error', msg: '任务无效' });
      return;
    }
    let species = typeof msg.species === 'string' && SPECIES.test(msg.species) ? msg.species : 'charizard';
    let dex = 6;
    let name = cleanName(msg.name);
    let userId = null;
    let characterId = null;
    let guildId = null;
    let guildRole = null;
    if (supabase) {
      if (!msg.token || !isUuid(msg.characterId)) {
        send(client, { t: 'error', msg: '请先登录' });
        return;
      }
      const found = await supabase.characterOf(msg.token, msg.characterId);
      if (client.closed) return;
      if (!found) {
        send(client, { t: 'error', msg: '角色不存在' });
        return;
      }
      userId = found.userId;
      characterId = found.characterId;
      species = found.species;
      dex = found.dex;
      guildId = found.guildId;
      guildRole = found.guildRole;
      name = found.name || name;
    } else if (requireAuth) {
      send(client, { t: 'error', msg: '服务器还没有配置 Supabase' });
      return;
    }
    if (msg.mode === 'guild' && supabase && !guildId) {
      send(client, { t: 'error', msg: '需要加入战队' });
      return;
    }
    client.queued = true;
    client.userId = userId;
    client.characterId = characterId;
    client.guildId = guildId;
    client.guildRole = guildRole;
    client.species = species;
    client.dex = dex;
    client.missionId = missionId;
    client.name = name;
    const key = `${msg.mode}:${missionId}:${guildId || ''}`;
    const bucket = queueOf(key, msg.mode);
    bucket.players.push(client);
    send(client, { t: 'queued', mode: msg.mode, waiting: bucket.players.length });
    tryStart(bucket);
  }

  function onMessage(client, raw) {
    if (client.budget <= 0) return;
    client.budget -= 1;
    let msg;
    try { msg = JSON.parse(raw.toString()); } catch { return; }
    if (!msg || typeof msg !== 'object') return;
    if (msg.t === 'queue') {
      void onQueue(client, msg);
      return;
    }
    if (msg.t === 'input' && client.room && !client.room.finished) {
      const move = Array.isArray(msg.move) ? msg.move : null;
      const x = move ? move[0] : msg.x;
      const y = move ? move[1] : msg.y;
      client.room.sim.setInput(client.id, x, y);
      if (msg.interact) {
        const result = client.room.sim.extract(client.id);
        if (!result.ok) send(client, { t: 'error', msg: result.msg });
        if (client.room.sim.ended) void finishRoom(client.room);
      }
      return;
    }
    if (msg.t === 'extract' && client.room && !client.room.finished) {
      const result = client.room.sim.extract(client.id);
      if (!result.ok) send(client, { t: 'error', msg: result.msg });
      if (client.room.sim.ended) void finishRoom(client.room);
      return;
    }
    if (msg.t === 'leave') {
      removeQueued(client);
      if (client.room && !client.room.finished) scheduleDeath(client.room, client.id);
    }
  }

  async function proxySupabase(req, res, url) {
    if (!supabase) {
      res.writeHead(503, { 'content-type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ message: 'supabase not configured' }));
      return;
    }
    if (!['GET', 'POST', 'PATCH', 'DELETE', 'HEAD'].includes(req.method || '')) {
      res.writeHead(405).end();
      return;
    }
    const path = url.pathname.replace(/^\/supabase/, '');
    if (path.includes('..') || (!path.startsWith('/auth/v1/') && !path.startsWith('/rest/v1/'))) {
      res.writeHead(404).end();
      return;
    }
    const rpc = path.match(/^\/rest\/v1\/rpc\/([a-z0-9_]+)$/);
    if (rpc && BLOCKED_RPC.has(rpc[1])) {
      res.writeHead(403, { 'content-type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ message: 'forbidden' }));
      return;
    }
    const apikey = req.headers.apikey || '';
    const authorization = req.headers.authorization || '';
    if (isServiceCredential(apikey, supabase.cfg.serviceKey) || isServiceCredential(authorization, supabase.cfg.serviceKey)) {
      res.writeHead(403, { 'content-type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ message: 'forbidden' }));
      return;
    }
    let body;
    try {
      body = await readBody(req, 256 * 1024);
    } catch (err) {
      res.writeHead(err.status || 400).end();
      return;
    }
    const headers = { apikey: supabase.cfg.anonKey, accept: 'application/json' };
    if (req.headers['content-type']) headers['content-type'] = req.headers['content-type'];
    if (req.headers.prefer) headers.prefer = req.headers.prefer;
    if (authorization) headers.authorization = authorization;
    const upstream = await fetchImpl(`${supabase.cfg.url}${path}${url.search}`, {
      method: req.method,
      headers,
      body: req.method === 'GET' || req.method === 'HEAD' ? undefined : body,
      signal: AbortSignal.timeout(15_000),
    });
    const buf = Buffer.from(await upstream.arrayBuffer());
    if (buf.length > 2_000_000) {
      res.writeHead(502).end();
      return;
    }
    const out = { 'content-type': upstream.headers.get('content-type') || 'application/json' };
    res.writeHead(upstream.status, out);
    res.end(buf);
  }

  const httpServer = http.createServer(async (req, res) => {
    const url = new URL(req.url || '/', 'http://localhost');
    const origin = allowOrigin(req);
    res.setHeader('access-control-allow-origin', origin);
    res.setHeader('access-control-allow-methods', 'GET, POST, PATCH, DELETE, OPTIONS');
    res.setHeader('access-control-allow-headers', 'authorization, apikey, content-type, prefer, accept');
    res.setHeader('access-control-max-age', '600');
    if (origin !== '*') res.setHeader('vary', 'Origin');
    if (req.method === 'OPTIONS') {
      res.writeHead(204).end();
      return;
    }
    try {
      if (url.pathname === '/health' || url.pathname === '/healthz') {
        let queued = 0;
        for (const bucket of queues.values()) queued += bucket.players.length;
        res.writeHead(200, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
        res.end(JSON.stringify({
          ok: true,
          supabase: Boolean(supabase),
          online: clients.size,
          matches: rooms.size,
          queued,
        }));
        return;
      }
      if (url.pathname === '/api/public-config') {
        const base = publicBase(req);
        const body = {
          supabaseUrl: supabase ? `${base}/supabase` : null,
          anonKey: supabase ? supabase.cfg.anonKey : null,
          wsUrl: toWs(base),
          modes: MODES,
        };
        const json = JSON.stringify(body);
        if (supabase && json.includes(supabase.cfg.serviceKey)) {
          res.writeHead(500).end();
          return;
        }
        res.writeHead(200, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
        res.end(json);
        return;
      }
      if (url.pathname.startsWith('/supabase/')) {
        await proxySupabase(req, res, url);
        return;
      }
      res.writeHead(404, { 'content-type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ message: 'not found' }));
    } catch (err) {
      console.error('[poketato] http', err);
      if (!res.headersSent) res.writeHead(500, { 'content-type': 'application/json; charset=utf-8' });
      res.end(JSON.stringify({ message: 'server error' }));
    }
  });

  const wss = new WebSocketServer({ noServer: true, maxPayload: 16 * 1024 });
  httpServer.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url || '/', 'http://localhost');
    if (url.pathname !== '/ws') {
      socket.destroy();
      return;
    }
    wss.handleUpgrade(req, socket, head, (ws) => wss.emit('connection', ws, req));
  });

  wss.on('connection', (ws) => {
    const client = {
      id: ++seq,
      ws,
      room: null,
      queued: false,
      closed: false,
      budget: 200,
      name: '训练家',
      species: 'charizard',
      dex: 6,
      missionId: null,
      userId: null,
      characterId: null,
      guildId: null,
      guildRole: null,
    };
    clients.add(client);
    send(client, { t: 'hello', id: client.id, modes: MODES });
    ws.on('message', (raw, isBinary) => {
      if (isBinary) return;
      onMessage(client, raw);
    });
    ws.on('close', () => {
      client.closed = true;
      clients.delete(client);
      removeQueued(client);
      if (client.room && !client.room.finished) scheduleDeath(client.room, client.id);
    });
    ws.on('error', () => {});
  });

  const budgetTimer = setInterval(() => {
    for (const client of clients) client.budget = 200;
  }, 1000);
  budgetTimer.unref?.();

  return {
    http: httpServer,
    supabase,
    listen() {
      return new Promise((resolve) => {
        httpServer.listen(port, host, () => resolve(httpServer.address()));
      });
    },
    close() {
      closing = true;
      clearInterval(budgetTimer);
      for (const timer of graceTimers) clearTimeout(timer);
      graceTimers.clear();
      for (const bucket of queues.values()) if (bucket.timer) clearTimeout(bucket.timer);
      for (const room of rooms) clearInterval(room.timer);
      for (const client of clients) {
        try { client.ws.terminate(); } catch { /* already gone */ }
      }
      wss.close();
      return new Promise((resolve) => httpServer.close(() => resolve()));
    },
  };
}
