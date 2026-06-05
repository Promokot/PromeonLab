# 3.1 Информационное обеспечение

Информационное обеспечение приложения PromeonLab составляют несколько видов данных: описания сцен с иерархией объектов и позами костей, анимационные дорожки с ключевыми кадрами, записи библиотек ассетов, копии исходных файлов импортированных моделей и изображений, а также миниатюры для галереи ассетов. В данном подразделе последовательно рассматриваются организация хранения этих данных, их структуры, способ сериализации и механизм автоматического сохранения.

## 3.1.1 Организация хранения данных

Все перечисленные данные размещаются в локальном хранилище, в каталоге Application.persistentDataPath. Этот путь определяется относительно устройства, на котором исполняется приложение: на VR-гарнитуре это внутреннее хранилище приложения, при запуске в редакторе Unity – служебная папка профиля пользователя. Код работы с данными от конкретного размещения не зависит, подключение к внешним системам для доступа к данным не требуется.

Хранилище разделено на две области по признаку принадлежности данных. Данные, не зависящие от конкретной сцены, собраны в каталоге asset-libraries: это записи библиотек ассетов, копии исходных файлов и миниатюры. Благодаря такому выделению однажды импортированная модель доступна в любой сцене без повторного импорта. Данные, принадлежащие отдельной сцене, лежат в её собственном каталоге внутри scenes: файл графа сцены scene.json и файл анимационных данных animation.json. Каталог сцены создаётся при её создании и удаляется целиком при удалении сцены, поэтому сцена не оставляет следов в общем хранилище. Структура каталогов приведена на Рисунке 3.1.

[РИСУНОК 3.1 – заготовка (оформить схемой):]
```
Application.persistentDataPath/
├── asset-libraries/               общие данные всех сцен
│   ├── imported-lib.json          записи импортированных ассетов
│   ├── saved-lib.json             записи сохранённых ассетов
│   ├── sources/                   копии исходных файлов (.glb, .gltf, .png, .jpg)
│   └── thumbnails/                миниатюры моделей (PNG)
└── scenes/
    └── {SceneId}/                 данные одной сцены
        ├── scene.json             граф сцены и позы костей
        └── animation.json         дорожки и ключевые кадры
```
*Рисунок 3.1 – Структура каталогов локального хранилища приложения*

Адресацию файлов внутри хранилища выполняет класс PathProvider – единственная точка построения путей в приложении. Остальной код не формирует строки путей самостоятельно, а запрашивает их у этого класса, поэтому структура хранилища описана ровно в одном месте и может быть изменена без правок в остальных частях системы. Полный текст класса приведён в Листинге 3.1.

Листинг 3.1 – Скрипт PathProvider.cs
```csharp
public class PathProvider
{
    private readonly string _root;

    [VContainer.Inject]
    public PathProvider() : this(Application.persistentDataPath) { }

    public PathProvider(string root) => _root = root;

    public string SceneRoot(string sceneId) =>
        Path.Combine(_root, "scenes", sceneId);

    public string SceneJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "scene.json");

    public string AnimationJson(string sceneId) =>
        Path.Combine(SceneRoot(sceneId), "animation.json");

    public string ScenesRoot() =>
        Path.Combine(_root, "scenes");

    public string ImportedLibraryPath =>
        Path.Combine(_root, "asset-libraries", "imported-lib.json");

    public string SavedLibraryPath =>
        Path.Combine(_root, "asset-libraries", "saved-lib.json");

    public string SourcesDir =>
        Path.Combine(_root, "asset-libraries", "sources");

    public string SourcePath(string assetId, string ext)
    {
        var clean = string.IsNullOrEmpty(ext) ? ""
            : (ext[0] == '.' ? ext : "." + ext);
        return Path.Combine(SourcesDir, assetId + clean);
    }

    public string ThumbnailsDir =>
        Path.Combine(_root, "asset-libraries", "thumbnails");

    public string ThumbnailPath(string assetId) =>
        Path.Combine(ThumbnailsDir, assetId + ".png");

    public static string ThumbnailRelativeRef(string assetId) =>
        Path.Combine("asset-libraries", "thumbnails", assetId + ".png");

    public string RootForSources => _root;
}
```

Внутри записей библиотек пути к исходным файлам и миниатюрам сохраняются не абсолютными, а относительными к корню хранилища: за их формирование отвечает, в частности, метод ThumbnailRelativeRef. Абсолютный путь к каталогу приложения может измениться, например после переустановки, и записи с абсолютными путями стали бы недействительными; относительные ссылки сохраняют работоспособность библиотек в любом размещении хранилища.

Каждая сцена идентифицируется коротким идентификатором из первых восьми символов GUID: он одновременно служит именем каталога сцены и ключом, по которому на сцену ссылаются остальные данные. Файлы сцены проходят следующий жизненный цикл: при создании сцены для неё добавляется каталог и записывается начальный scene.json. При открытии файл читается с диска и преобразуется в объекты приложения; повторные обращения обслуживаются кешем в памяти без чтения диска. При сохранении актуальное состояние записывается поверх прежнего. При удалении сцены её каталог удаляется рекурсивно вместе со всеми файлами. Доступ к этим операциям инкапсулирован в классе AppStorage; фрагмент, демонстрирующий создание и загрузку файла сцены, приведён в Листинге 3.2, полный текст скрипта – в Приложении Б (Листинг Б.1).

Листинг 3.2 – Скрипт AppStorage.cs (Фрагмент создания и загрузки сцены)
```csharp
public async Task<SceneData> CreateSceneAsync(
    string displayName, CancellationToken ct = default)
{
    var sceneId = Guid.NewGuid().ToString("N")[..8];
    var data = new SceneData
    {
        SceneId     = sceneId,
        DisplayName = displayName,
        CreatedAt   = DateTime.UtcNow.ToString("o")
    };

    Directory.CreateDirectory(_paths.SceneRoot(sceneId));
    await SaveSceneAsync(data, ct);
    _cache[sceneId] = data;
    return data;
}

public async Task<SceneData> LoadSceneAsync(
    string sceneId, CancellationToken ct = default)
{
    if (_cache.TryGetValue(sceneId, out var cached)) return cached;

    var path = _paths.SceneJson(sceneId);
    if (!File.Exists(path)) return null;

    var json = await File.ReadAllTextAsync(path, ct);
    var data = SceneSerializer.Deserialize(json);
    _cache[sceneId] = data;
    return data;
}
```

Описанная организация отвечает на вопрос, где располагаются данные приложения. Она устанавливает, что все обращения к диску выполняются асинхронно средствами Task и не блокируют поток рендеринга: остановка кадра на время файловой операции привела бы к заметному замиранию изображения в гарнитуре.

## 3.1.2 Структуры данных ассетов, сцен и анимации

Данные приложения связаны цепочкой ссылок: запись анимации ссылается на объект сцены, объект сцены – на запись в библиотеке ассетов, а та – на исходный файл в хранилище. Настоящий пункт последовательно проходит эту цепочку от библиотеки к анимации. Схема связей структур приведена на Рисунке 3.2.

[РИСУНОК 3.2 – заготовка (оформить схемой): три блока с полями и стрелки ссылок
  imported-lib.json: ImportedLabAsset {Id, SourceRef → sources/, ThumbnailRef → thumbnails/, Recipe}
  scene.json: SceneData {SceneId, Nodes[]} → NodeData {NodeId, AssetRef → ImportedLabAsset.Id, ParentNodeId → NodeData, BonePoses[]}
  animation.json: SceneAnimationData {Containers[]} → ActionContainer {OwnerNodeId → NodeData.NodeId, Tracks[]} → AnimTrackData {NodeId, Keys[]}]
*Рисунок 3.2 – Связи структур данных приложения*

Единицей библиотеки ассетов является запись с уникальным идентификатором, отображаемым именем и типом ассета. Тип задаётся перечислением AssetType и принимает три значения: Object для статических моделей, Rig для моделей со скелетом и Reference для плоских изображений-референсов. Запись импортированного ассета дополнительно хранит относительную ссылку SourceRef на копию исходного файла в каталоге sources, относительную ссылку ThumbnailRef на миниатюру и шаблон восстановления. Записи встроенных ассетов поставляются вместе с приложением и ссылок на исходные файлы не содержат, а записи импортированных и сохранённых ассетов сериализуются в файлы imported-lib.json и saved-lib.json соответственно.

Шаблон восстановления AssetEntityRecipe фиксирует результат обработки ассета, производимого один раз при импорте или в эдиторе: габариты и тип коллайдера, слой взаимодействия, параметры размещения, пропорции изображения для референсов, а для скелетных моделей – описание скелета с перечнем костей. При каждом размещении ассета в сцене шаблон применяется в готовом виде, поэтому повторный разбор модели не требуется, а представление объекта не меняется от запуска к запуску. Состав шаблона приведён в Листинге 3.3.

Листинг 3.3 – Скрипт AssetEntityRecipe.cs (Поля шаблона восстановления)
```csharp
[Serializable]
public class AssetEntityRecipe
{
    public int              schemaVersion = 1;
    public AssetType        type;

    public bool             selectable = true;
    public InteractionLayer interactionLayer = InteractionLayer.SceneObjects;

    public ColliderKind     colliderKind = ColliderKind.Box;
    public Vector3          colliderCenter;
    public Vector3          colliderSize = Vector3.one;
    public int              boneColliderDepth = 4;

    public Vector3          spawnOffset;

    public float            referenceAspect = 1f;
    public float            referenceBottomGap = 0.5f;
    public bool             referenceTwoSided = true;

    public RigDefinition    rig;
}
```

Сцена описывается структурой SceneData: помимо идентификатора, имени и даты создания она содержит плоский список нод NodeData. Нода представляет один объект сцены и хранит его трансформации, ссылку на ассет и ссылку на родительскую ноду; иерархия объектов задаётся именно полем ParentNodeId, а не вложенностью структур, что упрощает сериализацию и обход списка. Ссылка на ассет оформлена структурой AssetRef из двух полей: источника (встроенная, импортированная или сохранённая библиотека) и идентификатора записи в нём. Геометрия объекта в файле сцены не дублируется – при загрузке она восстанавливается по ссылке из библиотеки. Для скелетных моделей нода дополнительно содержит список поз костей BonePose с локальными трансформациями каждой кости. Перечисленные структуры приведены в Листинге 3.4.

Листинг 3.4 – Скрипты SceneData.cs, NodeData.cs и BonePose.cs
```csharp
[Serializable]
public class SceneData
{
    public int            SchemaVersion = 3;
    public string         SceneId;
    public string         DisplayName;
    public string         CreatedAt;
    public List<NodeData> Nodes = new();
}

[Serializable]
public class NodeData
{
    public string         NodeId;
    public AssetRef       AssetRef;
    public Vector3        Position;
    public Quaternion     Rotation;
    public Vector3        Scale;
    public string         DisplayName;
    public string         ParentNodeId;
    public List<BonePose> BonePoses = new();
}

[Serializable]
public class BonePose
{
    public string     BoneName;
    public Vector3    LocalPosition;
    public Quaternion LocalRotation;
    public Vector3    LocalScale;
}
```

Анимационные данные сцены собраны в структуре SceneAnimationData, которая хранит частоту кадров, общую для сцены, и список контейнеров действий. Контейнер ActionContainer привязан к одной ноде по полю OwnerNodeId и описывает уникальное для объекта сцены анимационное действие с такими параметрами, как длина в кадрах, режим интерполяции (линейная или ступенчатая), признак циклического воспроизведения и список дорожек (собственное поле Fps контейнера – лишь резерв на случай загрузки данных без общесценовой частоты). Дорожка AnimTrackData принадлежит конкретной ноде – самому объекту либо отдельной кости его скелета – и содержит упорядоченный по кадрам список ключей AnimKeyData. Ключ фиксирует номер кадра и полный набор трансформаций. Поля структур приведены в Листинге 3.5.

Листинг 3.5 – Скрипты структур анимационных данных (Поля без методов)
```csharp
[Serializable]
public class SceneAnimationData
{
    public int                   schemaVersion = 2;
    public int                   Fps           = 24;
    public List<ActionContainer> Containers    = new();
}

[Serializable]
public class ActionContainer
{
    public string              OwnerNodeId;
    public int                 Fps           = 24;
    public int                 TotalFrames   = 60;
    public InterpolationMode   Interpolation = InterpolationMode.Linear;
    public bool                Loop          = false;
    public List<AnimTrackData> Tracks        = new();
}

[Serializable]
public class AnimTrackData
{
    public string            NodeId;
    public List<AnimKeyData> Keys = new();
}

[Serializable]
public class AnimKeyData
{
    public int        Frame;
    public Vector3    Position;
    public Quaternion Rotation;
    public Vector3    Scale;
}
```

Ключ хранит сразу позицию, поворот и масштаб, то есть дорожка не разделяется на отдельные кривые по свойствам. Такое решение увеличивает объём файла, но упрощает модель данных и операции над ключами: постановка, перенос и удаление ключа затрагивают один элемент списка.

Во всех приведённых листингах присутствует поле schemaVersion. Под схемой данных понимается формат конкретного файла: состав полей сериализуемой структуры и правила их записи на диск. Каждый тип данных описывается собственной схемой – своя схема у файла сцены, своя у анимационных данных, своя у шаблона восстановления – и версионируются эти схемы независимо друг от друга: номер увеличивается только тогда, когда меняется формат именно этого файла. Поэтому номера разных схем не сопоставимы между собой: текущая третья версия схемы сцены означает лишь, что формат сцены пересматривался дважды, тогда как формат анимационных данных менялся один раз, а шаблона восстановления – ни разу. Номер версии записывается в сам файл, и при чтении приложение знает, по какой схеме файл был сохранён. Использование этого знания – порядок чтения файлов и приведения устаревших схем к актуальному виду – относится уже к механизму сериализации.

## 3.1.3 Сериализация и версионирование

Все структуры, описанные в предыдущем пункте, записываются на диск в формате JSON средствами встроенного сериализатора Unity JsonUtility. Сериализатор входит в состав движка и не требует внешних зависимостей; файлы записываются с отступами, поэтому их содержимое остаётся читаемым для человека, что упрощает отладку и проверку данных вне приложения. Пример файла сцены, записанного приложением, приведён на Рисунке 3.3: сцена содержит единственный объект – импортированную модель, приподнятую над полом.

[РИСУНОК 3.3 – заготовка (оформить как блок с рамкой или скриншот текста файла):]
```json
{
    "SchemaVersion": 3,
    "SceneId": "3f9c1a72",
    "DisplayName": "Demo scene",
    "CreatedAt": "2026-05-28T14:32:07.5214380Z",
    "Nodes": [
        {
            "NodeId": "a41b9c03",
            "AssetRef": {
                "Source": 1,
                "AssetId": "7e2d80f4"
            },
            "Position": { "x": 0.0, "y": 0.5, "z": 1.2 },
            "Rotation": { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 },
            "Scale":    { "x": 1.0, "y": 1.0, "z": 1.0 },
            "DisplayName": "Box",
            "ParentNodeId": "",
            "BonePoses": []
        }
    ]
}
```
*Рисунок 3.3 – Пример сериализованного файла сцены scene.json*

Пример демонстрирует особенности записи, выполняемой JsonUtility. Перечисления сохраняются числом: значение 1 в поле Source означает импортированную библиотеку. Поворот записывается четырьмя компонентами кватерниона, как он и хранится в структуре, без преобразования в углы. Пустой список BonePoses показывает, что объект не имеет скелета, а пустая строка ParentNodeId – что нода находится в корне иерархии. Версия схемы записана первым полем файла.

Преобразование данных сцены выполняет статический класс SceneSerializer, объединяющий две операции: запись структуры SceneData в строку JSON и обратное чтение с приведением устаревших схем к актуальной. Полный текст класса приведён в Листинге 3.6.

Листинг 3.6 – Скрипт SceneSerializer.cs
```csharp
public static class SceneSerializer
{
    public static string Serialize(SceneData data) =>
        JsonUtility.ToJson(data, prettyPrint: true);

    public static SceneData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var data = JsonUtility.FromJson<SceneData>(json);
        if (data == null) return null;
        if (data.SchemaVersion < 2)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene "
                + $"'{data.SceneId}' from v{data.SchemaVersion} to v2");
            data.SchemaVersion = 2;
            data.Nodes ??= new List<NodeData>();
        }
        if (data.SchemaVersion < 3)
        {
            Debug.LogWarning($"SceneSerializer: migrating scene "
                + $"'{data.SceneId}' from v{data.SchemaVersion} to v3");
            data.SchemaVersion = 3;
            foreach (var n in data.Nodes) n.BonePoses ??= new List<BonePose>();
        }
        return data;
    }
}
```

Приведение выполняется цепочкой последовательных шагов. Файл первой версии сначала дополняется до второй, затем до третьей: каждый шаг гарантирует наличие полей, появившихся в соответствующей версии схемы, заполняя их значениями по умолчанию – пустым списком нод при переходе ко второй версии и пустыми списками поз костей у каждой ноды при переходе к третьей. Такая цепочка позволяет открыть файл сцены, записанный любой из предыдущих версий приложения, без отдельного кода под каждое сочетание версий. Приведение происходит в памяти при чтении; на диск файл попадает уже в актуальной схеме при ближайшем сохранении сцены.

Подобное приведение уместно потому, что схема сцены развивалась аддитивно: новые версии добавляли поля, не меняя смысла существующих, и дополнение старого файла значениями по умолчанию не искажает сохранённую работу пользователя. Для анимационных данных выбран противоположный, неразрушающий подход: файл с неподдерживаемой версией схемы не приводится и вообще не модифицируется. Чтение анимации выполняет класс AnimationStorage; фрагмент загрузки приведён в Листинге 3.7, полный текст скрипта – в Приложении Б (Листинг Б.2).

Листинг 3.7 – Скрипт AnimationStorage.cs (Фрагмент загрузки данных)
```csharp
public async Task<SceneAnimationData> LoadAsync(
    string sceneId, CancellationToken ct)
{
    var path = _paths.AnimationJson(sceneId);
    if (!File.Exists(path)) return new SceneAnimationData();

    try
    {
        var json   = await File.ReadAllTextAsync(path, ct);
        var loaded = JsonUtility.FromJson<SceneAnimationData>(json);

        if (loaded == null || loaded.schemaVersion < 2
                           || loaded.schemaVersion > 2)
        {
            Debug.LogWarning($"AnimationStorage: '{path}' has unsupported "
                + $"schemaVersion={loaded?.schemaVersion ?? 0}. "
                + "Opening empty; file left untouched.");
            return new SceneAnimationData();
        }

        if (loaded.Fps <= 0)
            loaded.Fps = loaded.Containers.Count > 0
                ? Mathf.Max(1, loaded.Containers[0].Fps) : 24;

        return loaded;
    }
    catch (Exception ex)
    {
        Debug.LogError($"AnimationStorage: load failed '{path}': {ex.Message}");
        return new SceneAnimationData();
    }
}
```

Если версия анимационного файла не равна поддерживаемой второй, приложение записывает предупреждение в журнал и открывает в памяти пустой анимационный документ, а исходный файл остаётся на диске нетронутым. Логика этого решения отличается от логики приведения сцены: для анимационных дорожек совместимое преобразование между версиями схемы не определено, и автоматическое «исправление» рисковало бы незаметно исказить или потерять записанные ключевые кадры. Отказ от чтения сохраняет данные физически: файл по-прежнему доступен для восстановления внешними средствами. Тем же способом обрабатывается и повреждённый файл: ошибка разбора не прерывает запуск сцены, а приводит к открытию пустого документа. Дополнительно при загрузке проверяется корректность частоты кадров: неположительное значение заменяется значением по умолчанию. Таким образом, формат хранения объединяет два режима работы с версиями: дополняющее приведение там, где оно безопасно, и осторожный отказ.

## 3.1.4 Автосохранение и отслеживание изменений

Запись данных на диск не привязана к ручной команде сохранения: приложение само определяет моменты, когда состояние должно попасть в хранилище. При этом два файла сцены записываются с разной периодичностью. Файл scene.json записывается при создании сцены и затем при каждом завершении сеанса редактирования, тогда как animation.json обновляется непрерывно, по мере правки ключевых кадров. Различие объясняется характером данных: граф сцены меняется сравнительно редко и его выгодно фиксировать целиком, а анимация правится длинными сериями мелких операций, потеря которых обошлась бы дороже всего.

