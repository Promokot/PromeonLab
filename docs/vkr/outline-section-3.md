# План оглавления раздела 3 «ТЕХНОЛОГИЧЕСКИЙ РАЗДЕЛ»

Статус: ЧЕРНОВИК НА УТВЕРЖДЕНИЕ (2026-06-04).
Под каждым пунктом: содержание, охватываемые скрипты (Я=ядро, К=контекст), заготовки рисунков/листингов.
Решения брейншторма учтены: IK и тесты не упоминаются; forward-ссылки без номеров; гибридная подача UI.

---

## 3.1 Информационное обеспечение

Открывающий абзац: все данные – в Application.persistentDataPath, приложение автономно.

### 3.1.1 Организация хранения данных
Хранилище и адресация (StorageCore упоминается как имя модуля, без акцента на термине «подсистема»). Состав: AppStorage (Я: CRUD сцен, кеш, sandbox-сессия без записи на диск), PathProvider (Я: единственная точка построения путей). Актуальная структура каталогов: scenes/{id}/ (scene.json, animation.json) и глобальная asset-libraries/ (imported-lib.json, saved-lib.json, sources/, thumbnails/).
Рисунок: схема структуры каталогов (дерево). Листинги: фрагмент PathProvider (актуальный!), перечень методов AppStorage списком.

### 3.1.2 Структуры данных ассетов, сцен и анимации
Данные ассета: запись библиотеки imported-lib.json (рецепт: тип, источник, риг-данные), AssetRef{Source, AssetId} как связь ноды с библиотекой (Я). Данные сцены: SceneData → NodeData (трансформации, AssetRef, родитель, BonePoses) (Я). Анимационные данные: SceneAnimationData → ActionContainer → AnimTrackData → AnimKeyData, режим интерполяции и Loop как поля контейнера (Я – только как ДАННЫЕ, поведение в 3.2).
Листинги (фрагменты): запись рецепта/AssetRef; SceneData/NodeData; фрагмент структуры animation.json. Рисунок: схема вложенности данных (ассет→нода→сцена; контейнер→трек→ключ).
⚠ Поля IkChains в данных НЕ упоминаем (решение F4).

### 3.1.3 Сериализация и версионирование
JsonUtility, принцип schemaVersion во всех файлах (Я). SceneSerializer с инлайн-миграцией v1/v2→v3 (Я) – пример эволюции схемы (добавление поз костей). Неразрушающее чтение animation.json при неподдерживаемой версии (К).
Листинг: фрагмент Deserialize с миграцией.

### 3.1.4 Автосохранение и отслеживание изменений
SceneDirtyTracker (слушает SceneModifiedEvent) (Я), SceneAutoSaver: сохранение при выходе из режима (Я). Событийный механизм упоминается нейтрально («подсистемы обмениваются сообщениями через шину событий; подробно – в программном обеспечении»), без деталей реализации.
Листинг: фрагмент SceneAutoSaver.

---

## 3.2 Программное обеспечение

Открывающий абзац: подход к архитектуре – подсистемы, DI, события; связь с функциональной схемой из 2.1.

### 3.2.1 Архитектурный каркас приложения
EventBus: Publish/Subscribe, типизированные struct-события (Я). VContainer: иерархия RootLifetimeScope → сценные скоупы, время жизни сервисов (Я). SceneContext/SceneContextBinder: доступ к сценным сервисам из app-уровня (Я). AppBootstrap (К).
Режимы: ModeOrchestrator + ModeTransitionGraph (Я), SceneTransitionRunner + HeadFade: затемнение, Single-загрузка, порядок событий ModeExiting→ModeChanged (Я). Sandbox – один абзац (режим-песочница без персистентности).
Рисунки: схема скоупов; схема переходов режимов. Листинги: фрагмент EventBus; фрагмент ModeOrchestrator.

