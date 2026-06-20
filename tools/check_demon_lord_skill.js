const fs = require('fs');
const path = require('path');

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const units = JSON.parse(fs.readFileSync(path.join('Assets', 'Resources', 'Data', 'unit_data.json'), 'utf8'));
const manageSource = fs.readFileSync(path.join('Assets', 'Scripts', 'Systems', 'ManageEventResolver.cs'), 'utf8');
const battleSource = fs.readFileSync(path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'), 'utf8');

const demonLord = units.find((unit) => unit.id === 'demon_lord');
assert(demonLord, 'Missing demon_lord unit data');

assert(
  (demonLord.talents || []).some((skill) => skill.kind === 'on_gain_count_transfer_to_random_other_allies' && skill.count === 1),
  'demon_lord normal talent should transfer gained count to 1 other ally'
);
assert(
  (demonLord.goldTalents || []).some((skill) => skill.kind === 'on_gain_count_transfer_to_random_other_allies' && skill.count === 1 && skill.goldCount === 2),
  'demon_lord gold talent should transfer gained count to 2 other allies'
);
assert(
  (demonLord.battleSkills || []).some((skill) => skill.kind === 'battle_action_self_shield_if_none' && skill.layers === 1),
  'demon_lord normal battle skill should gain 1 shield on action if none'
);
assert(
  (demonLord.goldBattleSkills || []).some((skill) => skill.kind === 'battle_action_self_shield_if_none' && skill.layers === 2),
  'demon_lord gold battle skill should gain 2 shields on action if none'
);

assert(
  /case "on_gain_count_transfer_to_random_other_allies":/.test(manageSource),
  'ManageEventResolver should implement demon_lord count transfer talent'
);
assert(
  /RemoveGainedCount\(owner,\s*eventValue\)/.test(manageSource),
  'demon_lord transfer should remove the gained count from itself'
);
assert(
  /runState\.boardUnits\.Where\(unit => unit != owner\)/.test(manageSource),
  'demon_lord transfer should only target other allies'
);
assert(
  /PickRandom\(recipients,\s*Count\(talent,\s*owner\)\)/.test(manageSource),
  'demon_lord transfer should pick normal or gold recipient count'
);

assert(
  /ResolveActionSelfShieldIfNone\(actor,\s*events,\s*elapsed\);/.test(battleSource),
  'battle turns should check action shield before acting'
);
assert(
  /case "battle_action_self_shield_if_none":/.test(battleSource),
  'BattleStubSystem should implement action shield skill'
);

console.log('Demon lord skill rules OK');
