---
note_type: script
subsystem: RigBuilder
listing: "3.45"
---

> [!info] Назначение
> `BoneFollower` — `MonoBehaviour` на реальной кости скелета. Копирует локальные позицию и поворот прокси-объекта на кость каждый `LateUpdate`. Масштаб умножается на сохранённый базовый масштаб кости, чтобы не разрушить ненулевой rest-масштаб. Листинг 3.45.

### Обзор

##### Роль и место

`[ExecuteAlways]` — работает и в режиме Play, и в Editor (при активной превью-сцене). Добавляется фабрикой `RigEntityFabricator.BuildProxyNode` непосредственно на кость `SkinnedMeshRenderer`. Связь: прокси ведёт кость; кость деформирует меш.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `SetProxy(proxy)` | назначает цель слежения (вызывается фабрикой сразу после `AddComponent`) |
| `CaptureBase()` | один раз сохраняет `transform.localScale` кости в `_baseScale` |
| `Tick()` | копирует `localPosition`, `localRotation`; `localScale = Scale(_baseScale, _proxy.localScale)` |
| `LateUpdate()` | вызывает `Tick()` — гарантирует обновление после всех `Update` |
| `OnDestroy()` | обнуляет `_proxy` для безопасности |

---

### Разбор кода

##### CaptureBase — однократный захват rest-масштаба

```csharp
private void CaptureBase()
{
    if (_baseCaptured) return;
    _baseScale    = transform.localScale;
    _baseCaptured = true;
}

private void Awake() => CaptureBase();
```

> `_baseCaptured` — guard от повторного захвата. `Awake` вызывается при создании, но компонент создаётся через `AddComponent` на уже существующем GO — `localScale` к этому моменту установлен из glTF. Второй путь к `CaptureBase()` — внутри `Tick()` (строка `if (!_baseCaptured) CaptureBase()`): на случай если `Awake` был пропущен по какой-то причине (например, GO был неактивен при создании компонента).

##### Tick — порядок копирования transform

```csharp
public void Tick()
{
    if (_proxy == null) return;
    if (!_baseCaptured) CaptureBase();
    transform.localPosition = _proxy.localPosition;
    transform.localRotation = _proxy.localRotation;
    transform.localScale    = Vector3.Scale(_baseScale, _proxy.localScale);
}
```

> `localPosition` и `localRotation` — прямая копия: прокси-объект создаётся с `localScale = Vector3.one`, его трансформации совпадают с целевыми значениями для кости. Масштаб — исключение: `Vector3.Scale(_baseScale, _proxy.localScale)` трактует `_proxy.localScale` как **мультипликатор**. Если `_proxy.localScale = (1,1,1)` (дефолт), кость сохраняет свой rest-масштаб. Если гизмо растянул прокси до `(2,1,1)`, кость получит `(2*_baseScale.x, _baseScale.y, _baseScale.z)` — растяжение передаётся дочерним костям через иерархию локальных масштабов.

##### LateUpdate — порядок обновления сцены

```csharp
void LateUpdate() => Tick();
```

> `LateUpdate` вызывается после всех `Update` и `FixedUpdate`. Пользователь перемещает прокси в `Update` (через `GizmoDriver` → манипуляция). Если бы `BoneFollower` работал в `Update`, он мог бы запуститься до обновления прокси — получилась бы задержка на кадр. `LateUpdate` гарантирует: сначала прокси обновлён, потом кость.

##### [ExecuteAlways] — зачем в Editor

```csharp
[ExecuteAlways]
public class BoneFollower : MonoBehaviour
```

> Без `[ExecuteAlways]` `LateUpdate` не вызывался бы в Editor (только в Play). Это означало бы, что скелет в Scene View выглядел бы неправильно при редактировании — кости не следовали бы за прокси. `[ExecuteAlways]` исправляет это, но требует осторожности: код должен быть безопасен для вызова в Editor-контексте (нет `Time.deltaTime`-зависимостей, нет `Destroy` и т.д.).

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему масштаб копируется через `Vector3.Scale` а не напрямую?
> **О:** Кости в glTF могут иметь ненулевой rest-масштаб (например `(1, 0.01, 1)` из-за единиц экспорта). Прямая копия `_proxy.localScale` уничтожила бы rest-масштаб. `Vector3.Scale(_baseScale, _proxy.localScale)` трактует прокси-масштаб как относительное изменение: `(1,1,1)` = без изменений, `(2,1,1)` = удвоить только по X.

> [!question]
> **В:** Зачем `_proxy = null` в `OnDestroy`?
> **О:** Если кость уничтожается (например, вся модель удаляется из сцены), ссылка на прокси-Transform освобождается для GC. Без обнуления Unity мог бы держать GO-прокси в памяти через «скрытый» strong reference из уже уничтоженного компонента (в зависимости от порядка Destroy в иерархии).

> [!question]
> **В:** Почему компонент называется `BoneFollower`, а не `ProxyBoneSync` или `BoneCopier`?
> **О:** По соглашению проекта: «pattern suffix is acceptable when it IS the domain role». `Follower` точно описывает роль: компонент **следует** за другим объектом. Это устоявшийся паттерн в Unity (LookAt, Follow, Track) — его имя мгновенно понятно без объяснений.

---

### Связи

[[RigEntityFabricator]] · [[ProxyRigRuntime]] · [[Прокси-риг]] · [[Структуры скелета]]
