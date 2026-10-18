-- Poketato 搜打撤。对齐游戏设计：角色是宝可梦，装备是暗黑 2 稀有度，
-- 货币是探险币和战队勋章，工会表叫 guilds。普通关 4 人，Boss 和工会副本 8 人，世界战 20 人。
-- 技能输出用「系-格子」，例如 fire-0。客户端不能写钱包、结算和输出报告。

-- ---------------------------------------------------------------- 图鉴和任务

create table if not exists public.species (
  dex integer primary key check (dex > 0),
  slug text not null unique,
  name text not null,
  types text[] not null,
  playable boolean not null default false
);

create table if not exists public.missions (
  id text primary key,
  name text not null,
  mode text not null check (mode in ('extraction', 'boss', 'guild', 'world')),
  cap integer not null check (cap > 0),
  min_level integer not null,
  max_level integer not null,
  bonus bigint not null check (bonus >= 0)
);

create table if not exists public.item_bases (
  id text primary key,
  name text not null,
  slot text not null check (slot in ('weapon', 'helm', 'armor', 'gloves', 'boots', 'belt', 'amulet', 'ring')),
  required_level integer not null check (required_level between 1 and 40),
  weapon_damage integer not null default 0 check (weapon_damage >= 0),
  armor integer not null default 0 check (armor >= 0),
  spell boolean not null default false
);

create table if not exists public.unique_items (
  id text primary key,
  name text not null,
  base_id text not null references public.item_bases (id)
);

create table if not exists public.item_sets (
  id text primary key,
  name text not null,
  element text not null
);

create table if not exists public.shop_skus (
  id text primary key,
  name text not null,
  price bigint not null check (price > 0),
  kind text not null check (kind in ('consumable', 'respec', 'stash', 'cosmetic'))
);

create table if not exists public.achievement_defs (
  id text primary key,
  name text not null,
  description text not null
);

-- ---------------------------------------------------------------- 账号、角色、天赋、装备

