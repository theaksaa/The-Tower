# The Tower

Short Unity game project built around a tower-run loop:
`Main Menu -> Hero Select -> Run Overview -> Battle -> End Scene`.

## What's in the project

- Story and Endless run modes
- Hero selection with stats and move preview
- Run overview/map scene with loadout, shop, items, and progression
- Turn-based battle scene with rewards, effects, revive flow, and battle log
- Save/load support for continuing runs
- API-driven run config and endless encounters, with offline fallback data if the server is unavailable

## Main folders

- `Assets/Scenes` - playable scenes such as `MainMenu`, `HeroSelectScene`, `RunOverviewScene`, `BattleScene`, `StoryScene`, `EndScene`, and `LoadingScreen`
- `Assets/Scripts/Core` - shared systems like audio, config, scene loading, and persistence
- `Assets/Scripts/Domain` - runtime/domain models
- `Assets/Scripts/Gameplay` - scene/gameplay controllers for menu, hero select, overview, story, battle, and ending
- `Assets/Scripts/Session` - active run state and progression
- `Assets/Scripts/UI` - shared UI components, presenters, sprites, and drag/drop helpers
- `Assets/Resources` - sprites, audio, fonts, prefabs, and runtime-loaded assets
- `ProjectSettings` / `Packages` - Unity project configuration and dependencies

## Notes

- Unity version: `6000.4.4f1`
- The project uses URP, the Input System, TextMeshPro, and UGUI
- Server config is resolved by `ServerConfigService`; default API base URL points to `https://159.69.242.255:3000`
- Unity-generated folders like `Library`, `Temp`, and `Logs` do not need to be shared for normal source control workflows

## Current script structure

- `MainMenuController` handles mode selection and continue saves
- `HeroSelectController` loads heroes and starts a run
- `RunOverviewSceneController` manages map progression, shop, and move/item loadouts
- `TowerBattleController` drives combat, rewards, logging, and scene return flow
- `RunSession` keeps the active run state in memory
- `RunPersistenceService` handles saving and loading runs
