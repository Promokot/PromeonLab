# Rig Slice B — Runtime Proxy Rig (Parity) Design

> **Status:** Готов к ревью. Продолжение `2026-06-01-rig-in-entity-pipeline-design.md` (Срез B).

**Goal:** Импортированный (и встроенный) риг достигает паритета с нынешним `Crush Dummy`: прокси-кости строятся в рантайме, выбираются покостно с outline, bone-режим через панели, `BoneFollower` (гизмо двигает прокси → кость следует). Достигается распилом `PromeonProxyRigBuilder` на постройку (фабрика) + рантайм-координацию (`ProxyRigRuntime`).

**Ключевое решение — Подход A (прокси всегда строятся в рантайме, и builtin, и импорт):** единый путь постройки; `Crush Dummy` становится обычным префабом скин-меша (запечённых прокси нет). Это убирает `RegenerateMissingProxyMeshes`, `OnEnable`-репопуляцию из детей и зависимость от эдитор-bake-тулзы (Срез C может ждать до конца).

**Поправка к прежней спеке:** `RigRuntime` НЕ ретайрится. Он scene-scoped (`SceneContext.Rig`), используется `BoneInspectorPanel`/`IkWizardPanel` для ручного риггинга in-scene. Сохраняем, но `ApplyDefinition` перенаправляем на фабрику.

---

## Компоненты

**НОВОЕ:**
- `RigEntityFactory.BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, ProxyRigConfig cfg)` — постройка прокси (вынута из `PromeonProxyRigBuilder.BuildProxyHierarchy`/`BuildProxyNode` + мешестроители). Источник костей: `boneNames` смаплены на живые `rigRoot`-`SkinnedMeshRenderer.bones` по имени (импорт); `null` → все `smr.bones` (builtin / ручной риггинг). Навешивает `ProxyRigRuntime` на `rigRoot` и биндит ему список прокси. No-op при отсутствии скелета/0 костей. Поведение Crush Dummy не меняется (все кости).
- `ProxyRigRuntime : MonoBehaviour` (на руте рига) — рантайм-координация: DI `Construct(EventBus, OutlineConfig)`; подписка `SelectionChanged` → `ApplyBoneOutlineColors`; `SetVisualsEnabled(bool)`; `SetBonesInteractive(bool)` (bone-режим: активация прокси, чистка стека outline-материалов, re-assert `OutlineMode`/`RenderPriority`, re-tag слоя BoneProxies, инверсия рут-коллайдера). Рут-коллайдер достаётся ЛЕНИВО (`GetComponent<Collider>` в `SetBonesInteractive`/`OnEnable`), т.к. реестр добавляет его после постройки. БЕЗ `RegenerateMissingProxyMeshes`/`OnEnable`-репопуляции.
- `ProxyRigConfig` (ScriptableObject, суффикс Config): `Material BoneMaterial`, `float BoneWidth = 0.06`, `bool UseConvexCollider = true`. Регистрируется в Root (`[SerializeField]` на `RootLifetimeScope` + `RegisterInstance` с null-guard). Цвета outline берутся из существующего `OutlineConfig` (`BoneColor`/`BoneSelectedColor`) — в Config НЕ дублируем. Дефолтный `.asset` создаётся с текущими значениями (bone-материал переносится с `RigRuntime`/Crush Dummy).

**ПЕРЕПОДКЛЮЧАЕМ (сохраняем):**
- `RigRuntime.ApplyDefinition` — вместо `AddComponent<PromeonProxyRigBuilder>()` + `Rebuild()` зовёт `RigEntityFactory.BuildProxyRig(...)` + `IObjectResolver.InjectGameObject(root)`. `BuildFromSkinnedMesh` → через `RigDefinitionExtractor` (из Среза A). `RigRuntime` получает зависимость `RigEntityFactory` (scene→root, разрешено). → `BoneInspectorPanel`/`IkWizardPanel` работают через новый путь.
- `InspectorPanel`/`OutlinerPanel` — детект «is-rig» (`GetComponentInChildren<…>`) + `InspectorPanel.SetBonesInteractive`: `PromeonProxyRigBuilder` → `ProxyRigRuntime`.

