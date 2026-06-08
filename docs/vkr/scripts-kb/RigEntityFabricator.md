---
note_type: script
subsystem: AssetBrowser / RigBuilder
listing: "3.42, 3.43, 3.44, Б.21"
---

> [!info] Назначение
> `RigEntityFabricator` — фабрика прокси-рига. Строит иерархию видимых прокси-костей (меш-ромбы) поверх загруженной модели, привязывает `BoneFollower` к реальным костям, создаёт селекторные коллайдеры рига и передаёт всё `ProxyRigRuntime`. Листинги 3.42, 3.43, 3.44, Б.21.

### Обзор

##### Роль и место

Обычный C# класс в `RootLifetimeScope`. Зависит от `GltfModelImporter` (загрузка геометрии) и `ProxyRigConfig` (конфигурационный SO: материалы, ширина ромба). Вызывается из `RigEntityBuilder.RestoreAsync`.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `BuildProxyRig(rigRoot, boneNames, ...)` | главный метод: разрешает кости → строит ProxyRig GO → рекурсивный обход `BuildProxyNode` → `BuildSelectorColliders` → `ProxyRigRuntime.Bind` |
| `BuildProxyNode(bone, ...)` | рекурсия по иерархии: mesh-ромб → GO → компоненты → `BoneFollower` |
| `BuildSelectorColliders(...)` | box-коллайдеры «охвата» рига для выбора целиком |
| `ResolveTransforms(...)` | `SkinnedMeshRenderer.bones` → фильтрация по именам из рецепта |
| `BuildOrientedDiamondMesh` / `BuildCombinedDiamondMesh` | процедурная геометрия ромба |
| `FrameDistance` | (в `ThumbnailRenderer`) аналогично — здесь вычисление ширины ромба |

---

### Разбор кода

##### BuildProxyRig — определение корня прокси и корневых костей

```csharp
var set = new HashSet<Transform>(transforms);
set.Remove(null);

foreach (var bone in transforms)
{
    if (bone == null) continue;
    if (set.Contains(bone.parent)) continue; // not a root bone of the selected set
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
```

> `set.Contains(bone.parent)` — определение «корневой» кости набора. Если родитель кости тоже входит в набор, она не является корневой — её построит рекурсия родителя. Этот фильтр предотвращает двойное построение и выбирает только точки входа в иерархию.
>
> `proxyRoot` создаётся при первой корневой кости. `armature = bone.parent` — узел-«арматура» в glTF (часто называется `Armature` в Blender). `grandParent` — родитель арматуры (корень модели). `ProxyRig` помещается туда же и копирует **локальные** трансформации арматуры, чтобы системы координат прокси и реальных костей совпадали.
>
> `worldPositionStays: false` при `SetParent` — не пересчитывать мировую позицию; вместо этого `localPosition/Rotation/Scale` выставляются явно следующими тремя строками. Если бы `worldPositionStays: true`, Unity скорректировал бы локальные значения, сохраняя мировую позицию — это дало бы неверные результаты при неединичном масштабе grandParent.

##### BuildProxyNode — концевая кость, выбор оси

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
```

> `bone.InverseTransformDirection(worldDir)` преобразует мировое направление «от родителя к кости» в **локальное** пространство кости. Это ось, вдоль которой реально «смотрит» кость — Auto-режим использует именно её, чтобы ромб выглядел как продолжение скелетного сегмента.
>
> `sqrMagnitude < 0.0001f` — защита от случая, когда кость совпадает по позиции с родителем (нулевой bone offset). `InverseTransformDirection(Zero) = Zero`, `normalized` от нуля = `NaN`. Fallback `Vector3.up` — произвольное, но ненулевое направление.
>
> `length = parentLen * 0.5f` — длина концевого ромба равна половине смещения от родителя. Более длинный ромб «уходил» бы за пределы видимой части кости.

##### BuildProxyNode — сборка GO, удаление старых BoneFollower

```csharp
proxyGo.transform.SetPositionAndRotation(bone.position, bone.rotation);
proxyGo.transform.localScale = Vector3.one;
// ...
foreach (var stale in bone.GetComponents<BoneFollower>())
    UnityEngine.Object.Destroy(stale);
