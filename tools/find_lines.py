import os, sys

filepath = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Scripts', 'UI', 'RunSceneController.cs')
filepath = os.path.normpath(filepath)
outpath = os.path.join(os.path.dirname(__file__), '_key_lines.txt')

with open(filepath, 'r', encoding='utf-8') as f:
    lines = f.readlines()

keywords = [
    'BattleFloatingTextView',
    'FloatingTextRoutine',
    'ShowFloatingText(',
    'ShowUnitNumberFloatingText',
    'UpdateBattleFloatingTexts',
    'AddFloatingText(',
    'CountLossFloatingTextDelay',
    'BattleFloatingTextDuration',
    'CountLossActionPauseDuration',
    'scaleShrink',
    'AddDelayedFloatingText',
    'AddFloatingTextOutline',
    'SetHealth',
    'SetCount',
    'BattleUnitBarPresenter',
]

with open(outpath, 'w', encoding='utf-8') as out:
    for i, line in enumerate(lines):
        for kw in keywords:
            if kw in line:
                out.write(f'{i+1}: {line.rstrip()}\n')
                break

print(f'Written {outpath}')