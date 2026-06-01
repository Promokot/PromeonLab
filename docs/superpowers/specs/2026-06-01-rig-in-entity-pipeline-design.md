# Rig in the Entity Pipeline — Design

> **Status:** Готов к ревью.

**Goal:** Уложить рантайм-риг в ту же двухфазную модель, что Object/Reference — «импорт→билдер→рецепт» и «сцена→спавн по типу (instantiate | restore-from-recipe)» — разнеся нынешний `PromeonProxyRigBuilder`, который сливает постройку, рантайм-координацию и bake в одном классе.

**Approach:** Подход 1 — общее «ядро постройки» (DRY между bake и runtime-restore) + тонкий персистентный рантайм-компонент + эдитор-bake-тул.

**Критерий успеха:** Целимся в полный паритет импортированного рига с builtin (`Crush Dummy`): прокси-кости, выбор кости, анимация, outline. Ближайший срез допускает минимум (статичный меш + выбор рига целиком); архитектурные швы закладываются под паритет.

---

## Роли и компоненты

Нынешний клубок (`PromeonProxyRigBuilder` + `RigRuntime` + bake) разносится на чёткие единицы:

| # | Единица | Тип | Ответственность | Зависит от |
|---|---|---|---|---|
| 1 | `RigEntityBuilder` | DI-сервис (есть) | **Шов.** `BuildAsync`: читает скелет glTF → пишет `RigRecipe`. `RestoreAsync`: builtin→`Instantiate`, imported→зовёт фабрику | `RigEntityFactory`, `GltfModelLoader`, `AssetSourceStore` |
| 2 | `RigEntityFactory` | DI-сервис (нов.) | **Ядро постройки.** `BuildProxyRig(skeleton, recipe) → поддерево прокси-костей` (меши/коллайдеры/маркеры). Зовётся И из restore (рантайм), И из эдитор-bake | `GltfModelLoader`, конвенции прокси |
| 3 | `ProxyRigRuntime` | MonoBehaviour на риге (нов.) | **Рантайм-координатор.** follow костей, visibility-toggle, outline на `SelectionChanged`, «это риг»-маркер. Несут И builtin, И imported | `EventBus`, `OutlineConfig` |
| 4 | `RigBakeTool` (≈ нынешний `*Editor`) | Editor-only | Срез 3: зовёт `RigEntityFactory.BuildProxyRig` → запекает префаб → builtin-библиотека | `RigEntityFactory` |

**Судьба старого:**
- `PromeonProxyRigBuilder` **распадается**: постройка → `RigEntityFactory`; рантайм → `ProxyRigRuntime`; bake → `RigBakeTool`. Класс удаляется.
- `RigRuntime` (`BuildFromSkinnedMesh`/`ApplyDefinition`) **ретайрится**: извлечение скелета → в `RigEntityBuilder.BuildAsync`; сборка → в `RigEntityFactory`.
- `BoneProxy`/`BoneFollower`/`BoneSceneNodeMarker`/`RigDefinition`/`BoneRecord`/`RigSerializer` — **остаются** (кирпичики ядра).

## Симметрия по всем трём типам

Каждый тип: **Builder (шов) + Factory (постройка)**. Рантайм-компонент — **только у рига** (Object/Reference статичны, их выделение тянет общий outline-механизм). Bake-тул — на Срез 3, для Object/Reference тривиален.

| Тип | Builder (шов) | Factory (постройка) | Рантайм-компонент | Bake-тул (Срез 3) |
|---|---|---|---|---|
| Object | `ObjectEntityBuilder` | `ObjectEntityFactory` *(тонкий: glTF+capability)* | — | `ObjectBakeTool` |
| Reference | `ReferenceEntityBuilder` | `ReferenceEntityFactory` *(quad; был `ReferenceQuadFactory`)* | — | `ReferenceBakeTool` |
| Rig | `RigEntityBuilder` | `RigEntityFactory` *(прокси-ядро)* | `ProxyRigRuntime` | `RigBakeTool` |

«Упрощённый вид» Object/Reference = тонкая фабрика, без рантайм-компонента; структура та же.

`GltfModelLoader` остаётся низкоуровневым примитивом (glTF→GameObject), используется *внутри* `ObjectEntityFactory`/`RigEntityFactory`; в троицу фабрик не входит.

## Точка схождения (одна дверь перед сценой)

`AssetEntityBuilderRegistry` — диспетчер по `AssetType`; через него проходит и свежий спавн, и reload.

```
ИМПОРТ:  FilePicked → ImportPipeline → Registry.BuildAsync(type)
             → <Type>EntityBuilder.BuildAsync → recipe → library

СПАВН:   AssetSpawner (свежий)  ─┐
         SceneGraph   (reload)  ─┴→ Registry.RestoreAsync(asset)   ← ЕДИНАЯ ТОЧКА
                                       → <Type>EntityBuilder.RestoreAsync
                                            ├ builtin  → Instantiate(prefab)
                                            └ imported → <Type>EntityFactory (+ Rig: вешает ProxyRigRuntime)
                                       → InteractionCapability.Apply   ← общий финал
                                  → AddNode → DI inject
```

**Решение:** общий финал (`InteractionCapability.Apply`) поднимается из каждого билдера **в `Registry.RestoreAsync`** — билдеры делают только тип-специфичную геометрию, «сделать выбираемым + AddNode + inject» = единый хвост в одной точке.

