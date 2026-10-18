// 通关奖金和 supabase/migrations 里的 missions.bonus 一致。
// 主撤离拿全额，提前撤离拿一半，再加上本局金币。死亡为 0。

export const MISSIONS = {
  practice: { mode: 'extraction', cap: 4, bonus: 0 },
  'foggy-forest': { mode: 'extraction', cap: 4, bonus: 80 },
  'beach-cave': { mode: 'extraction', cap: 4, bonus: 120 },
  'steam-cave': { mode: 'extraction', cap: 4, bonus: 160 },
  'amp-plains': { mode: 'extraction', cap: 4, bonus: 180 },
  'quicksand-cave': { mode: 'extraction', cap: 4, bonus: 200 },
  'dark-hill': { mode: 'extraction', cap: 4, bonus: 240 },
  'hidden-land': { mode: 'extraction', cap: 4, bonus: 360 },
  'temporal-tower': { mode: 'boss', cap: 8, bonus: 400 },
  'guild-basement': { mode: 'guild', cap: 8, bonus: 300 },
  'northern-desert': { mode: 'world', cap: 20, bonus: 0 },
};

export function missionReward(missionId, outcome, runGold = 0) {
  const mission = MISSIONS[missionId];
  if (!mission) return 0;
  const gold = Math.max(0, Math.floor(Number(runGold) || 0));
  if (outcome === 'extracted') return mission.bonus + gold;
  if (outcome === 'extracted_early') return Math.floor(mission.bonus / 2) + gold;
  return 0;
}

export function capFor(mode) {
  if (mode === 'boss' || mode === 'guild') return 8;
  if (mode === 'world') return 20;
  return 4;
}
