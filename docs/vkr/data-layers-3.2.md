# Слои данных приложения и сериализуемое содержимое (к Рисунку 3.2)

Сверено с кодом 2026-06-05 (Assets/_App/Scripts). Схема: `figures/fig-3.2-v2.png` (.svg рядом).

## Слой A. Библиотеки ассетов (глобальные, общие для всех сцен)

Записи описывают, из чего восстанавливается объект. Два рабочих типа записей, общий контракт – интерфейс `ILabAsset` (Id, DisplayName, Type, Source, SourceRef, Icon, ThumbnailRef, Recipe).

### BuiltinLabAsset – встроенный ассет (НЕ сериализуется в JSON)

Живёт в сборке приложения: записи лежат в ScriptableObject `BuiltinAssetLibrary`, шаблон запекается в эдиторе (`BuiltinRecipeBaker`).

| Поле | Содержимое |
|---|---|
| Id, DisplayName, Type | идентификатор, имя, тип (Object / Rig / Reference) |
| Icon | спрайт для галереи (вместо миниатюры) |
| Prefab | готовый префаб-источник геометрии |
| Image | вход для генерации Reference (иначе игнорируется) |
| TerminalBonesAxis, InvertTerminalBonesAxis | ось концевых костей для Rig-записей |
| Recipe | запечённый AssetEntityRecipe |

SourceRef = null (файла-источника нет – геометрия из префаба), ThumbnailRef = null (используется Icon).

### ImportedLabAsset – импортированный ассет (сериализуется)

Файл: `asset-libraries/imported-lib.json` – обёртка `{schemaVersion: 2, entries: []}`.

| Поле | Содержимое |
|---|---|
| _id, _displayName, _type | идентификатор, имя, тип |
| _sourceRef | относительная ссылка на копию источника: `asset-libraries/sources/{Id}.glb/.gltf/.png/.jpg/.jpeg` |
| _thumbnailRef | относительная ссылка на миниатюру: `asset-libraries/thumbnails/{Id}.png` (изображения переиспользуют источник) |
| _recipe | AssetEntityRecipe, построенный при импорте |

### SavedLabAsset / saved-lib.json

Записи персистентны (файл пишется), но поток сохранения/спавна из Saved не реализован – на схему не вынесен.

### AssetEntityRecipe – шаблон восстановления (вложен в запись, сериализуется вместе с ней)

`{schemaVersion: 1, type, selectable, interactionLayer, colliderKind, colliderCenter, colliderSize, boneColliderDepth, spawnOffset, referenceAspect, referenceBottomGap, referenceTwoSided, rig}`

Вложенное описание скелета (только для Rig):

- `RigDefinition {SchemaVersion: 1, AssetId, TerminalBonesAxis, InvertTerminalBonesAxis, Bones[], IkChains[]}`;
- `BoneRecord {BoneName, TranslationLocked}`;
- `IkChainRecord {RootBone, EndBone, PoleBone, Weight}` – сериализуются, но солвер IK не реализован (данные впрок).

## Слой B. Сцена – `scenes/{SceneId}/scene.json` (schemaVersion 3)

- `SceneData {SchemaVersion = 3, SceneId, DisplayName, CreatedAt, Nodes[]}`;
- `NodeData {NodeId, AssetRef, Position, Rotation (кватернион), Scale, DisplayName, ParentNodeId, BonePoses[]}`;
- `AssetRef {Source, AssetId}` – Source: 0 = Builtin, 1 = Imported, 2 = Saved; пара выбирает библиотеку и запись в ней;
- `BonePose {BoneName, LocalPosition, LocalRotation, LocalScale}` – позы костей хранятся внутри записи модели, отдельных файлов поз нет;
- `ParentNodeId` – ссылка на родительскую ноду той же сцены (пустая строка = корень).

## Слой C. Анимация – `scenes/{SceneId}/animation.json` (schemaVersion 2)

- `SceneAnimationData {schemaVersion = 2, Fps, Containers[]}` – частота кадров общая для сцены;
- `ActionContainer {OwnerNodeId, Fps (резерв при загрузке), TotalFrames, Interpolation (Linear/Stepped), Loop, Tracks[]}`;
- `AnimTrackData {NodeId, Keys[]}` – NodeId указывает на сам объект либо на кость (составной идентификатор `bone:{нода}:{кость}`);
- `AnimKeyData {Frame, Position, Rotation, Scale}` – ключ хранит полный набор трансформаций, дорожка не разделяется на кривые по свойствам.

## Связи между слоями

- `NodeData.AssetRef → ILabAsset.Id` – узел сцены ссылается на запись библиотеки (Builtin или Imported), геометрия в сцене не дублируется;
- `ImportedLabAsset.SourceRef / ThumbnailRef → файлы` в `sources/` и `thumbnails/`;
- `ActionContainer.OwnerNodeId → NodeData.NodeId` – контейнер действия принадлежит ноде;
- `AnimTrackData.NodeId → объект или bone:-нода` – адресат дорожки;
- `NodeData.ParentNodeId → NodeData` – иерархия внутри сцены.

## Что сознательно НЕ сериализуется

- записи BuiltinLabAsset (поставляются в сборке), Icon и Prefab – ссылки Unity;
- прокси-риги и селекторные коллайдеры – строятся заново при каждом восстановлении по Recipe;
- кеш сцен в памяти, временная сцена песочницы `"__sandbox__"`.
