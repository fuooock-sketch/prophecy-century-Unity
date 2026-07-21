const fs = require('fs');
const path = require('path');

function read(file) {
  return fs.readFileSync(file, 'utf8');
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const units = JSON.parse(read(path.join('Assets', 'Resources', 'Data', 'unit_data.json')));
const manage = read(path.join('Assets', 'Scripts', 'Systems', 'ManageEventResolver.cs'));
const stub = read(path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'));
const realtime = read(path.join('Assets', 'Scripts', 'Systems', 'BattleRealtimeSystem.cs'));
const flow = read(path.join('Assets', 'Scripts', 'Systems', 'RunFlowController.cs'));
const ui = read(path.join('Assets', 'Scripts', 'UI', 'RunSceneController.cs'));

function unit(id) {
  const found = units.find(item => item.id === id);
  assert(found, `missing unit ${id}`);
  return found;
}

function onlySkill(list, unitId, label) {
  assert(Array.isArray(list) && list.length === 1, `${unitId} ${label} should have exactly one skill`);
  return list[0];
}

const mercenary = unit('mercenary_captain');
const mercenaryTalent = onlySkill(mercenary.talents, mercenary.id, 'talent');
assert(mercenaryTalent.kind === 'forest_gem_gift_count_bonus_aura', 'mercenary captain talent should boost forest gem gift count');
assert(mercenaryTalent.value === 1 && mercenaryTalent.count === 3, 'mercenary captain normal talent should be +1, max 3 per round');
const mercenaryGoldTalent = onlySkill(mercenary.goldTalents, mercenary.id, 'gold talent');
assert(mercenaryGoldTalent.kind === 'forest_gem_gift_count_bonus_aura', 'gold mercenary captain talent should boost forest gem gift count');
assert(mercenaryGoldTalent.value === 2 && mercenaryGoldTalent.count === 3, 'mercenary captain gold talent should be +2, max 3 per round');
assert(onlySkill(mercenary.battleSkills, mercenary.id, 'battle skill').targets === 2, 'mercenary captain normal battle should hit one extra target');
assert(onlySkill(mercenary.goldBattleSkills, mercenary.id, 'gold battle skill').targets === 3, 'mercenary captain gold battle should hit two extra targets');

const cheetah = unit('cheetah');
const cheetahTalent = onlySkill(cheetah.talents, cheetah.id, 'talent');
assert(cheetahTalent.kind === 'on_gift_action_self_gain_count_every_n', 'cheetah talent should count board gift actions and buff self');
assert(cheetahTalent.threshold === 5 && cheetahTalent.value === 4, 'cheetah normal talent should gain +4 every 5 gift actions');
const cheetahGoldTalent = onlySkill(cheetah.goldTalents, cheetah.id, 'gold talent');
assert(cheetahGoldTalent.kind === 'on_gift_action_self_gain_count_every_n', 'gold cheetah talent should count board gift actions and buff self');
assert(cheetahGoldTalent.threshold === 5 && cheetahGoldTalent.value === 8, 'cheetah gold talent should gain +8 every 5 gift actions');

const mole = unit('burrow_mole');
const moleTalent = onlySkill(mole.talents, mole.id, 'talent');
assert(moleTalent.kind === 'on_external_gift_action_self_gift_and_self_multiple_gain_forest_gem', 'burrow mole talent should echo external forest-gem gift actions and reward each count multiple');
assert(moleTalent.gift === 1 && moleTalent.threshold === 10 && moleTalent.gain === 1, 'burrow mole normal talent should gift 1 and gain 1 forest gem at each multiple of 10');
const moleGoldTalent = onlySkill(mole.goldTalents, mole.id, 'gold talent');
assert(moleGoldTalent.kind === 'on_external_gift_action_self_gift_and_self_multiple_gain_forest_gem', 'gold burrow mole talent should echo external forest-gem gift actions and reward each count multiple');
assert(moleGoldTalent.gift === 2 && moleGoldTalent.threshold === 10 && moleGoldTalent.gain === 1, 'burrow mole gold talent should gift 2 and gain 1 forest gem at each multiple of 10');
assert(manage.includes('reason != "burrow_mole_echo_gift"'), 'burrow mole echo gifts must not recursively trigger another echo');
assert(manage.includes('"burrow_mole_echo_gift"'), 'burrow mole self-gifts should carry an explicit recursion guard reason');
assert(stub.includes('unit.IsAlive && !unit.IsStealthed') && realtime.includes('!unit.IsAttached && !unit.IsStealthed'), 'stealthed units must not be selected as ordinary targets');
assert(stub.includes('FirstAttackDamageMultiplier') && realtime.includes('FirstAttackDamageMultiplier'), 'burrow mole first attack should use a damage multiplier instead of a forced critical');

const rangerRider = unit('ranger_rider');
assert(rangerRider.talents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 5 && skill.targetUnitId === 'elite_ranger_rider'), 'ranger rider should evolve after receiving 5 forest gems');
assert(rangerRider.goldTalents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 5 && skill.targetUnitId === 'elite_ranger_rider'), 'gold ranger rider should evolve after receiving 5 forest gems');