### 3.2.2 Система пространственных интерфейсов
Каркас: SpatialPanel (BodyLocked/WorldFixed/Free, биллборд) (Я); регионная модель PanelRegionRouter + NavBarConfig + RegionMember/RegionNavButton (Я); UserPanel с grip-перемещением (К/Я); VrKeyboard (К).
Краткий перечень панелей приложения одним списком (детали – в функциональных пунктах далее, БЕЗ номеров).
Рисунки: скриншот UserPanel с навбаром; схема регионов. Листинг: фрагмент SpatialPanel или PanelRegionRouter.

### 3.2.3 Главное меню и управление сценами
Перекличка с 2.3. MainMenuPanel, ScenePickerPanel: создание/выбор/удаление сцен (Я). Сценарий открытия: SceneOpenedEvent → построение рантайм-графа SceneGraph/SceneNode (Я – вводим граф сцены здесь). OutlinerPanel: отображение иерархии (Я-лайт).
Рисунки: скриншоты главного меню, пикера сцен, аутлайнера.

### 3.2.4 Библиотеки ассетов и импорт моделей
Перекличка с 2.2/2.3. Три библиотеки (Builtin/Imported/Saved) + AssetRegistry (Я; Saved – честно: записи хранятся, спавн в разработке → или К?). ImportPipeline: FilePicked→выбор импортёра→ImportWizardPanel→копирование источника+запись в библиотеку (Я). glTFast для glTF/GLB, изображения как референсы (Я). ThumbnailRenderer: офскрин-превью при импорте (Я). AssetBrowserPanel: галерея с превью (Я).
Размещение в сцене: AssetSpawnRequested → AssetSpawner → builders (Object/Rig/Reference), принцип build-once/restore-many (Я). Встроенная библиотека: рецепты «выпекаются» на этапе разработки editor-инструментами (BuiltinRecipeBaker – та же измерительная логика, что при импорте; ReferenceImagePrefabGenerator для референс-изображений), поэтому рантайм-спавн встроенных и импортированных ассетов идёт единым конвейером (Я-лайт; честно пометить: инструменты editor-only, в сборку не входят). Для скелетных моделей – словесная отсылка вперёд без номера (решение F3).
Рисунки: скриншоты браузера ассетов, мастера импорта; схема конвейера импорта. Листинги: фрагмент ImportPipeline; фрагмент AssetSpawner/реестра билдеров.

### 3.2.5 Построение скелетных структур
Перекличка с 2.4. RigDefinitionExtractor: извлечение иерархии костей из импортированной модели (Я). RigEntityFabricator.BuildProxyRig: прокси-кости + BoneFollower (Я). ProxyRigRuntime: координация прокси-костей (Я). Персистентность поз – отсылка назад к BonePoses из 3.1.2. Выбор и манипуляция отдельными костями описываются в следующем пункте (взаимодействие).
Рисунки: скриншот модели с прокси-костями; схема прокси-рига. Листинги: фрагменты экстрактора и BuildProxyRig.
⚠ Без IK. Без «ограничителей» (в коде их нет – TranslationLocked не работает; формулировки главы 2 здесь не подтверждаем деталями, обходим).

### 3.2.6 Взаимодействие с объектами и костями
Перекличка с 2.3. SelectionManager + SelectionChangedEvent (Я – селекция вводится здесь). XRPromeonInteractable: tap=select, hold-trigger=rotate, hold-grip=move (Я). Гизмо: GizmoDriver, GizmoDragSession, стратегии Move/Rotate/Scale по осям (Я). InteractionMaskBinder: контекстные маски (К). Подсветка выделения (SelectionVisualSync; QuickOutline – как стороннее) (К). PropertyPanel/InspectorPanel: числовое редактирование трансформаций (Я-лайт). Взаимодействие с костями: реакция ProxyRigRuntime на выделение, BoneEditMode (выбор/манипуляция костью) (Я).
Рисунки: скриншот гизмо на объекте; схема состояний взаимодействия. Листинги: фрагмент XRPromeonInteractable; фрагмент стратегии гизмо.