---

## `RigRecipe` и поток данных

**Решение:** все данные ассета — в **одной карточке** (`AssetEntityRecipe`), содержимое разнится по типу; отдельных файлов нет. Для рига карточка встраивает уже существующий `RigDefinition`:

```
AssetEntityRecipe {
   ... (type, selectable, collider*, spawnOffset — общие)
   RigDefinition rig;   // null для не-рига; Bones[] + IkChains[]
}

RigDefinition { int SchemaVersion; string AssetId(игнор при встраивании);
                List<BoneRecord> Bones;        // { BoneName; TranslationLocked }
                List<IkChainRecord> IkChains; } // { RootBone, EndBone, PoleBone, Weight }
```

**Инсайт:** при restore glTF перезагружается → `SkinnedMeshRenderer.bones` уже на месте. Скелет целиком хранить не надо — рецепт хранит только *решения* (какие кости/порядок/флаги/IK), restore **мапит их по имени** на живые трансформы. Чистый build-once/restore-many.

**Поток данных:**
```
ИМПОРТ (RigEntityBuilder.BuildAsync):
   load glTF → найти SkinnedMeshRenderer → собрать RigDefinition (имена костей[, IK])
   → recipe.rig = def   (логика переезжает сюда из RigRuntime.BuildFromSkinnedMesh)

RESTORE imported (RigEntityFactory):
   load glTF (меш+скелет) → смапить recipe.rig.Bones на живые smr.bones по имени
   → BuildProxyRig(skeleton, recipe) строит прокси → вешает ProxyRigRuntime
```

**Минимум сейчас:** `recipe.rig` пишется при импорте, но `BuildProxyRig` ещё не зовётся на restore → риг встаёт статичным мешем, выбирается целиком. Описатель уже в рецепте — паритетный срез просто «включает» построение прокси.

`RigSerializer`/`Rigs/rig-{assetId}.json` для импорта больше не нужны (всё в карточке); `RigSerializer` остаётся для эдитор-авторинга/bake при необходимости.

---

## Декомпозиция на срезы

**Срез A (сейчас) — симметрия пайплайна + фундамент рецепта рига. НЕ трогает рантайм-машинерию рига.**
- Троица фабрик: `ReferenceQuadFactory`→`ReferenceEntityFactory`; извлечь `ObjectEntityFactory`; создать `RigEntityFactory` (сервис, пока без `BuildProxyRig`).
- Поднять `InteractionCapability.Apply` в `Registry.RestoreAsync` (единый хвост; билдеры делают только тип-специфичную геометрию).
- `AssetEntityRecipe` += `RigDefinition rig`.
- `RigEntityBuilder.BuildAsync` пишет `recipe.rig` (или null при отсутствии костей); `RestoreAsync` (imported) = статичный меш + выбор целиком.
- `PromeonProxyRigBuilder`/`RigRuntime`/`Crush Dummy`/панели **не трогаем**.

**Срез B (паритет) — рантайм-построение прокси.**
- Наполнить `RigEntityFactory.BuildProxyRig` (постройка из `PromeonProxyRigBuilder`).
- Создать `ProxyRigRuntime` (рантайм-роль из `PromeonProxyRigBuilder`).
- Растворить `PromeonProxyRigBuilder` + ретайр `RigRuntime`; перенавесить is-rig-детект в панелях; перепечь `Crush Dummy`; починить тесты *(= задача #16)*.

**Срез C — bake-тулы** (`*BakeTool`, «Bake to Built-in Library»).

## Обработка ошибок

- `BuildAsync` (rig): нет `SkinnedMeshRenderer`/костей → warning + `recipe.rig = null` → ассет как статичный Object (скин-меш, импортированный *как Object* — валиден, не ошибка).
- `RestoreAsync` (rig): пустой `rig` или ни одна кость не смапилась → graceful fallback на статичный спавн, без исключения.

## Тестирование

EditMode (`_App.Tests`); glTFast вне юнитов (реальная загрузка — VR/руками).

**Срез A:**
- Рецепт round-trip: `AssetEntityRecipe` с заполненным `rig` и с `rig == null` переживает `JsonUtility`.
- Маппинг костей по имени: синтетические трансформы + `RigDefinition.Bones` → корректное сопоставление; отсутствующие имена отсеиваются.
- No-bones fallback: пустой/`null` `rig` → статичный выбираемый объект, без исключения.
- Registry — единый хвост: `RestoreAsync` применяет `InteractionCapability` ровно раз и диспетчит по типу верно.
- Регрессия фабрик: restore Object/Reference после извлечения/переименования даёт прежний результат.

**Срез B/C** — свои тесты в их планах.

---

## Cleanup / долги (вынести в отдельный проход)

- Удалить рудиментарную логику после распада `PromeonProxyRigBuilder` и ретайра `RigRuntime` (ссылки в `OutlinerPanel`/`InspectorPanel` — детект «is-rig» переключить на `ProxyRigRuntime`; `Crush Dummy` префаб; `PromeonProxyRigBuilderTests`).
- Обновить доки: `Assets/_App/Documentation/STRUCTURE.md`, `architecture_context.md`, `CLAUDE.md` (подсистема RigBuilder), мемори `project_asset_import_pipeline`.