Основанием для отложенной записи графа сцены служит отслеживание изменений. Каждая операция, меняющая сцену, – добавление и удаление объекта, перемещение, переименование, правка трансформаций – сопровождается публикацией сообщения SceneModifiedEvent во внутреннюю шину приложения. Класс SceneDirtyTracker подписан на эти сообщения и поднимает флаг несохранённых изменений; открытие сцены сбрасывает флаг. По состоянию флага приложение судит, расходится ли содержимое памяти с диском. Полный текст класса приведён в Листинге 3.8.

Листинг 3.8 – Скрипт SceneDirtyTracker.cs
```csharp
public class SceneDirtyTracker : IStartable, IDisposable
{
    private readonly EventBus _bus;
    private bool _isDirty;

    public bool IsDirty => _isDirty;

    public SceneDirtyTracker(EventBus bus) => _bus = bus;

    public void Start()
    {
        _bus.Subscribe<SceneModifiedEvent>(OnModified);
        _bus.Subscribe<SceneOpenedEvent>(OnOpened);
    }

    public void Dispose()
    {
        _bus.Unsubscribe<SceneModifiedEvent>(OnModified);
        _bus.Unsubscribe<SceneOpenedEvent>(OnOpened);
    }

    public bool CanNavigate() => !_isDirty;

    public void ClearDirty() => _isDirty = false;

    private void OnModified(SceneModifiedEvent _) => _isDirty = true;
    private void OnOpened(SceneOpenedEvent _)    => _isDirty = false;
}
```

Сохранение сцены выполняет класс SceneAutoSaver. Он реагирует на уведомление о предстоящем выходе из режима редактирования (ModeExitingEvent), которое публикуется, пока редактируемая сцена ещё загружена. Порядок действий внутри обработчика принципиален: состояние графа сцены фиксируется в снимок синхронно, до первой асинхронной операции, поскольку сразу после начала перехода объекты сцены могут быть выгружены; последующая запись на диск работает уже с отделённым снимком и не зависит от жизни сцены. Временная сцена режима песочницы (зарезервированный идентификатор "__sandbox__") на диск не записывается и обработчиком пропускается. Полный текст класса приведён в Листинге 3.9.

Листинг 3.9 – Скрипт SceneAutoSaver.cs (Метод сохранения по снимку)
```csharp
private void OnModeExiting(ModeExitingEvent e)
{
    if (e.From == AppMode.VrEditing && e.To != AppMode.VrEditing)
        _ = SaveCurrentAsync();
}

private async Task SaveCurrentAsync()
{
    try
    {
        var activeId = _storage.ActiveSceneId;
        if (string.IsNullOrEmpty(activeId) || activeId == "__sandbox__")
            return;

        var cached = _storage.GetCachedScene(activeId);
        if (cached == null) return;
        var snap = _graph.CaptureSnapshot(
            activeId, cached.DisplayName, cached.CreatedAt);

        await _storage.SaveSceneAsync(snap, CancellationToken.None);
        _bus.Publish(new SceneClosedEvent());
    }
    catch (Exception ex)
    {
        Debug.LogError($"SceneAutoSaver failed: {ex}");
    }
}
```

Анимационные данные сохраняются иначе. Каждая операция над ключами – постановка, перенос, удаление, изменение длины действия – запрашивает запись файла animation.json, однако фактическая запись откладывается на 200 миллисекунд, и поступивший за это время новый запрос перезапускает отсчёт. Приём известен как дебаунс: пока пользователь активно правит анимацию, диск не затрагивается вовсе, а файл записывается в первую же паузу. Тем самым серия из десятков правок превращается в одну дисковую операцию, что заметно снижает нагрузку на хранилище устройства, при этом потерять при аварийном завершении можно не более 200 миллисекунд работы. Механизм отложенной записи приведён в Листинге 3.10.

Листинг 3.10 – Скрипт AnimationStorage.cs (Фрагмент отложенной записи)
```csharp
public void RequestSave(SceneAnimationData data, string sceneId)
{
    _saveCts?.Cancel();
    _saveCts = new CancellationTokenSource();
    _ = DebouncedSave(data, sceneId, _saveCts.Token);
}

private async Task DebouncedSave(
    SceneAnimationData data, string sceneId, CancellationToken ct)
{
    try
    {
        await Task.Delay(SAVE_DEBOUNCE_MS, ct);
        if (!ct.IsCancellationRequested) await SaveAsync(data, sceneId, ct);
    }
    catch (TaskCanceledException) { }
}
```

Описанные механизмы завершают картину информационного обеспечения: данные приложения размещаются в локальном хранилище, описываются версионируемыми схемами, сериализуются в читаемый формат и попадают на диск автоматически – целиком в конце сеанса редактирования либо малыми порциями в паузах работы.

# 3.2 Программное обеспечение

Если информационное обеспечение описывает данные приложения, то программное обеспечение определяет его поведение: каким образом загружаются сцены, как пользователь наполняет их объектами, управляет скелетами, записывает или воспроизводит анимацию и выгружает результат. Приложение организовано как набор модулей, каждый из которых отвечает за собственную функциональную зону из числа выделенных при проектировании. Модули не вызывают друг друга напрямую, а связываются через общий каркас – обмен сообщениями и внедрение зависимостей. В данном подразделе сначала рассматривается основной архитектурный подход, затем система пространственных интерфейсов и далее, в порядке пользовательского взаимодействия с функциями программы: от главного меню до экспорта данных.

## 3.2.1 Архитектурный каркас приложения

Архитектурным каркасом приложения далее называется условная группа механизмов, общих для всех модулей. К ней отнесены обмен сообщениями через общую шину, управление временем жизни объектов на основе внедрения зависимостей (Dependency Injection, DI) и переключение режимов работы через оркестратор с графом разрешённых переходов. Эти механизмы рассматриваются по порядку, однако сначала следует обозначить общее для них основание – технологии виртуальной реальности.

Работу с виртуальной реальностью обеспечивает рантайм OpenXR – открытый стандарт, через который приложение взаимодействует с гарнитурой, не привязываясь к программному обеспечению одного производителя [ИСТ: OpenXR]. Пользователя в сцене представляет объект XR Origin (XR-риг): он отслеживает положение головы и контроллеров и содержит камеру. Слой ввода построен на пакете XR Interaction Toolkit (XRI) [ИСТ: XRI]: поверх него работают и лучевая указка интерфейса, и взаимодействие с объектами сцены, рассматриваемые в дальнейших пунктах. Соответственно все действия пользователя неизбежно проходят через данный программный пакет Unity.

Шину сообщений реализует класс EventBus. Сообщением служит типизированная структура с суффиксом Event в имени – к таким сообщениям относятся, например, SceneModifiedEvent и SceneOpenedEvent из описания автосохранения в предыдущем подразделе. Модуль-отправитель публикует структуру методом Publish, модули-получатели заранее оформляют подписку методом Subscribe. Отправитель не знает своих получателей, и наоборот [ИСТ: паттерн Publish-Subscribe]. Прямые вызовы между модулями тем самым исключаются: добавление нового получателя сообщения не требует правок в отправителе. Полный текст класса приведён в Листинге 3.11.

Листинг 3.11 – Скрипт EventBus.cs
```csharp
public class EventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<object>();
        _handlers[type].Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
            list.Remove(handler);
    }

    public void Publish<T>(T message) where T : struct
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;
        foreach (var handler in list.ToArray())
            ((Action<T>)handler).Invoke(message);
    }
}
```

Реализация компактна: словарь сопоставляет тип сообщения списку обработчиков. Метод Publish содержит существенную деталь: перед обходом список обработчиков копируется в массив, поэтому обработчик может в ответ на сообщение оформить или снять подписку, не нарушая идущий обход.

Время жизни объектов организовано через иерархию областей жизни (LifetimeScope). Каждая область конфигурирует собственный контейнер зависимостей – реестр, который создаёт объекты и передаёт их друг другу через параметры конструкторов. Подход реализован библиотекой VContainer – легковесной и широко распространённой в Unity-проектах. Она снимает ручную сборку объектов и поддерживает вложенные области без дополнительного кода [ИСТ: VContainer]. Корневая область RootLifetimeScope создаётся при запуске, переживает любые смены сцен и содержит объекты с временем жизни приложения. К ним относятся PathProvider, AppStorage и EventBus, а также библиотеки ассетов и конвейеры импорта и экспорта. Внутрисценная область создаётся вместе со сценой режима и уничтожается при выходе из него; контейнер внутрисценной области вложен в корневой, поэтому внутрисценный объект может зависеть от корневого, но не наоборот. Правило направления зависимостей иллюстрирует пример из пункта 3.1.4: внутрисценный SceneAutoSaver получает через конструктор корневые AppStorage и EventBus, тогда как обратная связь – корневой объект, удерживающий внутрисценный, – исключена самой иерархией. Одновременно разрешено существование только одной внутрисценной области. Состав областей приведён в Таблице 3.1.

Таблица 3.1 – Области жизни приложения и их ключевые объекты

| Область жизни | Время существования | Ключевые объекты |
|---|---|---|
| RootLifetimeScope | Всё время работы приложения | EventBus, PathProvider, AppStorage, SceneContext, ModeOrchestrator, Библиотеки Ассетов, ImportPipeline, SceneExporter, VrKeyboard, UserPanel |
| MainMenuSceneScope | Режим главного меню | SceneGraph, UI-панели MainMenu |
| VrEditingSceneScope | Режим редактирования | SceneGraph, SelectionManager, AssetSpawner, AnimationAuthoring, AnimationClock, AnimationStorage, AnimationPlaybackSampler, BoneEditMode, SelectionVisualSync, SceneAutoSaver, SceneDirtyTracker |
| SandboxSceneScope | Режим песочницы | SceneGraph, SelectionManager, AssetSpawner, BoneEditMode |

Регистрация классов выполняется в методе Configure соответствующей области. Характер записи виден из фрагмента корневой области в Листинге 3.12, полный текст скрипта приведён в Приложении Б (Листинг Б.3).

Листинг 3.12 – Скрипт RootLifetimeScope.cs (Фрагмент регистрации классов)
```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<PathProvider>(Lifetime.Singleton);
    builder.Register<AppStorage>(Lifetime.Singleton);
    builder.Register<EventBus>(Lifetime.Singleton);
    builder.Register<SceneContext>(Lifetime.Singleton);
    builder.RegisterInstance(_transitionGraph);

    builder.RegisterEntryPoint<ImportedAssetLibrary>(Lifetime.Singleton).AsSelf();
    builder.RegisterEntryPoint<SavedAssetLibrary>(Lifetime.Singleton).AsSelf();
    builder.Register<AssetRegistry>(Lifetime.Singleton).As<IAssetRegistry>();

    builder.Register<GltfAssetImporter>(Lifetime.Singleton).As<IAssetImporter>();
    builder.Register<ImageAssetImporter>(Lifetime.Singleton).As<IAssetImporter>();
    builder.RegisterEntryPoint<ImportPipeline>(Lifetime.Singleton).AsSelf();

    builder.RegisterEntryPoint<SceneExporter>(Lifetime.Singleton).AsSelf();
    // ...
}
```

В листинге применяются два способа регистрации, и различие между ними определяется ролью объекта. Большинство объектов пассивны: такой объект создаётся контейнером в момент, когда понадобится другому, и работает только в ответ на обращения – для этого достаточно обычного Register. Однако часть объектов должна действовать самостоятельно. Так, библиотека импортированных ассетов ImportedAssetLibrary, объявленная в Листинге 3.12, обязана прочитать свой файл с диска сразу при запуске приложения, без обращения со стороны других объектов. Компонентам Unity подобные возможности предоставляет движок, вызывая их методы Start и Update, но многие зарегистрированные в контейнере классы не наследуют MonoBehaviour – их экземпляры движок не отслеживает и методов жизненного цикла у них не вызывает. Для таких классов предусмотрена регистрация RegisterEntryPoint: жизненный цикл берёт на себя контейнер и сам вызывает объект в нужные моменты. О том, когда именно компонент должен вызываться, он заявляет реализацией интерфейсов: IStartable – вызов при построении области, ITickable – вызов каждый кадр. По этой же схеме работает, например, SceneDirtyTracker: контейнер внутрисценной области вызывает его метод Start, в котором объект оформляет подписки на сообщения. Покадровые вызовы в проекте применяются при воспроизведении анимации.

Из-за того, что наборы объектов во внутрисценных областях различаются, объектам корневой области нельзя удерживать прямые ссылки на внутрисценные: после смены режима такая ссылка указывала бы на уничтоженный объект. Посредником служит корневой объект SceneContext. Это фасад, через который постоянные потребители, прежде всего панели UI, обращаются к объектам текущей сцены. Полный текст фасада приведён в Листинге 3.13.

Листинг 3.13 – Скрипт SceneContext.cs
```csharp
public class SceneContext
{
    public SceneGraph         Graph     { get; private set; }
    public ISelectionManager  Selection { get; private set; }
    public AnimationAuthoring Authoring { get; private set; }
    public AnimationClock     Clock     { get; private set; }

    public bool HasScene => Graph != null;

    public void Bind(SceneGraph graph, ISelectionManager selection,
                     AnimationAuthoring authoring, AnimationClock clock)
    {
        Graph = graph; Selection = selection;
        Authoring = authoring; Clock = clock;
    }

    public void Clear()
    {
        Graph = null; Selection = null;
        Authoring = null; Clock = null;
    }
}
```

Заполнением и очисткой SceneContext занимается объект внутрисценной области SceneContextBinder, приведённый в Листинге 3.14. При старте внутрисценной области он заполняет фасад ссылками на граф сцены, выделение и анимационные объекты и публикует сообщение SceneContextChangedEvent; при уничтожении области фасад очищается с повторной публикацией сообщения. Заполнение выполняется защищённо: метод Resolve перехватывает отказ контейнера и оставляет в фасаде пустую ссылку, если класс в текущей области не зарегистрирован. Поэтому потребитель перед обращением проверяет доступность именно того объекта фасада, который собирается использовать.

Листинг 3.14 – Скрипт SceneContextBinder.cs
```csharp
public class SceneContextBinder : IStartable, IDisposable
{
    private readonly IObjectResolver _resolver;
    private readonly SceneContext    _ctx;
    private readonly EventBus        _bus;

    public SceneContextBinder(IObjectResolver resolver,
                              SceneContext ctx, EventBus bus)
    {
        _resolver = resolver;
        _ctx      = ctx;
        _bus      = bus;
    }

    public void Start()
    {
        _ctx.Bind(
            Resolve<SceneGraph>(),
            Resolve<ISelectionManager>(),
            Resolve<AnimationAuthoring>(),
            Resolve<AnimationClock>());

        _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
    }

    public void Dispose()
    {
        _ctx.Clear();
        _bus.Publish(new SceneContextChangedEvent { HasScene = _ctx.HasScene });
    }

    private T Resolve<T>() where T : class
    {
        try { return _resolver.Resolve<T>(); }
        catch (VContainerException) { return null; }
    }
}
```

Регистрация во внутрисценных областях имеет особенность: заметная часть их объектов существует не в виде чистых классов, а в виде компонентов, заранее размещённых в сцене режима. Область главного меню (см. Листинг 3.15) имеет довольно небольшой список зависимостей. Помимо отслеживания изменений в ней регистрируются только две статичные панели, и для них применяется третий способ регистрации – RegisterComponentInHierarchy, при котором контейнер не создаёт объект, а находит готовый компонент в иерархии загруженной сцены и берёт его на учёт.

Листинг 3.15 – Скрипт MainMenuSceneScope.cs
```csharp
public class MainMenuSceneScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<SceneDirtyTracker>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterComponentInHierarchy<ScenePickerPanel>();
        builder.RegisterComponentInHierarchy<MainMenuPanel>();
    }
}
```

Состав области редактирования существенно шире (см. Листинг 3.16). Первой строкой регистрируется связующий SceneContextBinder, далее объекты для работы с данными, выделением и анимацией, а также экземпляры объектов сцены: главная камера с XR Origin и конфигурация гизмо-инструмента. Для внутрисценных панелей применён явный шов между миром сцены и контейнером: компонент отыскивается вызовом FindAnyObjectByType и получает зависимости вызовом Inject. По соглашениям проекта это единственное место, где такой поиск допустим, остальной код получает зависимости исключительно через конструкторы. Завершающий блок регистрирует размещённые в сцене поверхности интерфейса в маршрутизаторе панелей PanelRegionRouter. Область песочницы не привносит ничего нового: она повторяет VrEditingSceneScope, но без отслеживания изменений, автосохранения и анимации, её полный текст приведён в Приложении Б (см. Листинг Б.4).

Листинг 3.16 – Скрипт VrEditingSceneScope.cs (Комментарии опущены)
```csharp
public class VrEditingSceneScope : LifetimeScope
{
    [SerializeField] private GizmoConfig _gizmoConfig;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint<SceneContextBinder>();
        if (_gizmoConfig != null) builder.RegisterInstance(_gizmoConfig);
        builder.RegisterInstance(Camera.main);
        builder.Register<SceneDirtyTracker>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneGraph>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SceneAutoSaver>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<SelectionManager>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.Register<BoneEditMode>(Lifetime.Scoped).AsSelf();
        builder.Register<SelectionVisualSync>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        var catcher = Object.FindAnyObjectByType<EmptySpaceClickDeselector>(FindObjectsInactive.Include);
        if (catcher != null)
            builder.RegisterBuildCallback(c => c.Inject(catcher));

        var propPanel = Object.FindAnyObjectByType<PropertyPanel>(FindObjectsInactive.Include);
        if (propPanel != null) builder.RegisterInstance(propPanel).AsImplementedInterfaces().AsSelf();

        builder.Register<AssetSpawner>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();

        var outliner = Object.FindAnyObjectByType<OutlinerPanel>(FindObjectsInactive.Include);
        if (outliner != null)
            builder.RegisterBuildCallback(c => c.Inject(outliner));

        var inspector = Object.FindAnyObjectByType<InspectorPanel>(FindObjectsInactive.Include);
        if (inspector != null)
            builder.RegisterBuildCallback(c => c.Inject(inspector));

        builder.RegisterEntryPoint<AnimationClock>(Lifetime.Scoped).AsSelf();
        builder.Register<AnimationStorage>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        builder.RegisterEntryPoint<AnimationPlaybackSampler>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<AnimationAuthoring>(Lifetime.Scoped).AsSelf();

        var gizmoActivator = Object.FindAnyObjectByType<GizmoDriver>(FindObjectsInactive.Include);
        if (gizmoActivator != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoActivator));

        var gizmoToolsPanel = Object.FindAnyObjectByType<GizmoToolsPanel>(FindObjectsInactive.Include);
        if (gizmoToolsPanel != null)
            builder.RegisterBuildCallback(c => c.Inject(gizmoToolsPanel));

        builder.RegisterBuildCallback(c =>
        {
            var router = c.Resolve<PanelRegionRouter>();

            foreach (var rm in Object.FindObjectsByType<RegionMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                c.Inject(rm);
                router.RegisterModule(rm.ModuleId, rm);
            }

            router.ApplyMode(c.Resolve<ModeOrchestrator>().CurrentMode);
        });
    }
}
```

Схема иерархии областей приведена на Рисунке 3.4.

[РИСУНОК 3.4 – заготовка (СХЕМА, генерирует Клод): корневая область с перечнем объектов наверху; ниже три внутрисценные области (MainMenu / VrEditing / Sandbox) с их наборами; стрелки вложенности контейнеров снизу вверх; сбоку SceneContext-фасад с подписью «доступ постоянных потребителей к внутрисценным объектам»]
*Рисунок 3.4 – Иерархия областей жизни приложения*