const eliteRangerRider = unit('elite_ranger_rider');
assert(onlySkill(eliteRangerRider.battleSkills, eliteRangerRider.id, 'battle skill').kind === 'on_attack_teleport_and_crit_chance', 'elite ranger rider should teleport beside its target on every attack');
assert(onlySkill(eliteRangerRider.battleSkills, eliteRangerRider.id, 'battle skill').chance === 0.5, 'elite ranger rider should have an absolute 50 percent crit chance');
assert(onlySkill(eliteRangerRider.goldBattleSkills, eliteRangerRider.id, 'gold battle skill').chance === 1, 'gold elite ranger rider should have an absolute 100 percent crit chance');

const ranger = unit('ranger');
assert(ranger.talents.some(skill => skill.kind === 'while_on_board_count_gain_events_evolve' && skill.threshold === 10 && skill.targetUnitId === 'sword_ranger'), 'ranger should evolve after 10 on-board count-gain events');
assert(ranger.goldTalents.some(skill => skill.kind === 'while_on_board_count_gain_events_evolve' && skill.threshold === 10 && skill.targetUnitId === 'sword_ranger'), 'gold ranger should evolve after 10 on-board count-gain events');

const swordRanger = unit('sword_ranger');
assert(onlySkill(swordRanger.talents, swordRanger.id, 'talent').kind === 'on_receive_gift_self_gain_attack', 'sword ranger should gain count when gifted forest gems');
assert(onlySkill(swordRanger.talents, swordRanger.id, 'talent').value === 4, 'sword ranger normal gifted gem bonus should be +4');
assert(onlySkill(swordRanger.goldTalents, swordRanger.id, 'gold talent').value === 8, 'sword ranger gold gifted gem bonus should be +8');

const watchfulSupport = unit('watchful_support');
assert(onlySkill(watchfulSupport.talents, watchfulSupport.id, 'talent').kind === 'round_end_forward_row_units_gift_forest_gem', 'watchful support should only gift the forward row');
assert(onlySkill(watchfulSupport.talents, watchfulSupport.id, 'talent').value === 1, 'watchful support normal gift should be 1 forest gem');
assert(onlySkill(watchfulSupport.goldTalents, watchfulSupport.id, 'gold talent').value === 2, 'watchful support gold gift should be 2 forest gems');

const riverCaptain = unit('river_captain');
assert(onlySkill(riverCaptain.talents, riverCaptain.id, 'talent').value === 1, 'river captain normal side gift should be 1 forest gem');
assert(onlySkill(riverCaptain.goldTalents, riverCaptain.id, 'gold talent').value === 2, 'river captain gold side gift should be 2 forest gems');

const archer = unit('archer');
assert(archer.talents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 5 && skill.targetUnitId === 'phantom_archer'), 'archer should evolve after receiving 5 forest gems');
assert(archer.goldTalents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 5 && skill.targetUnitId === 'phantom_archer'), 'gold archer should evolve after receiving 5 forest gems');
assert(archer.battleSkills.length === 0 && archer.goldBattleSkills.length === 0, 'archer should not have hidden battle skills when battle text is empty');

