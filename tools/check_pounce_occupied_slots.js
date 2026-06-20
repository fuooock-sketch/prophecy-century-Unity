const fs = require('fs');
const path = require('path');

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const source = fs.readFileSync(
  path.join('Assets', 'Scripts', 'Systems', 'BattleStubSystem.cs'),
  'utf8'
);

assert(
  /MovePouncerNextToTarget\(unit,\s*pounceTarget,\s*allies,\s*enemies\)/.test(source),
  'battle-start pounce should pass both teams into occupied-slot aware movement'
);
assert(
  /private static void MovePouncerNextToTarget\(BattleRuntimeUnit unit,\s*BattleRuntimeUnit target,\s*IEnumerable<BattleRuntimeUnit> allies,\s*IEnumerable<BattleRuntimeUnit> enemies\)/.test(source),
  'pounce movement should accept both teams so it can avoid occupied hexes'
);
assert(
  /BuildOccupiedHexSet\(allies,\s*enemies,\s*unit\)/.test(source),
  'pounce movement should build an occupied hex set excluding the pouncer itself'
);
assert(
  /Where\(coord => !occupied\.Contains\(HexKey\(coord\.Column,\s*coord\.Row\)\)\)/.test(source),
  'pounce destination candidates should exclude occupied neighbor hexes'
);

console.log('Pounce occupied-slot rules OK');
