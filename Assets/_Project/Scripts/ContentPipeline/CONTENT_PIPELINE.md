# MU-like Mobile Content Pipeline

## Goal
Single authoring flow in Unity (ScriptableObjects), exported to one JSON bundle consumed by server startup.

## Authoring Assets
Create these assets from `Create/MuLike/Content Pipeline/*`:

- `Item Catalog Database`
- `Monster Catalog Database`
- `Skill Catalog Database`
- `Map Catalog Database`
- `Spawn Table Database`
- `Drop Table Database`
- `Balance Config`
- `Pipeline Profile`

The profile links all previous assets and defines export output path.

## Export
1. Select a `GameContentPipelineProfile` in Project window.
2. Run menu: `MuLike/Content Pipeline/Export Selected Profile`.
3. Export writes JSON to `Assets/Resources/Data/Content/server_content_bundle.json` by default.

## End-to-End Example
Use menu:

`MuLike/Content Pipeline/Create Sample Profile + Export`

This creates sample assets under:

`Assets/_Project/ContentPipeline/Sample`

and exports the bundle immediately.

## Server Consumption
At startup, server calls importer:

- load from resources path `Data/Content/server_content_bundle`
- apply items/skills/maps/spawns/drops/balance
- if import fails, fallback to default seeded world

## Runtime Classes
- DTO + loader: `Assets/Shared/Content`
- Authoring + builder: `Assets/_Project/Scripts/ContentPipeline`
- Server importer: `Assets/Server/Infrastructure/ContentPipeline`
