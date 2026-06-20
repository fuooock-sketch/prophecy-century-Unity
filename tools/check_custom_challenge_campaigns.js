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

const runState = read(path.join('Assets', 'Scripts', 'Model', 'RunState.cs'));
const customSystem = fs.existsSync(path.join('Assets', 'Scripts', 'Systems', 'CustomChallengeSystem.cs'))
  ? read(path.join('Assets', 'Scripts', 'Systems', 'CustomChallengeSystem.cs'))
  : '';
const session = read(path.join('Assets', 'Scripts', 'Core', 'ProphecyGameSession.cs'));
const flow = read(path.join('Assets', 'Scripts', 'Systems', 'RunFlowController.cs'));
const battle = read(path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'));
const ui = read(path.join('Assets', 'Scripts', 'UI', 'RuntimeUiBootstrap.cs'));
const controller = read(path.join('Assets', 'Scripts', 'UI', 'RunSceneController.cs'));
const save = read(path.join('Assets', 'Scripts', 'Systems', 'SaveGameSystem.cs'));

assert(/public string customChallengeId;/.test(runState), 'RunState should track selected custom challenge id');
assert(/customChallengeCaptureRounds/.test(runState), 'RunState should store captured round boards for the active run');
assert(/customChallengeGenerated/.test(runState), 'RunState should prevent duplicate custom challenge generation');
assert(/CustomChallengeCampaignState/.test(runState), 'RunState should define serializable custom challenge campaign state');

assert(/CustomChallengeSystem/.test(customSystem), 'CustomChallengeSystem should exist');
assert(/custom_challenges\.json/.test(customSystem), 'custom challenges should save to a local custom_challenges.json file');
assert(/LoadAll\(\)/.test(customSystem), 'CustomChallengeSystem should load saved challenges');
assert(/SaveAll\(/.test(customSystem), 'CustomChallengeSystem should save challenge list');
assert(/RenameChallenge\(/.test(customSystem), 'CustomChallengeSystem should support rename');
assert(/DeleteChallenge\(/.test(customSystem), 'CustomChallengeSystem should support delete');
assert(/CreateFromRun\(/.test(customSystem), 'CustomChallengeSystem should create challenges from a completed run');

assert(/CustomChallengeSystem\.IsCustomChallengeId\(campaign\)/.test(session), 'StartNewRun should recognize custom challenge campaign ids');
assert(/customChallengeId =/.test(session), 'StartNewRun should store the selected custom challenge id');
assert(/snow_peak_defense_map/.test(customSystem), 'custom challenges should use a 20-round map shell');

assert(/CaptureCustomChallengeRound\(run,\s*completedRound\)/.test(flow) || /CaptureCustomChallengeRound\(run,\s*run\.round\)/.test(flow), 'RunFlowController should capture a board after each winning round advances');
assert(/TryCreateCustomChallengeFromCompletedRun\(run\)/.test(flow), 'RunFlowController should create custom challenge on 20-win completion');
assert(/campaignWins == 20/.test(flow), 'custom challenge creation should require 20 wins');
assert(/campaignLosses == 0/.test(flow), 'custom challenge creation should require zero losses');

assert(/BuildCustomChallengeEnemyRuntimeUnits/.test(battle), 'BattleStubSystem should build enemies from custom challenge rounds');
assert(/CustomChallengeSystem\.TryGetRound/.test(battle), 'BattleStubSystem should read selected custom challenge round data');

assert(/VerticalLayoutGroup/.test(ui) && /CampaignList/.test(ui), 'campaign selection should be a vertical list');
assert(/childControlHeight = true/.test(ui), 'campaign list layout should control item heights so rows do not overlap');
assert(/CreateCampaignListItem/.test(ui), 'campaign selection should create list items');
assert(/new Vector2\(196f,\s*110f\)/.test(ui), 'campaign list images should be reduced while staying readable in the list');
assert(/RenameCustomChallenge/.test(ui) && /DeleteCustomChallenge/.test(ui), 'custom challenge list items should expose rename/delete buttons');

assert(/SelectCampaignAndOpenHeroSelection/.test(controller), 'controller should still select built-in campaigns');
assert(/RenameCustomChallenge/.test(controller), 'controller should support custom challenge rename');
assert(/DeleteCustomChallenge/.test(controller), 'controller should support custom challenge delete');

assert(/CustomChallengeSystem\.Normalize/.test(save), 'SaveGameSystem should normalize custom challenge fields');

console.log('Custom challenge campaign rules OK');
