# Mobile Performance Pass (Unity 6 URP, Android Mid-Tier)

## Objetivo de esta pasada
- Estabilizar 60 FPS (o fallback 45/30 en combate masivo).
- Mantener draw calls y overdraw del HUD dentro de presupuesto.
- Evitar picos por instantiate/destroy con pooling.
- Reducir coste de animators y render de entidades lejanas.
- Estandarizar profiling en Editor para que el equipo itere rapido.

## Presupuestos recomendados (escena combate normal)
- Main camera batches: <= 220
- Main camera setpass: <= 120
- HUD batches: <= 45
- Triangles visibles: <= 250k (pico <= 400k)
- Texturas en memoria (frame estable): <= 500 MB en gama media
- CPU frame: <= 16.6 ms para 60 FPS
- GPU frame: <= 16.6 ms para 60 FPS

## Reglas de implementacion
1. Pooling obligatorio para entidades remotas, FX de skills, drops y proyectiles.
2. No usar Destroy en loops de gameplay para objetos reciclables.
3. Toda entidad remota debe tener DistanceLodController + DistanceCullingController.
4. Animator en NPC/monstruo lejano: culling agresivo.
5. HUD: separar capas criticas (HP/MP/skills) de capas decorativas y reducir raycast targets.
6. UI de cooldown/stats no actualizar cada frame si no cambia estado.
7. Carga de contenido runtime: Addressables-first; fallback Resources solo en dev.
8. Texturas de mundo en ASTC (Android), con mipmaps y sin resoluciones 4k innecesarias.
9. Medir en device real (no solo Game View) antes de cerrar cada slice.
10. Cualquier prefab nuevo debe declarar key de pool y presupuesto esperado.

## Scripts incluidos en esta pasada
- Pooling manager: Assets/_Project/Scripts/Performance/Pooling/MobilePoolManager.cs
- Entity recycler: Assets/_Project/Scripts/Performance/Pooling/EntityRecycler.cs
- Factory pooled (ejemplo): Assets/_Project/Scripts/Gameplay/Entities/PooledEntityViewFactory.cs
- LOD por distancia: Assets/_Project/Scripts/Performance/Rendering/DistanceLodController.cs
- Culling por distancia: Assets/_Project/Scripts/Performance/Rendering/DistanceCullingController.cs
- Animator optimization: Assets/_Project/Scripts/Performance/Rendering/AnimatorOptimizationProxy.cs
- UI throttling util: Assets/_Project/Scripts/Performance/UI/UiUpdateThrottler.cs
- Overdraw HUD optimizer: Assets/_Project/Scripts/Performance/UI/HudOverdrawOptimizer.cs
- Addressables loader wrapper: Assets/_Project/Scripts/Performance/Content/AddressablesContentLoader.cs
- Render budget monitor: Assets/_Project/Scripts/Performance/Profiling/MobileRenderBudgetMonitor.cs
- Editor toolkit: Assets/_Project/Scripts/Tools/Editor/MobilePerformanceToolkitWindow.cs

## Checklists concretos

### A) Pooling (entidades, FX, drops)
- [ ] Cada prefab spawnable tiene PoolableObject.
- [ ] El pool key esta documentado y no colisiona.
- [ ] Warmup configurado para pico esperado de combate.
- [ ] No quedan Instantiate/Destroy directos en ruta caliente.
- [ ] EntityRecycler devuelve correctamente al pool en despawn.

### B) Draw calls y render
- [ ] Se usa MobileRenderBudgetProfile con objetivos del mapa.
- [ ] Batches y SetPass dentro de presupuesto en combate normal.
- [ ] Materiales duplicados se consolidan.
- [ ] Transparencias grandes se minimizan.

### C) Overdraw HUD
- [ ] Decorative graphics con raycastTarget desactivado.
- [ ] Canvas no critico ocultable por CanvasGroup.
- [ ] Animaciones UI pesadas desactivadas fuera de combate.
- [ ] Texto/íconos actualizan por evento o throttled.

### D) LOD + culling
- [ ] Jugadores/monstruos tienen LOD simple (high/low).
- [ ] Culling por distancia aplicado a renderers y behaviours costosos.
- [ ] Distancias validadas por tipo de entidad y mapa.

### E) Animator
- [ ] CullingMode correcto por distancia.
- [ ] Root motion desactivado donde no aporte.
- [ ] Estado idle no dispara transiciones caras.

### F) Addressables
- [ ] Grupo de Addressables separado por dominio (Entities/FX/UI).
- [ ] Cargas asíncronas sin bloquear hilo principal.
- [ ] Releasing correcto de handles/instancias.
- [ ] Fallback Resources solo para desarrollo.

### G) Texturas mobile-friendly
- [ ] Android override a ASTC (o ETC2 fallback) según dispositivo.
- [ ] UI atlases comprimidos y con tamaños razonables.
- [ ] Mipmaps activos en world textures, off en UI cuando aplique.
- [ ] Sin normal maps 2K/4K innecesarios en assets pequeños.

### H) Profiling en editor
- [ ] Profiler + Frame Debugger revisados por escena objetivo.
- [ ] Captura de baseline guardada antes de cambios grandes.
- [ ] Regression check tras cada feature de combate/UI.

## Notas Addressables
Si el package com.unity.addressables no esta instalado, AddressablesContentLoader cae en Resources.LoadAsync para no romper flujo. Recomendado instalar y migrar gradualmente prefabs de alto churn (entidades/FX) a Addressables.
