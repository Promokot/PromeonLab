---
note_type: script
subsystem: SpatialUi / Bootstrap
listings: [Л3.24]
---

> [!info] Назначение
> `VrInputFieldFocusBridge` — компонент-посредник между текстовым полем `TMP_InputField` и VR-клавиатурой `VrKeyboard`. Перехватывает нажатие на поле ввода и публикует `KeyboardFocusEvent` через `EventBus`. Клавиатура подписана на это событие и не знает о конкретных полях. Принадлежит к подсистеме `SpatialUi`, физически расположен в `Bootstrap/`. Листинг 3.24.

### Обзор

##### Роль и место

Паттерн развязки: поле и клавиатура не ссылаются друг на друга. Добавление нового поля ввода в любую панель не требует изменения кода клавиатуры — достаточно навесить `VrInputFieldFocusBridge` на GameObject с `TMP_InputField`. Клавиатура, получив `KeyboardFocusEvent`, запоминает `Target`-поле и направляет ввод в него.

Компонент реализует `IPointerDownHandler` — Unity EventSystem вызывает `OnPointerDown` при нажатии на коллайдер/Canvas-элемент лучевой указкой (через XRI's UI ray).

##### Ключевые методы

| Метод | Суть |
|---|---|
| `Awake()` | Получает `TMP_InputField` и `EventBus` из корневого scope |
| `OnPointerDown(PointerEventData)` | Публикует `KeyboardFocusEvent` |

### Разбор кода

##### Awake — нестандартное разрешение зависимостей

```csharp
private void Awake()
{
    _field = GetComponent<TMP_InputField>();
    var scope = LifetimeScope.Find<RootLifetimeScope>();
    _bus = scope?.Container.Resolve<EventBus>();
}
```

> `LifetimeScope.Find<RootLifetimeScope>()` — это **единственное** место в кодовой базе, где допустимо такое разрешение зависимостей вне DI-инфраструктуры. `VrInputFieldFocusBridge` живёт на панелях, которые могут быть в разных сценах и областях жизни, и не имеет гарантированного `[Inject]`-метода (он размещается как добавочный компонент без регистрации в VContainer). `LifetimeScope.Find<T>` — VContainer API, ищет scope по типу среди всех активных. Корневой scope не разрушается (`DontDestroyOnLoad`), поэтому поиск всегда находит его.
>
> `scope?.Container.Resolve<EventBus>()` — null-conditional на случай, если компонент попал в тестовую сцену без корневого scope. Если `scope == null`, `_bus` остаётся `null`, и `OnPointerDown` молча ничего не делает благодаря `_bus?.Publish`.

##### OnPointerDown — публикация события

```csharp
public void OnPointerDown(PointerEventData _)
{
    if (_field != null)
        _bus?.Publish(new KeyboardFocusEvent { Target = _field });
}
```

> Параметр `PointerEventData` игнорируется (`_` — имя discard). Всё, что нужно знать клавиатуре — это `Target`-поле, которое уже находится в `_field`. Позиция касания, нажатая кнопка и прочие данные события не используются.
>
> `_field != null` — явная проверка перед публикацией. Если `TMP_InputField` не найден на объекте (`GetComponent` вернул null в `Awake`), событие не публикуется и ошибки не возникает.
>
> `IPointerDownHandler.OnPointerDown` вызывается Unity EventSystem при первом касании, не при удержании или отпускании. Это корректно: клавиатуре нужно активироваться именно в момент первого тапа по полю.

### К защите

##### Вероятные вопросы

> [!question]
> **В:** Почему поле и клавиатура не ссылаются друг на друга напрямую?
> **О:** Это намеренная развязка через событие. Клавиатура — один объект в корневой области жизни. Полей ввода может быть много в разных панелях и сценах. При прямой связи каждое поле нужно было бы регистрировать в клавиатуре, а клавиатура знала бы обо всех полях. Через `KeyboardFocusEvent` добавление нового поля сводится к навешиванию одного компонента-посредника.
>
> **В:** Почему `Awake` использует `LifetimeScope.Find` вместо `[Inject]`?
> **О:** Компонент добавляется на произвольные объекты панелей, которые не регистрируются в VContainer как точки инъекции. `[Inject]` требует, чтобы объект был зарегистрирован в контейнере или передан в `builder.Inject`. Для добавочных компонентов это неудобно; `LifetimeScope.Find` — явный обход DI, допустимый только когда у объекта нет жизненного цикла в VContainer.
>
> **В:** Что произойдёт, если на поле нажать дважды подряд?
> **О:** Каждый `OnPointerDown` публикует новый `KeyboardFocusEvent` с тем же `Target`. Клавиатура получит его повторно и просто переустановит уже активное поле — без видимого эффекта. Побочных проблем нет.
>
> **В:** Как клавиатура получает введённые символы обратно в поле?
> **О:** `VrKeyboard` (сторонний пакет) хранит ссылку на `TMP_InputField`-объект `Target` из события и вызывает его API напрямую — добавляет символы, управляет курсором. `VrInputFieldFocusBridge` в этот обмен уже не вовлечён.

### Связи

[[KeyboardFocusEvent]] · [[VrKeyboard]] · [[EventBus]] · [[RootLifetimeScope]] · [[Паттерн Publish-Subscribe]] · [[Внедрение зависимостей (VContainer)]] · [[UserPanel]] · [[SpatialPanel]]
