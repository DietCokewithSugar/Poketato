import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import pg from 'pg';

const DATABASE_URL = process.env.DATABASE_URL;
const sql = readFileSync(new URL('../../supabase/migrations/20261018120000_extraction_backend.sql', import.meta.url), 'utf8');
const A = '11111111-1111-4111-8111-111111111111';
const B = '22222222-2222-4222-8222-222222222222';

const prelude = `
do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'anon') then
    create role anon nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'authenticated') then
    create role authenticated nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'service_role') then
    create role service_role nologin bypassrls;
  else
    alter role service_role bypassrls;
  end if;
end $$;
grant anon, authenticated, service_role to current_user;
drop schema if exists public cascade;
drop schema if exists auth cascade;
create schema auth;
create schema public;
grant all on schema public to current_user;
grant usage on schema public to public;
create table auth.users (
  id uuid primary key,
  email text,
  raw_user_meta_data jsonb not null default '{}'::jsonb
);
create or replace function auth.uid()
returns uuid language sql stable as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim.sub', true), ''),
    nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'sub'
  )::uuid;
$$;
create or replace function auth.role()
returns text language sql stable as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim.role', true), ''),
    nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'role'
  );
$$;
grant usage on schema auth to anon, authenticated, service_role, public;
grant execute on all functions in schema auth to anon, authenticated, service_role, public;
`;

function locate(text, err) {
  const pos = Number(err.position);
  if (!pos) return err.message;
  const line = text.slice(0, pos).split('\n').length;
  return `${err.message}\n${text.split('\n')[line - 1]}\n(line ${line})`;
}