Третий механизм каркаса – переключение режимов. Каждому режиму соответствует собственная сцена Unity и внутрисценная область. С режимом связаны и доступные пользователю возможности – состав панелей интерфейса и кнопок навигации определяется режимом и перестраивается при его смене. Сменой режимов управляет класс ModeOrchestrator, он проверяет допустимость перехода по графу ModeTransitionGraph и делегирует фактическую перезагрузку сцены исполнителю переходов. Граф задан как ассет с перечнем разрешённых пар: меню соединено двусторонними переходами с редактированием и песочницей, прямой переход между редактированием и песочницей не предусмотрен (см. Рисунок 3.5). Полный текст оркестратора приведён в Листинге 3.17.

[РИСУНОК 3.5 – заготовка (СХЕМА, генерирует Клод): три узла MainMenu, VrEditing, Sandbox; двусторонние стрелки MainMenu↔VrEditing и MainMenu↔Sandbox]
*Рисунок 3.5 – Граф переходов между режимами приложения*

Сцены режимов намеренно различаются окружением и цветовой гаммой, поэтому на снимках экрана режим, в котором идёт работа, распознаётся по виду кадра (см. Рисунок 3.6).

[РИСУНОК 3.6 – заготовка (СКРИНШОТ-КОЛЛАЖ, готовит Макс): три кадра одним рисунком – виды сцен MainMenu, Sandbox и VrEditing; кадры подобрать так, чтобы цветовое различие окружений читалось]
*Рисунок 3.6 – Виды сцен трёх режимов приложения*

Листинг 3.17 – Скрипт ModeOrchestrator.cs
```csharp
public class ModeOrchestrator
{
    private readonly EventBus            _bus;
    private readonly ModeTransitionGraph _graph;
    private readonly ISceneTransition    _transition;

    private AppMode _current = AppMode.MainMenu;
    public AppMode CurrentMode => _current;

    public ModeOrchestrator(EventBus bus, ModeTransitionGraph graph,
                            ISceneTransition transition)
    {
        _bus        = bus;
        _graph      = graph;
        _transition = transition;
    }

    public void TransitionTo(AppMode target)
    {
        if (_current == target) return;
        if (_transition.IsTransitioning) return;
        if (!_graph.IsAllowed(_current, target))
        {
            Debug.LogWarning($"Transition {_current} → {target} not allowed");
            return;
        }

        var prev = _current;
        _current = target;

        _bus.Publish(new ModeExitingEvent { From = prev, To = target });

        _transition.Load(SceneNameFor(target), () =>
            _bus.Publish(new ModeChangedEvent
                { PreviousMode = prev, CurrentMode = target }));
    }

    private static string SceneNameFor(AppMode mode) => mode switch
    {
        AppMode.MainMenu  => "MainMenu",
        AppMode.VrEditing => "VrEditing",
        AppMode.Sandbox   => "Sandbox",
        _                 => null,
    };
}
```

Порядок событий внутри перехода устроен под нужды сохранения данных. Сообщение ModeExitingEvent публикуется до начала загрузки новой сцены, пока прежняя сцена и её область жизни ещё существуют. Именно на это сообщение опирается автосохранение. Сообщение ModeChangedEvent публикуется после того, как новая сцена загружена и её область построена, поэтому получатели могут сразу обращаться к новому окружению.

Фактический переход выполняет класс SceneTransitionRunner, приведённый в Листинге 3.18.

Листинг 3.18 – Скрипт SceneTransitionRunner.cs
```csharp
public class SceneTransitionRunner : MonoBehaviour, ISceneTransition
{
    [SerializeField] private HeadFade _fade;

    public bool IsTransitioning { get; private set; }

    public void Load(string sceneName, Action onLoaded)
    {
        if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return;
        IsTransitioning = true;
        StartCoroutine(RunRoutine(sceneName, onLoaded));
    }

    public void LoadInitial(string sceneName, Action onLoaded)
    {
        if (_fade != null) _fade.SetAlphaImmediate(1f);
        Load(sceneName, onLoaded);
    }

    private IEnumerator RunRoutine(string sceneName, Action onLoaded)
    {
        if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(1f));

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op != null) yield return op;

        onLoaded?.Invoke();

        if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(0f));

        IsTransitioning = false;
    }
}
```

Переход представлен в виде корутины из четырёх шагов: обзор плавно затемняется, сцена загружается в одиночном режиме, замещая прежнюю, вызывается обратный вызов с публикацией ModeChangedEvent, затемнение снимается. Затемнение решает две задачи:

1. Асинхронная загрузка сцены не гарантирует ровной частоты кадров и может сопровождаться короткими подвисаниями, за чёрным экраном эти артефакты не видны.
2. Служит пользователю наглядным маркером смены режима: окружение не подменяется на глазах, а проявляется уже новым. Флаг IsTransitioning защищает корутину от повторного входа – запрос перехода во время идущего перехода отбрасывается.

Тем же механизмом приложение пользуется при запуске. Сцена начальной загрузки содержит постоянный корень PersistentRoot, под которым размещены XR-риг пользователя с закреплёнными на нём панелями, исполнитель переходов и контейнер RootLifetimeScope. Точка входа AppBootstrap помечает этот корень как переживающий смены сцен и запускает первую загрузку главного меню методом LoadInitial. После первой одиночной загрузки сцена начальной загрузки выгружается, остаётся только постоянный корень. Полный текст точки входа приведён в Листинге 3.19.

Листинг 3.19 – Скрипт AppBootstrap.cs
```csharp
public class AppBootstrap : MonoBehaviour
{
    private const string MAIN_MENU_SCENE = "MainMenu";

    [SerializeField] private GameObject            _persistentRoot;
    [SerializeField] private SceneTransitionRunner _transitionRunner;

    private void Start()
    {
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (_persistentRoot != null) DontDestroyOnLoad(_persistentRoot);

        if (_transitionRunner != null)
            _transitionRunner.LoadInitial(MAIN_MENU_SCENE, null);
        else
            Debug.LogError("AppBootstrap: _transitionRunner not assigned - "
                + "first scene will not load.");
    }
}
```

Роли режимов в собранной системе распределены следующим образом. Главное меню служит отправной точкой – из него пользователь открывает сцены и настройки. Режим редактирования – основное рабочее пространство, в его области жизни зарегистрированы анимационные классы и автосохранение. Песочница повторяет редактирование за вычетом анимации и сохранения – размещение объектов, выделение и работа с костями доступны в полном объёме, но сцена временна и на диск не записывается – это пространство для свободного эксперимента, не угрожающего сохранённым сценам.

В совокупности перечисленные механизмы задают правила, по которым существует остальное приложение: шина сообщений связывает модули, не знакомя их друг с другом; иерархия областей жизни отводит каждому объекту время существования, отвечающее его роли; оркестратор переводит приложение между режимами, не теряя пользовательских данных.

## 3.2.2 Система пространственных интерфейсов

Оконная модель интерфейса предполагает плоскость экрана, на которой размещаются элементы управления. В виртуальной реальности такой плоскости нет: панели приходится располагать в том же трёхмерном пространстве, где пользователь работает с моделями, а роль курсора выполняет лучевая указка контроллера, построенная на механизмах XRI [ИСТ: пространственные интерфейсы VR]. Интерфейс приложения собран из пространственных панелей – объектов сцены с холстом Canvas, на котором размещаются кнопки, списки и поля ввода.

Общее поведение панели задаёт класс SpatialPanel. Режим крепления описывается перечислением PanelType: закрепление за пользователем (BodyLocked), неподвижное размещение в пространстве (WorldFixed) и свободное положение под управлением пользователя (Free). Особая логика предусмотрена только для первого значения – такая панель каждый кадр подводится к позиции перед камерой. Фактически в приложении применяется только этот режим. Независимо от режима крепления действует биллборд – разворот панели лицом к камере, сохраняющий текст читаемым с любой стороны. Для следования предусмотрен ленивый вариант: панель остаётся на месте, пока направление взгляда не отклонится дальше порогового угла, и лишь затем плавно перемещается к новой позиции (см. Листинг 3.20). Полный текст скрипта приведён в Приложении Б (Листинг Б.5).

Листинг 3.20 – Скрипт SpatialPanel.cs (Фрагмент режимов крепления)
```csharp
[SerializeField] private PanelType _panelType   = PanelType.BodyLocked;
[SerializeField] private bool      _billboard   = true;
[SerializeField] private Vector3   _defaultOffset = new Vector3(0, 0, 1.2f);
// ...
protected virtual void LateUpdate()
{
    if (_cameraTransform == null) return;

    if (_panelType == PanelType.BodyLocked)
        FollowCamera();

    if (_billboard)
        FaceCamera();
}

protected virtual void FollowCamera()
{
    var cam      = _cameraTransform;
    var idealPos = cam.position + cam.rotation * _defaultOffset;

    if (!_lazyFollow)
    {
        transform.position = idealPos;
        return;
    }

    if (!_lazyInit)
    {
        _lazyTarget        = idealPos;
        transform.position = idealPos;
        _lazyInit          = true;
        return;
    }

    var dir = transform.position - cam.position;
    if (dir.sqrMagnitude > 0.001f && Vector3.Angle(cam.forward, dir.normalized) > _lazyAngle)
        _lazyTarget = idealPos;

    transform.position = Vector3.Lerp(transform.position, _lazyTarget, Time.deltaTime * _lazySpeed);
}

protected void FaceCamera()
{
    var dir = transform.position - _cameraTransform.position;
    if (dir.sqrMagnitude > 0.001f)
        transform.rotation = Quaternion.LookRotation(dir);
}
```

SpatialPanel определяет поведение панели как объекта пространства. Содержимое панелей собирается из скриптов нескольких ролей, закреплённых суффиксами имён (см. Таблицу 3.2). Самостоятельные панели с суффиксом Panel владеют своим содержимым, видимостью и положением – именно они образуют окна интерфейса. Служебные виды (суффикс View) – отображающие компоненты внутри крупной панели: каждый вид отвечает за свой участок содержимого и не существует отдельно от панели-владельца. Так, панель анимации делит работу между видами транспорта, линейки кадров и указателя текущего кадра. Элементы списков (суффикс _Item) – это повторяемые составные части панелей: такой элемент представляет собой префаб одной записи с полями под её данные, и панель или вид создаёт его экземпляры по числу записей источника. По такому принципу построено всё списочное содержимое с обновляемыми данными: строки обозревателя иерархии (OutlinerNode_Item), карточки ассетов (LabAsset_Item), пункты списка сцен (SceneListNode_Item), дорожки таймлайна (TimelineRow_Item).

Таблица 3.2 – Роли скриптов пользовательского интерфейса

| Суффикс | Роль | Примеры |
|---|---|---|
| Panel | Самостоятельная панель: владеет содержимым, видимостью и положением | UserPanel, OutlinerPanel, SettingsPanel |
| View | Служебный вид: отображает свой участок содержимого панели-владельца | AnimatorTransportView, AnimatorRulerView |
| _Item | Элемент списка: префаб одной записи, создаётся по числу записей данных | OutlinerNode_Item, LabAsset_Item, TimelineRow_Item |
| Handle | Ручка: принимает ввод перетаскивания и передаёт его панели | PanelGrabHandle |

Панелей в приложении около десятка, и одновременный их показ перекрыл бы обзор. Эту задачу решает регионная модель. Каждая панель-модуль приписана к региону – группе взаимоисключающих панелей, внутри которой открытой может быть только одна. Привязку задаёт конфигурационный ассет NavBarConfig: запись о модуле содержит его идентификатор, перечень режимов приложения, в которых модуль доступен, имя региона и признак панели по умолчанию. Правила исполняет маршрутизатор PanelRegionRouter – объект корневой области жизни. Он принимает запросы открытия, закрытия и переключения, следит за единственностью открытой панели в регионе и сопровождает каждую перемену сообщением RegionChangedEvent (см. Листинг 3.21).

Листинг 3.21 – Скрипт PanelRegionRouter.cs (Фрагмент открытия и закрытия панелей)
```csharp
public void Open(string moduleId)
{
    if (!TryGetAlive(moduleId, out var surface)) return;

    if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region))
    {
        if (_openByRegion.TryGetValue(region, out var current) && current != moduleId
            && TryGetAlive(current, out var currentSurface))
        {
            currentSurface.Hide();
            ApplyButtonState(current);
        }
        _openByRegion[region] = moduleId;
        surface.Show();
        ApplyButtonState(moduleId);
        _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = moduleId });
    }
    else
    {
        surface.Show();
        ApplyButtonState(moduleId);
    }
}

public void Close(string moduleId)
{
    if (!TryGetAlive(moduleId, out var surface)) return;
    surface.Hide();
    ApplyButtonState(moduleId);

    if (_config.TryGetRegion(moduleId, out var region) && !string.IsNullOrEmpty(region)
        && _openByRegion.TryGetValue(region, out var current) && current == moduleId)
    {
        _openByRegion.Remove(region);
        _bus.Publish(new RegionChangedEvent { RegionKey = region, OpenModuleId = null });

        if (_config.TryGetRegionDefault(region, out var def) && def != moduleId)
            Open(def);
    }
}

public void Toggle(string moduleId)
{
    if (IsOpen(moduleId)) Close(moduleId);
    else Open(moduleId);
}
```

При открытии модуля прежний владелец региона скрывается; при закрытии регион не остаётся пустым – маршрутизатор возвращает в него панель по умолчанию. Видимая пользователю сторона модели – панель навигации: ряд кнопок RegionNavButton. Кнопка не хранит ссылку на панель – только строковый идентификатор модуля, задаваемый в инспекторе; тот же идентификатор служит ключом записи в NavBarConfig и ключом, под которым поверхность модуля зарегистрирована в маршрутизаторе. Связь «кнопка – конфигурация – панель» держится на совпадении этого ключа, и по нажатию кнопка лишь просит маршрутизатор переключить модуль (см. Листинг 3.22).

Листинг 3.22 – Скрипт RegionNavButton.cs (Фрагмент привязки к модулю)
```csharp
[SerializeField] private string _moduleId;
[SerializeField] private Button _button;

private PanelRegionRouter _router;

public string ModuleId => _moduleId;

[Inject]
public void Construct(PanelRegionRouter router) => _router = router;
// ...
public void SetVisible(bool visible)
{
    if (gameObject.activeSelf != visible)
        gameObject.SetActive(visible);
}

public void SetActiveHighlight(bool active)
{
    _highlight = active;
    ApplyColors();
}
// ...
private void OnClick()
{
    _router?.Toggle(_moduleId);
}
```

Состоянием кнопок – видимостью и подсветкой активной – управляет сам маршрутизатор. Настраивается кнопка лениво: её носитель стартует скрытым, и внедрение зависимостей происходит задолго до вызовов Awake и OnEnable, поэтому подготовка цветов и подключение обработчика выполняются при первом из жизненных вызовов и защищены от повторения. Получив сообщение ModeChangedEvent, маршрутизатор закрывает панели, недоступные в новом режиме, открывает панели по умолчанию в опустевших регионах и обновляет кнопки: состав интерфейса перестраивается при каждой смене режима без участия самих панелей. Постоянные панели и кнопки регистрируются в маршрутизаторе при построении корневой области жизни; членов, размещённых в сцене режима, повторно регистрирует внутрисценная область – регистрация идемпотентна, а записи, чей объект уничтожен вместе со сценой, маршрутизатор отбрасывает сам. Устройство модели иллюстрирует Рисунок 3.7; полные тексты маршрутизатора и кнопки приведены в Приложении Б (Листинги Б.6 и Б.7).

[РИСУНОК 3.7 – заготовка (СХЕМА, генерирует Клод): NavBarConfig как таблица записей слева; в центре PanelRegionRouter; справа регионы-контейнеры с панелями-модулями (в каждом выделена одна открытая); снизу ряд кнопок RegionNavButton; стрелки: кнопка → Toggle → маршрутизатор → Show/Hide поверхностей; вход ModeChangedEvent сверху]
*Рисунок 3.7 – Регионная модель пользовательского интерфейса*

Со стороны сцены модуль представляет компонент RegionMember – универсальная прослойка, предоставляющая операции показа и скрытия (см. Листинг 3.23). По умолчанию операции сводятся к включению и выключению игрового объекта. Однако при первом обращении компонент ищет на своём объекте другую реализацию интерфейса IRegionSurface и, найдя, во всём делегирует задачи ей. Так панель с особыми требованиями к показу встраивается в регионную модель без изменения основной общей логики для других элементов UI.

Листинг 3.23 – Скрипт RegionMember.cs
```csharp
public class RegionMember : MonoBehaviour, IRegionSurface
{
    [SerializeField] private string _moduleId;

    private IRegionSurface _custom;
    private bool _resolved;

    public string ModuleId => _moduleId;

    private IRegionSurface Custom
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                foreach (var s in GetComponents<IRegionSurface>())
                    if (!ReferenceEquals(s, this)) { _custom = s; break; }
            }
            return _custom;
        }
    }

    public bool IsOpen => Custom != null ? Custom.IsOpen : gameObject.activeSelf;

    public void Show()
    {
        if (Custom != null) Custom.Show();
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (Custom != null) Custom.Hide();
        else gameObject.SetActive(false);
    }
}
```

Делегирование используют только две панели. Мастер импорта ImportWizardPanel реализует логику сам: операции Show и Hide устанавливают собственный флаг открытости и активируют либо скрывают объект. Подписку на запрос импорта мастер оформляет в момент внедрения зависимостей, а не в OnEnable: почти всё время его объект неактивен, и OnEnable не выполняется, тогда как шина доставляет сообщения независимо от активности получателя; получив запрос, мастер открывает себя через маршрутизатор. Файловый браузер FileBrowserPanel – обёртка над компонентом стороннего пакета SimpleFileBrowser. Его встроенная операция Show открывает диалог выбора файла, выбранный путь публикуется сообщением FilePickedEvent, после чего регион закрывается.

Из стороннего пакета взята и экранная клавиатура VrKeyboard, обслуживающая текстовый ввод. Сами поля ввода о существовании клавиатуры не знают: на каждое поле требуется добавление небольшого компонента-посредника VrInputFieldFocusBridge, который перехватывает нажатие на поле и публикует через шину сообщение KeyboardFocusEvent со ссылкой на это поле (см. Листинг 3.24). Клавиатура подписана на это сообщение: получив его, она запоминает активное поле и направляет в него набираемые символы. Таким образом поле ввода и клавиатура не ссылаются друг на друга напрямую и связаны только сообщением, что позволяет добавлять новые поля в любые панели без изменения кода клавиатуры – достаточно навесить посредник. Подтверждение набора вызывает у активного поля обработчик завершения редактирования, а при переходе фокуса к другому полю прежнее подтверждается автоматически, и введённый текст не теряется.

Листинг 3.24 – Скрипт VrInputFieldFocusBridge.cs
```csharp
public class VrInputFieldFocusBridge : MonoBehaviour, IPointerDownHandler
{
    private TMP_InputField _field;
    private EventBus       _bus;

    private void Awake()
    {
        _field = GetComponent<TMP_InputField>();
        var scope = LifetimeScope.Find<RootLifetimeScope>();
        _bus = scope?.Container.Resolve<EventBus>();
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (_field != null)
            _bus?.Publish(new KeyboardFocusEvent { Target = _field });
    }
}
```

Носителем регионов служит пользовательская панель UserPanel – наследник класса с базовой логикой SpatialPanel и центральный элемент интерфейса, всегда доступный для пользователя. На ней закреплены панель навигации и сами контекстные панели-модули, поэтому открытые панели перемещаются вместе с ней как единое целое. Там же расположены кнопки возврата в главное меню и выхода из приложения. Вызывается панель основной кнопкой любого контроллера: нажатие показывает её перед пользователем, повторное скрывает (компонент UserPanelOpener). При первой активации панель отвязывается от XR-рига и помечается неразрушаемой для переходов между сценами. Смена сцены и соответственно режима скрывает панель со сбросом состояния, после загрузки новой сцены она открывается заново в режиме следования, сохраняя доступные для режима панели открытыми. Такая реализация не даёт панели остаться зафиксированной вдали от пользователя после перехода в другую локацию.

