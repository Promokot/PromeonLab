# PromeonLab — Convention-Conformance / Convention-Drift Audit (2026-05-30)

Scope: `Assets/_App/Scripts/**` + `Assets/_App/Editor/**`, judged against `CLAUDE.md` and
`Assets/_App/Documentation/conventions.md`.
Read-only investigation — no code or assets were modified.

Angle: this is **not** a bug/dead-code review (see the sibling `2026-05-30-project-review.md` for
that). For every place the code diverges from `CLAUDE.md`, this audit asks one question:
**fix the code, or fix the doc?**

> Tooling note: `git`, `Bash` and `PowerShell` were intermittently denied this session; all
> evidence below comes from `Grep`/`Glob`/`Read` over the working tree. `.meta`/prefab GUID
> cross-checks (e.g. whether a renamed type would orphan a prefab reference) could not be run and
> are flagged where relevant. ThirdParty (`QuickOutline`, `SimpleFileBrowser`, `Keyboard Package`)
> and `Assets/Samples`/`TextMesh Pro` are **out of scope** — they are vendored and CLAUDE.md
> explicitly exempts `ThirdParty/`.

---

## Summary

The single biggest gap is **the "forbidden generic suffix" rule contradicting the project's own
canonical vocabulary.** CLAUDE.md forbids `Manager`/`Handler`/`Controller`/`Service`/… yet the
same file (and `conventions.md`, and `architecture_context.md`) names core types
`SelectionManager`, `GizmoController`, `UndoKeyHandler`, `UiPanelOrchestrator`, and the architecture
doc is built almost entirely on the forbidden vocabulary. The code follows the architecture docs,
not the prohibition. This is a **doc-fix**: only **3** runtime types carry a forbidden suffix and
**all 3 are explicitly named in CLAUDE.md itself**.

Second gap: the **`FindObjectOfType`/`Find*` prohibition has a real, pervasive, *correct* exception**
(the DI-bootstrap shim inside `LifetimeScope.Configure`) that the convention never carves out — 28
sanctioned call sites vs **0** genuine violations in runtime code. Also a doc-fix.

Genuine code violations are few and small: one `async void Start` that is unwrapped, and four
`*Placeholder` types whose file name ≠ type name. Everything else is clean — and notably, the
`CancellationToken`-on-every-async rule, which I expected to be ignored, is actually **followed
thoroughly** (token threaded as last param across StorageCore/AssetBrowser/Animation).

| Category | Count | Verdict |
|---|---|---|
| A — real violations to fix in code | 4 findings | small, mechanical |
| B — conventions to rewrite to match reality | 2 findings | the important ones |

---

## Category A — Violations to Fix (in code)