create table if not exists public.profiles (
  user_id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null check (char_length(btrim(display_name)) between 1 and 16),
  stash_slots integer not null default 20 check (stash_slots between 20 and 200),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.wallets (
  user_id uuid primary key references public.profiles (user_id) on delete cascade,
  explorer_coin bigint not null default 0 check (explorer_coin >= 0),
  guild_medal bigint not null default 0 check (guild_medal >= 0),
  updated_at timestamptz not null default now()
);

create table if not exists public.currency_ledger (
  id bigint generated always as identity primary key,
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  currency text not null check (currency in ('explorer-coin', 'guild-medal')),
  amount bigint not null check (amount <> 0),
  balance_after bigint not null check (balance_after >= 0),
  reason text not null,
  ref_type text,
  ref_id text,
  created_at timestamptz not null default now()
);

create table if not exists public.characters (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  dex integer not null references public.species (dex),
  level integer not null default 1 check (level between 1 and 40),
  xp bigint not null default 0 check (xp >= 0),
  mew_shift text,
  created_at timestamptz not null default now(),
  constraint characters_id_user unique (id, user_id),
  constraint mew_shift_ok check (
    mew_shift is null or (
      dex = 151 and mew_shift in (
        'normal', 'fire', 'water', 'electric', 'grass', 'ice', 'fighting', 'poison',
        'ground', 'flying', 'psychic', 'bug', 'rock', 'ghost', 'dragon'
      )
    )
  )
);

create table if not exists public.talent_ranks (
  character_id uuid not null references public.characters (id) on delete cascade,
  skill_id text not null,
  rank integer not null check (rank > 0),
  primary key (character_id, skill_id)
);

create table if not exists public.items (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null,
  character_id uuid not null,
  base_id text not null references public.item_bases (id),
  rarity text not null check (rarity in ('normal', 'magic', 'rare', 'set', 'unique')),
  item_level integer not null check (item_level between 1 and 40),
  durability integer not null default 100 check (durability between 0 and 100),
  affixes jsonb not null default '[]'::jsonb,
  unique_id text references public.unique_items (id),
  set_id text references public.item_sets (id),
  equipped_slot text check (equipped_slot is null or equipped_slot in (
    'weapon', 'helm', 'armor', 'gloves', 'boots', 'belt', 'amulet', 'ring_left', 'ring_right'
  )),
  created_at timestamptz not null default now(),
  constraint items_owner_fk foreign key (character_id, user_id)
    references public.characters (id, user_id) on delete cascade
);

create unique index if not exists items_one_slot
  on public.items (character_id, equipped_slot)
  where equipped_slot is not null;

create table if not exists public.consumable_stacks (
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  sku text not null references public.shop_skus (id),
  qty integer not null check (qty > 0),
  primary key (user_id, sku)
);

-- ---------------------------------------------------------------- 工会、锁定、报告、排行榜

create table if not exists public.guilds (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  emblem_color text not null default '#888888',
  created_at timestamptz not null default now(),
  constraint guilds_name_unique unique (name),
  constraint guilds_name_len check (char_length(btrim(name)) between 2 and 16),
  constraint guilds_color check (emblem_color ~ '^#[0-9A-Fa-f]{6}$')
);

create table if not exists public.guild_members (
  guild_id uuid not null references public.guilds (id) on delete cascade,
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  role text not null check (role in ('leader', 'officer', 'member')),
  joined_at timestamptz not null default now(),
  primary key (guild_id, user_id)
);

create unique index if not exists one_guild_per_user on public.guild_members (user_id);

create table if not exists public.lockouts (
  guild_id uuid not null references public.guilds (id) on delete cascade,
  lockout_id text not null,
  completed_at timestamptz not null default now(),
  primary key (guild_id, lockout_id)
);

create table if not exists public.settlements (
  id uuid primary key,
  mission_id text not null references public.missions (id),
  mode text not null,
  created_at timestamptz not null default now()
);

create table if not exists public.encounter_reports (
  id uuid primary key,
  kind text not null check (kind in ('boss', 'guild', 'world')),
  mission_id text not null references public.missions (id),
  seed integer not null,
  started_at timestamptz not null,
  ended_at timestamptz not null,
  report jsonb not null,
  created_at timestamptz not null default now(),
  check (ended_at >= started_at)
);

create table if not exists public.skill_outputs (
  report_id uuid not null references public.encounter_reports (id) on delete cascade,
  character_id uuid not null,
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  skill_id text not null,
  casts integer not null check (casts >= 0),
  hits integer not null check (hits >= 0),
  damage bigint not null check (damage >= 0),
  healing bigint not null check (healing >= 0),
  primary key (report_id, character_id, skill_id),
  constraint skill_outputs_character_fk foreign key (character_id, user_id)
    references public.characters (id, user_id) on delete cascade
);

create table if not exists public.leaderboard_entries (
  board text not null check (board in (
    'extract-count', 'fastest-foggy', 'boss-damage', 'world-damage', 'richest'
  )),
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  display_name text not null,
  score bigint not null check (score >= 0),
  updated_at timestamptz not null default now(),
  primary key (board, user_id)
);

create table if not exists public.achievement_progress (
  user_id uuid not null references public.profiles (user_id) on delete cascade,
  achievement_id text not null references public.achievement_defs (id),
  progress bigint not null default 0 check (progress >= 0),
  unlocked_at timestamptz,
  primary key (user_id, achievement_id)
);

create index if not exists characters_user_idx on public.characters (user_id);
create index if not exists items_character_idx on public.items (character_id);
create index if not exists skill_outputs_character_idx on public.skill_outputs (character_id, skill_id);
create index if not exists ledger_user_idx on public.currency_ledger (user_id, created_at desc);

create or replace view public.character_skill_totals
with (security_invoker = true) as
select
  user_id,
  character_id,
  skill_id,
  sum(casts)::bigint as casts,
  sum(hits)::bigint as hits,
  sum(damage)::bigint as damage,
  sum(healing)::bigint as healing
from public.skill_outputs
group by user_id, character_id, skill_id;

comment on table public.encounter_reports is 'Boss、工会副本、世界战的输出报告。普通关只在打到 Boss 时才记。';
comment on table public.skill_outputs is '每个角色、每个技能一行。同一场里各技能伤害之和等于该角色的 damage。';
comment on table public.wallets is '探险币和战队勋章。不接真实支付。';

-- ---------------------------------------------------------------- 函数

create or replace function public.touch_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create or replace function public.json_bigint(p jsonb, p_key text, p_default bigint default 0)
returns bigint
language plpgsql
stable
as $$
declare
  v text;
  n numeric;
begin
  v := p ->> p_key;
  if v is null or btrim(v) = '' then
    return p_default;
  end if;
  n := v::numeric;
  if n < 0 then
    raise exception 'negative %', p_key;
  end if;
  if n > 1000000000 then
    raise exception 'too large %', p_key;
  end if;
  return trunc(n)::bigint;
end;
$$;

create or replace function public.mission_reward(p_mission text, p_outcome text, p_run_gold bigint)
returns bigint
language sql
stable
as $$
  select case
    when p_outcome = 'extracted' then m.bonus + greatest(coalesce(p_run_gold, 0), 0)
    when p_outcome = 'extracted_early' then m.bonus / 2 + greatest(coalesce(p_run_gold, 0), 0)
    else 0
  end
  from public.missions m
  where m.id = p_mission;
$$;

create or replace function public.apply_currency(
  p_user uuid,
  p_currency text,
  p_amount bigint,
  p_reason text,
  p_ref_type text,
  p_ref_id text
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
  v_balance bigint;
  v_name text;
begin
  if p_currency not in ('explorer-coin', 'guild-medal') then
    raise exception 'bad currency';
  end if;
  if p_amount = 0 then
    if p_currency = 'explorer-coin' then
      select explorer_coin into v_balance from public.wallets where user_id = p_user;
    else
      select guild_medal into v_balance from public.wallets where user_id = p_user;
    end if;
    return coalesce(v_balance, 0);
  end if;
  if p_currency = 'explorer-coin' then
    select explorer_coin into v_balance from public.wallets where user_id = p_user for update;
  else
    select guild_medal into v_balance from public.wallets where user_id = p_user for update;
  end if;
  if not found then
    raise exception 'wallet missing';
  end if;
  if v_balance + p_amount < 0 then
    raise exception 'insufficient funds';
  end if;
  if p_currency = 'explorer-coin' then
    update public.wallets
       set explorer_coin = explorer_coin + p_amount, updated_at = now()
     where user_id = p_user
     returning explorer_coin into v_balance;
    select display_name into v_name from public.profiles where user_id = p_user;
    insert into public.leaderboard_entries (board, user_id, display_name, score)
    values ('richest', p_user, coalesce(v_name, ''), v_balance)
    on conflict (board, user_id) do update
      set score = excluded.score, display_name = excluded.display_name, updated_at = now();
  else
    update public.wallets
       set guild_medal = guild_medal + p_amount, updated_at = now()
     where user_id = p_user
     returning guild_medal into v_balance;
  end if;
  insert into public.currency_ledger (user_id, currency, amount, balance_after, reason, ref_type, ref_id)
  values (p_user, p_currency, p_amount, v_balance, p_reason, p_ref_type, p_ref_id);
  return v_balance;
end;
$$;

create or replace function public.set_score(
  p_user uuid,
  p_board text,
  p_score bigint,
  p_op text
) returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_name text;
  v_current bigint;
begin
  if p_score is null or p_score < 0 then
    raise exception 'bad score';
  end if;
  if p_op = 'add' and p_score = 0 then
    return;
  end if;
  select display_name into v_name from public.profiles where user_id = p_user;
  select score into v_current
    from public.leaderboard_entries
   where board = p_board and user_id = p_user
   for update;
  if not found then
    insert into public.leaderboard_entries (board, user_id, display_name, score)
    values (p_board, p_user, coalesce(v_name, ''), p_score);
    return;
  end if;
  if p_op = 'max' and p_score <= v_current then
    return;
  end if;
  if p_op = 'min' and p_score >= v_current then
    return;
  end if;
  update public.leaderboard_entries
     set score = case p_op
           when 'add' then v_current + p_score
           when 'max' then p_score
           when 'min' then p_score
           else p_score
         end,
         display_name = coalesce(v_name, display_name),
         updated_at = now()
   where board = p_board and user_id = p_user;
end;
$$;

create or replace function public.unlock_achievement(p_user uuid, p_id text)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  if not exists (select 1 from public.achievement_defs where id = p_id) then
    raise exception 'unknown achievement';
  end if;
  insert into public.achievement_progress (user_id, achievement_id, progress, unlocked_at)
  values (p_user, p_id, 1, now())
  on conflict (user_id, achievement_id) do update
    set unlocked_at = coalesce(public.achievement_progress.unlocked_at, now()),
        progress = greatest(public.achievement_progress.progress, 1);
end;
$$;

create or replace function public.bump_extracts(p_user uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_progress bigint;
begin
  insert into public.achievement_progress (user_id, achievement_id, progress)
  values (p_user, 'first-extract', 1)
  on conflict (user_id, achievement_id) do update
    set progress = public.achievement_progress.progress + 1
  returning progress into v_progress;
  if v_progress >= 1 then
    update public.achievement_progress
       set unlocked_at = coalesce(unlocked_at, now())
     where user_id = p_user and achievement_id = 'first-extract';
  end if;
  insert into public.achievement_progress (user_id, achievement_id, progress)
  values (p_user, 'extract-10', 1)
  on conflict (user_id, achievement_id) do update
    set progress = public.achievement_progress.progress + 1
  returning progress into v_progress;
  if v_progress >= 10 then
    update public.achievement_progress
       set unlocked_at = coalesce(unlocked_at, now())
     where user_id = p_user and achievement_id = 'extract-10';
  end if;
end;
$$;

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  v_name text;
begin
  v_name := btrim(coalesce(new.raw_user_meta_data ->> 'display_name', ''));
  if v_name = '' then
    v_name := split_part(coalesce(new.email, ''), '@', 1);
  end if;
  if v_name = '' then
    v_name := '训练家';
  end if;
  v_name := left(v_name, 16);
  insert into public.profiles (user_id, display_name) values (new.id, v_name)
  on conflict (user_id) do nothing;
  insert into public.wallets (user_id) values (new.id)
  on conflict (user_id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

drop trigger if exists profiles_touch on public.profiles;
create trigger profiles_touch
  before update on public.profiles
  for each row execute function public.touch_updated_at();

create or replace function public.create_character(p_dex integer, p_shift text default null)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_id uuid;
  v_shift text := nullif(btrim(coalesce(p_shift, '')), '');
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  if not exists (select 1 from public.species where dex = p_dex and playable) then
    raise exception 'not playable';
  end if;
  if (select count(*) from public.characters where user_id = v_user) >= 30 then
    raise exception 'roster full';
  end if;
  if p_dex <> 151 and v_shift is not null then
    raise exception 'shift is only for mew';
  end if;
  insert into public.characters (user_id, dex, mew_shift)
  values (v_user, p_dex, v_shift)
  returning id into v_id;
  return v_id;
end;
$$;

create or replace function public.save_talents(p_character uuid, p_ranks jsonb)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_level integer;
  v_dex integer;
  v_shift text;
  v_types text[];
  v_key text;
  v_rank integer;
  v_total integer := 0;
  v_type text;
  v_slot integer;
  v_max integer;
  v_by jsonb := '{}'::jsonb;
  v_tree jsonb;
  v_need integer;
  v_have integer;
  v_s integer;
  v_gates integer[] := array[0, 0, 0, 5, 5, 5, 10, 10, 10, 15, 15, 15, 20, 20, 20, 25];
  v_caps integer[] := array[5, 1, 5, 1, 1, 5, 1, 3, 1, 1, 3, 5, 1, 1, 3, 1];
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  if jsonb_typeof(p_ranks) is distinct from 'object' then
    raise exception 'ranks';
  end if;
  select level, dex, mew_shift into v_level, v_dex, v_shift
    from public.characters where id = p_character and user_id = v_user;
  if not found then
    raise exception 'character not found';
  end if;
  select types into v_types from public.species where dex = v_dex;
  if v_dex = 151 and v_shift is not null and not (v_shift = any (v_types)) then
    v_types := v_types || v_shift;
  end if;

  for v_key, v_rank in
    select e.key, e.value::integer from jsonb_each_text(p_ranks) as e(key, value)
  loop
    if v_rank < 0 then
      raise exception 'negative rank';
    end if;
    if v_rank = 0 then
      continue;
    end if;
    if v_key !~ '^(normal|fire|water|electric|grass|ice|fighting|poison|ground|flying|psychic|bug|rock|ghost|dragon)-([0-9]|1[0-5])$' then
      raise exception 'bad skill';
    end if;
    v_type := split_part(v_key, '-', 1);
    v_slot := split_part(v_key, '-', 2)::integer;
    if not (v_type = any (v_types)) then
      raise exception 'wrong type';
    end if;
    if v_rank > v_caps[v_slot + 1] then
      raise exception 'rank too high';
    end if;
    v_total := v_total + v_rank;
    v_by := jsonb_set(v_by, array[v_type, v_slot::text], to_jsonb(v_rank), true);
  end loop;
  if v_total > least(v_level, 30) then
    raise exception 'not enough talent points';
  end if;

  for v_type, v_tree in select e.key, e.value from jsonb_each(v_by) as e(key, value)
  loop
    if coalesce((v_tree ->> '6')::integer, 0) > 0 and coalesce((v_tree ->> '1')::integer, 0) < 1 then
      raise exception 'missing prerequisite';
    end if;
    if coalesce((v_tree ->> '9')::integer, 0) > 0 and coalesce((v_tree ->> '6')::integer, 0) < 1 then
      raise exception 'missing prerequisite';
    end if;
    if coalesce((v_tree ->> '12')::integer, 0) > 0 and coalesce((v_tree ->> '9')::integer, 0) < 1 then
      raise exception 'missing prerequisite';
    end if;
    if coalesce((v_tree ->> '15')::integer, 0) > 0 and coalesce((v_tree ->> '12')::integer, 0) < 1 then
      raise exception 'missing prerequisite';
    end if;
    if coalesce((v_tree ->> '10')::integer, 0) > 0 and coalesce((v_tree ->> '4')::integer, 0) < 1 then
      raise exception 'missing prerequisite';
    end if;
    for v_s in 0..15 loop
      if coalesce((v_tree ->> v_s::text)::integer, 0) <= 0 then
        continue;
      end if;
      v_need := v_gates[v_s + 1];
      v_have := 0;
      for v_slot in 0..15 loop
        if v_gates[v_slot + 1] < v_need then
          v_have := v_have + coalesce((v_tree ->> v_slot::text)::integer, 0);
        end if;
      end loop;
      if v_have < v_need then
        raise exception 'tier locked';
      end if;
    end loop;
  end loop;

  delete from public.talent_ranks where character_id = p_character;
  for v_type, v_tree in select e.key, e.value from jsonb_each(v_by) as e(key, value)
  loop
    for v_key, v_rank in select e.key, e.value::integer from jsonb_each_text(v_tree) as e(key, value)
    loop
      insert into public.talent_ranks (character_id, skill_id, rank)
      values (p_character, v_type || '-' || v_key, v_rank);
    end loop;
  end loop;
end;
$$;

create or replace function public.purchase(p_sku text, p_character uuid default null)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_sku public.shop_skus%rowtype;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select * into v_sku from public.shop_skus where id = p_sku;
  if not found then
    raise exception 'unknown sku';
  end if;
  if v_sku.kind = 'respec' then
    if p_character is null or not exists (
      select 1 from public.characters where id = p_character and user_id = v_user
    ) then
      raise exception 'character not found';
    end if;
  end if;
  perform public.apply_currency(v_user, 'explorer-coin', -v_sku.price, 'shop_purchase', 'shop_sku', v_sku.id);
  if v_sku.kind in ('consumable', 'cosmetic') then
    insert into public.consumable_stacks (user_id, sku, qty)
    values (v_user, v_sku.id, 1)
    on conflict (user_id, sku) do update set qty = public.consumable_stacks.qty + 1;
  elsif v_sku.kind = 'respec' then
    delete from public.talent_ranks where character_id = p_character;
  elsif v_sku.kind = 'stash' then
    update public.profiles
       set stash_slots = least(200, stash_slots + 10)
     where user_id = v_user;
  end if;
end;
$$;

create or replace function public.equip_item(p_item uuid, p_slot text default null)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_char uuid;
  v_base text;
  v_base_slot text;
  v_req integer;
  v_level integer;
  v_slot text;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select i.character_id, i.base_id, b.slot, b.required_level
    into v_char, v_base, v_base_slot, v_req
    from public.items i
    join public.item_bases b on b.id = i.base_id
   where i.id = p_item and i.user_id = v_user;
  if not found then
    raise exception 'item not found';
  end if;
  select level into v_level from public.characters where id = v_char;
  if v_level < v_req then
    raise exception 'level too low';
  end if;
  if v_base_slot = 'ring' then
    if p_slot is null then
      if not exists (
        select 1 from public.items where character_id = v_char and equipped_slot = 'ring_left' and id <> p_item
      ) then
        v_slot := 'ring_left';
      else
        v_slot := 'ring_right';
      end if;
    elsif p_slot in ('ring_left', 'ring_right') then
      v_slot := p_slot;
    else
      raise exception 'wrong slot';
    end if;
  else
    if p_slot is not null and p_slot <> v_base_slot then
      raise exception 'wrong slot';
    end if;
    v_slot := v_base_slot;
  end if;
  update public.items
     set equipped_slot = null
   where character_id = v_char and equipped_slot = v_slot and id <> p_item;
  update public.items set equipped_slot = v_slot where id = p_item;
end;
$$;

create or replace function public.unequip_item(p_item uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  update public.items set equipped_slot = null where id = p_item and user_id = v_user;
  if not found then
    raise exception 'item not found';
  end if;
end;
$$;

create or replace function public.repair_item(p_item uuid)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_dur integer;
  v_level integer;
  v_cost bigint;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select durability, item_level into v_dur, v_level
    from public.items where id = p_item and user_id = v_user for update;
  if not found then
    raise exception 'item not found';
  end if;
  v_cost := greatest(1, round((1 - v_dur / 100.0) * v_level * 2)::bigint);
  perform public.apply_currency(v_user, 'explorer-coin', -v_cost, 'repair', 'item', p_item::text);
  update public.items set durability = 100 where id = p_item;
  return v_cost;
end;
$$;

create or replace function public.discard_item(p_item uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_slot text;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select equipped_slot into v_slot from public.items where id = p_item and user_id = v_user;
  if not found then
    raise exception 'item not found';
  end if;
  if v_slot is not null then
    raise exception 'unequip first';
  end if;
  delete from public.items where id = p_item and user_id = v_user;
end;
$$;

create or replace function public.create_guild(p_name text, p_color text default '#888888')
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_name text := btrim(coalesce(p_name, ''));
  v_color text := coalesce(nullif(btrim(coalesce(p_color, '')), ''), '#888888');
  v_id uuid;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  if char_length(v_name) < 2 or char_length(v_name) > 16 then
    raise exception 'invalid name';
  end if;
  if v_color !~ '^#[0-9A-Fa-f]{6}$' then
    raise exception 'invalid color';
  end if;
  if exists (select 1 from public.guild_members where user_id = v_user) then
    raise exception 'already in a guild';
  end if;
  insert into public.guilds (name, emblem_color) values (v_name, v_color) returning id into v_id;
  insert into public.guild_members (guild_id, user_id, role) values (v_id, v_user, 'leader');
  return v_id;
exception
  when unique_violation then
    raise exception 'name taken';
end;
$$;

create or replace function public.join_guild(p_guild uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_count integer;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  if not exists (select 1 from public.guilds where id = p_guild) then
    raise exception 'guild not found';
  end if;
  if exists (select 1 from public.guild_members where user_id = v_user) then
    raise exception 'already in a guild';
  end if;
  select count(*) into v_count from public.guild_members where guild_id = p_guild;
  if v_count >= 30 then
    raise exception 'guild full';
  end if;
  insert into public.guild_members (guild_id, user_id, role) values (p_guild, v_user, 'member');
end;
$$;

create or replace function public.leave_guild()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_role text;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select role into v_role from public.guild_members where user_id = v_user;
  if not found then
    raise exception 'not in a guild';
  end if;
  if v_role = 'leader' then
    raise exception 'leader must disband';
  end if;
  delete from public.guild_members where user_id = v_user;
end;
$$;

create or replace function public.set_member_role(p_user uuid, p_role text)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_guild uuid;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  if p_role not in ('officer', 'member') then
    raise exception 'bad role';
  end if;
  select guild_id into v_guild from public.guild_members where user_id = v_user and role = 'leader';
  if v_guild is null then
    raise exception 'not a leader';
  end if;
  if p_user = v_user then
    raise exception 'cannot change yourself';
  end if;
  update public.guild_members set role = p_role where guild_id = v_guild and user_id = p_user;
  if not found then
    raise exception 'not in your guild';
  end if;
end;
$$;

create or replace function public.kick_member(p_user uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_guild uuid;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select guild_id into v_guild from public.guild_members where user_id = v_user and role = 'leader';
  if v_guild is null then
    raise exception 'not a leader';
  end if;
  if p_user = v_user then
    raise exception 'cannot kick yourself';
  end if;
  delete from public.guild_members where guild_id = v_guild and user_id = p_user;
  if not found then
    raise exception 'not in your guild';
  end if;
end;
$$;

create or replace function public.disband_guild()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
  v_guild uuid;
begin
  if v_user is null then
    raise exception 'login required';
  end if;
  select guild_id into v_guild from public.guild_members where user_id = v_user and role = 'leader';
  if v_guild is null then
    raise exception 'not a leader';
  end if;
  delete from public.guilds where id = v_guild;
end;
$$;

create or replace function public.is_report_participant(p_report uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1 from public.skill_outputs
    where report_id = p_report and user_id = auth.uid()
  ) or exists (
    select 1 from public.encounter_reports r
    where r.id = p_report
      and exists (
        select 1 from jsonb_array_elements(r.report -> 'participants') p
        join public.characters c on c.id = (p ->> 'characterId')::uuid
        where c.user_id = auth.uid()
      )
  );
$$;

create or replace function public.settle_run(payload jsonb)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
  v_id uuid;
  v_mode text;
  v_mission text;
  v_cap integer;
  v_bonus_mode text;
  v_guild uuid;
  v_seed integer;
  v_started timestamptz;
  v_ended timestamptz;
  v_duration bigint;
  v_count integer;
  v_seen uuid[] := array[]::uuid[];
  v_player record;
  v_user uuid;
  v_char uuid;
  v_outcome text;
  v_gold bigint;
  v_coin bigint;
  v_inserted uuid;
  v_item record;
  v_affix_count integer;
  v_report jsonb;
  v_part record;
  v_skill record;
  v_sum bigint;
  v_heal bigint;
  v_lock text;
  v_monday date;
  v_has_officer boolean := false;
  v_part_char uuid;
  v_part_user uuid;
begin
  if coalesce(auth.role(), '') is distinct from 'service_role' then
    raise exception 'forbidden';
  end if;

  v_id := (payload ->> 'id')::uuid;
  v_mode := payload ->> 'mode';
  v_mission := payload ->> 'mission_id';
  if v_mode not in ('extraction', 'boss', 'guild', 'world') then
    raise exception 'bad mode';
  end if;
  select cap, mode into v_cap, v_bonus_mode from public.missions where id = v_mission;
  if not found then
    raise exception 'unknown mission';
  end if;
  if v_bonus_mode <> v_mode then
    raise exception 'mission mode mismatch';
  end if;
  v_guild := nullif(payload ->> 'guild_id', '')::uuid;
  v_seed := trunc(coalesce(nullif(payload ->> 'seed', ''), '0')::numeric)::integer;
  v_started := (payload ->> 'started_at')::timestamptz;
  v_ended := (payload ->> 'ended_at')::timestamptz;
  v_duration := public.json_bigint(payload, 'duration_ms', 0);
  if v_started is null or v_ended is null or v_ended < v_started then
    raise exception 'bad time';
  end if;
  if jsonb_typeof(payload -> 'players') is distinct from 'array' then
    raise exception 'players must be an array';
  end if;
  v_count := jsonb_array_length(payload -> 'players');
  if v_count < 1 or v_count > v_cap then
    raise exception 'player count';
  end if;
  if v_mode = 'guild' then
    if v_guild is null then
      raise exception 'guild required';
    end if;
  elsif v_guild is not null then
    raise exception 'unexpected guild';
  end if;

  for v_player in select value from jsonb_array_elements(payload -> 'players')
  loop
    v_user := (v_player.value ->> 'user_id')::uuid;
    v_char := (v_player.value ->> 'character_id')::uuid;
    v_outcome := v_player.value ->> 'outcome';
    if v_user = any (v_seen) then
      raise exception 'duplicate user';
    end if;
    v_seen := v_seen || v_user;
    if v_outcome not in ('extracted', 'extracted_early', 'died', 'failed', 'disconnected') then
      raise exception 'bad outcome';
    end if;
    if not exists (select 1 from public.characters where id = v_char and user_id = v_user) then
      raise exception 'character mismatch';
    end if;
    perform public.json_bigint(v_player.value, 'run_gold', 0);
    perform public.json_bigint(v_player.value, 'damage', 0);
    perform public.json_bigint(v_player.value, 'healing', 0);
    perform public.json_bigint(v_player.value, 'damage_taken', 0);
    if v_mode = 'guild' then
      if not exists (select 1 from public.guild_members where guild_id = v_guild and user_id = v_user) then
        raise exception 'player not in guild';
      end if;
      if exists (
        select 1 from public.guild_members
        where guild_id = v_guild and user_id = v_user and role in ('leader', 'officer')
      ) then
        v_has_officer := true;
      end if;
    end if;
    if v_player.value ? 'items' and jsonb_typeof(v_player.value -> 'items') is distinct from 'array' then
      raise exception 'items';
    end if;
    if coalesce(jsonb_array_length(v_player.value -> 'items'), 0) > 40 then
      raise exception 'too much loot';
    end if;
  end loop;
  if v_mode = 'guild' and not v_has_officer then
    raise exception 'officer required';
  end if;

  v_report := payload -> 'report';
  if v_mode in ('boss', 'guild', 'world') and (v_report is null or jsonb_typeof(v_report) <> 'object') then
    raise exception 'report required';
  end if;
  if v_report is not null and jsonb_typeof(v_report) = 'object' then
    if (v_report ->> 'kind') is distinct from (case when v_mode = 'extraction' then 'boss' else v_mode end) then
      raise exception 'bad report kind';
    end if;
    if jsonb_typeof(v_report -> 'participants') is distinct from 'array' then
      raise exception 'participants';
    end if;
    for v_part in select value from jsonb_array_elements(v_report -> 'participants')
    loop
      v_part_char := (v_part.value ->> 'characterId')::uuid;
      if not exists (
        select 1 from jsonb_array_elements(payload -> 'players') p
        where (p ->> 'character_id')::uuid = v_part_char
      ) then
        raise exception 'report character';
      end if;
      v_sum := 0;
      v_heal := 0;
      if jsonb_typeof(v_part.value -> 'skills') is distinct from 'array' then
        raise exception 'skills';
      end if;
      for v_skill in select value from jsonb_array_elements(v_part.value -> 'skills')
      loop
        if coalesce(v_skill.value ->> 'skillId', '') !~ '^(normal|fire|water|electric|grass|ice|fighting|poison|ground|flying|psychic|bug|rock|ghost|dragon)-([0-9]|1[0-5])$' then
          raise exception 'bad skill';
        end if;
        v_sum := v_sum + public.json_bigint(v_skill.value, 'damage', 0);
        v_heal := v_heal + public.json_bigint(v_skill.value, 'healing', 0);
      end loop;
      if v_sum <> public.json_bigint(v_part.value, 'damage', 0) then
        raise exception 'damage sum mismatch';
      end if;
      if v_heal <> public.json_bigint(v_part.value, 'healing', 0) then
        raise exception 'healing sum mismatch';
      end if;
    end loop;
  end if;

  insert into public.settlements (id, mission_id, mode)
  values (v_id, v_mission, v_mode)
  on conflict (id) do nothing
  returning id into v_inserted;
  if v_inserted is null then
    return v_id;
  end if;

  if v_report is not null then
    insert into public.encounter_reports (id, kind, mission_id, seed, started_at, ended_at, report)
    values (
      v_id,
      case when v_mode = 'extraction' then 'boss' else v_mode end,
      v_mission, v_seed, v_started, v_ended, v_report
    );
  end if;

  if v_mode = 'guild' and exists (
    select 1 from jsonb_array_elements(payload -> 'players') p where p ->> 'outcome' = 'extracted'
  ) then
    v_monday := date_trunc('week', v_ended)::date;
    v_lock := v_guild::text || ':' || v_monday::text;
    if exists (select 1 from public.lockouts where guild_id = v_guild and lockout_id = v_lock) then
      raise exception 'lockout';
    end if;
    insert into public.lockouts (guild_id, lockout_id) values (v_guild, v_lock);
  end if;

  if v_report is not null then
    for v_part in select value from jsonb_array_elements(v_report -> 'participants')
    loop
      v_part_char := (v_part.value ->> 'characterId')::uuid;
      select user_id into v_part_user from public.characters where id = v_part_char;
      for v_skill in select value from jsonb_array_elements(v_part.value -> 'skills')
      loop
        insert into public.skill_outputs (
          report_id, character_id, user_id, skill_id, casts, hits, damage, healing
        ) values (
          v_id,
          v_part_char,
          v_part_user,
          v_skill.value ->> 'skillId',
          public.json_bigint(v_skill.value, 'casts', 0),
          public.json_bigint(v_skill.value, 'hits', 0),
          public.json_bigint(v_skill.value, 'damage', 0),
          public.json_bigint(v_skill.value, 'healing', 0)
        );
      end loop;
      if (v_report ->> 'kind') = 'boss' then
        perform public.set_score(v_part_user, 'boss-damage', public.json_bigint(v_part.value, 'damage', 0), 'max');
      elsif (v_report ->> 'kind') = 'world' then
        perform public.set_score(v_part_user, 'world-damage', public.json_bigint(v_part.value, 'damage', 0), 'max');
      end if;
    end loop;
  end if;

  for v_player in select value from jsonb_array_elements(payload -> 'players')
  loop
    v_user := (v_player.value ->> 'user_id')::uuid;
    v_char := (v_player.value ->> 'character_id')::uuid;
    v_outcome := v_player.value ->> 'outcome';
    v_gold := public.json_bigint(v_player.value, 'run_gold', 0);
    v_coin := coalesce(public.mission_reward(v_mission, v_outcome, v_gold), 0);
    if v_outcome in ('died', 'failed', 'disconnected') then
      update public.items
         set durability = greatest(0, durability - 15)
       where character_id = v_char and equipped_slot is not null;
    elsif v_outcome in ('extracted', 'extracted_early') then
      if v_coin > 0 then
        perform public.apply_currency(v_user, 'explorer-coin', v_coin, 'extract', 'match', v_id::text);
      end if;
      if v_mode = 'guild' and v_outcome = 'extracted' then
        perform public.apply_currency(v_user, 'guild-medal', 1, 'guild_clear', 'match', v_id::text);
      end if;
      for v_item in select value from jsonb_array_elements(coalesce(v_player.value -> 'items', '[]'::jsonb))
      loop
        if not exists (select 1 from public.item_bases where id = v_item.value ->> 'base_id') then
          raise exception 'unknown base';
        end if;
        if coalesce(v_item.value ->> 'rarity', '') not in ('normal', 'magic', 'rare', 'set', 'unique') then
          raise exception 'bad rarity';
        end if;
        v_affix_count := coalesce(jsonb_array_length(v_item.value -> 'affixes'), 0);
        if (v_item.value ->> 'rarity') = 'normal' and v_affix_count <> 0 then
          raise exception 'bad affixes';
        end if;
        if (v_item.value ->> 'rarity') = 'magic' and v_affix_count <> 2 then
          raise exception 'bad affixes';
        end if;
        if (v_item.value ->> 'rarity') = 'rare' and (v_affix_count < 3 or v_affix_count > 6) then
          raise exception 'bad affixes';
        end if;
        if (v_item.value ->> 'rarity') = 'unique' and nullif(v_item.value ->> 'unique_id', '') is null then
          raise exception 'unique required';
        end if;
        if (v_item.value ->> 'rarity') = 'set' and nullif(v_item.value ->> 'set_id', '') is null then
          raise exception 'set required';
        end if;
        insert into public.items (
          user_id, character_id, base_id, rarity, item_level, durability, affixes, unique_id, set_id
        ) values (
          v_user,
          v_char,
          v_item.value ->> 'base_id',
          v_item.value ->> 'rarity',
          public.json_bigint(v_item.value, 'item_level', 1),
          100,
          coalesce(v_item.value -> 'affixes', '[]'::jsonb),
          nullif(v_item.value ->> 'unique_id', ''),
          nullif(v_item.value ->> 'set_id', '')
        );
      end loop;
      perform public.set_score(v_user, 'extract-count', 1, 'add');
      perform public.bump_extracts(v_user);
      if v_mission = 'foggy-forest' and v_outcome = 'extracted' and v_duration > 0 then
        perform public.set_score(v_user, 'fastest-foggy', v_duration, 'min');
      end if;
      if v_mission = 'foggy-forest' and v_outcome = 'extracted' then
        perform public.unlock_achievement(v_user, 'boss-venusaur');
      end if;
      if v_mission = 'temporal-tower' and v_outcome = 'extracted' then
        perform public.unlock_achievement(v_user, 'boss-mewtwo');
      end if;
      if v_outcome = 'extracted_early' and coalesce(jsonb_array_length(v_player.value -> 'items'), 0) >= 1 then
        perform public.unlock_achievement(v_user, 'early-out');
      end if;
      if v_mode = 'extraction' and v_count = 4 and v_outcome = 'extracted' then
        perform public.unlock_achievement(v_user, 'party-four');
      end if;
      if v_mode = 'guild' and v_outcome = 'extracted' then
        perform public.unlock_achievement(v_user, 'guild-clear');
      end if;
    end if;
    if v_mode = 'world' then
      perform public.unlock_achievement(v_user, 'world-join');
    end if;
    if v_player.value ? 'unlocks' and jsonb_typeof(v_player.value -> 'unlocks') = 'array' then
      for v_item in select value from jsonb_array_elements(v_player.value -> 'unlocks')
      loop
        perform public.unlock_achievement(v_user, v_item.value #>> '{}');
      end loop;
    end if;
  end loop;

  return v_id;
end;
$$;

-- ---------------------------------------------------------------- 行级权限

alter table public.species enable row level security;
alter table public.missions enable row level security;
alter table public.item_bases enable row level security;
alter table public.unique_items enable row level security;
alter table public.item_sets enable row level security;
alter table public.shop_skus enable row level security;
alter table public.achievement_defs enable row level security;
alter table public.profiles enable row level security;
alter table public.wallets enable row level security;
alter table public.currency_ledger enable row level security;
alter table public.characters enable row level security;
alter table public.talent_ranks enable row level security;
alter table public.items enable row level security;
alter table public.consumable_stacks enable row level security;
alter table public.guilds enable row level security;
alter table public.guild_members enable row level security;
alter table public.lockouts enable row level security;
alter table public.settlements enable row level security;
alter table public.encounter_reports enable row level security;
alter table public.skill_outputs enable row level security;
alter table public.leaderboard_entries enable row level security;
alter table public.achievement_progress enable row level security;

drop policy if exists catalog_species on public.species;
create policy catalog_species on public.species for select to anon, authenticated using (true);
drop policy if exists catalog_missions on public.missions;
create policy catalog_missions on public.missions for select to anon, authenticated using (true);
drop policy if exists catalog_bases on public.item_bases;
create policy catalog_bases on public.item_bases for select to anon, authenticated using (true);
drop policy if exists catalog_uniques on public.unique_items;
create policy catalog_uniques on public.unique_items for select to anon, authenticated using (true);
drop policy if exists catalog_sets on public.item_sets;
create policy catalog_sets on public.item_sets for select to anon, authenticated using (true);
drop policy if exists catalog_shop on public.shop_skus;
create policy catalog_shop on public.shop_skus for select to anon, authenticated using (true);
drop policy if exists catalog_achievements on public.achievement_defs;
create policy catalog_achievements on public.achievement_defs for select to anon, authenticated using (true);
drop policy if exists catalog_guilds on public.guilds;
create policy catalog_guilds on public.guilds for select to anon, authenticated using (true);
drop policy if exists catalog_board on public.leaderboard_entries;
create policy catalog_board on public.leaderboard_entries for select to anon, authenticated using (true);
drop policy if exists profiles_select on public.profiles;
create policy profiles_select on public.profiles for select to anon, authenticated using (true);
drop policy if exists profiles_update_own on public.profiles;
create policy profiles_update_own on public.profiles
  for update to authenticated
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

drop policy if exists wallets_own on public.wallets;
create policy wallets_own on public.wallets for select to authenticated using ((select auth.uid()) = user_id);
drop policy if exists ledger_own on public.currency_ledger;
create policy ledger_own on public.currency_ledger for select to authenticated using ((select auth.uid()) = user_id);
drop policy if exists characters_own on public.characters;
create policy characters_own on public.characters for select to authenticated using ((select auth.uid()) = user_id);
drop policy if exists talents_own on public.talent_ranks;
create policy talents_own on public.talent_ranks for select to authenticated using (
  exists (select 1 from public.characters c where c.id = character_id and c.user_id = (select auth.uid()))
);
drop policy if exists items_own on public.items;
create policy items_own on public.items for select to authenticated using ((select auth.uid()) = user_id);
drop policy if exists stacks_own on public.consumable_stacks;
create policy stacks_own on public.consumable_stacks for select to authenticated using ((select auth.uid()) = user_id);
drop policy if exists guild_members_select on public.guild_members;
create policy guild_members_select on public.guild_members for select to authenticated using (true);
drop policy if exists lockouts_select on public.lockouts;
create policy lockouts_select on public.lockouts for select to authenticated using (true);
drop policy if exists reports_select on public.encounter_reports;
create policy reports_select on public.encounter_reports
  for select to authenticated using ((select public.is_report_participant(id)));
drop policy if exists skills_select on public.skill_outputs;
create policy skills_select on public.skill_outputs
  for select to authenticated using ((select public.is_report_participant(report_id)));
drop policy if exists achievements_own on public.achievement_progress;
create policy achievements_own on public.achievement_progress
  for select to authenticated using ((select auth.uid()) = user_id);

-- ---------------------------------------------------------------- 授权

grant usage on schema public to anon, authenticated, service_role;
grant all on all tables in schema public to service_role;
grant all on all sequences in schema public to service_role;
revoke all on all tables in schema public from anon, authenticated;

grant select on public.species, public.missions, public.item_bases, public.unique_items,
  public.item_sets, public.shop_skus, public.achievement_defs, public.guilds,
  public.leaderboard_entries, public.profiles
  to anon, authenticated;
grant update (display_name) on public.profiles to authenticated;
grant select on public.wallets, public.currency_ledger, public.characters, public.talent_ranks,
  public.items, public.consumable_stacks, public.guild_members, public.lockouts,
  public.encounter_reports, public.skill_outputs, public.achievement_progress,
  public.character_skill_totals
  to authenticated;

do $$
declare
  r record;
begin
  for r in
    select p.oid::regprocedure as sig
    from pg_proc p
    join pg_namespace n on n.oid = p.pronamespace
    where n.nspname = 'public'
  loop
    execute format('revoke all on function %s from public, anon, authenticated', r.sig);
  end loop;
end
$$;

grant execute on function public.mission_reward(text, text, bigint) to anon, authenticated, service_role;
grant execute on function public.touch_updated_at() to authenticated;
grant execute on function public.create_character(integer, text) to authenticated;
grant execute on function public.save_talents(uuid, jsonb) to authenticated;
grant execute on function public.purchase(text, uuid) to authenticated;
grant execute on function public.equip_item(uuid, text) to authenticated;
grant execute on function public.unequip_item(uuid) to authenticated;
grant execute on function public.repair_item(uuid) to authenticated;
grant execute on function public.discard_item(uuid) to authenticated;
grant execute on function public.create_guild(text, text) to authenticated;
grant execute on function public.join_guild(uuid) to authenticated;
grant execute on function public.leave_guild() to authenticated;
grant execute on function public.set_member_role(uuid, text) to authenticated;
grant execute on function public.kick_member(uuid) to authenticated;
grant execute on function public.disband_guild() to authenticated;
grant execute on function public.is_report_participant(uuid) to authenticated;
grant execute on function public.settle_run(jsonb) to service_role;

do $$
begin
  if exists (select 1 from pg_roles where rolname = 'supabase_auth_admin') then
    grant execute on function public.handle_new_user() to supabase_auth_admin;
  end if;
end
$$;

-- ---------------------------------------------------------------- 种子

insert into public.species (dex, slug, name, types, playable) values
  (3, 'venusaur', '妙蛙花', '{grass,poison}', true),
  (6, 'charizard', '喷火龙', '{fire,flying}', true),
  (9, 'blastoise', '水箭龟', '{water}', true),
  (25, 'pikachu', '皮卡丘', '{electric}', true),
  (15, 'beedrill', '大针蜂', '{bug,poison}', true),
  (18, 'pidgeot', '比雕', '{normal,flying}', true),
  (36, 'wigglytuff', '胖可丁', '{normal}', true),
  (59, 'arcanine', '风速狗', '{fire}', true),
  (80, 'slowbro', '呆壳兽', '{water,psychic}', true),
  (94, 'gengar', '耿鬼', '{ghost,poison}', true),
  (68, 'machamp', '怪力', '{fighting}', true),
  (65, 'alakazam', '胡地', '{psychic}', true),
  (123, 'scyther', '飞天螳螂', '{bug,flying}', true),
  (134, 'vaporeon', '水伊布', '{water}', true),
  (135, 'jolteon', '雷伊布', '{electric}', true),
  (136, 'flareon', '火伊布', '{fire}', true),
  (142, 'aerodactyl', '化石翼龙', '{rock,flying}', true),
  (143, 'snorlax', '卡比兽', '{normal}', true),
  (144, 'articuno', '急冻鸟', '{ice,flying}', true),
  (145, 'zapdos', '闪电鸟', '{electric,flying}', true),
  (146, 'moltres', '火焰鸟', '{fire,flying}', true),
  (149, 'dragonite', '快龙', '{dragon,flying}', true),
  (150, 'mewtwo', '超梦', '{psychic}', true),
  (151, 'mew', '梦幻', '{psychic}', true),
  (130, 'gyarados', '暴鲤龙', '{water,flying}', true),
  (131, 'lapras', '拉普拉斯', '{water,ice}', true),
  (76, 'golem', '隆隆岩', '{rock,ground}', true),
  (51, 'dugtrio', '三地鼠', '{ground}', true),
  (34, 'nidoking', '尼多王', '{poison,ground}', true),
  (115, 'kangaskhan', '袋兽', '{normal}', true),
  (19, 'rattata', '小拉达', '{normal}', false),
  (16, 'pidgey', '波波', '{normal,flying}', false),
  (21, 'spearow', '烈雀', '{normal,flying}', false),
  (10, 'caterpie', '绿毛虫', '{bug}', false),
  (43, 'oddish', '走路草', '{grass,poison}', false),
  (41, 'zubat', '超音蝠', '{poison,flying}', false),
  (74, 'geodude', '小拳石', '{rock,ground}', false),
  (95, 'onix', '大岩蛇', '{rock,ground}', false),
  (129, 'magikarp', '鲤鱼王', '{water}', false),
  (92, 'gastly', '鬼斯', '{ghost,poison}', false),
  (77, 'ponyta', '小火马', '{fire}', false),
  (66, 'machop', '腕力', '{fighting}', false),
  (60, 'poliwag', '蚊香蝌蚪', '{water}', false),
  (81, 'magnemite', '小磁怪', '{electric}', false),
  (100, 'voltorb', '霹雳电球', '{electric}', false),
  (109, 'koffing', '瓦斯弹', '{poison}', false),
  (104, 'cubone', '卡拉卡拉', '{ground}', false),
  (88, 'grimer', '臭泥', '{poison}', false),
  (63, 'abra', '凯西', '{psychic}', false),
  (147, 'dratini', '迷你龙', '{dragon}', false)
on conflict (dex) do nothing;

insert into public.missions (id, name, mode, cap, min_level, max_level, bonus) values
  ('practice', '训练空地', 'extraction', 4, 1, 1, 0),
  ('foggy-forest', '雾林小径', 'extraction', 4, 1, 8, 80),
  ('beach-cave', '海滩洞窟', 'extraction', 4, 6, 14, 120),
  ('steam-cave', '蒸汽洞窟', 'extraction', 4, 12, 20, 160),
  ('amp-plains', '雷鸣平原', 'extraction', 4, 14, 22, 180),
  ('quicksand-cave', '流沙洞窟', 'extraction', 4, 18, 26, 200),
  ('dark-hill', '暗夜山丘', 'extraction', 4, 22, 30, 240),
  ('hidden-land', '隐藏之地', 'extraction', 4, 30, 40, 360),
  ('temporal-tower', '时空之塔', 'boss', 8, 28, 35, 400),
  ('guild-basement', '公会地下', 'guild', 8, 20, 30, 300),
  ('northern-desert', '北方争霸', 'world', 20, 15, 40, 0)
on conflict (id) do nothing;

insert into public.item_bases (id, name, slot, required_level, weapon_damage, armor, spell) values
  ('mengya-claw', '树果短刃', 'weapon', 1, 8, 0, false),
  ('sharp-fang', '尖牙', 'weapon', 10, 16, 0, false),
  ('type-staff', '属性手杖', 'weapon', 10, 14, 0, true),
  ('shell-hammer', '重壳锤', 'weapon', 20, 28, 0, false),
  ('dragon-blade', '龙骨巨刃', 'weapon', 30, 42, 0, false),
  ('leaf-cap', '叶帽', 'helm', 1, 0, 6, false),
  ('rock-helm', '岩盔', 'helm', 18, 0, 22, false),
  ('cloth-vest', '布甲', 'armor', 1, 0, 10, false),
  ('hide-armor', '皮甲', 'armor', 12, 0, 28, false),
  ('scale-mail', '鳞甲', 'armor', 24, 0, 48, false),
  ('cloth-gloves', '布手套', 'gloves', 1, 0, 4, false),
  ('spike-gauntlets', '刺甲护手', 'gloves', 16, 0, 14, false),
  ('soft-boots', '软靴', 'boots', 1, 0, 4, false),
  ('swift-boots', '疾风靴', 'boots', 14, 0, 12, false),
  ('rope-belt', '绳带', 'belt', 1, 0, 3, false),
  ('titan-belt', '巨兽腰带', 'belt', 20, 0, 16, false),
  ('berry-charm', '树果坠', 'amulet', 1, 0, 0, false),
  ('type-orb', '属性宝珠', 'amulet', 15, 0, 0, false),
  ('copper-ring', '铜戒指', 'ring', 1, 0, 0, false),
  ('gold-ring', '金戒指', 'ring', 18, 0, 0, false)
on conflict (id) do nothing;

insert into public.item_sets (id, name, element) values
  ('volcano', '火山之心', 'fire'),
  ('tide', '深海遗物', 'water')
on conflict (id) do nothing;

insert into public.unique_items (id, name, base_id) values
  ('char-fang', '喷火龙之牙', 'sharp-fang'),
  ('blastoise-cannon', '水箭龟之炮', 'type-staff'),
  ('pika-pouch', '皮卡丘颊囊', 'berry-charm'),
  ('snorlax-belt', '卡比兽之胃', 'titan-belt'),
  ('mewtwo-band', '超梦头环', 'rock-helm'),
  ('alakazam-glove', '胡地手套', 'spike-gauntlets'),
  ('aero-claw', '化石翼龙之爪', 'mengya-claw'),
  ('gengar-shroud', '耿鬼残影', 'hide-armor')
on conflict (id) do nothing;

insert into public.shop_skus (id, name, price, kind) values
  ('potion', '伤药', 50, 'consumable'),
  ('antidote', '解毒药', 40, 'consumable'),
  ('repair-hammer', '修理锤', 80, 'consumable'),
  ('respec', '天赋重置', 200, 'respec'),
  ('stash-page', '仓库扩页', 500, 'stash'),
  ('scarf-placeholder', '占位围巾', 300, 'cosmetic')
on conflict (id) do nothing;

insert into public.achievement_defs (id, name, description) values
  ('first-extract', '活着出来', '第一次成功撤离。'),
  ('extract-10', '老手', '成功撤离 10 次。'),
  ('die-rich', '带不走', '死亡时背包里至少有 1 件黄装。'),
  ('boss-venusaur', '雾心', '击败雾林的妙蛙花并撤离。'),
  ('boss-mewtwo', '塔顶', '在时空之塔击败超梦。'),
  ('guild-clear', '演武', '打通公会地下。'),
  ('world-join', '上沙场', '参加一次北方争霸。'),
  ('meter-top', '扛输出', '在一次 Boss 战里伤害全场第一。'),
  ('unique-drop', '暗金', '撤离时带出 1 件暗金。'),
  ('set-volcano', '火山之心', '凑齐 4 件火山之心并撤离。'),
  ('no-death-hidden', '隐藏之地', '隐藏之地无人死亡并全员撤离。'),
  ('keystone', '终极技', '任意系学会格子 15。'),
  ('mew-shift', '变身', '用梦幻的变身系打通一场。'),
  ('early-out', '见好就收', '用提前撤离带走至少 1 件装备。'),
  ('repair-broke', '碎掉了', '把一件耐久为 0 的装备修好。'),
  ('party-four', '满编', '4 人一起完成一场普通关。')
on conflict (id) do nothing;