В режиме следования панель не повторяет каждое движение головы. Пока пользователь смотрит в её сторону и расстояние до неё держится в допустимом коридоре, панель неподвижна. Когда направление взгляда уходит за угол перецентровки либо расстояние покидает коридор, перед пользователем назначается новая точка, и панель плавно перемещается к ней со сглаживанием SmoothDamp. Перемещение идёт в горизонтальной плоскости, высота панели не меняется. Поворот выполняется отдельно и нацелен в точку чуть ниже камеры, чтобы панель была менее резко наклонена при близком нахождении к пользователю. Кнопка фиксации в виде замка переключает три состояния: следование; фиксацию позиции, при которой панель стоит на месте, но продолжает доворачиваться к пользователю; полную фиксацию позиции и поворота. Каждое нажатие сдвигает состояние на один шаг, с возвратным перебором вида 1-2-3-2-1. Текущее состояние режима следования различимо по цвету кнопки (см. Листинг 3.25).

Листинг 3.25 – Скрипт UserPanel.cs (Фрагмент режимов фиксации)
```csharp
public enum LockMode { Follow, LockPosition, LockPositionRotation }
// ...
protected override void LateUpdate()
{
    if (_cameraTransform == null) return;

    if (!_isDragging && _lockMode == LockMode.Follow)
        UpdateSmartFollow();

    if (_lockMode != LockMode.LockPositionRotation)
        FaceCameraBelow();
}

public void CycleLockMode()
{
    int next = (int)_lockMode + _lockDir;
    if (next >= 2)      { next = 2; _lockDir = -1; }
    else if (next <= 0) { next = 0; _lockDir =  1; }
    _lockMode = (LockMode)next;

    _activeTarget   = null;
    _followVelocity = Vector3.zero;
    ApplyLockVisual();
}
```

Перемещается панель за ручку в нижней части – элемент с компонентом PanelGrabHandle. Наведя на ручку лучевую указку и удерживая кнопку захвата, пользователь перетаскивает панель за рукой. В момент захвата положение панели запоминается в локальных координатах точки крепления интерактора, поэтому перенос начинается без скачка. Переносится только позиция – автодоворот продолжает действовать, и панель остаётся обращённой к пользователю на всём пути. Штатный механизм выделения XRI на ручке отключён, ввод читается напрямую – по той же схеме строится и захват объектов сцены. Полный скрипт ручки приведён в Приложении Б (Листинг Б.8).

Внизу панели расположены две кнопки, которые меняют размер панели: множитель масштаба изменяется фиксированным шагом и ограничен диапазоном от 0,6 до 2,0 кратности. Выбранное значение сохраняется при закрытии и повторном открытии панели. Полный текст скрипта приведён в Приложении Б (Листинг Б.9). Внешний вид главной пользовательской панели с навигацией и открытым модулем-меню настроек показан на Рисунке 3.8.

[РИСУНОК 3.8 – заготовка (СКРИНШОТ, готовит Макс): UserPanel с панелью навигации и открытой панелью-модулем; желательно видеть ручку перемещения и кнопку фиксации]
*Рисунок 3.8 – Пользовательская панель с панелью навигации и открытой панелью-модулем*

На описанной основе построен весь набор панелей приложения: главное меню MainMenuPanel и выбор сцен ScenePickerPanel, панель настроек SettingsPanel, обозреватель иерархии OutlinerPanel, инспектор свойств InspectorPanel, панель инструментов гизмо GizmoToolsPanel, экранная клавиатура VrKeyboard, браузер ассетов AssetBrowserPanel с мастером импорта ImportWizardPanel и файловым браузером FileBrowserPanel, панель анимации AnimatorPanel и панель экспорта ExportPanel. Назначение некоторых панелей UI раскрывается более детально в последующих разделах вместе с задачами, которые они обслуживают.

## 3.2.3 Главное меню и управление сценами

Режим главного меню служит точкой входа приложения: в нём пользователь выбирает сцену для продолжения работы, создаёт новую или начинает сеанс в песочнице. Интерфейс режима составляют две панели – список сцен ScenePickerPanel и главное меню MainMenuPanel. Обе размещены непосредственно в сцене режима и подключаются к контейнеру зависимостей при построении внутрисценной области жизни. Друг о друге панели не знают и взаимодействуют только сообщениями шины событий.

ScenePickerPanel управляет набором сохранённых сцен. При старте панель запрашивает у хранилища AppStorage перечень сцен и создаёт по строке списка на каждую запись (см. Листинг 3.26). Поле ввода имени согласуется с клавиатурой от пользовательской панели через VrInputFieldFocusBridge, как и другие поля для ввода текста. Кнопка создания передаёт введённое имя методу CreateSceneAsync хранилища, а кнопка удаления стирает выбранную сцену с диска. После каждой операции список перестраивается по свежему перечню: прежние строки уничтожаются, выбор сбрасывается, и остальной интерфейс уведомляется об этом сообщением SceneSelectedEvent с пустым идентификатором. Нажатие на строку публикует то же сообщение, заполненное идентификатором и именем выбранной сцены. Полный текст панели приведён в Приложении Б (Листинг Б.10).

Листинг 3.26 – Скрипт ScenePickerPanel.cs (Фрагмент работы со списком сцен)
```csharp
private async Task RefreshAsync()
{
    foreach (Transform child in _listRoot)
        Destroy(child.gameObject);

    _selectedItem = null;
    _deleteButton.interactable = false;
    _bus.Publish(new SceneSelectedEvent { SceneId = string.Empty, DisplayName = string.Empty });

    var scenes = await _storage.GetAllScenesAsync(CancellationToken.None);
    foreach (var (sceneId, displayName) in scenes)
        SpawnItem(sceneId, displayName);
}

private void SpawnItem(string sceneId, string displayName)
{
    var go   = Instantiate(_sceneItemPrefab, _listRoot);
    var item = go.GetComponent<SceneListNode_Item>();
    item.Init(sceneId, displayName);
    item.Clicked += OnItemClicked;
}

private void OnItemClicked(SceneListNode_Item item)
{
    _selectedItem?.SetSelected(false);
    _selectedItem = item;
    item.SetSelected(true);
    _deleteButton.interactable = true;
    _bus.Publish(new SceneSelectedEvent { SceneId = item.SceneId, DisplayName = item.DisplayName });
}
```

Строка списка сцен – характерный пример UI-префаба с компонентом в роли элемента списка (см. Листинг 3.27). Компонент хранит идентификатор и отображаемое имя сцены, по нажатию встроенной кнопки извещает панель-владельца событием Clicked, а метод SetSelected переключает цвет подложки. Связей с хранилищем и шиной у элемента нет: данные он получает при инициализации, решения принимает панель.

Листинг 3.27 – Скрипт SceneListNode_Item.cs
```csharp
public class SceneListNode_Item : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image    _background;
    [SerializeField] private Button   _button;

    [SerializeField] private Color _normalColor   = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color _selectedColor = new Color(0.3f, 0.6f, 1f, 0.4f);

    public string SceneId     { get; private set; }
    public string DisplayName { get; private set; }

    public event Action<SceneListNode_Item> Clicked;

    public void Init(string sceneId, string displayName)
    {
        SceneId     = sceneId;
        DisplayName = displayName;
        _label.text = displayName;
        _button.onClick.AddListener(() => Clicked?.Invoke(this));
        SetSelected(false);
    }

    public void SetSelected(bool selected) =>
        _background.color = selected ? _selectedColor : _normalColor;
}
```

MainMenuPanel запускает открытие сцен VR-редактора и песочницы. Панель подписана на SceneSelectedEvent, но пока сцена не выбрана, кнопка открытия неактивна, после выбора она включается, и её подпись дополняется именем сцены. Открытие состоит из трёх шагов (см. Листинг 3.28). Сначала данные сцены загружаются методом LoadSceneAsync, загруженная сцена назначается активной, после чего публикуется сообщение SceneOpenedEvent, и оркестратору режимов передаётся запрос на переход в режим редактирования. Кнопка песочницы вместо загрузки с диска запрашивает у хранилища временную преднастроенную сцену (BeginSandboxSession), существующую только в памяти, и направляет приложение в режим Sandbox. Внешний вид обеих статичных UI-панелей режима показан на Рисунке 3.9. Полный текст скрипта панели MainMenuPanel приведён в Приложении Б (Листинг Б.11).

Листинг 3.28 – Скрипт MainMenuPanel.cs (Фрагмент открытия сцены и песочницы)
```csharp
private void OnOpenSandbox()
{
    var data = _storage.BeginSandboxSession();
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.Sandbox);
}

private async Task OpenSceneAsync()
{
    if (string.IsNullOrEmpty(_selectedSceneId)) return;
    var data = await _storage.LoadSceneAsync(_selectedSceneId, CancellationToken.None);
    if (data == null) return;
    _storage.SetActiveScene(data);
    _bus.Publish(new SceneOpenedEvent { SceneId = data.SceneId });
    _orchestrator.TransitionTo(AppMode.VrEditing);
}
```

[РИСУНОК 3.9 – заготовка (СКРИНШОТ, готовит Макс): режим главного меню одним кадром – панель MainMenuPanel и список сцен ScenePickerPanel с выбранной строкой; желательно, чтобы кнопка открытия была активна и содержала имя выбранной сцены]
*Рисунок 3.9 – Главное меню и список сцен*

Запрошенный переход, выполняемый по цепочке из пункта 3.2.1, завершается сменой сцены, и её содержимое строится заново. Эту работу выполняет граф сцены SceneGraph – объект внутрисценной области, реализующий интерфейс IStartable. Граф ведёт учёт нод – единиц содержимого сцены: восстанавливает их из файла при открытии, регистрирует добавленные и удаляет лишние, выдаёт ноду по идентификатору, а при сохранении превращает текущее состояние обратно в данные. При старте граф создаёт пустой корневой объект [Spawned], под которым размещаются все ноды. Полный текст графа приведён в Приложении Б (Листинг Б.12).

Нода – игровой объект с компонентом-паспортом SceneNode (см. Листинг 3.29). Паспорт хранит идентификатор ноды, ссылку на ассет AssetRef, отображаемое имя и флаги видимости и блокировки. Методы установки синхронизируют имя и видимость с игровым объектом.

Листинг 3.29 – Скрипт SceneNode.cs
```csharp
public class SceneNode : MonoBehaviour
{
    [SerializeField] private string   _nodeId;
    [SerializeField] private AssetRef _assetRef;
    [SerializeField] private string   _displayName;
    [SerializeField] private bool     _isVisible = true;
    [SerializeField] private bool     _isLocked;

    public string   NodeId      => _nodeId;
    public AssetRef AssetRef    => _assetRef;
    public string   DisplayName => _displayName;
    public bool     IsVisible   => _isVisible;
    public bool     IsLocked    => _isLocked;

    public void Init(string nodeId, AssetRef assetRef, string displayName)
    {
        _nodeId      = nodeId;
        _assetRef    = assetRef;
        _displayName = displayName;
    }

    public void SetNodeId(string newId) => _nodeId = newId;

    public void SetDisplayName(string name)
    {
        _displayName = name;
        gameObject.name = name;
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        gameObject.SetActive(visible);
    }

    public void SetLocked(bool locked) => _isLocked = locked;
}
```

Восстановление содержимого запускается при старте графа. Граф подписывается на сообщение SceneOpenedEvent, однако одной подписки недостаточно, и порядок сообщений здесь имеет большое значение: SceneOpenedEvent публикуется ещё в сцене меню, когда граф целевой сцены не существует и принять сообщение некому. Состояние передаётся через хранилище: идентификатор активной сцены запоминается вызовом SetActiveScene, а граф при старте проверяет этот идентификатор и, обнаружив открытую сцену, начинает загрузку. Сама загрузка построена как асинхронный обход записей файла сцены (см. Листинг 3.30). По каждой записи NodeData в реестре ассетов отыскивается ассет по ссылке AssetRef, и по его шаблону восстановления создаётся игровой объект. Подробности восстановления визуальных и функциональных свойств ассетов рассматриваются в пункте 3.2.4. Созданный объект получает данные записи через компонент SceneNode, проходит внедрение зависимостей, после чего к нему применяются сохранённые трансформации и, при наличии, позы костей (структуры BonePose). Отсутствие ассета не прерывает загрузку, запись пропускается, и сцена открывается без потерянного объекта. Вторым проходом, когда созданы все ноды, по ссылкам ParentNodeId восстанавливаются родительские связи – механизм заложен в формат данных и загрузку, однако пользовательским интерфейсом пока не задействован. Завершает загрузку сообщение SceneModifiedEvent, по которому обновляются зависимые панели.

Листинг 3.30 – Скрипт SceneGraph.cs (Фрагмент построения графа сцены)
```csharp
private async Task OnSceneOpenedAsync(SceneOpenedEvent e)
{
    try
    {
        ClearAll();
        var data = await _storage.LoadSceneAsync(e.SceneId, CancellationToken.None);
        if (data?.Nodes == null) return;

        foreach (var nd in data.Nodes)
        {
            var asset = _registry.Find(nd.AssetRef);
            if (asset == null)
            {
                Debug.LogWarning($"SceneGraph: asset not found {nd.AssetRef}");
                continue;
            }
            GameObject go;
            try
            {
                go = await _spawners.RestoreAsync(asset, nd.Position, nd.Rotation, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SceneGraph: spawn failed for {nd.AssetRef} - skipping node. {ex.Message}");
                continue;
            }
            go.transform.localScale = nd.Scale;
            AddNodeInternal(go, nd.NodeId, nd.AssetRef, nd.DisplayName, nd.ParentNodeId, isLoad: true);
            _resolver.InjectGameObject(go);
            if (nd.BonePoses != null && nd.BonePoses.Count > 0)
                go.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true)?.ApplyPoses(nd.BonePoses);
        }

        foreach (var nd in data.Nodes)
        {
            if (string.IsNullOrEmpty(nd.ParentNodeId)) continue;
            if (_nodes.TryGetValue(nd.NodeId, out var child)
                && _nodes.TryGetValue(nd.ParentNodeId, out var parent))
            {
                child.transform.SetParent(parent.transform, worldPositionStays: true);
            }
        }
        _bus.Publish(new SceneModifiedEvent());
    }
    catch (Exception ex)
    {
        Debug.LogError($"SceneGraph.OnSceneOpenedAsync failed for '{e.SceneId}': {ex}");
    }
}
```

Построенный граф пользователь видит в обозревателе иерархии OutlinerPanel – постоянной панели регионной модели, размещённой внутри префаба UserPanel. Доступ к графу и выбору/выделению объектов сцены панель получает через фасад SceneContext, а о переменах узнаёт из сообщений шины. Полная перестройка списка выполняется при смене внутрисценных объектов (SceneContextChangedEvent) и при каждом изменении сцены (SceneModifiedEvent). Перемена выделения (SelectionChangedEvent) обновляет только подсветку строк, а переименование ноды (NodeRenamedEvent) и переключение режима костей (BonesVisibilityChangedEvent) правят отдельные строки без перестройки списка.

Перестройка начинается с группировки (см. Листинг 3.31): ноды распределяются по спискам с ключом-идентификатором родителя, каждый список сортируется по имени, после чего выполняется обход в глубину от корневых нод [ИСТ: обход дерева в глубину]. Глубина вложенности передаётся строке как отступ, и иерархия читается по сдвигу подписей. Для нод с ригом вместо обычного префаба строки создаётся расширенное представление элемента списка (OutlinerNode_Rig_Item); сама кнопка переключения режима костей расположена не в строке, а в другой UI-панели – инспекторе свойств объекта. Нажатие на строку передаёт идентификатор ноды классу SelectionManager; правила выделения и работа с костями относятся к взаимодействию с объектами сцены и описываются в пункте 3.2.6. Полный текст панели приведён в Приложении Б (Листинг Б.13). Вид обозревателя с иерархией объектов показан на Рисунке 3.10.

Листинг 3.31 – Скрипт OutlinerPanel.cs (Фрагмент перестройки списка)
```csharp
private void Rebuild()
{
    if (_rowsRoot == null || _objectRowPrefab == null || _rigRowPrefab == null || _ctx.Graph == null) return;
    foreach (Transform t in _rowsRoot) Destroy(t.gameObject);

    var byParent = new Dictionary<string, List<SceneNode>>();
    foreach (var pair in _ctx.Graph.Nodes)
    {
        var p = GetParentId(pair.Value) ?? "";
        if (!byParent.TryGetValue(p, out var list))
            byParent[p] = list = new List<SceneNode>();
        list.Add(pair.Value);
    }
    foreach (var list in byParent.Values)
        list.Sort((a, b) => string.Compare(
            a.DisplayName ?? "", b.DisplayName ?? "",
            StringComparison.OrdinalIgnoreCase));
    AddRowsRecursive(null, 0, byParent);
    ApplyHighlight();
}

private void AddRowsRecursive(string parentId, int depth,
                               Dictionary<string, List<SceneNode>> byParent)
{
    if (!byParent.TryGetValue(parentId ?? "", out var children)) return;
    foreach (var node in children)
    {
        var isRig = node.GetComponentInChildren<ProxyRigRuntime>(includeInactive: true) != null;
        OutlinerNode_Item row = isRig
            ? Instantiate(_rigRowPrefab, _rowsRoot)
            : Instantiate(_objectRowPrefab, _rowsRoot);

        row.Bind(node, depth * _indentPx, () =>
        {
            if (AnyBonesModeActive()) return;
            _ctx.Selection?.Select(node.NodeId);
        });
        // ...
        AddRowsRecursive(node.NodeId, depth + 1, byParent);
    }
}
```

[РИСУНОК 3.10 – заготовка (СКРИНШОТ, готовит Макс): обозреватель иерархии с несколькими объектами, вложенной нодой с отступом и строкой рига; выбранная строка подсвечена]
*Рисунок 3.10 – Обозреватель иерархии сцены*

Обратное преобразование графа в данные выполняет метод CaptureSnapshot – источник снимка для автосохранения. Метод обходит словарь нод и для каждой формирует запись NodeData: положение, поворот и масштаб берутся из трансформа, родитель определяется по компоненту SceneNode на родительском трансформе, позы костей снимаются с рига, если он присутствует. Записи обратно собираются в структуру SceneData с текущей версией схемы, и дальнейшая запись на диск уже не зависит от жизненного цикла объектов сцены.

Завершается сеанс возвратом в главное меню кнопкой пользовательской панели – прямой выход из приложения не зафиксирует все внесённые правки. Запрос проходит тот же путь перехода, что и при открытии: перед выгрузкой сцены редактирования публикуется ModeExitingEvent, по которому автосохранение фиксирует снимок графа и записывает сцену на диск. После загрузки сцены меню список сцен строится заново, и сохранённая сцена доступна для следующего сеанса – жизненный цикл сцены замыкается.

## 3.2.4 Библиотеки ассетов и импорт моделей

Содержимое сцен составляют ассеты – трёхмерные модели и плоские изображения-референсы. Записи о доступных ассетах разнесены по трём библиотекам, различающимся происхождением содержимого (см. Таблицу 3.3). Встроенная библиотека BuiltinAssetLibrary поставляется в составе приложения, импортированная ImportedAssetLibrary наполняется файлами пользователя, сохранённая SavedAssetLibrary отведена под ассеты, сохраняемые из сцены. Все три реализуют общий интерфейс IAssetLibrary с перечнем записей и операциями загрузки, сохранения, добавления и удаления, поэтому остальной код работает с любой библиотекой одинаково. Встроенная библиотека выполнена конфигурационным ассетом, её записи заполняются в инспекторе на этапе разработки, а на операции изменения она отвечает исключением – состав фиксируется при сборке. Импортированная и сохранённая библиотеки – объекты корневой области жизни, при старте приложения каждая читает свой файл из общего хранилища. Поток наполнения сохранённой библиотеки в пользовательский интерфейс пока не выведен, поэтому она остаётся пустой заготовкой, и далее речь идёт о встроенных и импортированных ассетах.

Таблица 3.3 – Библиотеки ассетов приложения

| Библиотека | Происхождение записей | Хранение | Изменяемость |
|---|---|---|---|
| Встроенная (BuiltinAssetLibrary) | поставляется с приложением | конфигурационный ассет в сборке | только чтение |
| Импортированная (ImportedAssetLibrary) | импорт файлов пользователя | файл imported-lib.json в хранилище | пополнение импортом, удаление записей |
| Сохранённая (SavedAssetLibrary) | сохранение из сцены (не реализовано) | файл saved-lib.json в хранилище | заготовка, библиотека пуста |