**УДАЛЯЕМ (в B3):**
- `PromeonProxyRigBuilder` + `PromeonProxyRigBuilderEditor` + `PromeonProxyRigBuilderTests`.
- `BoneProxy.cs` — если подтвердится, что нигде не используется (по grep'у — только определение).

**Без изменений:** `BoneFollower`, `BoneSceneNodeMarker`, `RigDefinition`/`BoneRecord`/`IkChainRecord`, `RigSerializer`, `SceneContext.Rig`, `IRigRuntime`.

---

## Потоки

```
ИМПОРТ:  Registry.RestoreAsync → RigEntityBuilder.RestoreAsync(imported):
            go = factory.CreateAsync(glb)
            factory.BuildProxyRig(go, recipe.rig.Bones→имена, cfg)
            return go → Apply(рут-коллайдер/select) → AddNode → InjectGameObject(go)

BUILTIN: RigEntityBuilder.RestoreAsync(builtin):
            go = Instantiate(prefab)               // голый скин-меш
            factory.BuildProxyRig(go, null→все smr.bones, cfg)
            return go → (как выше)

РУЧНОЙ:  панель → RigRuntime.ApplyDefinition(def, smr):
            factory.BuildProxyRig(smr-root, def.Bones→имена, cfg) → InjectGameObject(root)

НЕТ СКЕЛЕТА: BuildProxyRig no-op → объект статичный (graceful, фолбэк Среза A)
```

**Инверсия управления (как сегодня):** гизмо двигает прокси-GO → `BoneFollower.Tick` (LateUpdate на кости) копирует `proxy.local`→`bone.local` (scale как множитель rest-scale). Прокси = выбираемое/анимируемое представление, скелет следует.

**Bone-режим:** `InspectorPanel` → `ProxyRigRuntime.SetBonesInteractive(true)` гасит рут-коллайдер (выбор целиком) и включает прокси-коллайдеры (слой BoneProxies, покостный выбор); `false` — наоборот.

**DI прокси:** рекурсивный `InjectGameObject(rigRoot)` на месте вызова покрывает `ProxyRigRuntime` + `Selectable`/`XRPromeonInteractable` на прокси. Отдельный inject не нужен.

---

## Поэтапная нарезка

**B1 — Параллельное ядро (ничего не ломаем):**
- `ProxyRigConfig` (класс + дефолтный `.asset` с текущими параметрами) + регистрация в Root.
- `RigEntityFactory.BuildProxyRig` (вынести постройку из `PromeonProxyRigBuilder`).
- `ProxyRigRuntime` (вынести рантайм-роль).
- `PromeonProxyRigBuilder` НЕ трогаем — новый код рядом. Тесты на синтетическом скелете.

**B2 — Переключение потребителей:**
- `RigEntityBuilder.RestoreAsync` (импорт + builtin) строит прокси через фабрику.
- `RigRuntime.ApplyDefinition` → фабрика.
- `InspectorPanel`/`OutlinerPanel` (детект + `SetBonesInteractive`) → `ProxyRigRuntime`.
- После B2 `PromeonProxyRigBuilder` никем не создаётся.

**B3 — Растворение:**
- Удалить `PromeonProxyRigBuilder` (+`*Editor`/`*Tests`).
- Снять с префаба `Crush Dummy` запечённый `ProxyRig` + компонент.
- Удалить `BoneProxy` если мёртв.
- Убрать `RigRuntime.SetMaterial`/`_boneMaterial` SerializeField (параметры теперь в `ProxyRigConfig`).
- Финальный прогон + VR.

---

## Обработка ошибок

- Нет `SkinnedMeshRenderer`/0 костей → `BuildProxyRig` no-op → статичный объект (не падаем).
- `ProxyRigConfig.BoneMaterial == null` → warning (как сегодня), прокси видны силуэтом outline.
- Стек outline-материалов (QuickOutline дублирует `outlineMask`/`outlineFill` на `OnEnable`) — чистка в `ProxyRigRuntime.SetBonesInteractive` перед включением Outline (перенос текущей логики).

## Тестирование

EditMode (`_App.Tests`); glTFast/префабы — VR/руками.
- `RigEntityFactory.BuildProxyRig` на синтетической иерархии костей (GO-кости): создаёт `ProxyRig` + N прокси с компонентами (`SceneNode`/`BoneSceneNodeMarker`/`Selectable`/`XRPromeonInteractable`/коллайдер), `BoneFollower` на каждой кости; маппинг по именам (отсутствующие отсеиваются); no-op при 0 костей.
- `ProxyRigRuntime.SetBonesInteractive(true/false)` на синтетических прокси: тоггл MeshRenderer/Outline/коллайдеров + инверсия рут-коллайдера.
- `ProxyRigRuntime` — цвета outline по `SelectionChanged` (selected vs default из `OutlineConfig`).
- VR/руками: реальный импорт рига, Crush Dummy (после снятия запечённого), bone-режим через панели, `BoneFollower` (гизмо двигает кость), persist через reload/рестарт.

## Долг из Среза A (закрыть здесь)

`RigDefinitionExtractor` ставит `RigDefinition.AssetId = smr.gameObject.name` (имя временного GO). При использовании дескриптора в `BuildProxyRig` AssetId не нужен для маппинга (мапим по `BoneName`), но если понадобится стабильный id — выставлять из `record.Id` в `RigEntityBuilder.BuildAsync`, а не в экстракторе.