bone.gameObject.AddComponent<BoneFollower>().SetProxy(proxyGo.transform);
```

> `SetPositionAndRotation` — атомарный вызов: Unity пересчитывает иерархию один раз вместо двух. Производительность при построении большого скелета заметно лучше двух отдельных присваиваний.
>
> `localScale = Vector3.one` — прокси всегда единичного масштаба. Масштаб передаётся косвенно через `BoneFollower`: `Vector3.Scale(_baseScale, _proxy.localScale)`. Ненулевой масштаб прокси интерпретируется как **мультипликатор** базового масштаба кости.
>
> Очистка старых `BoneFollower` через `GetComponents + Destroy` — защита от повторного вызова `BuildProxyRig` (например, при «перепечёкe» встроенного ассета в редакторе). Без очистки кость получила бы два `BoneFollower`, конфликтующих в `LateUpdate`.

##### BuildSelectorColliders — box по AABB набора ориджинов

```csharp
var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
foreach (var world in plan.WorldOrigins)
{
    var local = plan.Bone.InverseTransformPoint(world);
    min = Vector3.Min(min, local);
    max = Vector3.Max(max, local);
}

var box    = boxGo.AddComponent<BoxCollider>();
box.center = (min + max) * 0.5f;
box.size   = Vector3.Max(max - min, Vector3.one * minThk);
boxGo.SetInteractionLayer(InteractionLayer.SceneObjects);
```

> Все мировые позиции origin-точек переводятся в локальное пространство кости через `InverseTransformPoint`. AABB строится в локальном пространстве → `BoxCollider.center/size` в локальных координатах дочернего GO (который стоит в `localPosition = Vector3.zero` на кости). `Vector3.Max(size, minThk)` — минимальная толщина: у прямых цепей AABB вырождается в линию с нулевым поперечным размером — без этой защиты коллайдер не хитился бы боковым лучом.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Зачем `ProxyRig` копирует трансформации арматуры, а не корня модели?
> **О:** `bone.parent` в glTF-иерархии — это «Armature» (узел без геометрии, который несёт систему координат скелета). Копируя его `localPosition/Rotation/Scale`, мы помещаем `ProxyRig` точно в ту же систему координат — прокси и реальные кости будут выровнены с точностью до float. Если бы `ProxyRig` стоял в начале координат, первый же `LateUpdate` у `BoneFollower` рассинхронизировал бы визуал и кость.

> [!question]
> **В:** Почему `worldPositionStays: false` при `SetParent`, если мы сразу устанавливаем `localPosition`?
> **О:** `worldPositionStays: true` заставляет Unity пересчитать `localPosition` по мировой позиции объекта до прицепления. Мы всё равно перезаписываем `localPosition` следующей строкой — лишний пересчёт. `false` экономит одну матричную операцию, что при 50+ костях накапливается.

> [!question]
> **В:** Как `TerminalBoneAxis.Auto` определяет направление ромба концевой кости?
> **О:** `Auto` берёт вектор `bone.position - bone.parent.position` (мировое направление от родителя к кости) и переводит его в локальное пространство кости через `InverseTransformDirection`. Это направление и есть «ось роста» кости в её собственном пространстве — геометрически корректно для любой ориентации.

> [!question]
> **В:** Почему у концевой кости длина ромба = `parentLen * 0.5`, а не полная длина?
> **О:** Полная длина `parentLen` была бы расстоянием до родителя — для концевой кости это визуально слишком крупно (она бы перекрывала родительский ромб). Половина даёт пропорциональный «хвостик», указывающий направление, не мешая иерархии. Ширина дополнительно ограничивается через `EffectiveWidth = min(boneWidth, length * 0.2f)`.

> [!question]
> **В:** Зачем удалять старые `BoneFollower` перед добавлением нового?
> **О:** `BuildProxyRig` может быть вызван повторно (например, при пересборке встроенного ассета в редакторе). Без очистки на кости появится два `BoneFollower`: оба запустятся в `LateUpdate`, второй перезапишет результат первого с другим прокси-референсом — поведение непредсказуемо.

---

### Связи

[[AssetEntityBuilderRegistry]] · [[ProxyRigRuntime]] · [[BoneFollower]] · [[RigDefinitionExtractor]] · [[Структуры скелета]] · [[Прокси-риг]] · [[Внедрение зависимостей (VContainer)]]
