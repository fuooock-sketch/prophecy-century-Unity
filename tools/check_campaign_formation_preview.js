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

const systemPath = path.join('Assets', 'Scripts', 'Systems', 'CampaignFormationPreviewSystem.cs');
const viewPath = path.join('Assets', 'Scripts', 'UI', 'CampaignFormationPreviewView.cs');
const bootstrap = read(path.join('Assets', 'Scripts', 'UI', 'RuntimeUiBootstrap.cs'));
const controller = read(path.join('Assets', 'Scripts', 'UI', 'RunSceneController.cs'));
const system = fs.existsSync(systemPath) ? read(systemPath) : '';
const view = fs.existsSync(viewPath) ? read(viewPath) : '';

assert(system.includes('CampaignFormationPreviewSystem'), 'preview resolver system should exist');
assert(system.includes('BuildPreviewRounds'), 'preview resolver should expose BuildPreviewRounds');
assert(system.includes('BuildPreviewSummary'), 'preview resolver should expose a campaign difficulty summary');
assert(system.includes('DifficultyScore') && system.includes('RoundScore'), 'preview data should include campaign and round difficulty scores');
assert(system.includes('CalculateRoundScore') && system.includes('Math.Min(100'), 'difficulty scores should be clamped to a 0-100 scale');
assert(system.includes('CustomChallengeSystem.TryGetChallenge'), 'custom challenge previews should read saved custom rounds');
assert(system.includes('FindWorldMap') && system.includes('FindEnemyPreset'), 'built-in previews should read configured map enemy presets');
assert(!system.includes('BuildEnemyUnitSnapshotsFromPreset'), 'preview resolver must not use runtime enemy generation/fallback');
assert(!system.includes('FillWorldMapPresetLineup'), 'preview resolver must not include inferred fallback lineups');
assert(system.includes('preset.units') && system.includes('slotId'), 'built-in previews should use explicit preset units and slot ids');

assert(view.includes('CampaignFormationPreviewView'), 'formation preview view should exist');
assert(view.includes('ShowCampaign') && view.includes('RefreshRound'), 'preview view should show campaign rounds and refresh pages');
assert(view.includes('关卡难度') && view.includes('本回合强度'), 'preview view should show campaign and round difficulty scores');
assert(view.includes('PreviousRound') && view.includes('NextRound'), 'preview view should support left/right paging');
assert(view.includes('RuntimeUnitTooltip'), 'preview units should bind runtime unit tooltips');
assert(view.includes('UnitCardState'), 'preview tooltips should use UnitCardState data');
assert(view.includes('RuntimeUnitIconCache.ApplyTo'), 'preview units should render unit icons');
assert(view.includes('BoardSlot_') && view.includes('FormationUnit_'), 'preview view should render unit positions on board slots');
assert(view.includes('LegacyRuntime.ttf') && !view.includes('Arial.ttf'), 'preview view should use the Unity-supported built-in runtime font');

assert(bootstrap.includes('CreateCampaignFormationPreviewScreen'), 'bootstrap should create the preview screen');
assert(bootstrap.includes('ViewFormationButton'), 'campaign list rows should include a formation preview button');
assert(bootstrap.includes('难度') && bootstrap.includes('难度未知'), 'campaign list rows should show a difficulty score or unknown difficulty');
assert(bootstrap.includes('SetSelectionScreens(titlePanel, campaignSelectionScreen, heroSelectionScreen, formationPreviewScreen)'), 'controller should receive preview screen');

assert(controller.includes('formationPreviewScreen'), 'controller should track preview screen');
assert(controller.includes('OpenCampaignFormationPreview'), 'controller should open formation preview');
assert(controller.includes('ReturnToCampaignFromFormationPreview'), 'controller should return from preview to campaign list');

console.log('Campaign formation preview rules OK');
