# Отчёт: Рефакторинг PromeonInteractableRigBuilder — 2026-05-20

## Что было сделано

### 1. Исправление имени компонента
`[AddComponentMenu]` изменён с `"PromeonLab/Interactable Rig Builder"` на `"PromeonLab/Promeon Interactable Rig Builder"`. Теперь компонент находится в диалоге Add Component по поиску "Promeon Interactable".

### 2. Корректный fallback при отсутствии rig parent
Раньше: при `_buildConstraints=true` без установленного `_constraintRigParent` компонент создавал `Proxy_*` и `Visual_*` GOs, но без констрейнтов — коллайдеры висели в пространстве, не двигали кость.

Теперь: `Rebuild()` проверяет `_constraintRigParent` до создания `_proxyRoot`. Если rig parent не задан — тихо переходит в visual mode (proxy дочерний к кости), логирует `Debug.Log` вместо `Warning`.

### 3. Пропорциональная ширина diamond
Добавлен статический метод `EffectiveWidth(float boneWidth, float length)`:
```
effectiveWidth = Mathf.Min(boneWidth, length * 0.2f)
```
При длине кости ≥ 5×boneWidth — полная ширина. Короткие кости получают пропорционально более узкий diamond, без нагромождения.

Покрыт 3 unit-тестами (граничные случаи: длинная кость, короткая, точно на пороге).

### 4. Архитектурное разделение proxy и visual (constraint mode)
**Проблема:** independent proxy GO не следовал за костью при движении родителя через FK-иерархию — визуал расходился с реальным положением кости.

**Решение:** в constraint mode каждая пара костей теперь создаёт два GO:

| GO | Родитель | Компоненты | Список |
|---|---|---|---|
| `Proxy_*` | `_BoneProxies` | Только коллайдер (хэндл для VR захвата) | `_boneGOs` |
| `Visual_*` | Оригинальная кость | Diamond mesh + Outline | `_visualGOs` |

`Visual_*` всегда следует за костью через иерархию — автоматически, без per-frame кода. Proxy управляет костью через `MultiParentConstraint`.

Visual mode (без констрейнтов) не изменился: один комбинированный `Bone_*` GO, дочерний к кости.

### 5. Прочие улучшения
- `_boneMesh` теперь уничтожается в `DestroyBoneGOs` — нет утечки Mesh-ассета при повторном `Rebuild()`
- Предупреждение о null `_boneMaterial` логируется до присвоения, а не после
- Извлечены приватные хелперы `AddCollider` и `AddMeshAndOutline` — логика создания GO стала читаемее

## Файлы изменены

| Файл | Изменение |
|---|---|
| `Assets/_App/Subsystems/RigBuilder/PromeonInteractableRigBuilder.cs` | Все изменения выше |
| `Assets/_App/Subsystems/RigBuilder/Tests/PromeonInteractableRigBuilderTests.cs` | +3 теста для `EffectiveWidth` |
| `docs/superpowers/specs/2026-05-20-promeon-bone-renderer-design.md` | Обновлён под новую архитектуру |
| `docs/superpowers/plans/2026-05-20-rig-builder-proxy-visual-split.md` | Создан план реализации |

## Статус

Компилируется без ошибок. 11 EditMode тестов проходят. Ручное тестирование через Inspector "Rebuild" — следующий шаг перед интеграцией с полным VR flow.
