const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isUuid(value) {
  return typeof value === 'string' && UUID.test(value);
}

export function isJwt(key) {
  return typeof key === 'string' && key.startsWith('eyJ');
}

export function jwtRole(token) {
  if (!token || typeof token !== 'string') return null;
  const raw = token.replace(/^Bearer\s+/i, '');
  const part = raw.split('.')[1];
  if (!part) return null;
  try {
    const json = JSON.parse(Buffer.from(part.replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf8'));
    return typeof json.role === 'string' ? json.role : null;
  } catch {
    return null;
  }
}

export function isServiceCredential(value, serviceKey) {
  if (!value) return false;
  const raw = String(value).replace(/^Bearer\s+/i, '').trim();
  if (serviceKey && (raw === serviceKey || value === serviceKey)) return true;
  if (raw.startsWith('sb_secret_')) return true;
  return jwtRole(value) === 'service_role';
}

export function configFromEnv(env = process.env) {
  const url = env.SUPABASE_URL?.trim().replace(/\/+$/, '');
  const anonKey = (env.SUPABASE_ANON_KEY || env.SUPABASE_PUBLISHABLE_KEY || '').trim();
  const serviceKey = (env.SUPABASE_SERVICE_ROLE_KEY || env.SUPABASE_SECRET_KEY || '').trim();
  if (!url || !anonKey || !serviceKey) return null;
  return { url, anonKey, serviceKey };
}

export function createSupabase(cfg, fetchImpl = fetch) {
  const cache = new Map();

  async function request(method, path, body, service, bearer) {
    const key = service ? cfg.serviceKey : cfg.anonKey;
    const headers = { apikey: key, accept: 'application/json' };
    if (body !== undefined && body !== null) headers['content-type'] = 'application/json';
    const auth = bearer ? `Bearer ${bearer}` : (isJwt(key) ? `Bearer ${key}` : '');
    if (auth) headers.authorization = auth;
    const res = await fetchImpl(`${cfg.url}${path}`, {
      method,
      headers,
      body: body === undefined || body === null ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(10_000),
    });
    const text = await res.text();
    let data = null;
    if (text) {
      try { data = JSON.parse(text); } catch { data = { message: text }; }
    }
    return { status: res.status, data };
  }

  return {
    cfg,
    async user(token) {
      if (!token || token.length > 8192) return null;
      const now = Date.now();
      const hit = cache.get(token);
      if (hit && hit.until > now) return hit.user;
      const res = await request('GET', '/auth/v1/user', null, false, token);
      if (res.status !== 200 || !res.data || typeof res.data.id !== 'string') return null;
      const user = { id: res.data.id, email: typeof res.data.email === 'string' ? res.data.email : '' };
      if (cache.size > 500) cache.clear();
      cache.set(token, { user, until: now + 60_000 });
      return user;
    },
    async characterOf(token, characterId) {
      if (!isUuid(characterId)) return null;
      const user = await this.user(token);
      if (!user || !isUuid(user.id)) return null;
      const rows = await request(
        'GET',
        `/rest/v1/characters?id=eq.${characterId}&user_id=eq.${user.id}&select=id,dex,level`,
        null,
        true,
      );
      if (rows.status >= 300 || !Array.isArray(rows.data) || rows.data.length === 0) return null;
      const dex = rows.data[0].dex;
      const speciesRows = await request(
        'GET',
        `/rest/v1/species?dex=eq.${dex}&select=slug,name,types`,
        null,
        true,
      );
      const species = Array.isArray(speciesRows.data) ? speciesRows.data[0] : null;
      const guild = await request(
        'GET',
        `/rest/v1/guild_members?user_id=eq.${user.id}&select=guild_id,role`,
        null,
        true,
      );
      const membership = Array.isArray(guild.data) ? guild.data[0] : null;
      return {
        userId: user.id,
        characterId,
        dex,
        name: species?.name || '训练家',
        species: species?.slug || 'charizard',
        guildId: membership?.guild_id || null,
        guildRole: membership?.role || null,
      };
    },
    async settleRun(payload) {
      const res = await request('POST', '/rest/v1/rpc/settle_run', { payload }, true);
      if (res.status >= 300) {
        const msg = res.data && (res.data.message || res.data.error || res.data.msg);
        throw new Error(typeof msg === 'string' ? msg : `supabase ${res.status}`);
      }
      return res.data;
    },
  };
}

export const BLOCKED_RPC = new Set([
  'settle_run',
  'record_match_result',
  'apply_currency',
  'set_score',
  'bump_extracts',
  'unlock_achievement',
  'json_bigint',
  'handle_new_user',
]);