Единую точку поиска по библиотекам предоставляет реестр AssetRegistry (см. Листинг 3.32). По ссылке AssetRef реестр выбирает библиотеку, соответствующую полю Source, и перебором записей находит ассет с запрошенным идентификатором, а отсутствие записи возвращает пустым результатом, оставляя решение вызывающей стороне. Именно через реестр к библиотекам обращается граф сцены при загрузке – запись о ноде хранит такую ссылку вместо самой геометрии.

Листинг 3.32 – Скрипт AssetRegistry.cs
```csharp
public class AssetRegistry : IAssetRegistry
{
    private readonly BuiltinAssetLibrary  _builtin;
    private readonly ImportedAssetLibrary _imported;
    private readonly SavedAssetLibrary    _saved;

    public AssetRegistry(BuiltinAssetLibrary builtin, ImportedAssetLibrary imported, SavedAssetLibrary saved)
    {
        _builtin  = builtin;
        _imported = imported;
        _saved    = saved;
    }

    public ILabAsset Find(AssetRef r)
    {
        IAssetLibrary lib = r.Source switch
        {
            AssetSource.Builtin  => _builtin,
            AssetSource.Imported => _imported,
            AssetSource.Saved    => _saved,
            _                    => null,
        };
        if (lib == null) return null;
        foreach (var a in lib.Assets)
            if (a.Id == r.AssetId) return a;
        return null;
    }
}
```

Пользователю библиотеки представляет браузер ассетов AssetBrowserPanel – панель-модуль регионной модели (см. Рисунок 3.11). Три вкладки переключают активную библиотеку, по записям которой строится сетка карточек – элементов списка LabAsset_Item. Карточка показывает миниатюру с именем и извещает панель о выборе. Миниатюру карточке выдаёт метод ResolveIcon (см. Листинг 3.33): у встроенного ассета берётся спрайт записи, у импортированного изображение загружается из файла по ссылке ThumbnailRef – миниатюра по этой ссылке создаётся при импорте. Кнопка размещения активна при выбранной карточке в режимах редактирования и песочницы. Кнопка удаления доступна вне встроенной библиотеки и стирает запись вместе с файлом-источником. Кнопка импорта открывает как новое окно файловый браузер, который делит регион с браузером ассетов и мастером импорта, поэтому на время выбора файла панель скрывается и возвращает себя, когда регион пустеет. Полный текст панели приведён в Приложении Б (Листинг Б.14). За кнопкой импорта начинается путь импортированных ассетов. Путь встроенных короче: их записи попадают в библиотеку ещё на этапе разработки.

[РИСУНОК 3.11 – заготовка (СКРИНШОТ, готовит Макс): браузер ассетов, вкладка импортированной библиотеки с несколькими миниатюрами, выбранная карточка, активные кнопки размещения и удаления]
*Рисунок 3.11 – Браузер ассетов с миниатюрами*

Листинг 3.33 – Скрипт AssetBrowserPanel.cs (Фрагмент загрузки миниатюр)
```csharp
private Sprite ResolveIcon(ILabAsset asset)
{
    if (asset.Icon != null) return asset.Icon;

    var refPath = asset.ThumbnailRef;
    if (string.IsNullOrEmpty(refPath)) return null;

    if (_thumbCache.TryGetValue(refPath, out var cached)) return cached;

    Sprite sprite = null;
    try
    {
        var abs = _sources.AbsolutePath(refPath);
        if (System.IO.File.Exists(abs))
        {
            var bytes = System.IO.File.ReadAllBytes(abs);
            var tex   = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogWarning($"AssetBrowserPanel: failed to load thumbnail '{refPath}'. {ex.Message}");
    }

    _thumbCache[refPath] = sprite;
    return sprite;
}
```

Запись встроенной библиотеки несёт префаб, спрайт-иконку и шаблон восстановления, подготовленный заранее. Эту подготовку выполняет инструмент редактора Unity BuiltinRecipeBaker (см. Листинг 3.34): префаб записи загружается в изолированную превью-сцену, не затрагивая открытую сцену разработчика, и измеряется – по результату заполняются поля шаблона. Часть параметров выставляется вручную в редакторе – иконка, идентификатор ассета встроенной библиотеки, отображаемое имя. Для ассетов-референсов по исходному изображению дополнительно генерируется и сам префаб. Вычисленный шаблон инструмент записывает через доступ к полям типа по их именам. Он заполняет приватные сериализуемые поля записи, не требуя от рантайм-типов методов редактора Unity. Полный текст приведён в Приложении Б (Листинг Б.15).

Листинг 3.34 – Скрипт BuiltinRecipeBaker.cs (Фрагмент подготовки записи библиотеки)
```csharp
private static void BakeIndex(IList list, int i)
{
    var entry = (BuiltinLabAsset)list[i];

    AssetEntityRecipe recipe;
    GameObject generatedPrefab = null;

    switch (entry.Type)
    {
        case AssetType.Object:
            if (!TryGetPrefabPath(entry, "Object", out var objPath)) return;
            recipe = MeasurePrefab(objPath, go => ObjectEntityBuilder.RecipeFromInstance(go, AssetType.Object));
            break;

        case AssetType.Rig:
            if (!TryGetPrefabPath(entry, "Rig", out var rigPath)) return;
            recipe = MeasurePrefab(rigPath, go => RigEntityBuilder.RecipeFromInstance(
                go, entry.TerminalBonesAxis, entry.InvertTerminalBonesAxis));
            break;

        case AssetType.Reference:
            if (entry.Image == null) { Debug.LogWarning($"Bake: '{entry.Id}' Reference has no image – skipped."); return; }
            generatedPrefab = ReferenceImagePrefabGenerator.Generate(entry.Id, entry.Image, out recipe);
            break;

        default:
            Debug.LogWarning($"Bake: '{entry.Id}' unsupported AssetType {entry.Type} – skipped.");
            return;
    }

    object boxed = entry;
    RecipeField.SetValue(boxed, recipe);
    if (generatedPrefab != null)
        PrefabField.SetValue(boxed, generatedPrefab);
    list[i] = (BuiltinLabAsset)boxed;
}

private static AssetEntityRecipe MeasurePrefab(string assetPath, Func<GameObject, AssetEntityRecipe> measure)
{
    var root = PrefabUtility.LoadPrefabContents(assetPath);
    try { return measure(root); }
    finally { PrefabUtility.UnloadPrefabContents(root); }
}
```

Путь импортированных ассетов длиннее и проходит через конвейер импорта ImportPipeline – объект корневой области жизни, подписанный на сообщения выбора файла и подтверждения импорта. Выбранный в файловом браузере путь приходит конвейеру сообщением FilePickedEvent. Поддерживаемые форматы описаны набором импортёров – классов интерфейса IAssetImporter, зарегистрированных списком:

1. GltfAssetImporter принимает модели форматов .glb и .gltf [ИСТ: спецификация glTF 2.0].
2. ImageAssetImporter – изображения .png, .jpg и .jpeg.

Конвейер подбирает импортёр по расширению выбранного файла (см. Листинг 3.35), а для файла неподдерживаемого формата ограничивается предупреждением в журнале. Найденный импортёр определяет предлагаемый тип ассета – Object для моделей и Reference для изображений, и конвейер публикует запрос ImportRequestedEvent с путём, именем файла и этим типом. Полный текст конвейера приведён в Приложении Б (Листинг Б.16).

Листинг 3.35 – Скрипт ImportPipeline.cs (Фрагмент выбора импортёра)
```csharp
private void OnFilePicked(FilePickedEvent e)
{
    var handler = HandlerFor(e.Path);
    if (handler == null)
    {
        Debug.LogWarning($"ImportPipeline: no handler for '{Path.GetExtension(e.Path)}'");
        return;
    }
    _bus.Publish(new ImportRequestedEvent
    {
        FilePath      = e.Path,
        SuggestedName = Path.GetFileNameWithoutExtension(e.Path),
        SuggestedType = handler.SuggestedType,
    });
}

private IAssetImporter HandlerFor(string path)
{
    var ext = Path.GetExtension(path).ToLowerInvariant();
    return _handlers.FirstOrDefault(h => h.CanHandle(ext));
}
```

Запрос на импорт из панели файлового браузера, когда в системе уже выделен файл, открывает UI мастер импорта ImportWizardPanel (см. Рисунок 3.12). Мастер показывает имя выбранного файла, подставляет предлагаемое имя ассета в поле ввода и выставляет предлагаемый тип. Пользователь может изменить имя и переключить тип на любой из трёх – так модель со скелетом переводится в тип Rig, поскольку автоматическое распознавание скелета потребовало бы полной загрузки модели ещё до подтверждения импорта. Для скелетных моделей предусмотрена группа переключателей оси концевых/листовых костей. Они определяются, если у каких-либо костей скелета нет дочерних объектов, по которым просчитывается направление видимой части кости для остальной иерархии, поэтому ось задаётся явно с необязательной инверсией. Кнопка импорта публикует сообщение ImportConfirmedEvent с введёнными значениями, отмена – то же сообщение с признаком Confirmed, равным false, по которому конвейер ничего не предпринимает. Полный текст мастера приведён в Приложении Б (Листинг Б.17).

[РИСУНОК 3.12 – заготовка (СКРИНШОТ, готовит Макс): мастер импорта с именем файла, заполненным полем имени, переключателями типа Object/Rig/Reference и группой осей концевых костей]
*Рисунок 3.12 – Мастер импорта ассета*

Подтверждённый импорт конвейер выполняет асинхронно (см. Листинг 3.36). Импортёр создаёт запись: ассету назначается короткий случайный идентификатор, копия исходного файла помещается в каталог sources хранилища, и в запись попадает относительная ссылка на неё. Затем строится шаблон восстановления: скопированный источник загружается, и по нему фиксируются решения о коллайдере, слое взаимодействия и параметрах размещения – тем же ядром, которым подготавливаются шаблоны встроенных ассетов, поэтому ассеты одного типа получают одинаковые шаблоны независимо от происхождения. Для скелетных моделей в шаблон дополнительно вносится выбранная в мастере ось концевых костей. Чтение файлов glTF во всех случаях выполняет загрузчик GltfModelImporter, построенный на сторонней библиотеке glTFast [ИСТ: glTFast]. Запись с готовым шаблоном и миниатюрой добавляется в импортированную библиотеку, библиотека сохраняется на диск, и сообщение AssetImportedEvent извещает браузер о новом ассете. Ошибка любого шага записывается в журнал и прерывает только текущий импорт.

Листинг 3.36 – Скрипт ImportPipeline.cs (Фрагмент выполнения импорта)
```csharp
private async Task RunImportAsync(ImportConfirmedEvent e)
{
    try
    {
        var handler = HandlerFor(e.FilePath);
        if (handler == null) return;
        var record = await handler.ImportAsync(e.FilePath, e.ChosenType, e.DisplayName, CancellationToken.None);

        var recipe = await _builders.BuildAsync(record.Type, _store.AbsolutePath(record.SourceRef), CancellationToken.None);

        if (recipe.rig != null)
        {
            recipe.rig.TerminalBonesAxis       = e.TerminalBonesAxis;
            recipe.rig.InvertTerminalBonesAxis = e.InvertTerminalBonesAxis;
        }

        record.SetRecipe(recipe);

        await GenerateThumbnailAsync(record, CancellationToken.None);

        _library.Add(record);
        await _library.SaveAsync(CancellationToken.None);
        _bus.Publish(new AssetImportedEvent { AssetId = record.Id });
    }
    catch (Exception ex)
    {
        Debug.LogError($"ImportPipeline: import failed for '{e.FilePath}'. {ex}");
    }
}
```

Миниатюру изображения-референса изготавливать не требуется – ссылкой ThumbnailRef назначается сам файл-источник. Для моделей миниатюра строится рендером: загруженная модель временно помещается далеко под сценой, и ThumbnailRenderer выполняет съёмку камерой, не подключённой к выводу на дисплей (см. Листинг 3.37). Габариты модели собираются в общую ограничивающую рамку по всем отрисовываемым компонентам, а дистанция камеры вычисляется из радиуса описанной сферы и угла обзора, поэтому модель любого размера вписывается в кадр целиком. Кадр размером 256 на 256 пикселей считывается из текстуры рендера и сохраняется файлом PNG в каталог thumbnails, ссылка на него записывается в запись ассета. Сбой построения миниатюры импорт не прерывает: ассет остаётся работоспособным, лишь без изображения в галерее. Полный текст приведён в Приложении Б (Листинг Б.18). Общий ход конвейера от выбора файла до записи в библиотеке показан на Рисунке 3.13.

Листинг 3.37 – Скрипт ThumbnailRenderer.cs (Фрагмент построения миниатюры)
```csharp
internal static float FrameDistance(Bounds bounds, float verticalFovDeg)
{
    float radius  = Mathf.Max(0.0001f, bounds.extents.magnitude);
    float halfFov = verticalFovDeg * 0.5f * Mathf.Deg2Rad;
    return radius / Mathf.Sin(halfFov);
}

public Texture2D Render(GameObject model, int size, Color background)
{
    var bounds = ComputeBounds(model);

    var camGo = new GameObject("ThumbnailCamera");
    var cam   = camGo.AddComponent<Camera>();
    cam.enabled         = false;
    cam.clearFlags      = CameraClearFlags.SolidColor;
    cam.backgroundColor = background;
    cam.fieldOfView     = FovDeg * 2f;
    cam.cullingMask     = ~0;

    var dist = FrameDistance(bounds, cam.fieldOfView);
    cam.transform.position = bounds.center + ViewDir * dist;
    cam.transform.LookAt(bounds.center);
    cam.nearClipPlane = Mathf.Max(0.01f, dist - bounds.extents.magnitude * 2f);
    cam.farClipPlane  = dist + bounds.extents.magnitude * 4f;
    // ...
    var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
    var prevActive = RenderTexture.active;
    cam.targetTexture = rt;
    cam.Render();

    RenderTexture.active = rt;
    var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
    tex.Apply();
    // ...
    return tex;
}
```

[РИСУНОК 3.13 – заготовка (СХЕМА – генерирует Клод): конвейер импорта одной цепочкой: файловый браузер → FilePickedEvent → ImportPipeline (выбор импортёра по расширению) → ImportRequestedEvent → мастер импорта → ImportConfirmedEvent → копирование источника в sources → построение шаблона восстановления → миниатюра в thumbnails → запись в imported-lib.json → AssetImportedEvent → обновление браузера]
*Рисунок 3.13 – Конвейер импорта ассетов*

Когда запись – встроенная или импортированная – есть в библиотеке, остаётся поместить ассет в сцену. Размещение начинается в браузере: кнопка вычисляет точку на полу в 1,2 метра перед камерой и разворот, обращающий лицевую сторону объекта к пользователю, после чего публикует сообщение AssetSpawnRequestedEvent с выбранной записью. Принимает его AssetSpawner – объект внутрисценной области жизни (см. Листинг 3.38). К запрошенной позиции прибавляется смещение из шаблона – так изображения-референсы при первом размещении поднимаются над полом. При загрузке сцены смещение повторно не применяется, поскольку сохранённая позиция уже содержит его. Затем объект восстанавливается реестром билдеров, регистрируется нодой в графе сцены и проходит внедрение зависимостей.

Листинг 3.38 – Скрипт AssetSpawner.cs
```csharp
public class AssetSpawner : IStartable, IDisposable
{
    private readonly EventBus                   _bus;
    private readonly SceneGraph                 _graph;
    private readonly IObjectResolver            _resolver;
    private readonly AssetEntityBuilderRegistry _builders;

    public AssetSpawner(EventBus bus, SceneGraph graph, IObjectResolver resolver, AssetEntityBuilderRegistry builders)
    {
        _bus      = bus;
        _graph    = graph;
        _resolver = resolver;
        _builders = builders;
    }

    public void Start()   => _bus.Subscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);
    public void Dispose() => _bus.Unsubscribe<AssetSpawnRequestedEvent>(OnSpawnRequested);

    private void OnSpawnRequested(AssetSpawnRequestedEvent e) =>
        _ = SpawnCoreAsync(e);

    private async System.Threading.Tasks.Task SpawnCoreAsync(AssetSpawnRequestedEvent e)
    {
        try
        {
            var recipe = e.Asset.Recipe;
            var pos    = recipe != null ? e.Position + recipe.spawnOffset : e.Position;
            var go = await _builders.RestoreAsync(e.Asset, pos, e.Rotation, CancellationToken.None);
            var assetRef = new AssetRef { Source = e.Asset.Source, AssetId = e.Asset.Id };
            _graph.AddNode(go, assetRef, e.Asset.DisplayName);
            _resolver.InjectGameObject(go);
        }
        catch (Exception ex)
        {
            Debug.LogError($"AssetSpawner: failed to spawn '{e.Asset?.DisplayName}'. {ex}");
        }
    }
}
```

Реестр билдеров AssetEntityBuilderRegistry – общее ядро обработки обеих веток: он реализует принцип шаблона восстановления с однократным построением при импорте и подготовке встроенных записей и многократным восстановлением при размещении и загрузке сцены. Под каждый тип ассета зарегистрирован собственный билдер интерфейса IAssetEntityBuilder – ObjectEntityBuilder, RigEntityBuilder и ReferenceEntityBuilder, и реестр направляет вызов билдеру, соответствующему типу записи. Метод восстановления (см. Листинг 3.39) сначала получает от билдера геометрию: встроенный ассет создаётся копией префаба записи, импортированный загружается из файла-источника, референс собирается текстурированной пластиной с пропорциями из шаблона. Свойства взаимодействия билдеры не назначают – их в одной точке применяет сам реестр: операция InteractionCapability.Apply наделяет объект слоем взаимодействия, коллайдером записанного в шаблоне вида и признаком выбираемости. У скелетных моделей дополнительно регистрируются селекторные коллайдеры костей, чтобы попадание в любую кость выбирало модель целиком. Встроенный ассет без подготовленного шаблона восстановление отклоняет: без коллайдера и слоя объект оказался бы невыбираемым. Полный текст реестра приведён в Приложении Б (Листинг Б.19).

Листинг 3.39 – Скрипт AssetEntityBuilderRegistry.cs (Фрагмент восстановления ассета)
```csharp
public async Task<GameObject> RestoreAsync(ILabAsset asset, Vector3 position, Quaternion rotation, CancellationToken ct)
{
    var recipe = asset.Recipe;

    if (recipe == null && asset.Source == AssetSource.Builtin)
        throw new NotSupportedException(
            $"Builtin asset '{asset.Id}' has no baked recipe – bake it in the BuiltinAssetLibrary inspector.");

    var go = await Resolve(asset.Type).RestoreAsync(asset, recipe, position, rotation, ct);

    if (go != null && recipe != null)
        InteractionCapability.Apply(go, recipe.interactionLayer, recipe.colliderKind,
            recipe.colliderCenter, recipe.colliderSize, recipe.selectable);

    if (go != null && recipe != null && recipe.colliderKind == ColliderKind.BoneBoxes)
        go.GetComponent<ProxyRigRuntime>()?.RegisterSelectorColliders();

    return go;
}
```

Итог пути показывает Рисунок 3.14: модель, подготовленная во внешнем редакторе, появляется в сцене приложения с миниатюрой в галерее и полным набором свойств взаимодействия. Для скелетных моделей восстановление на этом не заканчивается: их шаблон несёт описание скелета, по которому поверх загруженной геометрии строится структура управляемых костей.

[РИСУНОК 3.14 – заготовка (СКРИНШОТ-КОЛЛАЖ, готовит Макс): пара кадров – модель в Blender и та же модель, размещённая в сцене приложения]
*Рисунок 3.14 – Модель во внешнем редакторе и в сцене приложения*

## 3.2.5 Построение скелетных структур

Скелетная модель отличается от статического объекта наличием управляемого скелета. В загруженной геометрии он представлен костями скинированного меша – вложенными друг в друга трансформациями без собственной видимой геометрии и коллайдеров. Напрямую выбрать или схватить такую кость в виртуальном пространстве невозможно, поэтому для работы со скелетом модели требуется отдельное представление костей.

