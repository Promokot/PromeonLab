# VKR screenshot — continuous PC-monitor mode

**Status:** Plan
**Branch:** `feature/vkr-screenshot-continuous` from `dev`
**Author:** Claude (brainstormed with user 2026-06-08)
**Scope:** ВКР-only tool extension. Delete with the rest of `ThesisTools/` after thesis defence.

## Goal

Добавить непрерывный поток моно-камеры на ПК-монитор (для записи видео взаимодействия в OBS), управляемый switch'ем в `SettingsPanel → General`. HMD-рендер не трогаем. Y-стилл работает как раньше — независимо от состояния switch'а.

## Architecture summary (approved)

Используем **существующую** `[ThesisScreenshotCamera]` под `Camera Offset/Main Camera`. Одна камера — два независимых сценария:

| Сценарий | Триггер | Что происходит |
|---|---|---|
| **Стилл** | Y (клава/контроллер) | как сегодня + ветка «continuous уже включён → читаем кадр напрямую из persistent RT» |
| **Непрерывный поток на ПК** | Switch в Settings | `camera.enabled=true`, `targetTexture=_persistentRt`, runtime ScreenSpace-Overlay Canvas с full-screen `RawImage(texture=_persistentRt)` поверх ПК-mirror'а |

Канва создаётся в рантайме скриптом, в префаб ничего не добавляется кроме toggle-row в General-табе. Default: **OFF**, без PlayerPrefs (включается каждый раз руками — намеренно, без surprise-GPU-нагрузки в обычном режиме).

## Files

| Изменение | Файл |
|---|---|
| Continuous-логика + RT + overlay Canvas + Y-fast-path | `Assets/_App/Scripts/ThesisTools/ThesisScreenshotCapturer.cs` |
| **Новый**: wiring UI Toggle → capturer | `Assets/_App/Scripts/ThesisTools/ThesisContinuousToggleBinder.cs` |
| **Префаб через MCP**: toggle-row в General-табе SettingsModule + binder-компонент | `Assets/_App/Content/Prefabs/XR/User XR Origin (XR Rig).prefab` |

Никаких изменений в `DefaultControlsProfile.asset` (это не binding, а runtime-setting; не путаем категории).

## Tasks

