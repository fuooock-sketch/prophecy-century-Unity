const fs = require('fs');

function read(path) {
  return fs.readFileSync(path, 'utf8');
}

function readJson(path) {
  return JSON.parse(read(path));
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

function unit(units, id) {
  const found = units.find((item) => item.id === id);
  assert(found, `missing unit ${id}`);
  return found;
}

const units = readJson('Assets/Resources/Data/unit_data.json');
const worldMaps = readJson('Assets/Resources/Data/world_maps.json');
const bossEnemies = readJson('Assets/Resources/Data/boss_enemies.json');
const runState = read('Assets/Scripts/Model/RunState.cs');
const boardSystem = read('Assets/Scripts/Systems/BoardSystem.cs');
const manageResolver = read('Assets/Scripts/Systems/ManageEventResolver.cs');
const battleStub = read('Assets/Scripts/Systems/BattleStubSystem.cs');
const battleRealtime = read('Assets/Scripts/Systems/BattleRealtimeSystem.cs');
const runScene = read('Assets/Scripts/UI/RunSceneController.cs');

const painFlame = unit(units, 'pain_flame');
for (const skill of [...painFlame.battleSkills, ...painFlame.goldBattleSkills]) {
  assert(skill.kind === 'on_death_explode_if_hits_next_round_team_count', 'pain_flame should grant next-round team count');
  assert(!('nextRoundAttack' in skill), 'pain_flame should not use nextRoundAttack');
  assert(skill.nextRoundCount === skill.value, 'pain_flame count value should match text value');
}

assert(runState.includes('public int pendingNextRoundTempCount;'), 'board units need pendingNextRoundTempCount');
assert(battleStub.includes('PendingRoundTempCount'), 'battle stub should carry pending next-round count');
assert(!battleStub.includes('PendingRoundTempAttack += Math.Max(0, skill.nextRoundAttack)'), 'pain_flame should not write pending attack');
assert(battleStub.includes('PendingRoundTempCount +=') && battleStub.includes('skill.nextRoundCount'), 'pain_flame should write pending count');
assert(battleRealtime.includes('next round team count'), 'realtime event should describe count, not attack');

assert(manageResolver.includes('HasSelfEntryTalent(target, reason)'), 'entry-effect listeners should use self-entry filtering');
assert(manageResolver.includes('HasSelfEntryTalent(unit, "retrigger")'), 'leave retrigger should only target self entry effects');
assert(!manageResolver.includes('HasEntryTalent(target)'), 'broad target entry filtering should not remain in listener paths');

const laborer = unit(units, 'laborer');
const laborerPriceTalents = [...laborer.talents, ...laborer.goldTalents].filter((skill) => skill.kind.includes('on_sell_price'));
assert(laborerPriceTalents.every((skill) => skill.kind === 'on_sell_price_if_count_threshold'), 'laborer sell price should use count threshold');
assert(laborerPriceTalents.some((skill) => skill.threshold === 30 && skill.price === 2), 'normal laborer should sell for 2 at count 30');
assert(laborerPriceTalents.some((skill) => skill.threshold === 30 && skill.price === 3), 'gold laborer should sell for 3 at count 30');
assert(boardSystem.includes('on_sell_price_if_count_threshold'), 'BoardSystem should resolve count-threshold sell price');
assert(!boardSystem.includes('on_sell_price_if_attack_threshold'), 'BoardSystem should not use attack-threshold sell price');

function assertDeferredFeedbackRefresh(methodName) {
  let start = runScene.indexOf(`private void ${methodName}`);
  if (start < 0) {
    start = runScene.indexOf(`private bool ${methodName}`);
  }
  assert(start >= 0, `missing method ${methodName}`);
  const next = runScene.indexOf('\n        private ', start + 1);
  const body = runScene.slice(start, next >= 0 ? next : runScene.length);
  assert(body.includes('PlayFeedbackThenRefresh('), `${methodName} should defer refresh through feedback`);
  assert(!body.includes('RefreshView();'), `${methodName} should not refresh before feedback animations`);
}

for (const method of ['DeployHandCard', 'DeployHandCardToSlot', 'SellHandCard', 'SellBoardCard', 'SellBoardSlot']) {
  assertDeferredFeedbackRefresh(method);
}
assertDeferredFeedbackRefresh('UseForestGemCardOnSlot');
assert(runScene.includes('private IEnumerator PlayFeedbackThenRefreshRoutine'), 'missing deferred feedback refresh routine');
assert(runScene.indexOf('yield return PlayDevourFeedbackRoutine(devourEvents);') < runScene.indexOf('RefreshView();', runScene.indexOf('private IEnumerator PlayFeedbackThenRefreshRoutine')), 'devour routine should complete before refresh');
assert(runScene.indexOf('RefreshView();', runScene.indexOf('private IEnumerator PlayFeedbackThenRefreshRoutine')) < runScene.indexOf('yield return PlayManageFeedbackRoutine(feedbackEvents);'), 'manage routine should run after refresh so added hand cards have targets');

const songMap = worldMaps.find((map) => map.id === 'song_sang_city_map');
assert(songMap, 'missing song_sang_city_map');
assert(Array.isArray(songMap.connections), 'song_sang_city_map should define route connections');
assert(songMap.connections.length === 20, `song_sang_city_map should have 20 linear connections, got ${songMap.connections.length}`);
let currentNodeId = songMap.startNodeId;
for (let round = 1; round <= 20; round += 1) {
  const expected = `song_sang_city_map_r${String(round).padStart(2, '0')}`;
  const connection = songMap.connections.find((item) => item.fromNodeId === currentNodeId);
  assert(connection, `missing connection from ${currentNodeId}`);
  assert(connection.toNodeId === expected, `expected ${currentNodeId} to connect to ${expected}, got ${connection.toNodeId}`);
  currentNodeId = expected;
}

function assertPresetUnits(presetId, expectedUnits) {
  const preset = bossEnemies.find((item) => item.id === presetId);
  assert(preset, `missing preset ${presetId}`);
  const actual = (preset.units || []).map((unit) => `${unit.slotId}|${unit.unitId}|${unit.count}|${unit.star}`);
  const expected = expectedUnits.map((unit) => `${unit[0]}|${unit[1]}|${unit[2]}|${unit[3]}`);
  assert(JSON.stringify(actual) === JSON.stringify(expected), `${presetId} lineup mismatch`);
}

assertPresetUnits('song_sang_city_r01', [
  ['1-1', 'ger_infantry', 23, 1],
  ['2-1', 'murloc_servant', 24, 1],
]);
assertPresetUnits('song_sang_city_r10', [
  ['1-1', 'ger_beast', 28, 1],
  ['2-1', 'thug', 174, 2],
  ['2-2', 'pain_flame', 12, 4],
  ['3-1', 'shadow_butcher', 242, 5],
  ['3-2', 'shadow_butcher', 192, 5],
  ['3-3', 'ger_beast', 27, 1],
  ['4-2', 'twin_tower_mage', 1, 6],
  ['4-3', 'exorcist_mount', 48, 4],
  ['4-4', 'ger_infantry', 26, 1],
]);
assertPresetUnits('song_sang_city_r20', [
  ['1-1', 'demon_lord', 107, 6],
  ['2-1', 'thug', 550, 2],
  ['2-2', 'exorcist_mount', 56, 4],
  ['3-1', 'shadow_butcher', 856, 5],
  ['3-2', 'pain_flame', 65, 4],
  ['3-3', 'shadow_butcher', 2226, 5],
  ['4-1', 'shadow_butcher', 638, 5],
  ['4-2', 'twin_tower_mage', 27, 6],
  ['4-3', 'exorcist_mount', 188, 4],
  ['4-4', 'xilinding', 72, 6],
]);

console.log('aligned issue checks ok');
