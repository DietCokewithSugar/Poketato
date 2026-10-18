import { missionReward } from './rewards.js';

const MOVES = {
  'fire-0': '火花', 'fire-1': '喷射火焰',
  'water-0': '水枪', 'water-1': '泡沫光线',
  'electric-0': '电击', 'electric-1': '十万伏特',
  'grass-0': '藤鞭', 'grass-1': '飞叶快刀',
  'normal-0': '撞击', 'normal-1': '电光一闪',
  'ice-0': '细雪', 'ice-1': '冰冻光束',
  'fighting-0': '空手劈', 'fighting-1': '劈瓦',
  'ghost-0': '舌舔', 'ghost-1': '暗影拳',
  'psychic-0': '念力', 'psychic-1': '幻象光线',
  'dragon-0': '龙息', 'dragon-1': '龙爪',
  'rock-0': '落石', 'rock-1': '岩石封锁',
  'ground-0': '掷泥', 'ground-1': '泥巴射击',
  'bug-0': '虫咬', 'bug-1': '双针',
  'flying-0': '啄', 'flying-1': '翅膀攻击',
  'poison-0': '毒针', 'poison-1': '污泥攻击',
};

export function buildSettle(room, final) {
  const players = final.players.map((p) => {
    const extracted = p.outcome === 'extracted' || p.outcome === 'extracted_early';
    const items = extracted
      ? [{
        base_id: 'rope-belt',
        rarity: 'normal',
        item_level: 1,
        affixes: [],
      }]
      : [];
    return {
      user_id: p.userId,
      character_id: p.characterId,
      outcome: p.outcome,
      run_gold: 0,
      damage: p.damage,
      healing: p.healing,
      damage_taken: p.taken,
      items,
      coin: missionReward(room.missionId, p.outcome, 0),
    };
  });
  const body = {
    id: room.id,
    mission_id: room.missionId,
    mode: room.mode,
    seed: room.seed,
    started_at: room.startedAt,
    ended_at: room.endedAt,
    duration_ms: room.durationMs,
    players,
  };
  if (room.guildId) body.guild_id = room.guildId;
  if (room.mode !== 'extraction') {
    body.report = {
      encounterId: room.id,
      kind: room.mode,
      missionId: room.missionId,
      seed: room.seed,
      startedAt: room.startedAt,
      endedAt: room.endedAt,
      participants: final.players.map((p) => ({
        characterId: p.characterId,
        speciesId: p.dex,
        name: p.name,
        skills: p.skills.map((s) => ({
          skillId: s.skillId,
          move: MOVES[s.skillId] || s.move,
          casts: s.casts,
          hits: s.hits,
          damage: s.damage,
          healing: s.healing,
        })),
        damage: p.damage,
        healing: p.healing,
        damageTaken: p.taken,
      })),
    };
  }
  return body;
}