const phantomArcher = unit('phantom_archer');
assert(onlySkill(phantomArcher.talents, phantomArcher.id, 'talent').value === 0, 'normal phantom archer should absorb gems without gaining count on other sales');
assert(onlySkill(phantomArcher.goldTalents, phantomArcher.id, 'gold talent').value === 1, 'gold phantom archer should gain +1 count on other sales');
assert(onlySkill(phantomArcher.battleSkills, phantomArcher.id, 'battle skill').distance === 8, 'phantom archer normal snipe crit distance should be 8');
assert(onlySkill(phantomArcher.goldBattleSkills, phantomArcher.id, 'gold battle skill').distance === 6, 'phantom archer gold snipe crit distance should be 6');
assert(stub.includes('aliveEnemies.Max(enemy => enemy.BoardRow)'), 'stub phantom archer should identify the back line by formation row');

const twinTowerMage = unit('twin_tower_mage');
assert(onlySkill(twinTowerMage.battleSkills, twinTowerMage.id, 'battle skill').kind === 'battle_start_attach_to_highest_count_allies', 'twin tower mage should attach to the highest-count ally');
assert(onlySkill(twinTowerMage.battleSkills, twinTowerMage.id, 'battle skill').count === 1, 'normal twin tower mage should attach to one ally');
assert(onlySkill(twinTowerMage.goldBattleSkills, twinTowerMage.id, 'gold battle skill').count === 2, 'gold twin tower mage should attach to two allies');

const mireFiend = unit('mire_fiend');
assert(mireFiend.goldTalents.some(skill => skill.kind === 'round_end_self_gift_forest_gem' && skill.value === 4), 'gold mire fiend should gift self 4 forest gems at round end');
assert(mireFiend.talents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 30 && skill.targetUnitId === 'blood_mire_fiend'), 'mire fiend should evolve after receiving 30 forest gems');
assert(mireFiend.goldTalents.some(skill => skill.kind === 'on_receive_gift_self_evolve' && skill.threshold === 30 && skill.targetUnitId === 'blood_mire_fiend'), 'gold mire fiend should evolve after receiving 30 forest gems');

assert(manage.includes('case "on_gift_action_self_gain_count_every_n"'), 'ManageEventResolver should handle cheetah self gift-action counter');
assert(manage.includes('case "while_on_board_count_gain_events_evolve"'), 'ManageEventResolver should handle ranger count-gain event evolution');
assert(manage.includes('case "round_end_forward_row_units_gift_forest_gem"'), 'ManageEventResolver should handle watchful support forward row gifts');
assert(/case "on_gift_action_self_gain_count_every_n":[\s\S]*return eventType == "on_gift_action";/.test(manage), 'cheetah self counter should listen for gift actions');
assert(stub.includes('case "on_attack_multi_nearest_targets"'), 'BattleStubSystem should resolve mercenary captain multi-target attacks');
assert(realtime.includes('case "battle_start_stealth_assassinate_lowest_hp"'), 'BattleRealtimeSystem should support burrow mole gold stealth assassinate');
assert(stub.includes('on_attack_teleport_and_crit_chance'), 'BattleStubSystem should support elite ranger rider teleport attacks');
assert(stub.includes('PendingRoundPermanentCount += Math.Max(0, skill.value)'), 'cheetah death reward should be permanent count');
assert(flow.includes('unit.baseCount += permanentCountGain'), 'next-round permanent count should be applied to base count');
assert(stub.includes('battle_start_attach_to_highest_count_allies') && realtime.includes('battle_start_attach_to_highest_count_allies'), 'both battle systems should support twin tower attachment');
assert(ui.includes('ApplyBattleAttachmentFeedback') && ui.includes('PlayBattleAttachedAttackEffect') && ui.includes('ApplyBattleAttachmentDeathFeedback'), 'UI should render twin tower attach, follow-up attack, and detach feedback');
assert(ui.includes('battleEvent.Message.IndexOf("pounces"'), 'UI should recognize elite ranger rider teleport events');

console.log('Gansei skill alignment OK');