Первый вариант представления строился на пакете Unity Animation Rigging [ИСТ: Animation Rigging]. От пакета ожидались две возможности: отображение костей и привязка их к управляющим объектам. Отображение средствами пакета оказалось редакторским – компонент BoneRenderer рисует кости только в окнах редактора Unity, в собранном приложении они не видны, поэтому видимая геометрия костей в любом случае оставалась собственной задачей. Привязка через констрейнт MultiParentConstraint была опробована: каждая пара из кости и управляющего объекта требовала собственного настроенного компонента в отдельной иерархии, конструкция собиралась громоздко и при манипуляции в виртуальной реальности вела себя ненадёжно. В результате зависимость от пакета исключена полностью. Принятое решение строит поверх модели собственную структуру прокси-костей – видимых управляемых объектов, к которым кости скелета привязываются простым компонентом следования.

Состав скелета фиксируется ещё при импорте в шаблоне восстановления. Описание оформлено структурой RigDefinition (см. Листинг 3.40): версия схемы данных, идентификатор, выбранная в мастере импорта ось концевых костей с признаком инверсии и список записей BoneRecord с именами костей.

Листинг 3.40 – Скрипты RigDefinition.cs и BoneRecord.cs (Фрагмент полей описания скелета)
```csharp
[Serializable]
public class RigDefinition
{
    public int SchemaVersion = 1;
    public string AssetId;
    public TerminalBoneAxis TerminalBonesAxis;
    public bool             InvertTerminalBonesAxis;
    public List<BoneRecord> Bones = new();
}

[Serializable]
public class BoneRecord
{
    public string BoneName;
}
```

Заполняет описание извлекатель RigDefinitionExtractor (см. Листинг 3.41). Это чистая функция над загруженной моделью: по массиву костей скинированного меша создаются записи с именами, объектов сцены при этом не строится. Когда пригодного скелета нет, извлекатель возвращает пустой результат – далее модель обрабатывается как статический объект.

Листинг 3.41 – Скрипт RigDefinitionExtractor.cs
```csharp
public static class RigDefinitionExtractor
{
    public static RigDefinition FromSkinnedMesh(SkinnedMeshRenderer smr)
    {
        if (smr == null || smr.bones == null || smr.bones.Length == 0) return null;

        var def = new RigDefinition { AssetId = smr.gameObject.name };
        foreach (var bone in smr.bones)
            if (bone != null)
                def.Bones.Add(new BoneRecord { BoneName = bone.name });

        return def.Bones.Count > 0 ? def : null;
    }
}
```

Восстановление скелетной модели начинается как у любого ассета: билдер загружает геометрию, реестр применяет свойства взаимодействия по шаблону. Записанный в шаблоне вид коллайдера BoneBoxes означает, что коллайдеры выбора объекту строит не реестр, а фабрика рига RigEntityFabricator – билдер вызывает её метод BuildProxyRig сразу после загрузки геометрии (см. Листинг 3.42). Метод отыскивает в модели кости по именам из шаблона, без списка имён берутся все кости скинированного меша. Корневой объект ProxyRig помещается рядом с узлом, содержащим корневую кость, и повторяет его локальные трансформации – так системы координат прокси-костей и настоящих костей совпадают. Затем обходом от корневых костей строится зеркальная иерархия прокси-объектов. Вместе с прокси фабрика расставляет по скелету селекторные коллайдеры всего рига – боксы, охватывающие кости до записанной в шаблоне глубины. Общее устройство построенной структуры показано на Рисунке 3.15.

[РИСУНОК 3.15 – заготовка (СХЕМА – генерирует Клод): три колонки – скелет скинированного меша (вложенные кости), структура прокси-объектов ProxyRig (зеркальная иерархия), связи BoneFollower от каждой кости к её прокси; сбоку селекторные коллайдеры рига и компонент ProxyRigRuntime на корне модели]
*Рисунок 3.15 – Структура прокси-рига скелетной модели*

Листинг 3.42 – Скрипт RigEntityFabricator.cs (Фрагмент построения иерархии прокси, комментарии опущены)
```csharp
public void BuildProxyRig(GameObject rigRoot, IReadOnlyList<string> boneNames, TerminalBoneAxis terminalAxis, bool invertAxis, int selectorDepth = 4)
{
    var transforms = ResolveTransforms(rigRoot, boneNames);
    if (transforms == null || transforms.Length == 0) return;

    var proxyGOs    = new List<GameObject>();
    var boneProxies = new Dictionary<string, Transform>();
    Transform proxyRoot = null;

    var set = new HashSet<Transform>(transforms);
    set.Remove(null);

    foreach (var bone in transforms)
    {
        if (bone == null) continue;
        if (set.Contains(bone.parent)) continue;
        if (bone.parent == null)       continue;

        if (proxyRoot == null)
        {
            var armature    = bone.parent;
            var grandParent = armature.parent;
            var rig = new GameObject("ProxyRig");
            rig.transform.SetParent(grandParent, worldPositionStays: false);
            rig.transform.localPosition = armature.localPosition;
            rig.transform.localRotation = armature.localRotation;
            rig.transform.localScale    = armature.localScale;
            proxyRoot = rig.transform;
        }

        BuildProxyNode(bone, proxyRoot, set, proxyGOs, boneProxies, terminalAxis, invertAxis);
    }

    if (proxyRoot == null) return;

    var selectorColliders = BuildSelectorColliders(transforms, set, selectorDepth);

    var runtime = rigRoot.GetComponent<ProxyRigRuntime>() ?? rigRoot.AddComponent<ProxyRigRuntime>();
    runtime.Bind(proxyRoot, proxyGOs, selectorColliders, boneProxies);
}
```

Видимая часть прокси-кости – меш-ромб. Все ромбы строятся из общего шестивершинного основания, ширина и материалы задаются конфигурационным ассетом ProxyRigConfig. Кость с дочерними костями получает комбинированный меш: по одному ромбу в направлении каждой дочерней кости, длиной до неё. У концевой кости дочерних нет, и направление её ромба берётся из шаблона (см. Листинг 3.43): заданная в мастере импорта ось с необязательной инверсией, а без явного выбора – автоматическое направление от родительской кости. Длина концевого ромба принимается равной половине смещения кости от родителя, ширина ограничивается долей длины, чтобы короткие кости не выглядели раздутыми.

Листинг 3.43 – Скрипт RigEntityFabricator.cs (Фрагмент выбора направления концевой кости)
```csharp
var worldDir    = bone.position - bone.parent.position;
float parentLen = Mathf.Max(worldDir.magnitude, 0.0001f);
float length    = parentLen * 0.5f;

Vector3 localLongAxis;
if (terminalAxis == TerminalBoneAxis.Auto)
{
    localLongAxis = bone.InverseTransformDirection(worldDir).normalized;
    if (localLongAxis.sqrMagnitude < 0.0001f) localLongAxis = Vector3.up;
}
else
{
    localLongAxis = terminalAxis switch
    {
        TerminalBoneAxis.X => Vector3.right,
        TerminalBoneAxis.Y => Vector3.up,
        TerminalBoneAxis.Z => Vector3.forward,
        _                  => Vector3.up,
    };
    if (invertAxis) localLongAxis = -localLongAxis;
}

float width = EffectiveWidth(_config.BoneWidth, length);
mesh = BuildOrientedDiamondMesh(localLongAxis, length, width);
```

Каждый прокси-объект собирается по единому составу (см. Листинг 3.44). Он получает меш-ромб с материалом и обводкой стороннего пакета QuickOutline, выпуклый меш-коллайдер по форме ромба, компонент ноды SceneNode и маркер прокси-кости BoneSceneNodeMarker, признак выбираемости и компонент взаимодействия на отдельном слое костей. На настоящую кость модели навешивается компонент следования BoneFollower со ссылкой на построенный прокси. Дочерние прокси строятся рекурсивно внутри родительского, повторяя иерархию скелета.

Листинг 3.44 – Скрипт RigEntityFabricator.cs (Фрагмент сборки прокси-объекта, комментарии опущены)
```csharp
var proxyGo = new GameObject($"proxy_{bone.name}");
proxyGo.transform.SetParent(proxyParent, worldPositionStays: false);
proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
proxyGo.transform.localScale = Vector3.one;

proxyGo.AddComponent<MeshFilter>().sharedMesh = mesh;
var mr = proxyGo.AddComponent<MeshRenderer>();
mr.sharedMaterial = _config.BoneMaterial;

var outline          = proxyGo.AddComponent<Outline>();
outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
outline.OutlineWidth = 3f;

if (_config.UseConvexCollider)
{
    var mc = proxyGo.AddComponent<MeshCollider>();
    mc.sharedMesh = mesh;
    mc.convex     = true;
}
// ...
var sceneNode = proxyGo.AddComponent<SceneNode>();
sceneNode.Init(bone.name, default, bone.name);
proxyGo.AddComponent<BoneSceneNodeMarker>();
proxyGo.AddComponent<Selectable>();
proxyGo.AddComponent<XRPromeonInteractable>().SetInteractionLayer(InteractionLayer.BoneProxies);

proxyGOs.Add(proxyGo);
boneProxies[bone.name] = proxyGo.transform;

bone.gameObject.AddComponent<BoneFollower>().SetProxy(proxyGo.transform);

foreach (var child in children)
    BuildProxyNode(child, proxyGo.transform, set, proxyGOs, boneProxies, terminalAxis, invertAxis);
```

Связь прокси и кости поддерживает компонент BoneFollower (см. Листинг 3.45). В каждом кадре после обновления сцены он копирует локальные положение и поворот своего прокси на кость, поэтому перемещение прокси-объекта немедленно отражается на скинированном меше. Масштаб переносится иначе: компонент один раз запоминает масштаб покоя кости и умножает его на масштаб прокси. Заданный модели при создании неединичный масштаб костей таким образом не разрушается, а растяжение кости передаётся дочерним через иерархию локальных масштабов.

Листинг 3.45 – Скрипт BoneFollower.cs (Комментарии опущены)
```csharp
[ExecuteAlways]
public class BoneFollower : MonoBehaviour
{
    [SerializeField] private Transform _proxy;

    private Vector3 _baseScale = Vector3.one;
    private bool    _baseCaptured;

    public void SetProxy(Transform proxy) => _proxy = proxy;

    private void Awake() => CaptureBase();

    private void CaptureBase()
    {
        if (_baseCaptured) return;
        _baseScale    = transform.localScale;
        _baseCaptured = true;
    }

    public void Tick()
    {
        if (_proxy == null) return;
        if (!_baseCaptured) CaptureBase();
        transform.localPosition = _proxy.localPosition;
        transform.localRotation = _proxy.localRotation;
        transform.localScale    = Vector3.Scale(_baseScale, _proxy.localScale);
    }

    void LateUpdate() => Tick();
    void OnDestroy() => _proxy = null;
}
```

Построенную структуру принимает во владение компонент ProxyRigRuntime – по одному на скелетную модель. Фабрика передаёт ему корень прокси-рига, список прокси-объектов, селекторные коллайдеры и словарь соответствия имён костей прокси-объектам. Словарь обслуживает перенос поз (см. Листинг 3.46): источником позы выступает локальная трансформация прокси. Метод CapturePoses собирает по словарю структуры BonePose для записи в файл сцены, ApplyPoses применяет сохранённые позы к прокси при загрузке, а записи с неизвестными именами костей пропускает. Полный текст компонента приведён в Приложении Б (Листинг Б.20).

Листинг 3.46 – Скрипт ProxyRigRuntime.cs (Фрагмент снятия и применения поз)
```csharp
public List<BonePose> CapturePoses()
{
    var poses = new List<BonePose>(_boneProxies.Count);
    foreach (var kv in _boneProxies)
    {
        var t = kv.Value;
        if (t == null) continue;
        poses.Add(new BonePose
        {
            BoneName      = kv.Key,
            LocalPosition = t.localPosition,
            LocalRotation = t.localRotation,
            LocalScale    = t.localScale,
        });
    }
    return poses;
}

public void ApplyPoses(IReadOnlyList<BonePose> poses)
{
    if (poses == null) return;
    foreach (var p in poses)
    {
        if (p == null || string.IsNullOrEmpty(p.BoneName)) continue;
        if (!_boneProxies.TryGetValue(p.BoneName, out var t) || t == null) continue;
        t.localPosition = p.LocalPosition;
        t.localRotation = p.LocalRotation;
        t.localScale    = p.LocalScale;
    }
}
```

Этот же компонент управляет видимостью структуры. Предусмотрен режим костей: прокси показываются и становятся доступными для выбора, а селекторные коллайдеры всего рига на это время отключаются (см. Листинг 3.47). В обратном режиме кости скрыты, и модель выбирается целиком – это состояние назначается сразу после построения. Переключение состояний задействуется при редактировании поз и рассматривается вместе с остальными механизмами выбора.

Листинг 3.47 – Скрипт ProxyRigRuntime.cs (Фрагмент переключения режима костей, сокращён)
```csharp
public void SetBonesInteractive(bool enabled)
{
    if (enabled && _proxyRoot != null && !_proxyRoot.gameObject.activeSelf)
        _proxyRoot.gameObject.SetActive(true);

    foreach (var go in _proxyGOs)
    {
        if (go == null) continue;
        if (enabled && !go.activeSelf) go.SetActive(true);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = enabled;
        // ...
        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = enabled;
    }

    foreach (var sc in _selectorColliders)
        if (sc != null) sc.enabled = !enabled;
    // ...
}
```

При регистрации модели в графе сцены прокси-кости получают собственные идентификаторы. Граф находит их по маркеру BoneSceneNodeMarker и переписывает идентификатор каждой ноды в составную форму bone:{идентификатор ноды модели}:{имя кости}, после чего регистрирует такие ноды транзитными: они находимы по идентификатору, но не отображаются в обозревателе иерархии и не попадают в файл сцены отдельными записями – позы костей сохраняются списком в записи самой модели. Полный текст фабрики приведён в Приложении Б (Листинг Б.21).

Вид модели с построенной структурой костей показан на Рисунке 3.16. Прокси-кости являются полноценными объектами с геометрией и коллайдерами, поэтому дальнейшая работа с ними сводится к выбору и манипуляции – общим для всех объектов сцены механизмам.

[РИСУНОК 3.16 – заготовка (СКРИНШОТ, готовит Макс): скелетная модель в режиме костей – видимые ромбы прокси-костей поверх меша, одна кость выделена]
*Рисунок 3.16 – Модель с построенными прокси-костями*

## 3.2.6 Взаимодействие с объектами и костями

Наполнение сцены редактируется непосредственно в виртуальном пространстве: объекты и прокси-кости выбираются лучом контроллера, перемещаются, вращаются и масштабируются жестами или вспомогательным манипулятором. Все операции опираются на общее состояние выбора.

Состояние выбора принадлежит объекту внутрисценной области жизни SelectionManager (см. Листинг 3.48). Он хранит идентификатор единственной выбранной ноды – одновременный выбор нескольких сущностей в приложении не предусмотрен, а значение null означает пустой выбор. Метод Select при смене значения публикует сообщение SelectionChangedEvent, на которое реагируют гизмо, прокси-риги, маска взаимодействия и панели интерфейса. К выбору ведут два пути: попадание лучом по объекту в сцене и нажатие на строку обозревателя иерархии – обработчик строки вызывает тот же метод Select с идентификатором своей ноды. Сбрасывается выбор нажатием в пустоту: компонент EmptySpaceClickDeselector отслеживает нажатия триггера обеих рук и очищает выбор, когда луч не наведён ни на объект, ни на элемент интерфейса. Полный текст компонента приведён в Приложении Б (Листинг Б.22).

Листинг 3.48 – Скрипт SelectionManager.cs
```csharp
public class SelectionManager : ISelectionManager, IStartable, IDisposable
{
    private readonly EventBus _bus;
    private string _selectedNodeId;

    public string SelectedNodeId => _selectedNodeId;

    public SelectionManager(EventBus bus) => _bus = bus;

    public void Start()   { }
    public void Dispose() { }

    public void Select(string nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        _bus.Publish(new SelectionChangedEvent { SelectedNodeId = _selectedNodeId });
    }
}
```

Видимое подтверждение выбора обеспечивает SelectionVisualSync (см. Листинг 3.49) – объект внутрисценной области, который по сообщению о смене выбора обходит ноды графа и переключает визуальное состояние их компонентов Selectable. Выбранный объект получает цветную обводку (см. Рисунок 3.17). Отрисовку обводки выполняет сторонний пакет QuickOutline [ИСТ: QuickOutline]: компонент Selectable добавляет его компонент Outline при первом выделении, а далее включает и выключает, назначая цвет и материалы из конфигурации OutlineConfig (см. Листинг 3.50). Обводки разных сущностей упорядочены приоритетами отрисовки: контур объекта рисуется первым, поверх него – контуры костей и гизмо. Строку выбранной ноды одновременно подсвечивает обозреватель иерархии, подписанный на то же сообщение.

[РИСУНОК 3.17 – заготовка (СКРИНШОТ, готовит Макс): выделенный объект сцены с цветной обводкой]
*Рисунок 3.17 – Выделенный объект с обводкой*

Листинг 3.49 – Скрипт SelectionVisualSync.cs
```csharp
public class SelectionVisualSync : IStartable, IDisposable
{
    private readonly EventBus   _bus;
    private readonly SceneGraph _graph;

    public SelectionVisualSync(EventBus bus, SceneGraph graph)
    {
        _bus   = bus;
        _graph = graph;
    }

    public void Start()   => _bus.Subscribe<SelectionChangedEvent>(OnSelectionChanged);
    public void Dispose() => _bus.Unsubscribe<SelectionChangedEvent>(OnSelectionChanged);

    private void OnSelectionChanged(SelectionChangedEvent e)
    {
        foreach (var pair in _graph.Nodes)
        {
            var sel = pair.Value.GetComponent<Selectable>();
            if (sel == null) continue;
            sel.SetVisualState(pair.Key == e.SelectedNodeId
                ? SelectionVisual.Selected
                : SelectionVisual.None);
        }
    }
}
```

Листинг 3.50 – Скрипт Selectable.cs
```csharp
public class Selectable : MonoBehaviour
{
    private SceneNode     _node;
    private Outline       _outline;
    private OutlineConfig _outlineConfig;

    public string    NodeId => _node?.NodeId;
    public SceneNode Node   => _node;

    private void Awake()
    {
        _node = GetComponent<SceneNode>();
    }

    [Inject]
    public void Construct(OutlineConfig outlineConfig)
    {
        _outlineConfig = outlineConfig;
    }

    public void SetVisualState(SelectionVisual state)
    {
        EnsureOutline();
        switch (state)
        {
            case SelectionVisual.None:
                _outline.enabled = false;
                break;
            case SelectionVisual.Selected:
                _outline.enabled        = true;
                _outline.OutlineColor   = _outlineConfig != null ? _outlineConfig.SelectColor : new Color(1f, 0.95f, 0.15f);
                _outline.OutlineWidth   = 6f;
                _outline.RenderPriority = 0;
                break;
        }
    }

    private void EnsureOutline()
    {
        if (_outline == null)
        {
            _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
        }
        if (_outlineConfig != null)
            _outline.SetOutlineMaterials(_outlineConfig.MaskMaterial, _outlineConfig.FillMaterial);
    }
}
```

Прямое взаимодействие реализует компонент XRPromeonInteractable – наследник базового интерактивного объекта пакета XR Interaction Toolkit [ИСТ: XR Interaction Toolkit]. Штатный механизм выбора пакета отключён: метод IsSelectableBy всегда возвращает false, и компонент читает входы контроллера напрямую – кнопку-триггер и боковую кнопку-грип. Логика построена машиной состояний (см. Листинг 3.51). Короткое нажатие триггера – до 0,5 секунды – выбирает объект. Удержание триггера вращает объект вслед за поворотом руки, удержание грипа перемещает его вслед за движением руки. Обе манипуляции доступны только для уже выбранного объекта, поэтому работа всегда начинается с короткого нажатия. Когда луч проходит сквозь несколько объектов, вход обрабатывает только тот, чьему коллайдеру принадлежит текущая точка попадания. Смещения захвата фиксируются в локальных координатах точки крепления руки, поэтому перемещаемый объект сохраняет взаимное расположение с контроллером. Полный текст компонента приведён в Приложении Б (Листинг Б.23).

