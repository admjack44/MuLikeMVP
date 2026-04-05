# Android Mid-Range MMO Optimization (Town_01 / Field_01 / World_Dev)

## Performance targets
- Stable 45-60 FPS on mid-range Android.
- Minimize GC spikes during combat and crowded areas.
- Keep scene transitions smooth between Town_01, Field_01 and World_Dev.

## Runtime code changes included
- Pooling for combat VFX and floating damage popups.
- Pooled entity-view factory preference at bootstrap.
- UI update throttling for HUD combat feedback.
- Distance culling check throttling.
- World installer auto-wiring for camera culling and URP quality applier.

## 1) Object pooling (VFX + damage popups)
- Configure MobilePoolManager pools:
  - vfx.hit
  - vfx.crit
  - ui.damage-popup
- Recommended warmup:
  - vfx.hit: 24
  - vfx.crit: 12
  - ui.damage-popup: 48
- Recommended max size:
  - vfx.hit: 96
  - vfx.crit: 48
  - ui.damage-popup: 160

## 2) Pooling monsters/entity views
- Prefer PooledEntityViewFactory in world scenes.
- Keys:
  - entity.remote-player
  - entity.monster
  - entity.pet
  - entity.drop
  - entity.fallback
- Recommended warmup (World_Dev baseline):
  - entity.monster: 60
  - entity.remote-player: 30
  - entity.drop: 40
  - entity.pet: 20

## 3) UI throttling
- Keep heavy HUD visuals at 20-30 updates per second.
- Current combat feedback view uses 24 UPS.
- Keep gameplay logic updates independent from visual throttling.

## 4) Camera culling and distance activation
- Apply MobileCameraCullingConfigurator to MainCamera:
  - near clip: 0.2
  - far clip: 90
  - Monster layer: 45
  - Drop layer: 25
  - VFX layer: 35
- Use DistanceCullingController with check interval around 0.2s.

## 5) URP mobile quality baseline
- Use MobileUrpQualityProfile values:
  - targetFrameRate: 60
  - vSync: 0
  - renderScale: 0.85-0.95
  - shadowDistance: 18-28
  - antiAliasing: 0
  - HDR: off
  - Soft particles: off

## 6) Texture compression strategy
- Android texture format:
  - UI/icons: ASTC 6x6
  - World/vfx: ASTC 8x8
- Texture import:
  - mipmaps disabled for UI/icons
  - max size: 2048 UI, 1024 world default
- Use tool:
  - MuLike/Performance/Content/Apply Android Texture Compression (Selected)

## 7) Batching and atlases
- Use static batching for static world geometry in Town_01/Field_01.
- Keep SRP Batcher enabled in URP settings.
- Create atlases with tool:
  - MuLike/Performance/Content/Create Mobile Sprite Atlases
- Atlas outputs:
  - Assets/_Project/Art/Atlases/UI_Main.spriteatlasv2
  - Assets/_Project/Art/Atlases/VFX_UI.spriteatlasv2

## Scene prep checklist
- Town_01
  - Lower NPC update rates, aggressive culling on drops.
- Field_01
  - Higher monster pool warmup, stricter VFX culling distance.
- World_Dev
  - Keep debug overlays off by default for performance runs.

## Validation workflow
1. Run 10-minute sessions with stress combat waves.
2. Capture Profiler Timeline and Memory snapshots.
3. Track:
   - GC alloc/frame
   - spikes over 25ms
   - CPU main thread over 18ms
4. Adjust pool warmups and culling distances by scene.
