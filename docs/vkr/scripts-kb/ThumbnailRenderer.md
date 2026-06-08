---
note_type: script
subsystem: AssetBrowser
listing: "3.37, Б.18"
---

> [!info] Назначение
> `ThumbnailRenderer` — off-screen рендер уже загруженной модели в квадратную `Texture2D` (256×256, RGB24). Создаёт временную камеру и освещение, рендерит в `RenderTexture`, читает пиксели, уничтожает временные объекты. Ничего не знает о glTF. Листинги 3.37, Б.18.

### Обзор

##### Роль и место

Обычный C# класс (не `MonoBehaviour`), живёт в `RootLifetimeScope`. Вызывается из `ImportPipeline.GenerateThumbnailAsync` только для моделей типа `Object`/`Rig`; изображения-референсы используют файл-источник напрямую.

##### Ключевые методы

| Метод | Назначение |
|---|---|
| `FrameDistance(bounds, fovDeg)` | дистанция камеры, при которой bounding sphere вписывается в кадр |
| `Render(model, size, background)` | создаёт камеру + свет, рендерит, возвращает `Texture2D` |
| `ComputeBounds(model)` | объединяет bounds всех `Renderer` в иерархии |

---

### Разбор кода

##### FrameDistance — геометрия вписывания в кадр

```csharp
internal static float FrameDistance(Bounds bounds, float verticalFovDeg)
{
    float radius  = Mathf.Max(0.0001f, bounds.extents.magnitude);
    float halfFov = verticalFovDeg * 0.5f * Mathf.Deg2Rad;
    return radius / Mathf.Sin(halfFov);
}
```

> Формула из тригонометрии: `sin(halfFov) = radius / distance`. При `distance = radius / sin(halfFov)` bounding sphere касается краёв frustum — модель вписана точно. `Mathf.Max(0.0001f, ...)` защищает от деления на ноль, если модель без геометрии (bounds.extents == 0). `internal` открывает метод для unit-тестов в `_App.Tests`, не делая его публичным API.

##### Render — управление RenderTexture.active

```csharp
var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
var prevActive = RenderTexture.active;
cam.targetTexture = rt;
cam.Render();

RenderTexture.active = rt;
var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
tex.Apply();

RenderTexture.active = prevActive;
cam.targetTexture    = null;
rt.Release();
Object.Destroy(rt);
Object.Destroy(camGo);
Object.Destroy(lightGo);
```

> `RenderTexture.active` — глобальный статик Unity: `ReadPixels` читает из текущего активного RT. Без `prevActive`/восстановления следующий рендер на дисплей мог бы читать из нашего RT. `rt.Release()` освобождает GPU-буфер немедленно; `Destroy(rt)` освобождает C#-объект. Оба вызова нужны: `Release` без `Destroy` утечёт C#-объект в GC, `Destroy` без `Release` может не освободить GPU-память до GC.
>
> `cam.enabled = false` — камера не рисует в дисплей автоматически; `cam.Render()` — ручной рендер в `targetTexture`. Без `enabled = false` Unity добавил бы её в список активных камер и она рендерила бы каждый кадр.
>
> `TextureFormat.RGB24` для итоговой `Texture2D` против `ARGB32` для `RenderTexture` — намеренно. `RT` нужна alpha-поддержка для корректного clear. Финальная текстура миниатюры — непрозрачная, `RGB24` экономит 25% памяти и ускоряет `EncodeToPNG`.

##### cam.fieldOfView = FovDeg * 2f

```csharp
cam.fieldOfView = FovDeg * 2f;  // FovDeg is the half-angle used by FrameDistance
```

> `FovDeg = 30f` — это **полу-угол** для `FrameDistance`. Unity `Camera.fieldOfView` — полный вертикальный угол. Поэтому камере назначается `60°`, а `FrameDistance` вызывается с `60°` (full FOV). Если передать `30°` в `FrameDistance` — расстояние удвоилось бы, модель заняла бы только нижнюю половину кадра.

##### ComputeBounds — объединение всех Renderer

```csharp
var renderers = model.GetComponentsInChildren<Renderer>();
if (renderers.Length == 0)
    return new Bounds(model.transform.position, Vector3.one);

var b = renderers[0].bounds;
for (int i = 1; i < renderers.Length; i++)
    b.Encapsulate(renderers[i].bounds);
return b;
```

> `Renderer.bounds` — world-space AABB. `Encapsulate` расширяет текущий AABB до включения переданного. Стартовый `b = renderers[0].bounds` вместо `new Bounds(Vector3.zero, Vector3.zero)` важен: AABB из нуля потребовал бы Encapsulate корня координат, раздув bounds для моделей не в начале координат. Fallback `Vector3.one` при пустых рендерах даёт непустой bounds — камера не застынет на нулевом расстоянии.

---

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему RenderTexture создаётся с глубиной 24, а не 0?
> **О:** Depth 24 означает 24-битный depth+stencil буфер. Без него Z-тест не работает и дальние треугольники нарисуются поверх ближних — модель отобразится неправильно. 0 уместен только для 2D-спрайтов или пост-эффектов без геометрии.

> [!question]
> **В:** Зачем сохранять и восстанавливать `RenderTexture.active`?
> **О:** `RenderTexture.active` — глобальное состояние. Если после вызова `Render` оставить его указывающим на наш RT, следующий `ReadPixels` в любом другом месте Unity прочитает данные из нашей миниатюры вместо правильного буфера. Восстановление `prevActive` = корректный teardown временного рендер-состояния.

> [!question]
> **В:** Почему для миниатюры изображения-референса ThumbnailRef = SourceRef, а рендер не делается?
> **О:** Изображение уже является своей собственной миниатюрой. Рендерить его ещё раз через off-screen камеру означало бы загрузить в GPU, нарисовать на плоскость и прочитать обратно — лишняя работа. Браузер загрузит байты напрямую через `File.ReadAllBytes` в `ResolveIcon`.

---

### Связи

[[ImportPipeline]] · [[AssetBrowserPanel]] · [[RigEntityFabricator]]
