# Android Build Checklist (Internal Release)

## Build Core
- [ ] Platform: Android
- [ ] Build Type: Release (non-development)
- [ ] Scripting Backend: IL2CPP
- [ ] Target Architectures: ARM64
- [ ] Strip Engine Code: On
- [ ] Managed Stripping Level: Medium (or validated High)

## Performance Runtime
- [ ] `MobilePerformanceConfigurator` present in startup path
- [ ] Mid-range `MobileDeviceProfile` assigned
- [ ] VSync forced off in mobile runtime
- [ ] Target FPS set to 60, fallback to 45 validated
- [ ] URP mobile values applied (renderScale/HDR/shadows/postprocess)

## Rendering/Assets
- [ ] Texture compression ASTC applied for Android
- [ ] SRP Batcher enabled
- [ ] Static batching baked where applicable
- [ ] Shadow distance and resolution validated on real devices
- [ ] Postprocess cost validated (prefer minimal/off during combat)

## UI/HUD
- [ ] Safe area fitting active (`HudSafeAreaFitter` / `MobileSafeAreaBootstrap`)
- [ ] Decorative graphics raycast disabled where possible
- [ ] Dynamic UI canvases isolated from static canvases
- [ ] No expensive per-frame layout rebuild loops

## Input/Orientation
- [ ] Input system configured and touch controls validated
- [ ] Orientation locked to intended gameplay mode (landscape)
- [ ] Notch devices tested (safe area)

## Verification
- [ ] CPU/GPU frame time measured in device profiler session
- [ ] GC spikes checked in 20-minute stress session
- [ ] Combat + movement + HUD + networking tested under load
- [ ] APK/AAB smoke-tested on at least 2 mid-range devices