Листинг 3.51 – Скрипт XRPromeonInteractable.cs (Фрагмент машины состояний)
```csharp
public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
{
    base.ProcessInteractable(phase);
    if (phase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

    if (_state != State.Idle && (_locked == null || !_locked.isActiveAndEnabled))
    { EndInteraction(); return; }

    if (_selectionManager == null) return;

    switch (_state)
    {
        case State.Idle:
            UpdateLastHovering();
            var ni = CurrentHoverer();
            if (ni == null) break;
            if (!IsPrimaryFor(ni)) break;

            if (ni.activateInput.ReadWasPerformedThisFrame())
            {
                Lock(ni);
                _pressTime = Time.time;
                _state = State.TriggerPressed;
                break;
            }

            if (ni.selectInput.ReadWasPerformedThisFrame() && IsObjectSelected())
            {
                Lock(ni);
                CapturePositionOffset();
                _state = State.GripMove;
            }
            break;

        case State.TriggerPressed:
            if (_locked.activateInput.ReadWasCompletedThisFrame())
            {
                var node = _node;
                EndInteraction();
                if (node != null) _selectionManager.Select(node.NodeId);
                break;
            }
            if (Time.time - _pressTime > _tapWindow && IsObjectSelected())
            {
                CaptureRotationOffset();
                _state = State.TriggerRotate;
            }
            break;

        case State.TriggerRotate:
            if (_locked.activateInput.ReadWasCompletedThisFrame())
            {
                EndInteraction();
                break;
            }
            ApplyRotate();
            break;

        case State.GripMove:
            if (_locked.selectInput.ReadWasCompletedThisFrame())
            {
                EndInteraction();
                break;
            }
            ApplyMove();
            break;
    }
}
```

Точные манипуляции выполняются гизмо – вспомогательным трёхмерным манипулятором у выбранного объекта. Его включает панель инструментов гизмо GizmoToolsPanel – панель-модуль с кнопками режимов перемещения, вращения и масштабирования. Открытие и закрытие панели и смена режима публикуются сообщениями, других обязанностей у панели нет. Принимает их компонент GizmoDriver (см. Листинг 3.52): гизмо видимо, пока панель открыта и существует цель выбора. Экземпляр создаётся из префаба, заданного конфигурацией GizmoConfig, следует за целью и сохраняет постоянный размер независимо от габаритов объекта. Для прокси-кости размер уменьшается вдвое, чтобы манипулятор не закрывал её геометрию. Видимую часть гизмо составляют хэндлы – управляющие элементы с собственными коллайдерами: стрелки осей перемещения, кольца вращения, кубы масштабирования. Текущий режим определяет, какой набор хэндлов показан. Применение гизмо показано на Рисунке 3.18. Полный текст приведён в Приложении Б (Листинг Б.24).

[РИСУНОК 3.18 – заготовка (СКРИНШОТ, готовит Макс): гизмо на выбранном объекте в момент манипуляции, по возможности с панелью инструментов гизмо в кадре]
*Рисунок 3.18 – Гизмо на выбранном объекте*

Листинг 3.52 – Скрипт GizmoDriver.cs (Фрагмент видимости гизмо)
```csharp
private void OnSelectionChanged(SelectionChangedEvent e)
{
    if (_drag.IsActive) return;
    _target       = (e.SelectedNodeId != null) ? _graph?.GetNode(e.SelectedNodeId)?.transform : null;
    _targetNodeId = e.SelectedNodeId;
    RefreshVisibility();
}

private void RefreshVisibility()
{
    bool shouldShow = _panelOpen && _target != null;
    if (shouldShow && _instance == null)       Spawn();
    else if (!shouldShow && _instance != null) Despawn();
    else if (shouldShow && _instance != null)  { Despawn(); Spawn(); }
}

private float CurrentSize()
{
    float size = _config != null ? _config.FixedSize : 1f;
    if (_target != null && _target.GetComponent<BoneSceneNodeMarker>() != null) size *= 0.5f;
    return size;
}
```

Захват хэндла начинает сессию перетаскивания GizmoDragSession. На время сессии гизмо служит первоисточником изменений: стратегия захваченного хэндла изменяет трансформацию самого гизмо, а цель подтягивается следом – позицией в режиме перемещения, поворотом в режиме вращения, а в режимах масштабирования – пропорциональным коэффициентом, вычисленным относительно момента захвата. Режим зафиксирован до отпускания хэндла. Отмена сессии возвращает цели исходную позу, снятую при захвате. Начало и конец перетаскивания публикуются сообщениями GizmoDragStartedEvent и GizmoDragEndedEvent. Полный текст сессии приведён в Приложении Б (Листинг Б.25).

Поведение каждого вида хэндла описано отдельной стратегией общего интерфейса IGizmoDragStrategy. Стратегия перемещения проецирует смещение руки на выбранную ось и сдвигает гизмо вдоль неё. Стратегии масштабирования преобразуют смещение в коэффициент, осевая изменяет одну компоненту, равномерная – все три. Стратегия вращения переводит смещение в угол по коэффициенту градусов на метр и поворачивает гизмо вокруг оси кольца. Стратегии с коэффициентами защищены мёртвой зоной – малое смещение руки не вызывает изменений. Полный текст стратегии перемещения, представляющей общую схему, приведён в Приложении Б (Листинг Б.26).

Какие коллайдеры видит луч, определяет контекстная маска. Компонент InteractionMaskBinder размещён на корневом объекте, объединяющем камеру гарнитуры и контроллеры, переживает смены сцен и слушает корневую шину. Контекстов три (см. Листинг 3.53). Когда открыта панель инструментов гизмо и есть выбранный объект, маска сужается до слоя хэндлов – выбранный объект позади гизмо не перехватывает попадания. Когда включён показ костей скелетной модели, маска переключается на слой прокси-костей, и луч проходит к кости сквозь геометрию модели. В остальное время действует слой объектов сцены. Слой интерфейса присутствует в маске всегда – графический ввод панелей читает ту же маску луча. При смене режима приложения контекст сбрасывается к слою объектов, поскольку внутрисценные издатели не повторяют свои сообщения для новой сцены. Полный текст приведён в Приложении Б (Листинг Б.27).

Листинг 3.53 – Скрипт InteractionMaskBinder.cs (Фрагмент выбора контекстной маски)
```csharp
private void OnBonesVisibility(BonesVisibilityChangedEvent e) { _bonesMode    = e.Visible;                Apply(); }
private void OnGizmoPanelOpened(GizmoToolsPanelOpenedEvent _) { _panelOpen    = true;                     Apply(); }
private void OnGizmoPanelClosed(GizmoToolsPanelClosedEvent _) { _panelOpen    = false;                    Apply(); }
private void OnSelectionChanged(SelectionChangedEvent e)      { _hasSelection = e.SelectedNodeId != null;  Apply(); }

private void Apply()
{
    InteractionLayer context =
        (_panelOpen && _hasSelection) ? InteractionLayer.GizmoHandles
        : _bonesMode                  ? InteractionLayer.BoneProxies
        :                               InteractionLayer.SceneObjects;

    int unity = InteractionLayers.UnityLayer(context);
    if (unity < 0) return;
    int mask = (1 << unity) | _uiMask;

    foreach (var c in _nearCasters) if (c != null) c.physicsLayerMask = mask;
    foreach (var c in _farCasters)  if (c != null) c.raycastMask      = mask;
}
```

Свойства выбранной сущности отображает инспектор InspectorPanel – постоянная панель-модуль пользовательской панели. По идентификатору выбора инспектор различает три состояния: пустой выбор, объект сцены и кость – идентификаторы костей распознаются префиксом bone: (см. Листинг 3.54). Для объекта отображаются тип ассета и значения позиции, поворота и масштаба. Для кости – её имя, имя рига-владельца и та же тройка трансформаций. Поле имени редактируемо: ввод сразу меняет отображаемое имя ноды и публикует сообщение переименования для обозревателя, а подтверждение дополнительно отмечает сцену изменённой. Кнопка удаления доступна только для объекта: нода удаляется из графа, выбор сбрасывается. Для скелетной модели инспектор показывает тумблер показа костей. Полный текст панели приведён в Приложении Б (Листинг Б.28).

Листинг 3.54 – Скрипт InspectorPanel.cs (Фрагмент состояний отображения)
```csharp
private void Refresh()
{
    if (!_ctx.HasScene) return;

    var activeId = _ctx.Selection.SelectedNodeId;
    var state    = string.IsNullOrEmpty(activeId)            ? InspectorState.Empty
                 : activeId.StartsWith("bone:")              ? InspectorState.Bone
                 :                                             InspectorState.Single;

    if (_emptyState != null) _emptyState.SetActive(state == InspectorState.Empty);
    if (_content    != null) _content   .SetActive(state == InspectorState.Single);
    if (_boneState  != null) _boneState .SetActive(state == InspectorState.Bone);
    // ...
    if (_deleteButton != null) _deleteButton.gameObject.SetActive(state == InspectorState.Single && _bound != null);
    // ...
}
```

Тумблер показа костей передаёт управление объекту внутрисценной области BoneEditMode (см. Листинг 3.55) – владельцу режима костей. Вход в режим включает интерактивность прокси-костей рига методом SetBonesInteractive, сбрасывает текущий выбор и публикует сообщение BonesVisibilityChangedEvent. По нему контекстная маска переводит луч на слой прокси-костей, а обозреватель помечает строку рига признаком режима. Выход возвращает выбор самому ригу, и маска возвращается к слою объектов. На время режима выбор других объектов временно заблокирован с обеих сторон: в сцене луч не видит слой объектов, а обозреватель игнорирует нажатия по строкам, пока режим костей активен. Завершается режим только тем же тумблером.

Листинг 3.55 – Скрипт BoneEditMode.cs
```csharp
public class BoneEditMode
{
    private readonly ISelectionManager _selection;
    private readonly ISceneGraph       _graph;
    private readonly EventBus          _bus;

    public string ActiveRigId { get; private set; }
    public bool   IsActive => !string.IsNullOrEmpty(ActiveRigId);

    [VContainer.Inject]
    public BoneEditMode(ISelectionManager selection, ISceneGraph graph, EventBus bus)
    {
        _selection = selection;
        _graph     = graph;
        _bus       = bus;
    }

    public void SetActive(string rigNodeId, bool on)
    {
        var rigNode = string.IsNullOrEmpty(rigNodeId) ? null : _graph?.GetNode(rigNodeId);
        var rig     = rigNode != null ? rigNode.GetComponentInChildren<ProxyRigRuntime>(true) : null;
        if (rig == null) return;

        rig.SetBonesInteractive(on);
        _bus?.Publish(new BonesVisibilityChangedEvent { RigNodeId = rigNodeId, Visible = on });

        if (on)
        {
            ActiveRigId = rigNodeId;
            _selection?.Select(null);
        }
        else
        {
            ActiveRigId = null;
            _selection?.Select(rigNodeId);
        }
    }

    public void ClearActive() => ActiveRigId = null;
}
```

К прокси-костям применимы те же приёмы, что и к объектам: короткое нажатие выбирает кость, удержание триггера и грипа вращает и перемещает её, гизмо появляется у выбранной кости в уменьшенном размере. Выбранную кость прокси-риг отмечает и визуально: по сообщению выбора он назначает костям цвет обводки и заменяет материал ромба выбранной кости на акцентный (см. Листинг 3.56). Пример показан на Рисунке 3.19.

[РИСУНОК 3.19 – заготовка (СКРИНШОТ, готовит Макс): модель в режиме костей, одна кость выбрана – акцентный материал и обводка]
*Рисунок 3.19 – Выделенная кость прокси-рига*

Листинг 3.56 – Скрипт ProxyRigRuntime.cs (Фрагмент подсветки выбранной кости)
```csharp
private void OnSelectionChanged(SelectionChangedEvent evt) => ApplyBoneSelection(evt.SelectedNodeId);

private void ApplyBoneSelection(string selectedId)
{
    foreach (var go in _proxyGOs)
    {
        if (go == null) continue;
        var sn = go.GetComponent<SceneNode>();
        if (sn == null) continue;
        bool isSelected = sn.NodeId == selectedId;

        var outline = go.GetComponent<Outline>();
        if (outline != null && _outlineConfig != null)
            outline.OutlineColor = isSelected ? _outlineConfig.BoneSelectedColor : _outlineConfig.BoneColor;

        ApplyBoneMaterial(go, isSelected);
    }
}
```

Описанные механизмы образуют единый контур редактирования: выбор определяет цель, жесты и гизмо изменяют её трансформации, инспектор отражает результат.

## 3.2.7 Создание и воспроизведение анимации

Анимация создаётся покадрово: объекты и кости расставляются в нужные позы, а позы фиксируются ключами на временной шкале. Данные организованы вокруг контейнера действия ActionContainer, структура которого приведена в пункте 3.1.2: контейнер принадлежит одной ноде-владельцу и содержит дорожки – по одной на каждую анимируемую сущность. Анимируются объекты любого типа: и примитив, и скелетная модель проходят через один механизм, различие сводится к набору дорожек. Классы анимационной системы AnimationAuthoring, AnimationClock и AnimationPlaybackSampler зарегистрированы во внутрисценной области режима редактирования – в песочнице и главном меню они отсутствуют.

Рабочее место аниматора – панель аниматора AnimatorPanel, постоянная панель-модуль на отдельной вкладке панели навигации. Содержимое панели определяется текущим выбором. Идентификатор выбранной ноды приводится к владельцу методом OwnerOf: для объекта сцены владелец совпадает с самой нодой, для кости из идентификатора с префиксом bone: извлекается идентификатор рига. Далее возможны три состояния. При пустом выборе служебный вид пустых состояний показывает подсказку выбрать объект. Если у владельца ещё нет контейнера, выводится предложение создать анимацию с кнопкой добавления. Когда контейнер существует, панель раскрывает таймлайн. Отдельно обрабатывается режим костей: пока он активен, таймлайн рига остаётся открытым даже без выбранной кости, а признак режима отслеживается и при скрытой панели – тумблер показа костей расположен в инспекторе, и режим мог включиться до открытия вкладки аниматора.

Кнопка добавления анимации создаёт контейнер с длиной и частотой кадров по умолчанию и сразу добавляет дорожку самого владельца (см. Листинг 3.57). Дорожка владельца присутствует в любом контейнере независимо от типа объекта: перемещение рига целиком записывается той же дорожкой, что и перемещение примитива. Дорожки костей добавляются позже, по мере постановки ключей. Каждая дорожка контейнера отображается строкой таймлайна – элементом списка TimelineRow_Item с именем ноды и маркерами ключей. Нажатие по имени строки выбирает её ноду, строка выбранной ноды выделена фоном, маркеры ключей различают цветом дорожки объектов и костей. Полный текст панели приведён в Приложении Б (Листинг Б.29).

Листинг 3.57 – Скрипт AnimatorPanel.cs (Фрагмент создания контейнера и перестройки дорожек)
```csharp
private void OnAddAnimationClicked()
{
    if (_ctx.Authoring == null) return;
    var selected = _ctx.Selection?.SelectedNodeId;
    var owner    = AnimationAuthoring.OwnerOf(selected);
    if (string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(_boneModeRig))
        owner = _boneModeRig;
    if (string.IsNullOrEmpty(owner)) return;

    _ctx.Authoring.CreateContainer(owner, _config.DefaultTotalFrames, _config.DefaultFps);
    _ctx.Authoring.EnsureTrack(owner, owner);
}

private void RebuildRows(ActionContainer c)
{
    foreach (var r in _rowPool) if (r != null) r.gameObject.SetActive(false);
    if (_rowsContent == null || _rowPrefab == null) return;

    for (int i = 0; i < c.Tracks.Count; i++)
    {
        var t       = c.Tracks[i];
        var go      = _ctx.Graph?.GetNode(t.NodeId);
        var display = go != null ? go.DisplayName : t.NodeId;
        bool isBone = t.NodeId.StartsWith("bone:");

        var row = GetOrCreateRow(i);
        row.gameObject.SetActive(true);
        row.Bind(t.NodeId, display, isBone, () => _ctx.Selection.Select(t.NodeId));
        row.SetActive(t.NodeId == _ctx.Selection.SelectedNodeId);
        row.SetKeys(_ctx.Authoring.GetKeyFrames(t.NodeId), _ctx.Clock.CurrentFrame);
    }
}
```

Сама панель – координатор: отображение отдельных частей таймлайна выполняют служебные виды, их роли сведены в Таблице 3.4. Числовые метрики – ширина кадра в пикселях, высота строки, размеры и цвета маркеров, значения длины и частоты по умолчанию – заданы конфигурацией AnimatorPanelConfig, общей для панели и её видов. Панель с открытым таймлайном показана на Рисунке 3.20.

Таблица 3.4 – Роли модулей панели аниматора

| Модуль | Назначение |
|---|---|
| AnimatorToolbarView | поля текущего кадра, длины и частоты кадров, кнопки ключа, интерполяции и удаления анимации |
| AnimatorTransportView | кнопки перемотки, перехода к соседним ключам, воспроизведения и переключатель циклического режима |
| AnimatorRulerView | линейка кадров с делениями и подписями через заданный интервал |
| AnimatorPlayheadView | бегунок текущего кадра с числовой меткой |
| AnimatorEmptyStateView | подсказки пустых состояний и кнопка добавления анимации |
| TimelineRow_Item | строка дорожки: имя ноды и маркеры ключей |
| TimelineScrubInput | перевод нажатий и протягиваний указателя в номер кадра |

[РИСУНОК 3.20 – заготовка (СКРИНШОТ, готовит Макс): пользовательская панель с открытой панелью аниматора – таймлайн с несколькими дорожками и ключами, видны тулбар и транспорт]
*Рисунок 3.20 – Панель аниматора с открытым таймлайном*

Поля и кнопки связаны с операциями напрямую. Поле текущего кадра, линейка и протягивание указателя по полю таймлайна перематывают позицию – TimelineScrubInput пересчитывает точку нажатия в номер кадра по ширине кадра из конфигурации. Поле длины меняет число кадров контейнера, при сокращении длины ключи за новой границей отбрасываются. Поле частоты задаёт частоту кадров сцены, общую для всех контейнеров. Кнопка интерполяции переключает режим контейнера между линейным и ступенчатым, после переключения текущий кадр подаётся повторно, и сцена пересчитывается уже по новым кривым.

Операции над данными выполняет AnimationAuthoring – владелец документа SceneAnimationData. Кнопка ключа фиксирует текущие локальные трансформации выбранной ноды: метод SetKey находит контейнер по владельцу ноды, создаёт дорожку, если ключ для этой ноды первый, и записывает либо перезаписывает ключ кадра (см. Листинг 3.58). Ключ ставится только выбранной дорожке – позы остальных дорожек кадр не затрагивает. О появлении дорожки публикуется сообщение AnimationContainerChangedEvent, о самом ключе – AnimationKeyframeChangedEvent, по ним панель перестраивает строки и маркеры. Удаление ключа симметрично: ключ снимается с выбранной дорожки, опустевшая дорожка удаляется из контейнера. Кнопки перехода к соседним ключам ищут ближайший ключ по всем дорожкам контейнера и перематывают позицию к нему. Каждая операция завершается запросом отложенной записи файла animation.json средствами AnimationStorage и перестройкой клипов семплера. Полный текст приведён в Приложении Б (Листинг Б.30).

Листинг 3.58 – Скрипт AnimationAuthoring.cs (Фрагмент постановки ключа)
```csharp
public void SetKey(string nodeId, int frame)
{
    var go = _sceneGraph?.GetNode(nodeId);
    if (go == null) return;
    SetKey(nodeId, frame, go.transform.localPosition, go.transform.localRotation, go.transform.localScale);
}

public void SetKey(string nodeId, int frame, Vector3 pos, Quaternion rot, Vector3 scale)
{
    var owner = OwnerOf(nodeId);
    if (string.IsNullOrEmpty(owner)) return;
    EnsureData();
    var c = _data.FindByOwner(owner);
    if (c == null) return;

    bool trackIsNew = c.FindTrack(nodeId) == null;
    var track       = c.GetOrCreateTrack(nodeId);
    bool existed    = track.HasKey(frame);
    track.UpsertKey(frame, pos, rot, scale);

    if (trackIsNew)
        _bus.Publish(new AnimationContainerChangedEvent
        {
            OwnerNodeId = owner,
            Change      = ContainerChange.TracksChanged
        });

    _bus.Publish(new AnimationKeyframeChangedEvent
    {
        NodeId      = nodeId,
        OwnerNodeId = owner,
        Frame       = frame,
        Change      = existed ? KeyframeChange.Overwritten : KeyframeChange.Added
    });
    RequestSave();
    _sampler?.OnDataChanged(owner);
}
```