### 3.2.7 Создание и воспроизведение анимации
Перекличка с 2.5. AnimationAuthoring: CRUD ключей на треках выбранного контейнера (Я). AnimationClipBaker: трек→AnimationClip, тангенты Linear/Stepped (Я). Транспорт AnimationClock: скраб, play/pause, fps, single-shot (Я). AnimationPlaybackSampler: семплирование, дробная позиция при воспроизведении, фоновые Loop-контейнеры (Я). AnimatorPanel + view-модули (Toolbar/Transport/Ruler/Playhead/EmptyState): таймлайн (Я). AnimationStorage – отсылка назад к 3.1. Clipboard-функции (К, одним предложением или игнор – решить при написании).
Рисунки: скриншот таймлайна; схема data-flow «ключи→клип→семплирование». Листинги: фрагменты ClipBaker и PlaybackSampler.

### 3.2.8 Экспорт данных
Перекличка с 2.6. SceneExporter: запрос через событие, снапшот через SceneContext, чистая BuildBundle, запись ZIP в Documents/{productName} в фоновом потоке (Я). Состав бандла: scene.json (SceneBundle, one-way) + models/ + textures/, дедупликация (Я). Ограничение: встроенные ассеты без исходника → geometryMissing (К, честно). ExportPanel (Я-лайт).
Рисунки: скриншот панели экспорта; схема состава бандла. Листинг: фрагмент BuildBundle.

---

## 3.3 Технологическое обеспечение

Без подпунктов (или 2-3 при разрастании). Существенно расширить относительно v11 (там полстраницы):
целевая платформа Meta Quest 2/3, автономность (standalone Android, без ПК); стек: Unity 6000.3.7f1, OpenXR (кроссплатформенность, не привязан к Meta SDK), URP; сборка под Android/Quest; требования к устройству и месту; импорт файлов через системный пикер.
Отдельный честный абзац: реальное тестирование проводилось только на Meta Quest 3; билд не полностью адаптирован под автономный режим – возможна нестабильность, особенно в части консистентности данных. Это формулируется нейтрально, без самокритики, как ограничение текущей версии.
⚠ Без юнит-тестов (решение F5). Editor-инструменты бейка рецептов описываются в 3.2.4 (решение F6 пересмотрено).
Рисунок: возможно, схема стека технологий.

## Выводы

Зеркалят фактическую реализацию (БЕЗ undo, БЕЗ IK, БЕЗ «additive-сцен» – ошибки выводов v11 не повторять).

---

## Приложение Б (предварительные кандидаты, уточняется после написания текстов)

Полные скрипты, функционально значимые, НЕ процитированные в тексте целиком, не из _Archive:
AppStorage, SceneSerializer, SceneGraph, AssetSpawner, ImportPipeline, RigEntityFabricator, ProxyRigRuntime, AnimationAuthoring, AnimationClipBaker, AnimationPlaybackSampler, SceneExporter, PanelRegionRouter, ModeOrchestrator, XRPromeonInteractable, GizmoDriver.
Итоговый состав сверим по правилу: «в приложении – то, на что в тексте есть ссылка „Листинг Б.N“».

## Сверка покрытия (все 13 подсистем)
Core→3.2.1; Bootstrap→3.2.1; ModeOrchestrator→3.2.1; SpatialUi→3.2.2 (+панели по пунктам); StorageCore→3.1; SceneComposition→3.2.3/3.2.5; AssetBrowser→3.2.4; VrInteraction→3.2.5; RigBuilder→3.2.6; Animation→3.2.7 (+3.1.2); ExportPipeline→3.2.8; InputBindings→3.2.2 (настройки, К); ErrorHandling – пуст (игнор).

---

## G. РЕШЕНИЯ по коду/листингам/рисункам (2026-06-04, Макс)