### T1 — Branch + probe SettingsModule General content
- `git checkout -b feature/vkr-screenshot-continuous` from `dev`
- Через MCP `manage_prefabs get_hierarchy` посмотреть структуру `UserPanel/Center_top/SettingsModule` и понять, какой GO привязан к `_generalContent` (поле SettingsPanel'а)
- Зафиксировать путь/instanceID для T4

### T2 — `ThesisScreenshotCapturer.cs` — continuous core
1. Поля: `bool _continuousMode`, `RenderTexture _persistentRt`, `Canvas _overlayCanvas`, `RawImage _overlayImage`, `Vector2Int _rtSize`
2. Public `void SetContinuousMode(bool on)`:
   - **ON**: ensure fake cam resolved, allocate `_persistentRt` (Screen.width×Screen.height, ARGB32), `cam.targetTexture = _persistentRt`, `cam.stereoTargetEye = None`, `cam.gameObject.SetActive(true)`, `cam.enabled = true`. Build runtime overlay (`new GameObject("[ThesisContinuousOverlay]")` → `Canvas{renderMode=ScreenSpaceOverlay, sortingOrder=32000}` → child `RawImage` anchored stretch, `texture=_persistentRt`). `DontDestroyOnLoad`.
   - **OFF**: `cam.targetTexture = null`, `cam.enabled = false`, `cam.gameObject.SetActive(false)`, `Destroy(_overlayCanvas.gameObject)`, `_persistentRt.Release(); Destroy(_persistentRt)`.
3. Update — если `_continuousMode && (Screen.width != _rtSize.x || Screen.height != _rtSize.y)`, re-allocate RT, переприсвоить и `_overlayImage.texture`.
4. Refactor `Capture()`:
   - extract `ReadRTToFile(RenderTexture, string)` из текущего `RenderFakeCamToFile`
   - if continuous ON → `ReadRTToFile(_persistentRt, path)` (no manual Render)
   - else → текущий путь (SetActive(true)→Render→SetActive(false))
5. OnDestroy — гарантировать `SetContinuousMode(false)` чтобы не утечь RT/Canvas

### T3 — `ThesisContinuousToggleBinder.cs` — wiring
```
MonoBehaviour
  [SerializeField] Toggle _toggle
  
  Start():
    cap = FindObjectsByType<ThesisScreenshotCapturer>(...).FirstOrDefault()
    if (cap == null) { Debug.LogWarning(...); return }
    _toggle.SetIsOnWithoutNotify(false)   // default OFF
    cap.SetContinuousMode(false)
    _toggle.onValueChanged.AddListener(cap.SetContinuousMode)
    
  OnDestroy():
    _toggle.onValueChanged.RemoveListener(...)
```
Логика отдельным файлом, чтобы при удалении ВКР-инструмента уйти одной папкой `ThesisTools/`.

### T4 — Prefab edit через MCP
1. `manage_prefabs open_prefab_stage` на `User XR Origin (XR Rig).prefab`
2. Внутри General-content GO (из T1):
   - `manage_gameobject create` дочерний `ThesisToggleRow` с HorizontalLayoutGroup
   - дочерний `TMP_Text` с label «Камера на ПК (ВКР)»
   - дочерний стандартный Unity Toggle (или Button-с-чекбоксом — посмотреть по существующему стилю проекта в T1)
   - `manage_components add` `ThesisContinuousToggleBinder` на `ThesisToggleRow`, прокинуть ссылку на Toggle
3. `save_prefab_stage` + `close_prefab_stage`

### T5 — Compile + tests
- `mcp__unityMCP__refresh_unity` scripts/request
- `mcp__unityMCP__read_console` filter ThesisScreenshot / CS-errors
- `mcp__unityMCP__run_tests` EditMode — ожидаемо 247/250 (те же 3 PathProvider Windows-pre-existing)

### T6 — Live verify (за пользователем)
- Editor Play Mode → надеть шлем → User Panel → Settings → General → flip toggle ON
- ПК-окно (Game view) показывает моно-картинку без артефакта левого глаза
- Y нажат → PNG в `Pictures\Screenshots\` сохранён
- Toggle OFF → game view возвращается к HMD-mirror'у (левый глаз)
- HMD-рендер всё это время идёт ровно, без артефактов

### T7 — Commit + merge
- 1 коммит: «Add continuous PC-monitor mono camera feed (Settings toggle, RT-blit overlay)»
- Мерж в `dev` через `--no-ff` (по образцу прошлой ветки)
- Удалить `feature/vkr-screenshot-continuous`

## Verification matrix

| Acceptance | Кто/как |
|---|---|
| Y сохраняет PNG в обоих режимах switch'а | user, manual |
| Toggle ON → game view = моно POV-камера | user, manual |
| Toggle OFF → game view = HMD mirror | user, manual |
| HMD-рендер без артефактов / fps | user, manual |
| Resize Editor game view → RT перевыделяется без ошибок | claude, через лог `[ThesisScreenshot] RT resized to ...` |
| EditMode tests 247/250, те же pre-existing | claude, MCP |
| Удаление папки `ThesisTools/` + child из префаба + строка из ControlsProfile полностью убирает фичу | post-VKR, проверить в [project-thesis-screenshot-tool.md](../../memory check) |

## Risks / known caveats

- **Шлем для тоггла** — user явно выбрал A; для итераций придётся каждый раз надевать. Workaround на будущее: можно вынести дублирующий хоткей (например Shift+Y) — но это вне scope этой итерации.
- **Overlay прячет Unity dev-UI на ПК** — для чистой записи это плюс. Если потребуется временно увидеть Editor-overlay, OFF toggle.
- **GPU cost** — лишний камера-pass per frame в Editor. В Quest standalone никто не включит (нет ПК-вьюера). На Quest Link рендерит ПК-GPU, так что заметить будет сложно.
- **URP camera stacking** — `targetTexture≠null` + `stereoTargetEye=None` должен изолировать камеру от main XR pipeline. Если в реальности будут артефакты — fallback к manual `Camera.Render()` в `LateUpdate` вместо `enabled=true`.

## Memory updates after merge
- Дополнить [project_thesis_screenshot_tool.md](../../../../memory/project_thesis_screenshot_tool.md): добавить раздел про continuous mode + расширить cleanup-recipe (toggle-row в префабе, ThesisContinuousToggleBinder.cs)
