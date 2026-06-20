const fs = require('fs');
const path = require('path');

const dataDir = path.join('Assets', 'Resources', 'Data');
const logPath = path.join(
  process.env.USERPROFILE || 'C:\\Users\\Administrator',
  'AppData',
  'LocalLow',
  'DefaultCompany',
  'prophecy_century',
  'player_state_log.jsonl'
);

function readJson(file) {
  return JSON.parse(fs.readFileSync(path.join(dataDir, file), 'utf8'));
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const campaigns = readJson('campaigns.json');
const maps = readJson('world_maps.json');
const presets = readJson('boss_enemies.json');
const units = readJson('unit_data.json');
const sessionSource = fs.readFileSync(
  path.join('Assets', 'Scripts', 'Core', 'ProphecyGameSession.cs'),
  'utf8'
);
const battleStubSource = fs.readFileSync(
  path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'),
  'utf8'
);

const unitIds = new Set(units.map((unit) => unit.id));
const presetIds = new Set(presets.map((preset) => preset.id));

const campaign = campaigns.find((item) => item.id === 'snow_peak_defense');
assert(campaign, 'Missing snow_peak_defense campaign');
assert(
  campaign.mapId === 'snow_peak_defense_map',
  `snow_peak_defense mapId should be snow_peak_defense_map, got ${campaign.mapId}`
);

const map = maps.find((item) => item.id === 'snow_peak_defense_map');
assert(map, 'Missing snow_peak_defense_map');
assert(map.startNodeId === 'snow_peak_defense_map_start', 'Unexpected snow peak start node');
assert(
  /case\s+"snow_peak_defense":\s*return\s+20;/s.test(sessionSource),
  'snow_peak_defense campaign round limit should be 20'
);
assert(
  /preset\.id\.StartsWith\("snow_peak_defense_"/.test(battleStubSource),
  'snow_peak_defense presets should be treated as fixed captured presets'
);

const roundNodes = map.nodes.filter((node) => /^snow_peak_defense_map_r\d{2}$/.test(node.id));
assert(roundNodes.length === 20, `Expected 20 snow peak round nodes, got ${roundNodes.length}`);
assert(map.connections.length === 20, `Expected 20 snow peak connections, got ${map.connections.length}`);

function latestCompleteBattleRun(limit) {
  const battles = fs.readFileSync(logPath, 'utf8')
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => JSON.parse(line))
    .filter((entry) => entry.type === 'battle' && Array.isArray(entry.playerUnits));

  let currentRun = [];
  let latestComplete = null;
  for (const battle of battles) {
    if (battle.round === 1) {
      currentRun = [];
    }
    currentRun.push(battle);
    if (
      currentRun.length === limit
      && currentRun.every((entry, index) => entry.round === index + 1)
    ) {
      latestComplete = currentRun.slice();
    }
  }

  assert(latestComplete, `Could not find complete continuous 1-${limit} battle run`);
  return latestComplete;
}

const latestSourceRun = latestCompleteBattleRun(20);

for (let round = 1; round <= 20; round += 1) {
  const suffix = String(round).padStart(2, '0');
  const presetId = `snow_peak_defense_r${suffix}`;
  assert(presetIds.has(presetId), `Missing enemy preset ${presetId}`);

  const node = roundNodes.find((item) => item.id === `snow_peak_defense_map_r${suffix}`);
  assert(node, `Missing round node ${suffix}`);
  assert(node.enemyPresetId === presetId, `Round ${suffix} should reference ${presetId}`);

  const preset = presets.find((item) => item.id === presetId);
  assert(preset.units.length > 0, `${presetId} has no units`);
  if (round === 1) {
    assert(preset.units.length === 1, `${presetId} should have exactly one first-round unit`);
  }
  const sourceUnits = latestSourceRun[round - 1].playerUnits;
  assert(
    preset.units.length === sourceUnits.length,
    `${presetId} unit count ${preset.units.length} does not match source ${sourceUnits.length}`
  );
  for (const unit of preset.units) {
    assert(unitIds.has(unit.unitId), `${presetId} references missing unit ${unit.unitId}`);
    assert(Number.isInteger(unit.count) && unit.count > 0, `${presetId} has invalid count`);
    assert(Number.isInteger(unit.star) && unit.star > 0, `${presetId} has invalid star`);
  }
}

console.log('Snow Peak Defense data OK');