Правило размещения кода:
- Фрагмент в тексте (Листинг 3.N) = выразительный ОТРЫВОК (алгоритм, ключевой метод). Полный текст того же скрипта – в Приложении Б (Листинг Б.N), отрывок и полный листинг не считаются дублем.
- Код присутствует щедро – в каждом пункте, где он задействован (допустимо «пощадить» 1-2 пункта без листинга).
- UI-панели: скриншоты обязательны И код на общих основаниях – фрагменты в тексте там, где у панели есть выразительная логика (UserPanel – обязательно показать), полные скрипты панелей – в Приложение Б (решение Макса 2026-06-04).
- Приложение Б: полные тексты всех крупных ЯДРО-скриптов + скрипты панелей.
- Мелкие файлы (≤ ~60 строк: EventBus, PathProvider, SceneSerializer, SceneData/NodeData, ActionContainer, AssetSpawner, ModeOrchestrator, RigDefinitionExtractor, BoneFollower и т.п.) цитируются ЦЕЛИКОМ в тексте как Листинг 3.N и в Приложение Б НЕ дублируются.
- Доп. иллюстрации-сравнения: скриншот применения гизмо; парный рисунок «модель в Blender ↔ та же модель после импорта/экспорта в приложении» (просто визуальное сравнение, не анализ кода).

### Карта «пункт → листинги в тексте → рисунки → полные скрипты в Приложении Б»

| Пункт | Листинги-фрагменты в тексте | Рисунки | В Приложение Б (полный) |
|---|---|---|---|
| 3.1.1 Организация хранения | PathProvider (фрагмент путей); методы AppStorage – списком | Дерево каталогов persistentDataPath | AppStorage, PathProvider |
| 3.1.2 Структуры данных | запись рецепта+AssetRef; SceneData/NodeData; фрагмент animation.json | Схема вложенности данных | SceneData, NodeData, ActionContainer/AnimTrackData/AnimKeyData |
| 3.1.3 Сериализация | Deserialize с миграцией v1/v2→v3 | (схема версий – опц.) | SceneSerializer |
| 3.1.4 Автосохранение | SceneAutoSaver (фрагмент save-on-exit) | — | SceneAutoSaver, SceneDirtyTracker |
| 3.2.1 Каркас | EventBus (Publish/Subscribe); ModeOrchestrator (валидация перехода) | Схема скоупов; схема переходов режимов | EventBus, ModeOrchestrator, RootLifetimeScope |
| 3.2.2 Интерфейсы | PanelRegionRouter или SpatialPanel (фрагмент) | Скриншот UserPanel+навбар; схема регионов | SpatialPanel, PanelRegionRouter |
| 3.2.3 Меню и сцены | SceneGraph (фрагмент построения графа) | Скриншоты: главное меню, пикер сцен, аутлайнер | SceneGraph, MainMenuPanel(?) |
| 3.2.4 Ассеты и импорт | ImportPipeline (выбор импортёра/подтверждение); AssetSpawner+реестр билдеров; BuiltinRecipeBaker (фрагмент бейка рецепта) | Скриншоты: браузер ассетов, мастер импорта; схема конвейера импорта; ПАРНЫЙ рисунок Blender↔приложение | ImportPipeline, AssetSpawner, AssetEntityBuilderRegistry, BuiltinRecipeBaker |
| 3.2.5 Скелетные структуры | RigDefinitionExtractor (фрагмент); BuildProxyRig (фрагмент) | Скриншот прокси-костей; схема прокси-рига | RigDefinitionExtractor, RigEntityFabricator, ProxyRigRuntime |
| 3.2.6 Взаимодействие | XRPromeonInteractable (tap/hold); стратегия гизмо (фрагмент) | Скриншот применения гизмо; схема состояний взаимодействия | XRPromeonInteractable, GizmoDriver |
| 3.2.7 Анимация | AnimationClipBaker (тангенты); AnimationPlaybackSampler (семплирование) | Скриншот таймлайна; схема «ключи→клип→семпл» | AnimationAuthoring, AnimationClipBaker, AnimationPlaybackSampler |
| 3.2.8 Экспорт данных | BuildBundle (фрагмент сборки ZIP) | Скриншот панели экспорта; схема состава бандла | SceneExporter |
| 3.3 Технологическое | (пункт без листингов – «пощажён») | Схема стека технологий (опц.) | — |

