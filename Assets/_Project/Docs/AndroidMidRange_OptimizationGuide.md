# Android Mid-Range Optimization Guide (Unity 6.4 + URP)

## Target
- Device class: Android mid-range (4-8 GB RAM, Adreno/Mali mid-tier)
- Stability target: 60 FPS where possible, graceful fallback to 45 FPS
- Feel target: consistent mobile input/HUD responsiveness under combat load

## 1) URP Asset Recommendations
Use one mobile-first URP asset and avoid desktop defaults.

Recommended values:
- Render Scale: 0.85-0.92 (start at 0.90)
- HDR: Off
- Opaque Texture: Off
- Depth Texture: Off unless a shader requires it
- Soft Particles: Off
- MSAA: 0x or 2x (prefer 0x for heavy scenes)
- SRP Batcher: On (Project Settings -> Graphics)

If you need per-scene tuning, keep one URP baseline asset and adjust only `renderScale` by profile.

## 2) Quality by Tiers
Define 3 runtime presets using `MobileDeviceProfile` assets:
- Low tier (<= 3 GB RAM)
- Mid tier (4-6 GB RAM)
- High tier (>= 8 GB RAM)

Suggested values:
- Low: 45 FPS, renderScale 0.82, shadows short/cheap, postprocess off
- Mid: 60 FPS, renderScale 0.90, moderate shadows, postprocess mostly off
- High: 60 FPS, renderScale 0.95, better shadows, selective postprocess

## 3) Shadows
Keep shadows readable but cheap.

Recommended:
- Shadow Quality: `All` for mid/high, `HardOnly` or lower for low tier
- Shadow Distance: 18-26
- Shadow Resolution: Medium
- Disable extra shadow casters on non-critical props
- Bake static lighting when possible

## 4) Postprocess
Postprocessing is usually the first cost spike in mid-range Android.

Recommended baseline:
- Off globally for combat scenes
- If enabled, allow only lightweight effects (subtle color adjustments)
- Avoid heavy bloom/DoF/motion blur in gameplay camera

Runtime toggle is handled by `MobilePerformanceConfigurator` via camera data.

## 5) Texture Compression
No art rework required, only import/settings.

Recommended Android import:
- UI/icons: ASTC 6x6
- World/FX: ASTC 8x8
- UI mipmaps: Off
- World mipmaps: On
- Master Texture Limit: 0 (mid/high), 1 (low tier if memory pressure)

Use existing tools:
- MuLike -> Performance -> Content -> Apply Android Texture Compression (Selected)
- MuLike -> Performance -> Content -> Create Mobile Sprite Atlases

## 6) Batching
- SRP Batcher: On
- Static batching for non-moving world geometry
- Dynamic batching: only if it helps your actual meshes/materials (verify in profiler)
- Minimize material variants and runtime material instancing in hot paths

## 7) Target Frame Rate
- Mobile: `Application.targetFrameRate = 60`
- Fallback: 45 under sustained thermal/CPU pressure
- VSync on mobile: Off (`QualitySettings.vSyncCount = 0`)

Managed by `MobilePerformanceConfigurator` + profile.

## 8) GC Spike Mitigation
- Pool transient gameplay objects (already partially done in project)
- Avoid frequent string allocations in Update/UI loops
- Prefer event-driven UI updates over every-frame layout/text updates
- Optional scheduled GC only during safe windows (enabled via profile)
- Enable incremental GC in Player Settings when available

## 9) Safe Area Setup
Use existing `HudSafeAreaFitter` consistently:
- Add `MobileSafeAreaBootstrap` in boot/world scene
- It ensures root HUD canvases have a safe-area fitter if missing
- Keep one safe-area root per HUD canvas to avoid double fitting

## 10) Canvas/UI Rebuild Control
To reduce rebuild/raycast overhead:
- Split dynamic and static UI into separate canvas roots
- Disable raycastTarget on decorative graphics
- Keep frequent text updates isolated from static UI
- Use `MobileCanvasPerformanceConfigurator` on HUD root
- Keep `Canvas.pixelPerfect` off on mobile HUD

## Runtime Scripts Added
- `Assets/_Project/Scripts/Performance/Runtime/MobileDeviceProfile.cs`
- `Assets/_Project/Scripts/Performance/Runtime/MobilePerformanceConfigurator.cs`
- `Assets/_Project/Scripts/UI/MobileHUD/MobileSafeAreaBootstrap.cs`
- `Assets/_Project/Scripts/Performance/UI/MobileCanvasPerformanceConfigurator.cs`

## Player Settings (Android) Recommended
- Scripting Backend: IL2CPP
- Target Architectures: ARM64 (required), ARMv7 optional only if needed
- API Compatibility: .NET Standard 2.1
- Managed Stripping Level: Medium (test High only if safe)
- Incremental GC: Enabled
- Texture Compression: ASTC
- Graphics APIs: Vulkan first, GLES3 fallback (validate per device matrix)
- Optimize Mesh Data: On
- Strip Engine Code: On

## Input and Orientation
- Active Input Handling: Input System Package (or Both only during migration)
- Supported Orientation:
  - Landscape Left/Right enabled
  - Portrait disabled for combat-heavy HUD unless UI is designed for it
- Disable auto-rotation into unsupported layouts

## Internal Publishing Checklist (Android)
- [ ] IL2CPP enabled
- [ ] ARM64 enabled
- [ ] Stripping validated (no missing runtime refs)
- [ ] Incremental GC enabled
- [ ] Input handling validated on touch devices
- [ ] Orientation lock validated (landscape flow)
- [ ] URP mobile profile assigned
- [ ] MobileDeviceProfile assets assigned in configurator
- [ ] Safe area bootstrap active in HUD scenes
- [ ] Canvas optimization pass applied
- [ ] Profiling run on at least 2 mid-range physical devices
- [ ] 20-minute soak test (combat + movement + HUD + networking)
- [ ] Thermal/perf degradation validated (fallback FPS behavior)

## Suggested Scene Wiring
1. Add `MobilePerformanceConfigurator` to persistent bootstrap object.
2. Assign low/mid/high `MobileDeviceProfile` assets.
3. Add `MobileSafeAreaBootstrap` to HUD root or bootstrap scene object.
4. Add `MobileCanvasPerformanceConfigurator` to HUD canvas root.
5. Run playtest on device and tune only profile values first.
