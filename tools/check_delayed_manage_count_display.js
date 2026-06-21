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

const controller = read(path.join('Assets', 'Scripts', 'UI', 'RunSceneController.cs'));

assert(controller.includes('_delayedCountDisplayOverrides'), 'RunSceneController should keep delayed count display overrides');
assert(controller.includes('BeginDelayedCountDisplay('), 'RunSceneController should begin delayed count display before manage feedback');
assert(controller.includes('EndDelayedCountDisplay('), 'RunSceneController should clear delayed count display and refresh final counts');
assert(controller.includes('CreateDisplayCountCard('), 'card binding should be able to use delayed display counts');
assert(controller.includes('PlayFeedbackThenRefresh(devourEvents, feedbackEvents, before)'), 'manage actions should pass pre-change snapshots into delayed feedback');
assert(controller.includes('PlayRoundEndFeedbackThenRefresh('), 'round end should use a capped delayed feedback routine');
assert(controller.includes('RoundEndFeedbackBudgetSeconds'), 'round end delayed feedback should have a budget');
assert(/PlayFeedbackThenRefreshRoutine[\s\S]*?BeginDelayedCountDisplay\(before\)[\s\S]*?PlayManageFeedbackRoutine[\s\S]*?EndDelayedCountDisplay\(\)[\s\S]*?RefreshView\(\)/.test(controller), 'generic feedback routine should refresh final counts only after manage feedback');

console.log('Delayed manage count display rules OK');