### Реестр скриптов с объёмами (строк) и способом включения

Обозначения: Т = целиком в тексте (Листинг 3.N, мал); Ф+Б = фрагмент в тексте + полный в Приложении Б; Б = только Приложение Б (в тексте проза + ссылка); П = только проза/скриншот.

Хранение и данные:
- Т: PathProvider (51), SceneSerializer (28), SceneData (12), NodeData (16), ActionContainer (49), SceneDirtyTracker (31), SceneAutoSaver (54)
- Ф+Б: AppStorage (99)

Каркас:
- Т: EventBus (28), ModeOrchestrator (49), SceneTransitionRunner (46)
- Ф+Б: RootLifetimeScope (162)

Интерфейсы (каркас):
- Т: VrKeyboard (40), SpatialPanel (78 – на грани, можно Т)
- Ф+Б: PanelRegionRouter (178), UserPanel (262 – фрагмент grip-захвата обязателен)

Панели (полные – в Приложение Б; в тексте скриншот + фрагмент, если есть выразительная логика):
- Ф+Б: AnimatorPanel (448), AssetBrowserPanel (284), InspectorPanel (283), OutlinerPanel (158)
- Б: ImportWizardPanel (126), ExportPanel (125), SettingsPanel (94), ScenePickerPanel (81)
- Т или Б: MainMenuPanel (66), PropertyPanel (57), FileBrowserPanel (44)

Сцена и ассеты:
- Т: SelectionManager (22), AssetSpawner (50), AssetEntityBuilderRegistry (53), GltfAssetImporter (28)
- Ф+Б: SceneGraph (224), ImportPipeline (146), ThumbnailRenderer (76), BuiltinRecipeBaker (106)

Риг:
- Т: RigDefinitionExtractor (19), BoneFollower (37), BoneEditMode (50)
- Ф+Б: RigEntityFabricator (274), ProxyRigRuntime (200)

Взаимодействие:
- Ф+Б: XRPromeonInteractable (262), GizmoDriver (183), GizmoDragSession (167)

Анимация:
- Т: AnimationClipBaker (66 – на грани), AnimationClock (88 – на грани, можно Ф+Б), AnimationStorage (83)
- Ф+Б: AnimationAuthoring (441), AnimationPlaybackSampler (191)

Экспорт:
- Т: SceneBundle (59)
- Ф+Б: SceneExporter (267)

Итого в Приложение Б: ~22-25 полных скриптов, суммарно ≈ 4000-4500 строк ≈ 90-100 страниц Courier New 10. ⚠ ОБЪЁМ! Если многовато – кандидаты на вылет из Б: SettingsPanel, ScenePickerPanel, ImportWizardPanel, FileBrowserPanel, ThumbnailRenderer, GizmoDragSession (фрагмент в тексте, без полной версии).
Точный список фиксируется по фактическим ссылкам «Листинг Б.N» в готовых текстах.

### Прочие принятые правки структуры
- 3.1.2 объединён: ассеты + сцены + анимация (AssetRef связывает их).
- Порядок 3.2: …→ 3.2.4 Ассеты/импорт → 3.2.5 Скелетные структуры (только построение) → 3.2.6 Взаимодействие (объекты И кости: селекция, гизмо, BoneEditMode) → 3.2.7 Анимация → 3.2.8 Экспорт данных. Forward-ссылка снята.
- 3.2.8 переименован в «Экспорт данных» (перекличка с 2.6).
- 3.3 дополнен честным абзацем об ограничениях тестирования (только Quest 3, автономность не полностью отлажена, риск консистентности данных).
