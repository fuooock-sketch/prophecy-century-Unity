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

function unit(id) {
  const found = units.find(item => item.id === id);
  assert(found, `missing unit ${id}`);
  return found;
}

function devourShopSkills(item) {
  return [...(item.talents || []), ...(item.goldTalents || [])].filter(skill =>
    skill.kind === 'on_entry_devour_random_shop_gain_stats' ||
    skill.kind === 'round_end_devour_shop_highest_attack_gain_attack' ||
    skill.kind === 'while_on_board_on_entry_race_devour_shop_gain_attack' ||
    skill.kind === 'on_entry_race_units_devour_shop_gain_attack' ||
    skill.kind === 'round_end_tagged_units_devour_shop_gain_attack'
  );
}

const ger = unit('ger_beast');
assert(ger.talentText.includes('当前数量/10'), 'ger beast normal text should say current shop count divided by 10');
assert(ger.goldTalentText.includes('当前数量/5'), 'ger beast gold text should say current shop count divided by 5');
assert(!ger.talentText.includes('默认数量') && !ger.goldTalentText.includes('默认数量'), 'ger beast devour text should not mention default count');

const gerNormal = ger.talents.find(skill => skill.kind === 'on_entry_devour_random_shop_gain_stats');
const gerGold = ger.goldTalents.find(skill => skill.kind === 'on_entry_devour_random_shop_gain_stats');
assert(gerNormal && gerNormal.ratio === 0.1, 'ger beast normal ratio should be 0.1');
assert(gerGold && gerGold.ratio === 0.2, 'ger beast gold ratio should be 0.2');

const giant = unit('ger_giant_beast');
const giantNormal = giant.talents.find(skill => skill.kind === 'on_devour_self_gain_attack');
const giantGold = giant.goldTalents.find(skill => skill.kind === 'on_devour_self_gain_attack');
assert(giantNormal && giantNormal.value === 2, 'ger giant beast should gain +2 count on each devour event');
assert(giantGold && giantGold.value === 4, 'gold ger giant beast should gain +4 count on each devour event');
assert(![...(giant.talents || []), ...(giant.goldTalents || [])].some(skill => skill.kind === 'round_end_tagged_units_devour_shop_gain_attack'), 'ger giant beast should not trigger round-end tagged shop devours');

const gangerDevourUnits = units.filter(item => item.race === '甘格尔' && devourShopSkills(item).length > 0);
const expectedShopDevourUnitIds = ['ger_beast', 'exorcist_mount', 'shadow_butcher', 'wailing_beast'];
assert(expectedShopDevourUnitIds.every(id => gangerDevourUnits.some(item => item.id === id)), 'expected to audit all Ganger shop-devour units');
for (const item of gangerDevourUnits) {
  const combinedText = [item.talentText, item.goldTalentText].filter(Boolean).join('\n');
  assert(!combinedText.includes('默认数量'), `${item.id} shop-devour text should not use default count wording`);
}

assert(manage.includes('card?.baseCount > 0 ? card.baseCount : ResolveStartCount(UnitDef(card))'), 'shop devour should use the card current shop count when present');
assert(manage.includes('CurrentShopCount(entry.card)'), 'shop devour target selection should use current shop count');
assert(manage.includes('OrderByDescending(entry => CurrentShopCount(entry.card))'), 'highest-count shop devour should pick the highest current shop count');
assert(!manage.includes('OrderByDescending(entry => EffectiveAttack(entry.card))'), 'highest-count shop devour should not pick by attack');
assert(manage.includes('Math.Floor(cardCount * multiplier * ratio)'), 'ratio-based shop devour should floor fractional gains');
assert(!manage.includes('Math.Round(cardCount * multiplier * ratio)'), 'ratio-based shop devour should not round fractional gains');
assert(manage.includes('case "on_devour_self_gain_attack"'), 'ManageEventResolver should support ger giant beast self gain on devour');
assert(/case "on_devour_self_gain_attack":[\s\S]*GainCount\(runState, owner, Value\(talent, owner/.test(manage), 'ger giant beast self devour trigger should gain count on itself');

console.log('Ganger devour shop formula OK');
