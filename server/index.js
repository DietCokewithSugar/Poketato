import { configFromEnv } from './src/supabase.js';
import { createGameServer } from './src/server.js';

const cfg = configFromEnv();
const requireAuth = process.env.REQUIRE_AUTH === '1'
  ? true
  : process.env.REQUIRE_AUTH === '0'
    ? false
    : Boolean(cfg);

const server = createGameServer({
  host: process.env.HOST || '0.0.0.0',
  port: Number(process.env.PORT || 8787),
  supabase: cfg,
  requireAuth,
  matchDurationMs: Number(process.env.MATCH_DURATION_MS || 180_000),
  queueWaitMs: Number(process.env.QUEUE_WAIT_MS || 20_000),
  disconnectGraceMs: Number(process.env.DISCONNECT_GRACE_MS || 60_000),
});

server.listen().then((address) => {
  const where = typeof address === 'object' && address ? address.port : process.env.PORT;
  console.log(`[poketato] game server listening on ${where} supabase=${cfg ? 'on' : 'off'} requireAuth=${requireAuth}`);
});
