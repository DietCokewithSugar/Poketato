import { capFor } from './rewards.js';

export const TICK_MS = 100;
export const MODES = ['extraction', 'boss', 'guild', 'world'];

const TYPE_OF = {
  charizard: 'fire', arcanine: 'fire', flareon: 'fire', moltres: 'fire',
  blastoise: 'water', vaporeon: 'water', gyarados: 'water',
  pikachu: 'electric', jolteon: 'electric', zapdos: 'electric',
  venusaur: 'grass',
  articuno: 'ice',
  machamp: 'fighting',
  gengar: 'ghost',
  alakazam: 'psychic', mewtwo: 'psychic', mew: 'psychic',
  dragonite: 'dragon',
  golem: 'rock',
  dugtrio: 'ground',
  beedrill: 'bug',
  pidgeot: 'flying',
  snorlax: 'normal', wigglytuff: 'normal', kangaskhan: 'normal',
};

function skillsFor(species) {
  const type = TYPE_OF[species] || 'normal';
  return [
    { id: `${type}-0`, cd: 5, power: 8, move: 'basic' },
    { id: `${type}-1`, cd: 8, power: type === 'water' ? 12 : 18, move: 'opener' },
  ];
}

export function loadoutFor(species) {
  return skillsFor(species);
}

export { capFor };

function clamp(n, a, b) {
  return Math.max(a, Math.min(b, n));
}

function round2(n) {
  return Math.round(n * 100) / 100;
}

/**
 * 权威对局。客户端只上报移动和撤离，伤害在这里结算。
 * 完整宝可梦模拟还没接进来；这套节拍把「每个角色每个技能的输出」先记下来。
 * 暴击：tick 能被 10 整除时，这一跳的伤害翻倍。
 * 搜打撤：坐标到右上角（x、y 都 >= 80）且至少过了 5 个 tick 才能撤离。
 * 不在撤离区每 tick 掉 4 点 HP。死亡、掉线、超时都带不走本局掉落。
 */
export function createMatch({ mode, seed = 1, durationMs = 1000, players }) {
  if (!MODES.includes(mode)) throw new Error('bad mode');
  const maxTicks = Math.max(1, Math.round(durationMs / TICK_MS));
  const start = mode === 'extraction' ? [70, 70] : [10, 10];
  const roster = players.map((player, index) => ({
    id: player.id,
    name: player.name || '训练家',
    species: player.species || 'charizard',
    dex: player.dex || 6,
    userId: player.userId || null,
    characterId: player.characterId || null,
    side: mode === 'world' ? index % 2 : 0,
    loadout: loadoutFor(player.species || 'charizard'),
    x: player.x ?? start[0] + (index % 4) * 2,
    y: player.y ?? start[1] + Math.floor(index / 4) * 2,
    ix: 0,
    iy: 0,
    hp: 100,
    damage: 0,
    taken: 0,
    outcome: null,
    skills: {},
  }));

  const match = {
    mode,
    seed,
    tick: 0,
    ended: false,
    reason: null,
    bossHp: mode === 'boss' || mode === 'guild' ? 5000 * roster.length : null,
    players: roster,
    final: null,

    player(id) {
      return roster.find((p) => p.id === id) || null;
    },

    setInput(id, x, y) {
      const p = match.player(id);
      if (!p || p.outcome || match.ended) return;
      const nx = Number(x);
      const ny = Number(y);
      p.ix = Number.isFinite(nx) ? clamp(nx, -1, 1) : 0;
      p.iy = Number.isFinite(ny) ? clamp(ny, -1, 1) : 0;
    },

    extract(id) {
      const p = match.player(id);
      if (!p || match.ended || p.outcome) return { ok: false, msg: '已经结束' };
      if (mode !== 'extraction') return { ok: false, msg: '这里不能撤离' };
      if (match.tick < 5 || p.x < 80 || p.y < 80) return { ok: false, msg: '还不能撤离' };
      p.outcome = 'extracted';
      if (roster.every((row) => row.outcome)) match.finish('extracted');
      return { ok: true };
    },

    disconnect(id) {
      const p = match.player(id);
      if (!p || match.ended || p.outcome) return;
      p.outcome = 'disconnected';
      if (roster.every((row) => row.outcome)) match.finish('disconnect');
    },

    step() {
      if (match.ended) return match;
      match.tick += 1;
      for (const p of roster) {
        if (p.outcome) continue;
        let dx = p.ix;
        let dy = p.iy;
        const mag = Math.hypot(dx, dy);
        if (mag > 1) {
          dx /= mag;
          dy /= mag;
        }
        p.x = clamp(p.x + dx * 5, 0, 100);
        p.y = clamp(p.y + dy * 5, 0, 100);
        if (mode === 'extraction' && (p.x < 80 || p.y < 80)) {
          p.hp -= 4;
          p.taken += 4;
        }
        if (p.hp <= 0) {
          p.hp = 0;
          p.outcome = 'died';
          continue;
        }
        for (const skill of p.loadout) {
          if (match.tick % skill.cd !== 0) continue;
          const crit = match.tick % 10 === 0;
          const amount = crit ? skill.power * 2 : skill.power;
          const row = p.skills[skill.id] || (p.skills[skill.id] = {
            skillId: skill.id, move: skill.move, hits: 0, damage: 0, crits: 0, heals: 0,
          });
          row.hits += 1;
          row.damage += amount;
          if (crit) row.crits += 1;
          p.damage += amount;
          if (match.bossHp != null) match.bossHp -= amount;
        }
      }
      if ((mode === 'boss' || mode === 'guild') && match.bossHp <= 0) match.finish('boss');
      else if (roster.every((p) => p.outcome)) match.finish('all-out');
      else if (match.tick >= maxTicks) match.finish('time');
      return match;
    },

    finish(reason) {
      if (match.ended) return;
      match.ended = true;
      match.reason = reason;
      if (mode === 'boss' || mode === 'guild') {
        const cleared = match.bossHp <= 0;
        for (const p of roster) if (!p.outcome) p.outcome = cleared ? 'extracted' : 'failed';
      } else if (mode === 'world') {
        for (const p of roster) if (!p.outcome) p.outcome = p.hp > 0 ? 'extracted' : 'died';
      } else {
        for (const p of roster) if (!p.outcome) p.outcome = 'failed';
      }
      match.final = {
        reason,
        bossHp: match.bossHp,
        players: roster.map((p, index) => ({
          id: p.id,
          name: p.name,
          species: p.species,
          userId: p.userId,
          characterId: p.characterId,
          side: p.side,
          outcome: p.outcome,
          damage: p.damage,
          taken: p.taken,
          hp: p.hp,
          x: round2(p.x),
          y: round2(p.y),
          skills: Object.values(p.skills).map((s) => ({
            skillId: s.skillId,
            move: s.move,
            casts: s.hits,
            hits: s.hits,
            damage: s.damage,
            healing: s.heals,
          })),
          damage: p.damage,
          healing: 0,
        })),
      };
    },

    state() {
      return {
        tick: match.tick,
        ended: match.ended,
        bossHp: match.bossHp,
        players: roster.map((p) => ({
          id: p.id,
          x: round2(p.x),
          y: round2(p.y),
          hp: p.hp,
          outcome: p.outcome,
          damage: p.damage,
        })),
      };
    },
  };

  return match;
}

