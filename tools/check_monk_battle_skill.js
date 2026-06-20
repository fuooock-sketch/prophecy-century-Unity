const fs = require('fs');
const path = require('path');

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const units = JSON.parse(fs.readFileSync(path.join('Assets', 'Resources', 'Data', 'unit_data.json'), 'utf8'));
const battleStubSource = fs.readFileSync(
  path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'),
  'utf8'
);

const monk = units.find((unit) => unit.id === 'monk');
assert(monk, 'Missing monk unit data');
assert(monk.faith === '莱特', `monk should be 莱特 faith, got ${monk.faith}`);
assert(
  (monk.battleSkills || []).some((skill) => skill.kind === 'battle_start_self_count_percent_per_faith_count' && skill.faith === '莱特'),
  'monk should count 莱特 faith units at battle start'
);
assert(
  (monk.goldBattleSkills || []).some((skill) => skill.kind === 'battle_start_self_count_percent_per_faith_count' && skill.faith === '莱特'),
  'gold monk should count 莱特 faith units at battle start'
);

const lightIllusion = units.find((unit) => unit.id === 'light_illusion');
assert(lightIllusion, 'Missing light_illusion unit data');
assert(lightIllusion.faith === '莱特', `light_illusion should be 莱特 faith, got ${lightIllusion.faith}`);

assert(
  /var\s+initialAllies\s*=\s*allies\.ToList\(\);/.test(battleStubSource),
  'battle start faith count should freeze the initial ally list'
);
assert(
  /CountFaith\(initialAllies,\s*skill\.faith,\s*unit\.Faith\)/.test(battleStubSource),
  'monk battle-start count should use initial allies, not order-mutated allies'
);
assert(
  /ResolveFaithSummonCountBonuses\(allies,\s*summoned,\s*events,\s*elapsed\);/.test(battleStubSource),
  'summoned faith units should immediately trigger monk count bonuses'
);
assert(
  /ApplySelfCountPercentPerFaithCount\(ally,\s*1,\s*skill,\s*events,\s*elapsed\)/.test(battleStubSource),
  'summon-triggered monk count bonus should apply exactly one faith unit per summoned unit'
);

console.log('Monk battle skill rules OK');
