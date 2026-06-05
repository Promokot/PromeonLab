# Слои данных PromeonLab: что и куда сериализуется

Справка к Рисунку 3.2 (figures/fig-3.2-v2.png). Сверено с кодом 2026-06-05
(AssetBrowser/, StorageCore/, RigBuilder/, Animation/Data).

## Слой 1. Библиотеки ассетов (глобальные, переиспользуются всеми сценами)

Два типа записей с общим шаблоном восстановления.

### BuiltinLabAsset — встроенный ассет
- Где сериализуется: ассет приложения `BuiltinAssetLibrary` (ScriptableObject, список записей);
  в пользовательское хранилище НЕ пишется, поставляется в сборке.
- Поля: `Id`, `DisplayName`, `Type` (Object | Rig | Reference), `Icon` (спрайт галереи),
  `Prefab` (готовая геометрия), `Image` (вход для Reference), `TerminalBonesAxis` +
  `InvertTerminalBonesAxis` (ось концевых костей для Rig), `Recipe`.
- `SourceRef = null`, `ThumbnailRef = null` (иконка задаётся в инспекторе).
- `Recipe` запекается на этапе разработки (BuiltinRecipeBaker, эдитор-код).

### ImportedLabAsset — импортированный ассет
- Где сериализуется: `asset-libraries/imported-lib.json` — обёртка
  `LibraryJson { schemaVersion = 2, entries: ImportedLabAsset[] }` (JsonUtility).
- Поля: `Id`, `DisplayName`, `Type`, `SourceRef` (относительный путь к копии исходника в
  `asset-libraries/sources/{id}.{ext}`), `ThumbnailRef` (миниатюра в
  `asset-libraries/thumbnails/{id}.png`), `Recipe`.
- `Recipe` строится один раз при импорте (ImportPipeline).

Примечание: третья библиотека (saved-lib.json, SavedLabAsset) сериализуется тем же способом,
но поток сохранения из сцены не реализован, Recipe у таких записей пуст — на схему не вынесена.

### AssetEntityRecipe — общий шаблон восстановления (вложен в обе записи)
- `schemaVersion = 1`, `type`;
- взаимодействие: `selectable`, `interactionLayer`;
- коллайдер: `colliderKind` (Box | BoneBoxes | None), `colliderCenter`, `colliderSize`,
  `boneColliderDepth` (глубина селекторных боксов скелета);
- размещение: `spawnOffset` (один раз при первом спавне);
- референсы: `referenceAspect`, `referenceBottomGap`, `referenceTwoSided`;
- скелет: `rig: RigDefinition` (только для Type = Rig; проверка HasRig — по числу костей).

### RigDefinition (внутри Recipe)
- `SchemaVersion = 1`, `AssetId`, `TerminalBonesAxis` + `InvertTerminalBonesAxis`,
- `Bones[]: BoneRecord { BoneName, TranslationLocked }`,
- `IkChains[]: IkChainRecord` (сериализуются, солвером пока не используются).

## Слой 2. Сцена (`scenes/{SceneId}/scene.json`, JsonUtility)

### SceneData
- `SchemaVersion = 3` (миграция v1/v2 → v3 в SceneSerializer.Deserialize),
- `SceneId` (8 символов GUID), `DisplayName`, `CreatedAt`, `Nodes[]`.

### NodeData (запись ноды)
- `NodeId`,
- `AssetRef { Source: AssetSource, AssetId }` — ссылка на запись библиотеки: по `Source`
  выбирается библиотека (Builtin | Imported | Saved), по `AssetId` — запись; геометрия в
  сцене НЕ хранится,
- `Position`, `Rotation` (кватернион), `Scale`,
- `DisplayName`,
- `ParentNodeId` — ссылка на родительскую NodeData (пустая строка = корень),
- `BonePoses[]` — только для ригов (схема v3).

### BonePose
- `BoneName` — соответствие кости из `RigDefinition.Bones` по имени,
- `LocalPosition`, `LocalRotation`, `LocalScale` — локальная трансформация прокси-кости.

## Слой 3. Анимация (`scenes/{SceneId}/animation.json`, JsonUtility)

### SceneAnimationData
- `schemaVersion = 2` (несовместимые версии не загружаются и не затираются),
- `Fps` — частота кадров, общая для сцены,
- `Containers[]`.

### ActionContainer (одно действие одного объекта)
- `OwnerNodeId` → `NodeData.NodeId` владельца,
- `Fps` — резерв на случай загрузки данных без общесценовой частоты,
- `TotalFrames`, `Interpolation` (Linear | Stepped), `Loop`,
- `Tracks[]`.

### AnimTrackData (дорожка одной ноды)
- `NodeId` — сам объект либо кость; кость адресуется составным идентификатором
  `bone:{идентификатор ноды}:{имя кости}`,
- `Keys[]` (упорядочены по кадру).

### AnimKeyData (ключ)
- `Frame`, `Position`, `Rotation`, `Scale` — полный набор трансформаций одним ключом
  (дорожка не делится на кривые по свойствам).

## Сквозные связи (пунктир на схеме)

1. `NodeData.AssetRef` → запись библиотеки (по `Source` + `AssetId`).
2. `ImportedLabAsset.SourceRef / ThumbnailRef` → файлы `sources/` и `thumbnails/`
   (пути относительные к persistentDataPath).
3. `NodeData.ParentNodeId` → другая `NodeData` (иерархия).
4. `BonePose.BoneName` ↔ `RigDefinition.Bones[].BoneName` (позы применяются по именам).
5. `ActionContainer.OwnerNodeId` → `NodeData.NodeId`.
6. `AnimTrackData.NodeId` → объект или кость (`bone:{нода}:{кость}`).