test('design schema, rls and settlement', { timeout: 30_000 }, async (t) => {
  if (!DATABASE_URL) {
    t.skip('DATABASE_URL is not set');
    return;
  }
  const url = new URL(DATABASE_URL);
  if (!['localhost', '127.0.0.1'].includes(url.hostname)) throw new Error('refusing non-local database');
  if (!['poketato', 'poketato_rls'].includes(url.pathname.replace(/^\//, ''))) {
    throw new Error('DATABASE_URL database must be poketato or poketato_rls');
  }
  const client = new pg.Client({ connectionString: DATABASE_URL });
  await client.connect();

  async function asRole(role, userId, fn) {
    if (!['anon', 'authenticated', 'service_role'].includes(role)) throw new Error('bad role');
    await client.query('select set_config($1, $2, false)', ['request.jwt.claim.sub', userId || '']);
    await client.query('select set_config($1, $2, false)', ['request.jwt.claim.role', role]);
    await client.query('select set_config($1, $2, false)', ['request.jwt.claims', JSON.stringify({ sub: userId || null, role })]);
    await client.query(`set role ${role}`);
    try {
      return await fn();
    } finally {
      await client.query('reset role');
    }
  }
  async function coins(user) {
    const { rows } = await asRole('authenticated', user, () => client.query(
      'select explorer_coin, guild_medal from public.wallets where user_id = $1',
      [user],
    ));
    return [BigInt(rows[0].explorer_coin), BigInt(rows[0].guild_medal)];
  }
  function settle(body) {
    return asRole('service_role', '', () => client.query('select public.settle_run($1::jsonb)', [JSON.stringify(body)]));
  }

  const started = new Date(Date.now() - 60_000).toISOString();
  const ended = new Date().toISOString();
  const foggy = '33333333-3333-4333-8333-333333333333';
  const death = '44444444-4444-4444-8444-444444444444';
  const tower = '55555555-5555-4555-8555-555555555555';
  const guildMatch = '66666666-6666-4666-8666-666666666666';
  const guildAgain = '77777777-7777-4777-8777-777777777777';

  try {
    await client.query('set statement_timeout = 20000');
    try { await client.query(prelude); } catch (err) { throw new Error(`prelude: ${locate(prelude, err)}`); }
    try { await client.query(sql); } catch (err) { throw new Error(locate(sql, err)); }

    await client.query(
      'insert into auth.users (id, email, raw_user_meta_data) values ($1,$2,$3::jsonb), ($4,$5,$6::jsonb)',
      [A, 'a@example.com', '{"display_name":"红"}', B, 'b@example.com', '{"display_name":"蓝"}'],
    );
    assert.deepEqual(await coins(A), [0n, 0n]);

    const reward = await client.query("select public.mission_reward('foggy-forest','extracted',10) as full, public.mission_reward('foggy-forest','extracted_early',10) as early");
    assert.equal(reward.rows[0].full, '90');
    assert.equal(reward.rows[0].early, '50');

    await asRole('anon', '', async () => {
      const { rows } = await client.query("select price from public.shop_skus where id = 'potion'");
      assert.equal(rows[0].price, '50');
      await assert.rejects(() => client.query('select * from public.wallets'));
    });

    const charA = await asRole('authenticated', A, async () => {
      await assert.rejects(() => client.query('select public.create_character(19)'), /not playable/);
      await assert.rejects(() => client.query("select public.create_character(6, 'fire')"), /shift is only for mew/);
      const { rows } = await client.query('select public.create_character(6) as id');
      await client.query("select public.save_talents($1, '{\"fire-0\":1}'::jsonb)", [rows[0].id]);
      await assert.rejects(
        () => client.query("select public.save_talents($1, '{\"fire-0\":2}'::jsonb)", [rows[0].id]),
        /not enough talent points/,
      );
      await assert.rejects(
        () => client.query("select public.save_talents($1, '{\"electric-0\":1}'::jsonb)", [rows[0].id]),
        /wrong type/,
      );
      await assert.rejects(() => client.query('update public.wallets set explorer_coin = 999 where user_id = $1', [A]));
      await assert.rejects(() => client.query('select public.settle_run($1::jsonb)', ['{}']), /forbidden|permission denied/);
      return rows[0].id;
    });
    await asRole('authenticated', A, () => client.query("select public.create_character(151, 'fire')"));

    const charB = await asRole('authenticated', B, async () => {
      const { rows } = await client.query('select public.create_character(9) as id');
      return rows[0].id;
    });

    const player = (user, character, outcome, extra = {}) => ({
      user_id: user,
      character_id: character,
      outcome,
      run_gold: 0,
      damage: 0,
      healing: 0,
      damage_taken: 0,
      items: [],
      ...extra,
    });

    await settle({
      id: foggy,
      mission_id: 'foggy-forest',
      mode: 'extraction',
      seed: 1,
      started_at: started,
      ended_at: ended,
      duration_ms: 90000,
      players: [player(A, charA, 'extracted', {
        items: [{ base_id: 'rope-belt', rarity: 'normal', item_level: 1, affixes: [] }],
      })],
    });
    assert.deepEqual(await coins(A), [80n, 0n]);
    await settle({
      id: foggy,
      mission_id: 'foggy-forest',
      mode: 'extraction',
      seed: 1,
      started_at: started,
      ended_at: ended,
      duration_ms: 90000,
      players: [player(A, charA, 'extracted', {
        items: [{ base_id: 'rope-belt', rarity: 'normal', item_level: 1, affixes: [] }],
      })],
    });
    assert.deepEqual(await coins(A), [80n, 0n]);

    const belt = await client.query('select id from public.items where character_id = $1', [charA]);
    assert.equal(belt.rows.length, 1);
    await asRole('authenticated', A, () => client.query('select public.equip_item($1)', [belt.rows[0].id]));
    await asRole('authenticated', A, () => client.query("select public.purchase('potion')"));
    assert.deepEqual(await coins(A), [30n, 0n]);

    const before = await client.query('select count(*)::int as n from public.items where character_id = $1', [charA]);
    await settle({
      id: death,
      mission_id: 'foggy-forest',
      mode: 'extraction',
      seed: 2,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      players: [player(A, charA, 'died', {
        items: [{ base_id: 'sharp-fang', rarity: 'unique', item_level: 10, unique_id: 'char-fang', affixes: [] }],
      })],
    });
    const after = await client.query('select count(*)::int as n from public.items where character_id = $1', [charA]);
    assert.equal(after.rows[0].n, before.rows[0].n);
    const worn = await client.query('select durability from public.items where id = $1', [belt.rows[0].id]);
    assert.equal(worn.rows[0].durability, 85);
    assert.deepEqual(await coins(A), [30n, 0n]);

    const report = (damage) => ({
      kind: 'boss',
      participants: [{
        characterId: charA,
        speciesId: 6,
        name: '喷火龙',
        skills: [{ skillId: 'fire-0', move: '火花', casts: 2, hits: 2, damage, healing: 0 }],
        damage,
        healing: 0,
        damageTaken: 0,
      }],
    });
    const broken = report(24);
    broken.participants[0].damage = 10;
    await assert.rejects(() => settle({
      id: '88888888-8888-4888-8888-888888888888',
      mission_id: 'temporal-tower',
      mode: 'boss',
      seed: 3,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      report: broken,
      players: [player(A, charA, 'extracted', { damage: 10 })],
    }), /damage sum mismatch/);

    await settle({
      id: tower,
      mission_id: 'temporal-tower',
      mode: 'boss',
      seed: 4,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      report: report(24),
      players: [player(A, charA, 'extracted', { damage: 24 })],
    });
    await asRole('authenticated', A, async () => {
      const { rows } = await client.query(
        'select skill_id, damage from public.skill_outputs where report_id = $1',
        [tower],
      );
      assert.equal(rows[0].skill_id, 'fire-0');
      assert.equal(rows[0].damage, '24');
    });
    await asRole('authenticated', B, async () => {
      const { rows } = await client.query('select * from public.skill_outputs where report_id = $1', [tower]);
      assert.equal(rows.length, 0);
    });
    await asRole('anon', '', async () => {
      const { rows } = await client.query(
        "select score from public.leaderboard_entries where board = 'extract-count' and user_id = $1",
        [A],
      );
      assert.equal(Number(rows[0].score) >= 1, true);
    });

    const guild = await asRole('authenticated', A, async () => {
      const { rows } = await client.query("select public.create_guild('火箭队', '#cc3344') as id");
      await assert.rejects(() => client.query('select public.leave_guild()'), /leader must disband/);
      return rows[0].id;
    });
    await asRole('authenticated', B, () => client.query('select public.join_guild($1)', [guild]));
    await assert.rejects(() => settle({
      id: '99999999-9999-4999-8999-999999999999',
      mission_id: 'guild-basement',
      mode: 'guild',
      guild_id: guild,
      seed: 5,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      report: {
        kind: 'guild',
        participants: [{
          characterId: charB, speciesId: 9, name: '水箭龟',
          skills: [{ skillId: 'water-0', move: '水枪', casts: 1, hits: 1, damage: 8, healing: 0 }],
          damage: 8, healing: 0, damageTaken: 0,
        }],
      },
      players: [player(B, charB, 'extracted', { damage: 8 })],
    }), /officer required/);

    await settle({
      id: guildMatch,
      mission_id: 'guild-basement',
      mode: 'guild',
      guild_id: guild,
      seed: 6,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      report: {
        kind: 'guild',
        participants: [
          {
            characterId: charA, speciesId: 6, name: '喷火龙',
            skills: [{ skillId: 'fire-0', move: '火花', casts: 1, hits: 1, damage: 8, healing: 0 }],
            damage: 8, healing: 0, damageTaken: 0,
          },
          {
            characterId: charB, speciesId: 9, name: '水箭龟',
            skills: [{ skillId: 'water-0', move: '水枪', casts: 1, hits: 1, damage: 8, healing: 0 }],
            damage: 8, healing: 0, damageTaken: 0,
          },
        ],
      },
      players: [
        player(A, charA, 'extracted', { damage: 8 }),
        player(B, charB, 'extracted', { damage: 8 }),
      ],
    });
    const medals = await coins(A);
    assert.equal(medals[1], 1n);
    await assert.rejects(() => settle({
      id: guildAgain,
      mission_id: 'guild-basement',
      mode: 'guild',
      guild_id: guild,
      seed: 7,
      started_at: started,
      ended_at: ended,
      duration_ms: 1000,
      report: {
        kind: 'guild',
        participants: [{
          characterId: charA, speciesId: 6, name: '喷火龙',
          skills: [{ skillId: 'fire-0', move: '火花', casts: 1, hits: 1, damage: 8, healing: 0 }],
          damage: 8, healing: 0, damageTaken: 0,
        }],
      },
      players: [player(A, charA, 'extracted', { damage: 8 })],
    }), /lockout/);
  } finally {
    await client.end();
  }
});