Для воспроизведения дорожка преобразуется в кривые стандартного клипа Unity. Статический класс AnimationClipBaker строит из дорожки объект AnimationClip с десятью кривыми: три компоненты позиции, четыре компоненты кватерниона поворота и три компоненты масштаба, время каждого ключа равно отношению номера кадра к частоте [ИСТ: Unity AnimationClip]. Форму кривых между ключами определяют тангенты, и рассчитываются они вручную (см. Листинг 3.59): редакторский класс AnimationUtility, который обычно выполняет эту работу, в сборке приложения недоступен. Ступенчатому режиму соответствуют бесконечные тангенты – значение удерживается до следующего ключа, линейному – наклон, равный отношению разности значений соседних ключей к разности их времён. Полный текст приведён в Приложении Б (Листинг Б.31).

Листинг 3.59 – Скрипт AnimationClipBaker.cs (Фрагмент расчёта тангентов)
```csharp
public static void ApplyInterpolation(AnimationCurve curve, InterpolationMode mode)
{
    var keys = curve.keys;
    for (int i = 0; i < keys.Length; i++)
    {
        if (mode == InterpolationMode.Stepped)
        {
            keys[i].inTangent  = float.PositiveInfinity;
            keys[i].outTangent = float.PositiveInfinity;
        }
        else
        {
            if (i < keys.Length - 1)
            {
                float dt = keys[i + 1].time - keys[i].time;
                keys[i].outTangent = dt > 0f ? (keys[i + 1].value - keys[i].value) / dt : 0f;
            }
            if (i > 0)
            {
                float dt = keys[i].time - keys[i - 1].time;
                keys[i].inTangent = dt > 0f ? (keys[i].value - keys[i - 1].value) / dt : 0f;
            }
        }
    }
    curve.keys = keys;
}
```

Позицией воспроизведения управляет транспорт AnimationClock. Он хранит целочисленный текущий кадр и дробный накопитель позиции: каждый кадр рендеринга накопитель увеличивается на произведение прошедшего времени и частоты, а целочисленный кадр меняется только при пересечении границы (см. Листинг 3.60). О смене целого кадра публикуется сообщение FrameChangedEvent – по нему панель передвигает бегунок и обновляет метки. Воспроизведение одиночное: достигнув последнего кадра, транспорт останавливается и возвращает позицию к нулевому кадру, признак завершения передаётся в сообщении PlaybackStateChangedEvent. Кнопки транспорта обращаются к методам Play, Pause и Seek, а при смене активного контейнера панель передаёт транспорту его длину и частоту сцены методом Configure. Полный текст приведён в Приложении Б (Листинг Б.32).

Листинг 3.60 – Скрипт AnimationClock.cs (Фрагмент продвижения позиции)
```csharp
public void Tick()
{
    if (!IsPlaying) return;
    _accumulated += Time.deltaTime * Fps;
    var next = Mathf.FloorToInt(_accumulated);
    if (next == CurrentFrame) return;
    AdvanceFrame(next);
}

internal void AdvanceFrame(int next)
{
    if (next >= TotalFrames)
    {
        IsPlaying    = false;
        CurrentFrame = 0;
        _accumulated = 0f;
        _bus.Publish(new FrameChangedEvent         { Frame = 0 });
        _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = false, Frame = 0, Completed = true });
        return;
    }

    CurrentFrame = next;
    _bus.Publish(new FrameChangedEvent         { Frame     = CurrentFrame });
    _bus.Publish(new PlaybackStateChangedEvent { IsPlaying = IsPlaying, Frame = CurrentFrame });
}
```

Применение анимации к нодам сцены – семплирование – выполняет AnimationPlaybackSampler. Семплер читает документ через привязку, установленную AnimationAuthoring, хранит клипы дорожек активного контейнера и пересобирает их после каждой правки данных. Применяются клипы двумя путями. Во время воспроизведения метод Tick семплирует активный контейнер по дробной позиции транспорта каждый кадр рендеринга (см. Листинг 3.61) – движение остаётся плавным и не квантуется частотой кадров анимации, целочисленные сообщения транспорта в это время передвигают только бегунок. При перемотке и на паузе работает второй путь: по сообщению FrameChangedEvent метод ApplyFrame семплирует контейнер в позиции целого кадра (см. Листинг 3.62). Оба пути сходятся в одном методе Sample, который проходит дорожки контейнера и применяет клип каждой дорожки к её ноде. Полный текст приведён в Приложении Б (Листинг Б.33).

Листинг 3.61 – Скрипт AnimationPlaybackSampler.cs (Фрагмент покадрового семплирования)
```csharp
public void Tick()
{
    var data = Data;
    if (data == null) return;
    float fps = Fps;

    if (_loopCursors.Count > 0)
    {
        foreach (var owner in new List<string>(_loopCursors.Keys))
        {
            var c = data.FindByOwner(owner);
            if (c == null || !c.Loop) { StopLoopPlayback(owner); continue; }
            float cursor = AdvanceLoopCursor(_loopCursors[owner], Time.deltaTime * fps, c.TotalFrames);
            _loopCursors[owner] = cursor;
            if (_loopClips.TryGetValue(owner, out var clips))
                Sample(c, clips, cursor / Mathf.Max(1f, fps));
            PublishLoopFrameIfChanged(owner, cursor);
        }
    }

    if (_clock != null && _clock.IsPlaying
        && !string.IsNullOrEmpty(_activeContainerOwner)
        && !_loopCursors.ContainsKey(_activeContainerOwner)
        && fps > 0f)
    {
        var c = data.FindByOwner(_activeContainerOwner);
        if (c != null) Sample(c, _clips, _clock.CurrentFrameContinuous / fps);
    }
}
```

Листинг 3.62 – Скрипт AnimationPlaybackSampler.cs (Фрагмент применения целого кадра)
```csharp
private void ApplyFrame(int frame)
{
    if (string.IsNullOrEmpty(_activeContainerOwner)) return;
    if (_loopCursors.ContainsKey(_activeContainerOwner)) return;
    if (_clock != null && _clock.IsPlaying) return;
    var c = Data?.FindByOwner(_activeContainerOwner);
    if (c == null) return;
    int fps = Fps;
    if (fps <= 0) return;
    Sample(c, _clips, (float)frame / fps);
}

private void Sample(ActionContainer c, Dictionary<string, AnimationClip> clips, float seconds)
{
    foreach (var track in c.Tracks)
    {
        if (!clips.TryGetValue(track.NodeId, out var clip)) continue;
        var go = _graph?.GetNode(track.NodeId);
        if (go == null) continue;
        clip.SampleAnimation(go, seconds);
    }
}
```

Помимо одиночного прохода контейнер поддерживает циклическое воспроизведение. Переключатель режима в транспорте устанавливает признак Loop контейнера, после чего кнопка воспроизведения запускает и останавливает фоновый цикл вместо транспорта. Цикл идёт на собственном дробном курсоре: курсор продвигается в том же методе Tick семплера и заворачивается к началу при достижении длины контейнера (см. Листинг 3.61). Курсоры не зависят ни от транспорта, ни от текущего выбора, поэтому циклы нескольких объектов воспроизводятся одновременно – сцену можно наполнить независимо движущимися объектами и продолжать редактировать её. О смене целого кадра цикла семплер публикует сообщение LoopFrameChangedEvent, и если цикл принадлежит выбранному объекту, панель передвигает бегунок по нему. Снятие признака Loop останавливает цикл владельца.

Анимационный контур замыкается: панель аниматора переводит действия пользователя в операции над документом, AnimationAuthoring изменяет ключи и дорожки, AnimationClipBaker превращает дорожки в кривые, транспорт задаёт позицию, а семплер применяет её к нодам сцены. Результат сохраняется отложенной записью и воспроизводится в той же сцене без промежуточных преобразований.

## 3.2.8 Экспорт данных

Завершающий этап работы со сценой – экспорт. Собранная сцена вместе с объектами и анимациями упаковывается в самодостаточный ZIP-пакет для передачи во внешние инструменты трёхмерной графики. Модуль экспорта составляют панель ExportPanel и экспортёр SceneExporter – объект корневой области жизни. Связь между ними событийная: нажатие кнопки экспорта публикует SceneExportRequestedEvent с введённым именем файла, экспортёр выполняет операцию и отвечает событием SceneExportedEvent, несущим путь сохранения и сообщение о результате.

Панель экспорта открывается соответствующей кнопкой панели навигации и содержит поле имени файла, подпись конечного пути, имя активной сцены, кнопку запуска и строку статуса (Рисунок 3.21). По событию SceneContextChangedEvent панель обновляет подписи, а пустое поле имени заполняет отображаемым именем сцены. Подпись пути пересчитывается при каждом изменении имени методом BuildTargetPath экспортёра. Имя очищается от недопустимых для файловой системы символов, при пустом значении подставляется export. На время операции кнопка запуска экспорта блокируется, по событию результата строка статуса получает итоговое сообщение (см. Листинг 3.63). Полный текст панели приведён в Приложении Б (Листинг Б.34).

[РИСУНОК 3.21 – заготовка (СКРИНШОТ, готовит Макс): панель экспорта с заполненным именем файла, подписью пути и строкой статуса]
*Рисунок 3.21 – Панель экспорта сцены*

Листинг 3.63 – Скрипт ExportPanel.cs (Фрагмент запуска экспорта и обновления пути)

```csharp
private void OnExportClicked()
{
    if (_bus == null) return;
    var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
    _bus.Publish(new SceneExportRequestedEvent { FileName = name });

    if (_statusLabel != null)
        _statusLabel.text = "Exporting…";
    if (_exportButton != null)
        _exportButton.interactable = false;
}

private void OnExported(SceneExportedEvent e)
{
    if (_statusLabel != null)
        _statusLabel.text = e.Message;
    if (_exportButton != null)
        _exportButton.interactable = true;
}

private void RefreshPathLabel()
{
    if (_pathLabel == null || _exporter == null) return;
    var name = _fileNameInput != null ? _fileNameInput.text : string.Empty;
    _pathLabel.text = _exporter.BuildTargetPath(name);
}
```

Получив запрос, экспортёр проверяет наличие открытой сцены по свойству HasScene фасада SceneContext – при отсутствии сцены операция завершается событием с признаком неуспеха. Затем на главном потоке собирается снимок состояния: метод CaptureSnapshot графа сцены формирует структуру SceneData по текущей иерархии, метод CaptureForExport возвращает анимационные данные всех контейнеров действий. В песочнице анимационные данные отсутствуют, и соответствующая часть снимка опускается. Для каждой ноды снимка по ссылке AssetRef в реестре AssetRegistry находится запись библиотеки – хранимая в ней относительная ссылка на файл-источник преобразуется в абсолютный путь, после чего проверяется существование файла. Полный текст экспортёра приведён в Приложении Б (Листинг Б.35).

Целевую форму пакета задаёт класс SceneBundle (см. Листинг 3.64) – внешняя схема данных файла scene.json. Корень содержит версию схемы, метку времени экспорта, идентификатор и имя сцены, частоту кадров анимации и перечень нод. Нода описывается трансформацией, позами костей и анимационным блоком своего контейнера действия, включающим количество кадров, тип интерполяции, признак цикла и дорожки с ключами. Геометрию ноды задают ссылка на файл внутри архива или флаг её отсутствия geometryMissing. Структуры поз костей BonePose и ключей AnimKeyData переиспользуются из внутренних форматов без изменений.

Листинг 3.64 – Скрипт SceneBundle.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SceneBundle
{
    public int       schemaVersion = 1;
    public string    exportedAtUtc;
    public SceneRef  scene = new();
    public int       fps = 24;
    public List<Node> nodes = new();

    [Serializable]
    public class SceneRef
    {
        public string id;
        public string name;
    }

    [Serializable]
    public class Node
    {
        public string     nodeId;
        public string     displayName;
        public string     parentNodeId;
        public string     assetSource;
        public string     assetId;
        public string     assetType;
        public string     geometryFile;
        public bool       geometryMissing;
        public Vector3    position;
        public Quaternion rotation;
        public Vector3    scale;
        public List<BonePose> bonePoses = new();
        public Animation  animation;
    }

    [Serializable]
    public class Animation
    {
        public int    totalFrames;
        public string interpolation;
        public bool   loop;
        public List<Track> tracks = new();
    }

    [Serializable]
    public class Track
    {
        public string targetNodeId;
        public List<AnimKeyData> keys = new();
    }
}
```

Заполняет эту структуру статический метод BuildBundle (см. Листинг 3.65). Он выполняет единственную функцию – на вход поступают снимок сцены, анимационные данные и делегат разрешения ссылок, на выходе формируется заполненный пакет и перечень файлов для упаковки. Обращений к объектам Unity внутри метода нет – сборка пакета сведена к преобразованию данных. Для импортированного ассета с существующим файлом-источником в пакет добавляется ссылка на копию файла внутри архива. Модели попадают в каталог models, референсные изображения – в textures, имя составляется из идентификатора ассета. Повторное использование одного ассета несколькими нодами дублирования не вызывает – файл включается в перечень один раз. Встроенные ассеты не имеют исходного файла с идентификатором, так как состоят из заготовленных префабов, поэтому соответствующие ноды помечаются флагом geometryMissing и переносятся без геометрии, с одной трансформацией и анимацией.

Листинг 3.65 – Скрипт SceneExporter.cs (Фрагмент сборки пакета)

```csharp
internal static (SceneBundle bundle, List<SourceFile> sources) BuildBundle(
    SceneData scene,
    SceneAnimationData anim,
    Func<AssetRef, AssetResolution> resolve,
    string exportedAtUtc)
{
    var bundle = new SceneBundle
    {
        schemaVersion = 1,
        exportedAtUtc = exportedAtUtc,
        fps           = anim?.Fps ?? 24,
    };
    bundle.scene.id   = scene?.SceneId ?? "none";
    bundle.scene.name = scene?.DisplayName ?? "Unknown";

    var sources = new List<SourceFile>();
    var seenEntries = new HashSet<string>();

    if (scene?.Nodes != null)
    {
        foreach (var nd in scene.Nodes)
        {
            var res = resolve(nd.AssetRef);
            var node = new SceneBundle.Node
            {
                nodeId          = nd.NodeId,
                displayName     = nd.DisplayName,
                parentNodeId    = nd.ParentNodeId ?? "",
                assetSource     = res.Source.ToString(),
                assetId         = nd.AssetRef.AssetId,
                assetType       = res.Type.ToString(),
                position        = nd.Position,
                rotation        = nd.Rotation,
                scale           = nd.Scale,
                bonePoses       = nd.BonePoses != null
                                    ? new List<BonePose>(nd.BonePoses)
                                    : new List<BonePose>(),
                animation       = BuildAnimation(anim, nd.NodeId),
            };

            if (res.Source == AssetSource.Imported && res.SourceExists
                && !string.IsNullOrEmpty(res.SourcePath))
            {
                var folder = res.Type == AssetType.Reference ? "textures" : "models";
                var ext    = Path.GetExtension(res.SourcePath);
                var entry  = $"{folder}/{nd.AssetRef.AssetId}{ext}";
                node.geometryFile    = entry;
                node.geometryMissing = false;
                if (seenEntries.Add(entry))
                    sources.Add(new SourceFile { EntryPath = entry, AbsolutePath = res.SourcePath });
            }
            else
            {
                node.geometryFile    = "";
                node.geometryMissing = true;
            }

            bundle.nodes.Add(node);
        }
    }

    return (bundle, sources);
}
```

Запись архива вынесена с главного потока. Метод WriteZipBundle (см. Листинг 3.66) работает только с файловой системой, без обращений к Unity API, поэтому выполняется на потоке пула через Task.Run и не задерживает отрисовку кадров. Архив собирается классом ZipArchive из System.IO.Compression [ИСТ: System.IO.Compression, документация .NET]: первой записью помещается scene.json, затем потоково копируются файлы-источники. Готовый пакет сохраняется по пути Documents/{имя приложения}/{имя файла}.zip – за пределами локального хранилища, поскольку адресован пользователю, а не внутренним механизмам приложения. Сообщение результата содержит путь сохранения и количество нод, оставшихся без геометрии. Состав получившегося пакета показывает Рисунок 3.22.

Листинг 3.66 – Скрипт SceneExporter.cs (Фрагмент записи архива)

```csharp
internal static void WriteZipBundle(string zipPath, string sceneJson, IReadOnlyList<SourceFile> sources)
{
    var dir = Path.GetDirectoryName(zipPath);
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    if (File.Exists(zipPath)) File.Delete(zipPath);

    using var fs  = new FileStream(zipPath, FileMode.CreateNew);
    using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

    var jsonEntry = zip.CreateEntry("scene.json");
    using (var w = new StreamWriter(jsonEntry.Open()))
        w.Write(sceneJson);

    var seen = new HashSet<string>();
    foreach (var s in sources)
    {
        if (!seen.Add(s.EntryPath)) continue;
        if (!File.Exists(s.AbsolutePath)) continue;
        var entry = zip.CreateEntry(s.EntryPath);
        using var es  = entry.Open();
        using var src = File.OpenRead(s.AbsolutePath);
        src.CopyTo(es);
    }
}
```

[РИСУНОК 3.22 – заготовка (СКРИНШОТ, готовит Макс): экспортированный пакет, открытый в проводнике/архиваторе – scene.json, каталоги models и textures; пояснения «что где лежит» добавляются при сборке рисунка]
*Рисунок 3.22 – Состав экспортированного ZIP-пакета*

Экспортированный пакет самодостаточен, однако scene.json описывает сцену в собственной схеме приложения – односторонней, не предназначенной для обратного импорта, – а не в одном из общепринятых обменных форматов. Для считывания пакета сторонним редактором трёхмерной графики на принимающей стороне необходим программный мост – например, аддон для Blender, восстанавливающий по содержимому пакета объекты, их иерархию и анимации. Разработка такого моста является самостоятельной задачей, выходящей за рамки настоящей работы, и отнесена к направлениям развития. В текущем виде модуль экспорта замыкает рабочий цикл приложения: от импорта исходных моделей до передачи собранной анимированной сцены за пределы гарнитуры.

# 3.3 Технологическое обеспечение

Технологическое обеспечение охватывает аппаратную конфигурацию, средства реализации и порядок развёртывания приложения. Рабочая конфигурация текущей версии – связка персонального компьютера и гарнитуры виртуальной реальности Meta Quest 3: приложение исполняется на компьютере, а подключённая гарнитура работает в режиме трансляции, принимая стереоизображение и передавая обратно положение головы и ввод контроллеров. Все рабочие данные – сцены, библиотеки ассетов и экспортные архивы – размещаются в локальном хранилище компьютера, сетевое подключение для работы не используется.

Приложение реализовано на игровом движке Unity версии 6000.3.7f1 с конвейером рендеринга Universal Render Pipeline [ИСТ: документация Unity/URP]. Взаимодействие с гарнитурой построено на открытом стандарте OpenXR [ИСТ: стандарт OpenXR], который описывает вывод изображения, отслеживание положения и ввод единым образом для устройств разных производителей. Обработка пользовательского ввода и манипуляции с виртуальными объектами опираются на пакеты XR Interaction Toolkit и Input System.

Проект подготовлен к сборке под платформу Android и потенциально готов к автономному исполнению непосредственно на гарнитуре. В текущей версии достоверно проверена конфигурация с исполнением приложения на компьютере и подключённой гарнитурой Quest 2/3/3s. Проверка автономного сценария и работы с гарнитурами других производителей отнесена к дальнейшему развитию проекта. Для эксплуатации приложения на данном этапе требуются компьютер, поддерживающий работу с виртуальной реальностью, и VR-гарнитура с парой контроллеров.