| # | Finding | File:line | Rule violated | Recommended fix |
|---|---|---|---|---|
| A1 | `async void Start()` whose body `await RefreshAsync()` runs with **no try/catch wrap**. `async void` itself is allowed here (Unity entry point), but the rule requires it be *wrapped with explicit error handling*. | `Assets/_App/Scripts/SpatialUi/Panels/ScenePickerPanel.cs:27-33` | CLAUDE.md L139 / conventions.md L143: "No `async void` except Unity lifecycle entry points, **wrapped with explicit error handling**." | Wrap body in try/catch routing to the `ErrorHandling` subsystem. (Note: `MainMenuPanel`/`AssetBrowserPanel` avoid this by using `void Start` + `_ = SomethingAsync()` fire-and-forget — ScenePickerPanel is the lone `async void`.) |
| A2 | Four empty `*Placeholder` types where **file name ≠ type name**: `InputBindings.cs`→`InputBindingsPlaceholder`; `ExportPipeline.cs`→`ExportPipelinePlaceholder`; `ErrorHandling.cs`→`ErrorHandlingPlaceholder`; `AnimationPlayback.cs`→`AnimationPlaybackPlaceholder` (this last one's own comment says "intentionally empty"). | `InputBindings/InputBindings.cs:2`, `ExportPipeline/ExportPipeline.cs:2`, `ErrorHandling/ErrorHandling.cs:2`, `Animation/AnimationPlayback.cs:3` | "One public type per file — file name matches the type name exactly." | Rename file to `*Placeholder.cs`, or (better) delete the stub once the subsystem has real content. `AnimationPlayback` is dead-merged into `AnimationAuthoring`/`AnimationClock` (per its comment) → delete candidate; `ErrorHandling` already has `ErrorLevel.cs`+`Events/` → its placeholder is removable too. |
| A3 | `EditorPlaceholder` — empty `public static class EditorPlaceholder {}`; Editor asmdef now has real content. (Also dead per prior review; added here as the same scaffold/filename concern.) | `Assets/_App/Editor/EditorPlaceholder.cs` | Same one-type / no-empty-scaffold spirit as A2. | Delete. |
| A4 | `GameObject.Find(SandboxCanvasName)` in **editor** tooling — the only literal `GameObject.Find` in the whole `_App` tree; brittle string lookup. | `Assets/_App/Editor/AnimatorPanelModuleBuilder.cs:1219` | Borderline — CLAUDE.md forbids `GameObject.Find` "**at runtime**"; this is `_App.Editor`, so technically outside the ban. Smell only. | Acceptable for an editor build-tool; optionally swap for a scene-root walk. No runtime impact — leave unless it misbehaves. |

Confirmed **clean** (checked, no violation):
- **`Resources.Load`**: zero in `Scripts/**` / `Editor/**`. Only hits are vendored ThirdParty
  (`QuickOutline/Outline.cs`, `SimpleFileBrowser/FileBrowser.cs`). `FileBrowserSurface.cs:25` only
  *mentions* it in a comment explaining it deliberately broke the legacy Resources path. ✔
- **`#if UNITY_EDITOR` in runtime files**: zero in `Scripts/**`. ✔
- **`Singleton.Instance`**: none. ✔
- **`static` mutable runtime state**: every `static` in `Scripts/**` is a pure function
  (`SceneSerializer`, `RigSerializer`, `BoundsFitter`, `*Strategy.LocalAxis`,
  `AnimationAuthoring.OwnerOf`, `ModeOrchestrator.SceneNameFor`, `GizmoActivator.SafeRatio`) or a
  `static readonly` cached constant (`HeadFade.ColorId`, `GizmoHierarchy.*Id`, `UserPanel.Color*`).
  No mutable static fields. ✔
- **`CancellationToken` as last param on async methods**: followed thoroughly. Every storage/IO
  async method takes `CancellationToken ct` last (`AppStorage.*Async`, `AssetImporter.ImportAsync`,
  `ImportedAssetLibrary`/`SavedAssetLibrary.Load/SaveAsync`, `ILabAsset.SpawnAsync`,
  `AnimationAuthoring.Load/Save/DebouncedSave`). `AnimationAuthoring` even runs a real debounced
  `CancellationTokenSource` (`:22,125-127,134`). Fire-and-forget callers correctly pass
  `CancellationToken.None`. ✔
- **`async` suffix / `_camelCase` private fields / `I`+Pascal interfaces**: consistent. ✔
- **`MonoBehaviour` as data container**: none found — data lives in `[Serializable]` plain classes
  (`BoneRecord`, `NodeData`, `AnimKeyData`, etc.); see B1 note on their `public` fields. ✔

---

## Category B — Conventions to Rewrite (to match reality)

### B1 — The forbidden-suffix rule contradicts the project's own canonical names (HIGHEST PRIORITY)

**The contradiction, in CLAUDE.md's own words:**
- CLAUDE.md L153 / conventions.md L159: *"Generic type name suffixes: `Manager`, `Handler`,
  `Utils`, `Helper`, `Controller`, `Processor`, `Service`"* — **strictly forbidden**.
- The **same CLAUDE.md** names, as architecture, `SelectionManager` (L39, L55) and `GizmoController`
  (L39, L61); conventions.md L53 lists `UiPanelOrchestrator`/`PanelRegistry` as framework types.
- `architecture_context.md` is **built on the forbidden vocabulary**: `UiPanelManager`,
  `SelectionManager`, `PlaybackController`, `PlaybackSpeedController`, `ExportOrchestrator`,
  `ActionRouter`, `InteractionController`, `ErrorRecoveryHandler`, `ThumbnailService`,
  `AssetBrowserController`, `ConstraintConfigurator`, `KeyboardSource`, etc.

**Every forbidden-suffix type in `_App/Scripts`, exhaustively:**

| Type | File | Forbidden suffix | Blessed by CLAUDE.md? |
|---|---|---|---|
| `SelectionManager` (+ `ISelectionManager`) | `SceneComposition/SelectionManager.cs:4`, `ISelectionManager.cs:1` | `Manager` | **Yes** — CLAUDE.md L39 & L55 |
| `GizmoController` | `VrInteraction/GizmoController.cs:4` | `Controller` | **Yes** — CLAUDE.md L39 & L61 |
| `UndoKeyHandler` | `Bootstrap/UndoKeyHandler.cs:4` | `Handler` | **Yes** — referenced in scope code + project memory |

That is the **complete** runtime list — only **3** types. There are **no** `*Service`, `*Utils`,
`*Helper`, `*Processor` runtime types at all. (`UiPanelOrchestrator`/`ModeOrchestrator` use
`Orchestrator`, which is **not** on the forbidden list — the orchestrator family is already the
project's de-facto coordinator suffix and is compliant.)

**Verdict: fix the DOC, not the code.** The three names are domain-meaningful (`SelectionManager`
owns the single selection; `GizmoController` controls the gizmo; `UndoKeyHandler` handles the undo
key). Renaming them would contradict the architecture docs that *prescribe* them, risk orphaning the
`UndoKeyHandler` `MonoScript` GUID in scenes/prefabs (it is a scene-placed `MonoBehaviour` — a
GUID-check is mandatory before any rename), and churn for zero clarity gain.

**Proposed new wording (replaces the "forbidden suffixes" bullet):**

> **Prefer domain-oriented type names over pattern-suffix names.** Don't reach for
> `*Manager`/`*Handler`/`*Helper`/`*Utils`/`*Processor`/`*Service`/`*Controller` as a *default*
> when a domain noun exists (`SceneGraph`, not `SceneManager`; `CommandStack`, not `UndoManager`).
> A pattern suffix is acceptable when it **is** the domain role and the prefix is specific:
> `SelectionManager`, `GizmoController`, `UndoKeyHandler`, `*Orchestrator`. Banned outright:
> bare/over-generic names with no domain prefix (`Manager`, `Utils`, `Helper`, `DataController`)
> and the catch-all `*Service`/`*Utils`/`*Helper` for grab-bag classes.

This keeps the rule's real intent (no junk-drawer `Utils`/`Helper` classes) while legalizing the
three names the architecture already mandates.

---

### B2 — The `FindObjectOfType`/`Find*` ban needs an explicit DI-bootstrap carve-out

**Reality:** there are **28** `Find*` call sites in `_App/Scripts`, and **every one** sits inside a
`LifetimeScope.Configure(...)` or a `RegisterBuildCallback` lambda within it:

- `RootLifetimeScope.cs`: lines 29, 38, 45, 49, 67, 77, 79, 82.
- `VrEditingSceneScope.cs`: lines 27, 31, 35, 38, 41, 44, 49, 53, 63, 67, 71, 84.
- `SandboxSceneScope.cs`: lines 25, 29, 33, 36, 39, 42, 47, 51, 58, 62, 74.

All use `FindAnyObjectByType` / `FindObjectsByType` with `FindObjectsInactive.Include` and feed the
result straight into `builder.Inject(...)` / `RegisterInstance(...)`. This is the **sanctioned
bridge** from scene-placed `MonoBehaviour`s (instantiated by Unity, not VContainer) into the DI
graph — the *opposite* of the lazy runtime-service-location anti-pattern the rule targets.

**Genuine violations: 0.** No `Find*` appears in any non-bootstrap runtime class (panels, gizmo,
animation, storage all receive deps via `[Inject]`/constructor; e.g. `WorldClickCatcher`,
`FileBrowserSurface`, `BoneProxy`, every `*Panel` use `[Inject] Construct`).

**Verdict: fix the DOC.** Make the exception explicit so contributors don't think the scopes violate
policy, nor copy a `Find*` into a panel thinking it's allowed.

**Proposed addition under "Strictly Forbidden":**

> - `FindObjectOfType` / `FindAnyObjectByType` / `GameObject.Find` in **gameplay/runtime** code —
>   use DI. **Exception:** the DI-bootstrap shim inside `LifetimeScope.Configure` (and its
>   `RegisterBuildCallback`s) may use
>   `FindAnyObjectByType`/`FindObjectsByType(..., FindObjectsInactive.Include)` *solely* to locate
>   scene-placed `MonoBehaviour`s and hand them to `builder.Inject`/`RegisterInstance`. This is the
>   only legal home for `Find*`; it must never appear in a panel, behavior, or service.

(The 28 sites are near-identical — a shared `BaseSceneScope.InjectIfPresent<T>()` helper would
shrink them, but that's a refactor, not a convention change. See prior review DUP1/DUP2/DUP3.)

---

## Naming-Suffix Contradiction Analysis (detail)

Quantified inventory across `Assets/_App/Scripts/**` (ThirdParty excluded):

- `Manager`: **1** (`SelectionManager` + `ISelectionManager`). Blessed.
- `Controller`: **1** (`GizmoController`). Blessed.
- `Handler`: **1** (`UndoKeyHandler`). Blessed.
- `Service`: **0**. `Helper`: **0**. `Utils`: **0**. `Processor`: **0**.
- `Orchestrator` (NOT forbidden): `ModeOrchestrator`, `UiPanelOrchestrator` — preferred coordinator
  suffix; already compliant.

The prohibition currently has a **100% false-positive rate**: every type it would flag is one the
architecture docs explicitly prescribe, and it flags **none** of the grab-bag classes it was
written to prevent (there are none). The rule is both wrong (contradicts blessed names) and inert
(catches nothing real). B1's rewrite resolves both.

Adjacent observation (NOT a suffix issue) — **`public` fields on serializable data classes.**
Many `[Serializable]` data containers expose `public` fields directly: `BoneRecord.BoneName`/
`TranslationLocked` (`RigBuilder/BoneRecord.cs:6-7`), `NodeData.*` (`StorageCore/NodeData.cs:7-13`),
`AnimKeyData.*`, `AssetEntry.*`, `IkChainRecord.*`, `RigDefinition.AssetId`, event structs, etc.
CLAUDE.md's "never `public` fields" rule is written for **inspector-exposed `MonoBehaviour`/SO
fields** (the full rule is "`[SerializeField] private` for inspector-exposed fields — never
`public`"), and those are clean. But the **plain-old-data serialization classes** lean on `public`
fields because Unity `JsonUtility` requires them. This is a consistent, deliberate pattern (≈15
files), so it is a **B-style deviation**, not a violation — but the doc should say so explicitly:

> `public` fields are forbidden on `MonoBehaviour`/`ScriptableObject` (use `[SerializeField]
> private` + property). Plain `[Serializable]` data classes/structs serialized by `JsonUtility` may
> use `public` fields, since `JsonUtility` does not serialize private fields without
> `[SerializeField]` and these types carry no behavior.

(This is optional to formalize; flagged for completeness since a naive reading of "never `public`
fields" would wrongly flag ~15 data files.)

---

## FindObjectType Exception Analysis (detail)

| Site | Context | Classification |
|---|---|---|
| `RootLifetimeScope.cs` ×8 | `Configure` + build callback → `Inject`/`RegisterInstance` | **Sanctioned DI shim** |
| `VrEditingSceneScope.cs` ×12 | `Configure` + build callback | **Sanctioned DI shim** |
| `SandboxSceneScope.cs` ×11 | `Configure` + build callback | **Sanctioned DI shim** |
| `Editor/AnimatorPanelModuleBuilder.cs:1219` | `GameObject.Find` in editor build tool | Out of runtime ban; editor smell (A4) |
| any panel / behavior / gizmo / animation / storage class | — | **None exist** — all use `[Inject]`/ctor |

The rule needs the carve-out (B2). There is no runtime-gameplay `Find*` to fix. The uniform
`FindObjectsInactive.Include` choice (scene objects can start inactive — e.g. panels) is deliberate
and worth capturing in the carved-out wording.

---

## Open Questions

1. **`UndoKeyHandler` GUID safety:** it is a scene-placed `MonoBehaviour`. If B1 were rejected and a
   rename pursued, a `.meta`/scene/prefab GUID sweep is mandatory first (cf. memory note
   `name_collision_suffix`). Not runnable this session.
2. **Placeholder types (A2):** `AnimationPlayback` is explicitly merged-away (delete). Are
   `InputBindings`/`ExportPipeline`/`ErrorHandling` still planned as real runtime subsystems (keep
   stub, rename file to `*Placeholder.cs`) or abandoned (delete)? Roadmap call — same flavor as the
   prior review's `PanelRegistry` question. Note `ErrorHandling` is referenced as a real subsystem in
   CLAUDE.md (`ErrorDispatcher`) yet only `ErrorLevel`/`ErrorOccurredEvent` exist — the dispatcher is
   unimplemented, so logging currently goes straight to `Debug.Log*` (consistent with prior review H3).
3. **Doc reconciliation surface:** B1/B2 (and the optional public-field note) require edits to
   **three** docs that currently agree with each other but disagree with the code — `CLAUDE.md`,
   `Assets/_App/Documentation/conventions.md`, and (for the suffix vocabulary) `architecture_context.md`.
   Whoever applies the rewrites must touch all three or they will re-drift.
4. **`async void` lint:** worth a one-line convention reaffirming the project's actual idiom —
   `void Start()` + `_ = DoAsync()` (fire-and-forget) is used everywhere *except* ScenePickerPanel
   (A1). If that idiom is the intended standard, say so; otherwise standardize on wrapped `async void`.
