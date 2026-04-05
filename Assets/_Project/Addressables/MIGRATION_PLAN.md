# Addressables Migration Plan (Mobile MMO)

## Target labels
- ui
- maps
- characters
- monsters
- skills
- audio
- vfx

## Step 1 - Keep compatibility (current stage)
- Keep existing Resources assets and paths untouched.
- Start marking new assets as Addressables using the labels above.
- Runtime loading uses ContentAddressablesService:
  - Addressables first (when enabled)
  - Resources fallback when package/symbol/key/label is unavailable

## Step 2 - Login dependencies
- Move login sprites, atlases and audio clips to Addressables.
- Apply labels: ui, audio.
- Validate preload group login.dependencies in device builds.
- Keep Resources copies only for non-migrated assets.

## Step 3 - World HUD and combat dependencies
- Move HUD atlas/icons, skill icons and VFX prefabs.
- Apply labels: ui, skills, vfx, audio.
- Validate preload group world.hud.dependencies.

## Step 4 - World content modularization
- Move map chunks, character prefabs, monster prefabs.
- Apply labels: maps, characters, monsters.
- Validate cold start and scene transition memory.

## Step 5 - Remove duplicated Resources assets
- For each migrated folder, remove duplicated Resources copies.
- Keep a rollback branch until QA passes on low-end devices.
- Update docs and authoring conventions for new content.

## Operational notes
- Enable Addressables package in Packages/manifest.json.
- Define MULIKE_USE_ADDRESSABLES in Player Settings for builds that use Addressables APIs directly.
- Build and update Addressables content catalogs as part of CI/CD.
- Prefer label-based preload for scene entry and key-based load for specific assets.
