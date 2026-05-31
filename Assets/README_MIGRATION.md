# Prophecy Century Unity Migration

Current target: a minimal playable Unity version of the original Web/Electron project.

What is already landed:

- `Assets/Resources/Data`
  - raw exports for `campaigns.json`, `heroes.json`, `unit_data.json`
  - Unity-friendly `unity_game_config.json`
- `Assets/Scripts/Core`
  - session bootstrap and persistent game session
- `Assets/Scripts/Data`
  - serializable config and definition models
  - repository loader for Resources-based JSON
- `Assets/Scripts/Model`
  - first-pass `RunState`, hand card, board unit, and hero state models
- `Assets/Scripts/Systems`
  - minimal `ShopSystem`, `BoardSystem`, `RunFlowController`
- `Assets/Scripts/UI`
  - `RunSceneDebugController` for context-menu driven smoke tests
  - `RunSceneController` and `BootstrapSceneController` for the first visible migration scene
- `Assets/Editor`
  - `SceneSetupGenerator` to generate `Bootstrap` and `RunScene`

What is intentionally not done yet:

- actual Unity UI screens for title, shop, hand, board, and battle
- battle simulator parity with `BattleManager.js`
- skill resolver parity with `BattleSkillManager.js`
- save/load persistence
- art/audio import and binding

Recommended next step inside the Unity Editor:

1. Open `Assets/Scenes/SampleScene.unity`.
2. Press Play.
3. The migration debug UI will be created at runtime automatically.
4. Use the generated buttons to refresh shop, buy the first card, deploy the first card, and resolve the placeholder battle.

Migration rule:

- Keep rules and data from the Web project.
- Rebuild scene, UI, input, and battle presentation in Unity.
- Current battle resolution is a placeholder stub, not parity with `BattleManager.js`.
- Unity batch scene generation is currently blocked on this machine by a Licensing client handshake issue, so runtime bootstrap is the reliable fallback for now.
