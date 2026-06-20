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

function writeJson(file, data) {
  fs.writeFileSync(path.join(dataDir, file), `${JSON.stringify(data, null, 2)}\n`, 'utf8');
}

function loadBattleRecords() {
  const lines = fs.readFileSync(logPath, 'utf8').split(/\r?\n/).filter(Boolean);
  return lines
    .map((line) => JSON.parse(line))
    .filter((entry) => entry.type === 'battle' && Array.isArray(entry.playerUnits));
}

function latestCompleteBattleRun(limit) {
  const battles = loadBattleRecords();

  if (battles.length < limit) {
    throw new Error(`Expected at least ${limit} battle records, found ${battles.length}`);
  }

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

  if (!latestComplete) {
    throw new Error(`Could not find a complete continuous 1-${limit} battle run`);
  }

  return latestComplete;
}

function buildSnowPeakMap() {
  const layers = [{ index: 0, name: 'Start' }];
  const nodes = [
    {
      id: 'snow_peak_defense_map_start',
      name: 'Start',
      layer: 0,
      type: 'start',
      x: 0.5,
      y: 0.03,
    },
  ];
  const connections = [];

  for (let round = 1; round <= 20; round += 1) {
    const suffix = String(round).padStart(2, '0');
    const nodeId = `snow_peak_defense_map_r${suffix}`;
    layers.push({ index: round, name: `R${suffix}` });
    nodes.push({
      id: nodeId,
      name: `Round ${suffix}`,
      layer: round,
      type: round === 20 ? 'boss' : round === 19 ? 'boss_guard' : 'normal_battle',
      x: 0.5,
      y: Number((0.03 + round * 0.046).toFixed(3)),
      enemyPresetId: `snow_peak_defense_r${suffix}`,
    });
    connections.push({
      fromNodeId: round === 1 ? 'snow_peak_defense_map_start' : `snow_peak_defense_map_r${String(round - 1).padStart(2, '0')}`,
      toNodeId: nodeId,
    });
  }

  return {
    id: 'snow_peak_defense_map',
    name: 'Snow Peak Defense',
    startNodeId: 'snow_peak_defense_map_start',
    layers,
    nodes,
    connections,
  };
}

function main() {
  const latest = latestCompleteBattleRun(20);

  const campaigns = readJson('campaigns.json');
  const snowPeak = campaigns.find((campaign) => campaign.id === 'snow_peak_defense');
  if (!snowPeak) {
    throw new Error('Missing snow_peak_defense campaign');
  }
  snowPeak.desc = 'A 20-round defense challenge built from the latest captured player battle snapshots.';
  snowPeak.mapId = 'snow_peak_defense_map';
  writeJson('campaigns.json', campaigns);

  const worldMaps = readJson('world_maps.json').filter((map) => map.id !== 'snow_peak_defense_map');
  worldMaps.push(buildSnowPeakMap());
  writeJson('world_maps.json', worldMaps);

  const presets = readJson('boss_enemies.json').filter((preset) => !/^snow_peak_defense_r\d{2}$/.test(preset.id));
  latest.forEach((entry, index) => {
    const round = index + 1;
    const suffix = String(round).padStart(2, '0');
    presets.push({
      id: `snow_peak_defense_r${suffix}`,
      name: `Snow Peak Defense R${suffix}`,
      type: round >= 19 ? (round === 20 ? 'boss' : 'boss_guard') : 'normal',
      units: entry.playerUnits.map((unit) => ({
        slotId: unit.slot,
        unitId: unit.id,
        count: Math.max(1, Math.trunc(Number(unit.count) || 1)),
        star: Math.max(1, Math.trunc(Number(unit.star) || 1)),
      })),
    });
  });
  writeJson('boss_enemies.json', presets);

  console.log(`Applied ${latest.length} latest battle records to snow_peak_defense`);
  console.log(`Source rounds: ${latest.map((entry) => entry.round).join(', ')}`);
  console.log(`Source nodes: ${latest[0].nodeId} -> ${latest[latest.length - 1].nodeId}`);
}

main();
