# Стартовый промпт новой сессии (3.2.6)

Привет. Продолжаем ВКР PromeonLab, раздел 3. Ты – помощник по ВКР со знаниями Unity-разработчика, база в docs/vkr/.

Перед работой прочитай: REQUIREMENTS.md (правила текста), section-3/plan-3.2.md (решения по подразделу), section-3/numbering-registry.md (занятые номера), section-3/3.2.5.md (последний принятый пункт – образец стиля).

ЗАДАЧА: пункт 3.2.6 – взаимодействие с объектами и костями (SelectionManager, подсветка SelectionVisualSync/QuickOutline, XRPromeonInteractable tap/hold/grip, гизмо GizmoDriver/GizmoDragSession и стратегии осей, маски InteractionMaskBinder, PropertyPanel/InspectorPanel, BoneEditMode). Порядок: детальный план с вопросами мне (интерактивный выбор) → текст section-3/3.2.6.md. Свободно: Листинг с 3.48, Рисунок с 3.17, Таблица с 3.4, Приложение с Б.22. Скрипты – Assets/_App/Scripts/VrInteraction/, SceneComposition/SelectionManager.cs, RigBuilder/BoneEditMode.cs.

Помни: 3.2.8 уже написан вне очереди с плейсхолдерами 3.Э-N (перенумерация после 3.2.6–3.2.7).

Критичный процесс:
- запись файлов только bash-heredoc с LF (Write/Edit на S:\ заблокированы);
- вид песочницы залипает на устаревших обрезках: файлы Макса перечитывать прямым Read, править полной перезаписью по эталону Read, после записи – контроль прямым Read;
- мои правки в текстах = решения; новые документы сразу прикреплять в чат;
- нереализованное не описывать как готовое; обратные отсылки – минимум (REQUIREMENTS 5.13).

После приёмки пункта обновить: numbering-registry.md, plan-3.2.md, PLAN.md, NEXT-SESSION-PROMPT.md.
