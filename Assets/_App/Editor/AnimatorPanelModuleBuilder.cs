using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AnimatorPanelModuleBuilder
{
    private const string PrefabPath        = "Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/AnimatorPanelModule.prefab";
    private const string ConfigPath        = "Assets/_App/Subsystems/SpatialUi/Data/AnimatorPanelConfig.asset";
    private const string TrackRowPath      = "Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TrackRow.prefab";
    private const string TickPath          = "Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TimelineTick.prefab";
    private const string TickLabelPath     = "Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TimelineTickLabel.prefab";
    private const string LanePath          = "Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TimelineLane.prefab";
    private const string KeyDiamondPath    = "Assets/_App/Subsystems/SpatialUi/Prefabs/Items/TimelineKeyDiamond.prefab";
    private const string UserPanelPath     = "Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab";
    private const string NavBarConfigPath  = "Assets/_App/Subsystems/SpatialUi/Data/DefaultNavBarConfig.asset";
    private const string AnimatorEntryId   = "animator";

    private const string SandboxSceneDir   = "Assets/_App/Scenes/_Sandbox";
    private const string SandboxScenePath  = "Assets/_App/Scenes/_Sandbox/AnimatorPanelSandbox.unity";
    private const string SandboxCanvasName = "SandboxCanvas";
    private const string SandboxRootName   = "AnimatorPanelModule";

    // ---------------------------------------------------------------------
    // HTML mockup v4 dimensions (FRAME_PX=30, TOTAL=60)
    // Colors per CSS variable mapping noted in task spec.
    // ---------------------------------------------------------------------

    private static readonly Color BgPrimary      = new Color32(0x16, 0x16, 0x16, 0xFF);
    private static readonly Color BgSecondary    = new Color32(0x20, 0x20, 0x20, 0xFF);
    private static readonly Color BgInfo         = new Color32(0x2E, 0x59, 0xA8, 0xFF);
    private static readonly Color BgDangerSubtle = new Color32(0x2A, 0x18, 0x18, 0xFF);
    private static readonly Color TextInfo       = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    private static readonly Color TextDanger     = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
    private static readonly Color TextPrimary    = new Color32(0xE6, 0xE6, 0xE6, 0xFF);
    private static readonly Color TextSecondary  = new Color32(0xA0, 0xA0, 0xA0, 0xFF);
    private static readonly Color TextTertiary   = new Color32(0x70, 0x70, 0x70, 0xFF);
    private static readonly Color BorderTertiary = new Color32(0x2B, 0x2B, 0x2B, 0xFF);
    private static readonly Color BorderSecondary= new Color32(0x3A, 0x3A, 0x3A, 0xFF);
    private static readonly Color BorderInfo     = new Color32(0x3D, 0x6F, 0xCE, 0xFF);
    private static readonly Color BorderDanger   = new Color32(0x5A, 0x24, 0x24, 0xFF);

    private const float FRAME_PX        = 30f;
    private const int   DEFAULT_TOTAL   = 60;

    private const float ToolbarTopH     = 64f;   // padding 10+10 + 44 button
    private const float ToolbarBotH     = 80f;   // brief target — padding 9+9 + 52 button + breathing room
    private const float BodyH           = 432f;  // header 36 + 4 lanes*52 + room
    private const float TracksColW      = 220f;
    private const float TracksHeaderH   = 36f;
    private const float TrackRowH       = 52f;
    private const float RulerH          = 36f;
    private const float BtnH            = 44f;
    private const float BtnLargeH       = 52f;
    private const float FrameInputW     = 72f;
    private const float SmallInputW     = 60f;
    private const float DividerH        = 32f;

    [MenuItem("PromeonLab/Build/Verify Animator Panel Module Wiring")]
    public static void VerifyWiring()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[Verify] prefab not found at " + PrefabPath); return; }

        int missing = 0;
        missing += CheckComponent<AnimatorPanel>(prefab,
            new[] { "_config", "_timelineContent", "_toolbar", "_transport", "_emptyState", "_activeStateRoot",
                    "_ruler", "_lanes", "_playhead", "_timelineInput", "_tracksColumnContent", "_trackRowPrefab" });
        missing += CheckComponent<AnimatorSubEmptyState>(prefab,
            new[] { "_noSelectionPanel", "_noContainerPanel", "_addAnimationButton" });
        missing += CheckComponent<AnimatorSubToolbar>(prefab,
            new[] { "_currentFrameInput", "_totalFramesInput", "_fpsInput", "_setKeyButton", "_deleteKeyButton",
                    "_copyButton", "_pasteButton", "_removeAnimationButton" });
        missing += CheckComponent<AnimatorSubTransport>(prefab,
            new[] { "_prevKeyButton", "_prevFrameButton", "_startButton", "_playPauseButton",
                    "_endButton", "_nextFrameButton", "_nextKeyButton", "_playPauseIcon" });
        missing += CheckComponent<AnimatorSubRuler>(prefab,
            new[] { "_content", "_tickPrefab", "_labelPrefab", "_config" });
        missing += CheckComponent<AnimatorSubLanes>(prefab,
            new[] { "_root", "_lanePrefab" });
        missing += CheckComponent<AnimatorSubPlayhead>(prefab,
            new[] { "_root", "_frameLabel", "_config" });
        missing += CheckComponent<TimelineScrubInput>(prefab,
            new[] { "_content", "_config" });
        missing += CheckComponent<TimelineScrollSync>(prefab,
            new[] { "_leftTracks", "_rightTimeline" });

        Debug.Log("[Verify] missing-field count: " + missing);
    }

    private static int CheckComponent<T>(GameObject prefab, string[] fields) where T : Component
    {
        var comp = prefab.GetComponentInChildren<T>(includeInactive: true);
        if (comp == null) { Debug.LogError("[Verify] component missing: " + typeof(T).Name); return fields.Length; }
        var so = new SerializedObject(comp);
        int missing = 0;
        foreach (var f in fields)
        {
            var p = so.FindProperty(f);
            if (p == null) { Debug.LogError("[Verify] " + typeof(T).Name + "." + f + " NOT FOUND"); missing++; continue; }
            if (p.objectReferenceValue == null) { Debug.LogError("[Verify] " + typeof(T).Name + "." + f + " is NULL"); missing++; }
        }
        return missing;
    }

    [MenuItem("PromeonLab/Build/Animator Panel Module")]
    public static void BuildAndSave()
    {
        var root = BuildRoot();
        if (root == null)
        {
            Debug.LogError("[AnimatorPanelModuleBuilder] BuildRoot returned null. Aborting.");
            return;
        }

        var dir = Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            Debug.LogError("[AnimatorPanelModuleBuilder] Folder missing: " + dir);
            Object.DestroyImmediate(root);
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
        Object.DestroyImmediate(root);

        if (success) Debug.Log("[AnimatorPanelModuleBuilder] saved " + PrefabPath);
        else         Debug.LogError("[AnimatorPanelModuleBuilder] save failed");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------------
    // Root
    // ---------------------------------------------------------------------

    private static GameObject BuildRoot()
    {
        var config = AssetDatabase.LoadAssetAtPath<AnimatorPanelConfig>(ConfigPath);
        if (config == null) { Debug.LogError("Missing AnimatorPanelConfig at " + ConfigPath); return null; }
        var trackRowPrefab = AssetDatabase.LoadAssetAtPath<TrackRow>(TrackRowPath);
        if (trackRowPrefab == null) { Debug.LogError("Missing TrackRow prefab at " + TrackRowPath); return null; }

        var root = new GameObject("AnimatorPanelModule",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image),
            typeof(SpatialPanelDetachable), typeof(AnimatorPanel));

        var rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200f, 680f);

        var bg = root.GetComponent<Image>();
        bg.color = BgSecondary;
        bg.raycastTarget = true;

        var canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = false;

        // EmptyStateRoot
        var emptyRt = CreateChild(root.transform, "EmptyStateRoot",
            typeof(RectTransform), typeof(AnimatorSubEmptyState));
        StretchFill((RectTransform)emptyRt.transform);
        BuildEmptyState(emptyRt);

        // ActiveStateRoot
        var activeRt = CreateChild(root.transform, "ActiveStateRoot",
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        StretchFill((RectTransform)activeRt.transform);
        var vlg = activeRt.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.spacing = 0;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var toolbarTop = BuildToolbarTop(activeRt.transform);
        var body       = BuildBody(activeRt.transform, config);
        var toolbarBot = BuildToolbarBottom(activeRt.transform);

        // Wire AnimatorPanel
        var view = root.GetComponent<AnimatorPanel>();
        var so = new SerializedObject(view);
        SetRef(so, "_config",              config);
        SetRef(so, "_timelineContent",     body.timelineContent);
        SetRef(so, "_toolbar",             toolbarTop.GetComponent<AnimatorSubToolbar>());
        SetRef(so, "_transport",           toolbarBot.GetComponent<AnimatorSubTransport>());
        SetRef(so, "_emptyState",          emptyRt.GetComponent<AnimatorSubEmptyState>());
        SetRef(so, "_activeStateRoot",     activeRt);
        SetRef(so, "_ruler",               body.ruler.GetComponent<AnimatorSubRuler>());
        SetRef(so, "_lanes",               body.lanesContent.GetComponent<AnimatorSubLanes>());
        SetRef(so, "_playhead",            body.playhead.GetComponent<AnimatorSubPlayhead>());
        SetRef(so, "_timelineInput",       body.lanesContent.GetComponent<TimelineScrubInput>());
        SetRef(so, "_tracksColumnContent", body.tracksColumnContent);
        SetRef(so, "_trackRowPrefab",      trackRowPrefab);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Initial visibility: panel hidden, both state roots active so view can toggle children
        activeRt.SetActive(true);
        emptyRt.SetActive(true);
        root.SetActive(false);
        return root;
    }

    // ---------------------------------------------------------------------
    // EmptyStateRoot
    // ---------------------------------------------------------------------

    private static void BuildEmptyState(GameObject emptyRoot)
    {
        var view = emptyRoot.GetComponent<AnimatorSubEmptyState>();

        var noSel = BuildCenteredPanel(emptyRoot.transform, "NoSelectionPanel",
            labelText: "Select an object to view its animator", labelSize: 16,
            buttonInfo: null, hintText: null);

        var noCont = BuildCenteredPanel(emptyRoot.transform, "NoContainerPanel",
            labelText: "This object has no animation container yet", labelSize: 15,
            buttonInfo: new ButtonInfo("+ add animation", BgInfo, TextInfo, new Vector2(220f, BtnLargeH)),
            hintText: "creates an action container with default 60 frames @ 24 fps");

        Button addBtn = noCont.GetComponentInChildren<Button>(includeInactive: true);

        var so = new SerializedObject(view);
        SetRef(so, "_noSelectionPanel",   noSel);
        SetRef(so, "_noContainerPanel",   noCont);
        SetRef(so, "_addAnimationButton", addBtn);
        so.ApplyModifiedPropertiesWithoutUndo();

        noSel.SetActive(true);
        noCont.SetActive(false);
    }

    private struct ButtonInfo
    {
        public string  Text;
        public Color   Bg;
        public Color   Fg;
        public Vector2 Size;
        public ButtonInfo(string text, Color bg, Color fg, Vector2 size)
        { Text = text; Bg = bg; Fg = fg; Size = size; }
    }

    private static GameObject BuildCenteredPanel(Transform parent, string name,
        string labelText, int labelSize, ButtonInfo? buttonInfo, string hintText)
    {
        var go = CreateChild(parent, name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        StretchFill((RectTransform)go.transform);
        var vlg = go.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(40, 40, 60, 60);
        vlg.spacing = 16;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        if (labelText != null)
            BuildLabel(go.transform, "Label", labelText, labelSize,
                TextPrimary, 600f, labelSize * 1.6f);

        if (buttonInfo.HasValue)
        {
            var b = buttonInfo.Value;
            BuildButton(go.transform, "AddAnimationButton", b.Text, b.Bg, b.Fg, b.Size);
        }

        if (hintText != null)
            BuildLabel(go.transform, "Hint", hintText, 12,
                TextTertiary, 600f, 20f);

        return go;
    }

    // ---------------------------------------------------------------------
    // ToolbarTop
    // ---------------------------------------------------------------------

    private static GameObject BuildToolbarTop(Transform parent)
    {
        var go = CreateChild(parent, "ToolbarTop",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(AnimatorSubToolbar));

        go.GetComponent<Image>().color = BgPrimary;

        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 10, 10);
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var topLE = go.GetComponent<LayoutElement>();
        topLE.preferredHeight = ToolbarTopH;
        topLE.minHeight       = ToolbarTopH;
        topLE.flexibleHeight  = 0f;

        BuildLabel(go.transform, "FrameLabel", "frame", 13, TextSecondary, 44f, BtnH);
        var curIn = BuildInputField(go.transform, "CurrentFrameInput", "12", FrameInputW, BtnH);
        BuildLabel(go.transform, "SlashLabel", "/", 16, TextPrimary, 10f, BtnH);
        var totIn = BuildInputField(go.transform, "TotalFramesInput", "60", FrameInputW, BtnH);
        BuildLabel(go.transform, "FpsLabel", "fps", 13, TextSecondary, 30f, BtnH);
        var fpsIn = BuildInputField(go.transform, "FpsInput", "24", SmallInputW, BtnH);
        BuildDivider(go.transform, "Divider", 1f, DividerH);

        var setKey  = BuildToolbarButton(go.transform, "SetKeyButton",          "+ key", 80f,  BtnH, BgInfo,         TextInfo,    BorderInfo);
        var delKey  = BuildToolbarButton(go.transform, "DeleteKeyButton",       "- key", 80f,  BtnH, BgDangerSubtle, TextDanger,  BorderDanger);
        var copyBtn = BuildToolbarButton(go.transform, "CopyButton",            "copy",  60f,  BtnH, BgSecondary,    TextPrimary, BorderSecondary);
        var pasteBt = BuildToolbarButton(go.transform, "PasteButton",           "paste", 60f,  BtnH, BgSecondary,    TextPrimary, BorderSecondary);
        BuildSpacer(go.transform, "Spacer");
        var rmAnim  = BuildToolbarButton(go.transform, "RemoveAnimationButton", "remove animation", 200f, BtnH, BgDangerSubtle, TextDanger, BorderDanger);

        var view = go.GetComponent<AnimatorSubToolbar>();
        var so = new SerializedObject(view);
        SetRef(so, "_currentFrameInput",     curIn);
        SetRef(so, "_totalFramesInput",      totIn);
        SetRef(so, "_fpsInput",              fpsIn);
        SetRef(so, "_setKeyButton",          setKey);
        SetRef(so, "_deleteKeyButton",       delKey);
        SetRef(so, "_copyButton",            copyBtn);
        SetRef(so, "_pasteButton",           pasteBt);
        SetRef(so, "_removeAnimationButton", rmAnim);
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ---------------------------------------------------------------------
    // Body (TracksColumn + TimelineColumn)
    // ---------------------------------------------------------------------

    private struct BodyResult
    {
        public GameObject     body;
        public RectTransform  tracksColumnContent;
        public RectTransform  timelineContent;
        public GameObject     ruler;
        public GameObject     lanesContent;
        public GameObject     playhead;
        public ScrollRect     leftScroll;
        public ScrollRect     rightScroll;
    }

    private static BodyResult BuildBody(Transform parent, AnimatorPanelConfig config)
    {
        var body = CreateChild(parent, "Body",
            typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(TimelineScrollSync));

        var hlg = body.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = 0;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var bodyLE = body.GetComponent<LayoutElement>();
        bodyLE.flexibleHeight  = 1f;
        bodyLE.preferredHeight = 400f;
        bodyLE.minHeight       = 300f;

        // ---------- TracksColumn ----------
        var tracksCol = CreateChild(body.transform, "TracksColumn",
            typeof(RectTransform), typeof(Image), typeof(RectMask2D),
            typeof(VerticalLayoutGroup), typeof(LayoutElement));
        tracksCol.GetComponent<Image>().color = BgPrimary;
        var tcLE = tracksCol.GetComponent<LayoutElement>();
        tcLE.preferredWidth = TracksColW;
        tcLE.minWidth       = TracksColW;
        tcLE.flexibleWidth  = 0f;
        var tcVLG = tracksCol.GetComponent<VerticalLayoutGroup>();
        tcVLG.padding = new RectOffset(0, 0, 0, 0);
        tcVLG.spacing = 0;
        tcVLG.childControlWidth = true;
        tcVLG.childControlHeight = false;
        tcVLG.childForceExpandWidth = true;
        tcVLG.childForceExpandHeight = false;

        // Right border (1px BorderTertiary) — sibling overlay, anchored right
        var tcBorder = CreateChild(tracksCol.transform, "RightBorder",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var tcBorderRt = (RectTransform)tcBorder.transform;
        tcBorderRt.anchorMin = new Vector2(1f, 0f);
        tcBorderRt.anchorMax = new Vector2(1f, 1f);
        tcBorderRt.pivot     = new Vector2(1f, 0.5f);
        tcBorderRt.sizeDelta = new Vector2(1f, 0f);
        tcBorder.GetComponent<Image>().color = BorderTertiary;
        var tcBorderLE = tcBorder.GetComponent<LayoutElement>();
        tcBorderLE.ignoreLayout = true;

        // HeaderRow
        var header = CreateChild(tracksCol.transform, "HeaderRow",
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        header.GetComponent<Image>().color = BgSecondary;
        var headerLE = header.GetComponent<LayoutElement>();
        headerLE.preferredHeight = TracksHeaderH;
        headerLE.minHeight       = TracksHeaderH;
        headerLE.flexibleHeight  = 0f;
        // Header label as plain child with stretch anchors (no LE conflict)
        var headerLabelGo = CreateChild(header.transform, "Label",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        var hlRt = (RectTransform)headerLabelGo.transform;
        hlRt.anchorMin = new Vector2(0f, 0f);
        hlRt.anchorMax = new Vector2(1f, 1f);
        hlRt.offsetMin = new Vector2(14f, 0f);
        hlRt.offsetMax = new Vector2(-14f, 0f);
        var hlT = headerLabelGo.GetComponent<TextMeshProUGUI>();
        hlT.text = "objects . bones";
        hlT.fontSize = 13;
        hlT.color = TextSecondary;
        hlT.alignment = TextAlignmentOptions.MidlineLeft;

        // TracksScroll
        var tracksScrollGo = CreateChild(tracksCol.transform, "TracksScroll",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        tracksScrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        tracksScrollGo.GetComponent<LayoutElement>().flexibleHeight = 1;
        var tracksScroll = tracksScrollGo.GetComponent<ScrollRect>();
        tracksScroll.horizontal = false;
        tracksScroll.vertical = true;
        tracksScroll.movementType = ScrollRect.MovementType.Clamped;

        var tracksViewport = CreateChild(tracksScrollGo.transform, "Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        tracksViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        tracksViewport.GetComponent<Mask>().showMaskGraphic = false;
        StretchFill((RectTransform)tracksViewport.transform);

        var tracksContent = CreateChild(tracksViewport.transform, "TracksColumnContent",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var tcRt = (RectTransform)tracksContent.transform;
        tcRt.anchorMin = new Vector2(0f, 1f);
        tcRt.anchorMax = new Vector2(1f, 1f);
        tcRt.pivot     = new Vector2(0.5f, 1f);
        tcRt.anchoredPosition = Vector2.zero;
        tcRt.sizeDelta = Vector2.zero;
        var contentVLG = tracksContent.GetComponent<VerticalLayoutGroup>();
        contentVLG.padding = new RectOffset(0, 0, 0, 0);
        contentVLG.spacing = 0;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = false;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        var csf = tracksContent.GetComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        tracksScroll.viewport = (RectTransform)tracksViewport.transform;
        tracksScroll.content  = tcRt;

        // ---------- TimelineColumn ----------
        var timelineCol = CreateChild(body.transform, "TimelineColumn",
            typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
        timelineCol.GetComponent<LayoutElement>().flexibleWidth = 1;

        var timelineScrollGo = CreateChild(timelineCol.transform, "TimelineScroll",
            typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        timelineScrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        StretchFill((RectTransform)timelineScrollGo.transform);
        var timelineScroll = timelineScrollGo.GetComponent<ScrollRect>();
        timelineScroll.horizontal = true;
        timelineScroll.vertical = true;
        timelineScroll.movementType = ScrollRect.MovementType.Clamped;

        var timelineViewport = CreateChild(timelineScrollGo.transform, "Viewport",
            typeof(RectTransform), typeof(Image), typeof(Mask));
        timelineViewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        timelineViewport.GetComponent<Mask>().showMaskGraphic = false;
        StretchFill((RectTransform)timelineViewport.transform);

        var timelineContent = CreateChild(timelineViewport.transform, "TimelineContent",
            typeof(RectTransform));
        var tcRt2 = (RectTransform)timelineContent.transform;
        tcRt2.anchorMin = new Vector2(0f, 1f);
        tcRt2.anchorMax = new Vector2(0f, 1f);
        tcRt2.pivot     = new Vector2(0f, 1f);
        tcRt2.anchoredPosition = Vector2.zero;
        // (DEFAULT_TOTAL+1) * FRAME_PX = 61 * 30 = 1830 ; height = 520 (36 ruler + 4 lanes max + room)
        tcRt2.sizeDelta = new Vector2((config.DefaultTotalFrames + 1) * config.FramePx, 520f);

        // LanesContent (sibling 0) — below ruler
        var lanesContent = CreateChild(timelineContent.transform, "LanesContent",
            typeof(RectTransform), typeof(AnimatorSubLanes), typeof(TimelineScrubInput));
        var lcRt = (RectTransform)lanesContent.transform;
        lcRt.anchorMin = new Vector2(0f, 1f);
        lcRt.anchorMax = new Vector2(1f, 1f);
        lcRt.pivot     = new Vector2(0.5f, 1f);
        lcRt.anchoredPosition = new Vector2(0f, -RulerH);
        lcRt.sizeDelta = new Vector2(0f, 520f - RulerH);

        // Ruler (sibling 1)
        var ruler = CreateChild(timelineContent.transform, "Ruler",
            typeof(RectTransform), typeof(Image), typeof(AnimatorSubRuler));
        var rRt = (RectTransform)ruler.transform;
        rRt.anchorMin = new Vector2(0f, 1f);
        rRt.anchorMax = new Vector2(1f, 1f);
        rRt.pivot     = new Vector2(0.5f, 1f);
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = new Vector2(0f, RulerH);
        ruler.GetComponent<Image>().color = BgSecondary;

        var rulerContent = CreateChild(ruler.transform, "Content", typeof(RectTransform));
        StretchFill((RectTransform)rulerContent.transform);

        // Playhead (sibling 2)
        var playhead = CreateChild(timelineContent.transform, "Playhead",
            typeof(RectTransform), typeof(AnimatorSubPlayhead));
        var pRt = (RectTransform)playhead.transform;
        pRt.anchorMin = new Vector2(0f, 0f);
        pRt.anchorMax = new Vector2(0f, 1f);
        pRt.pivot     = new Vector2(0.5f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta = new Vector2(20f, 0f);

        var line = CreateChild(playhead.transform, "Line",
            typeof(RectTransform), typeof(Image));
        var lRt = (RectTransform)line.transform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 1f);
        lRt.pivot     = new Vector2(0.5f, 0.5f);
        lRt.offsetMin = new Vector2(8f, 0f);
        lRt.offsetMax = new Vector2(-8f, 0f);
        // Soft fill (~14% danger alpha) per mockup
        var lineImg = line.GetComponent<Image>();
        lineImg.color = new Color(TextDanger.r, TextDanger.g, TextDanger.b, 0.14f);

        // Left vertical edge of playhead (2px danger)
        var edgeL = CreateChild(playhead.transform, "EdgeLeft", typeof(RectTransform), typeof(Image));
        var edgeLRt = (RectTransform)edgeL.transform;
        edgeLRt.anchorMin = new Vector2(0f, 0f);
        edgeLRt.anchorMax = new Vector2(0f, 1f);
        edgeLRt.pivot     = new Vector2(0f, 0.5f);
        edgeLRt.sizeDelta = new Vector2(2f, 0f);
        edgeL.GetComponent<Image>().color = TextDanger;

        // Right vertical edge (2px danger)
        var edgeR = CreateChild(playhead.transform, "EdgeRight", typeof(RectTransform), typeof(Image));
        var edgeRRt = (RectTransform)edgeR.transform;
        edgeRRt.anchorMin = new Vector2(1f, 0f);
        edgeRRt.anchorMax = new Vector2(1f, 1f);
        edgeRRt.pivot     = new Vector2(1f, 0.5f);
        edgeRRt.sizeDelta = new Vector2(2f, 0f);
        edgeR.GetComponent<Image>().color = TextDanger;

        var frameLabelGo = CreateChild(playhead.transform, "FrameLabel",
            typeof(RectTransform), typeof(Image));
        var flRt = (RectTransform)frameLabelGo.transform;
        flRt.anchorMin = new Vector2(0.5f, 1f);
        flRt.anchorMax = new Vector2(0.5f, 1f);
        flRt.pivot     = new Vector2(0.5f, 1f);
        flRt.anchoredPosition = new Vector2(0f, -4f);
        flRt.sizeDelta = new Vector2(48f, 22f);
        frameLabelGo.GetComponent<Image>().color = BgPrimary;

        var frameLabelText = CreateChild(frameLabelGo.transform, "Text",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        StretchFill((RectTransform)frameLabelText.transform);
        var fltT = frameLabelText.GetComponent<TextMeshProUGUI>();
        fltT.text = "0";
        fltT.fontSize = 13;
        fltT.color = TextDanger;
        fltT.alignment = TextAlignmentOptions.Center;

        timelineScroll.viewport = (RectTransform)timelineViewport.transform;
        timelineScroll.content  = tcRt2;

        // Wire sub-views
        WireAnimatorSubLanes (lanesContent.GetComponent<AnimatorSubLanes>(), lcRt);
        WireTimelineScrubInput(lanesContent.GetComponent<TimelineScrubInput>(), lcRt, config);
        WireAnimatorSubRuler (ruler.GetComponent<AnimatorSubRuler>(), (RectTransform)rulerContent.transform, config);
        WireAnimatorSubPlayhead(playhead.GetComponent<AnimatorSubPlayhead>(), pRt, fltT, config);

        // Wire TimelineScrollSync on Body
        var sync = body.GetComponent<TimelineScrollSync>();
        var soSync = new SerializedObject(sync);
        SetRef(soSync, "_leftTracks",    tracksScroll);
        SetRef(soSync, "_rightTimeline", timelineScroll);
        soSync.ApplyModifiedPropertiesWithoutUndo();

        return new BodyResult
        {
            body                = body,
            tracksColumnContent = tcRt,
            timelineContent     = tcRt2,
            ruler               = ruler,
            lanesContent        = lanesContent,
            playhead            = playhead,
            leftScroll          = tracksScroll,
            rightScroll         = timelineScroll,
        };
    }

    private static void WireAnimatorSubLanes(AnimatorSubLanes lanes, RectTransform root)
    {
        var so = new SerializedObject(lanes);
        SetRef(so, "_root",       root);
        SetRef(so, "_lanePrefab", AssetDatabase.LoadAssetAtPath<TimelineLane>(LanePath));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireTimelineScrubInput(TimelineScrubInput input, RectTransform content, AnimatorPanelConfig config)
    {
        var so = new SerializedObject(input);
        SetRef(so, "_content", content);
        SetRef(so, "_config",  config);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireAnimatorSubRuler(AnimatorSubRuler ruler, RectTransform content, AnimatorPanelConfig config)
    {
        var so = new SerializedObject(ruler);
        SetRef(so, "_content",     content);
        SetRef(so, "_tickPrefab",  AssetDatabase.LoadAssetAtPath<RectTransform>(TickPath));
        SetRef(so, "_labelPrefab", AssetDatabase.LoadAssetAtPath<TMP_Text>(TickLabelPath));
        SetRef(so, "_config",      config);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireAnimatorSubPlayhead(AnimatorSubPlayhead ph, RectTransform root, TMP_Text label, AnimatorPanelConfig config)
    {
        var so = new SerializedObject(ph);
        SetRef(so, "_root",       root);
        SetRef(so, "_frameLabel", label);
        SetRef(so, "_config",     config);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------------
    // ToolbarBottom
    // ---------------------------------------------------------------------

    private static GameObject BuildToolbarBottom(Transform parent)
    {
        var go = CreateChild(parent, "ToolbarBottom",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement), typeof(AnimatorSubTransport));

        go.GetComponent<Image>().color = BgPrimary;
        var hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 9, 9);
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var botLE = go.GetComponent<LayoutElement>();
        botLE.preferredHeight = ToolbarBotH;
        botLE.minHeight       = ToolbarBotH;
        botLE.flexibleHeight  = 0f;

        var prevKey = BuildToolbarButton(go.transform, "PrevKeyButton",   "<<", BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);
        var prevFr  = BuildToolbarButton(go.transform, "PrevFrameButton", "<",  BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);
        var startB  = BuildToolbarButton(go.transform, "StartButton",     "|<", BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);
        var playPB  = BuildToolbarButton(go.transform, "PlayPauseButton", "",   BtnLargeH, BtnLargeH, BgInfo,      TextInfo,    BorderInfo);
        var endB    = BuildToolbarButton(go.transform, "EndButton",       ">|", BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);
        var nextFr  = BuildToolbarButton(go.transform, "NextFrameButton", ">",  BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);
        var nextKey = BuildToolbarButton(go.transform, "NextKeyButton",   ">>", BtnLargeH, BtnLargeH, BgSecondary, TextPrimary, BorderSecondary);

        var iconGo = CreateChild(playPB.transform, "PlayPauseIcon",
            typeof(RectTransform), typeof(Image));
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot     = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(24f, 24f);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;

        var view = go.GetComponent<AnimatorSubTransport>();
        var so = new SerializedObject(view);
        SetRef(so, "_prevKeyButton",   prevKey);
        SetRef(so, "_prevFrameButton", prevFr);
        SetRef(so, "_startButton",     startB);
        SetRef(so, "_playPauseButton", playPB);
        SetRef(so, "_endButton",       endB);
        SetRef(so, "_nextFrameButton", nextFr);
        SetRef(so, "_nextKeyButton",   nextKey);
        SetRef(so, "_playPauseIcon",   iconImg);
        // _playSprite and _pauseSprite intentionally left null per spec section 9.
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    // ---------------------------------------------------------------------
    // Generic helpers
    // ---------------------------------------------------------------------

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
        return go;
    }

    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
    }

    private static TMP_Text BuildLabel(Transform parent, string name, string text, int fontSize, Color color, float preferredW, float preferredH)
    {
        var go = CreateChild(parent, name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        var t  = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = preferredW;
        le.preferredHeight = preferredH;
        le.minWidth        = preferredW;
        le.minHeight       = preferredH;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;
        return t;
    }

    private static Button BuildButton(Transform parent, string name, string label, Color bg, Color fg, Vector2 size)
    {
        var go = CreateChild(parent, name,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = size.x;
        le.preferredHeight = size.y;
        le.minWidth        = size.x;
        le.minHeight       = size.y;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        AddBorderEdges(go.transform, BorderInfo);

        var labelGo = CreateChild(go.transform, "Label",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        StretchFill((RectTransform)labelGo.transform);
        var labelT = labelGo.GetComponent<TextMeshProUGUI>();
        labelT.text = label;
        labelT.fontSize = 15;
        labelT.color = fg;
        labelT.alignment = TextAlignmentOptions.Center;

        return go.GetComponent<Button>();
    }

    private static Button BuildToolbarButton(Transform parent, string name, string label, float width, float height, Color bg, Color fg, Color border)
    {
        var go = CreateChild(parent, name,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);
        var img = go.GetComponent<Image>();
        img.color = bg;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = height;
        le.minWidth        = width;
        le.minHeight       = height;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        // Border as 4 thin children (top/bottom/left/right) — Outline is glow not box
        AddBorderEdges(go.transform, border);

        var labelGo = CreateChild(go.transform, "Label",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(12f, 0f);
        labelRt.offsetMax = new Vector2(-12f, 0f);
        var lt = labelGo.GetComponent<TextMeshProUGUI>();
        lt.text = label;
        lt.fontSize = 14;
        lt.color = fg;
        lt.alignment = TextAlignmentOptions.Center;
        lt.textWrappingMode = TextWrappingModes.NoWrap;

        return go.GetComponent<Button>();
    }

    private static void AddBorderEdges(Transform parent, Color color)
    {
        // top
        var top = CreateChild(parent, "BorderTop", typeof(RectTransform), typeof(Image));
        var topRt = (RectTransform)top.transform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot     = new Vector2(0.5f, 1f);
        topRt.sizeDelta = new Vector2(0f, 1f);
        top.GetComponent<Image>().color = color;
        top.GetComponent<Image>().raycastTarget = false;

        // bottom
        var bot = CreateChild(parent, "BorderBottom", typeof(RectTransform), typeof(Image));
        var botRt = (RectTransform)bot.transform;
        botRt.anchorMin = new Vector2(0f, 0f);
        botRt.anchorMax = new Vector2(1f, 0f);
        botRt.pivot     = new Vector2(0.5f, 0f);
        botRt.sizeDelta = new Vector2(0f, 1f);
        bot.GetComponent<Image>().color = color;
        bot.GetComponent<Image>().raycastTarget = false;

        // left
        var left = CreateChild(parent, "BorderLeft", typeof(RectTransform), typeof(Image));
        var leftRt = (RectTransform)left.transform;
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0f, 1f);
        leftRt.pivot     = new Vector2(0f, 0.5f);
        leftRt.sizeDelta = new Vector2(1f, 0f);
        left.GetComponent<Image>().color = color;
        left.GetComponent<Image>().raycastTarget = false;

        // right
        var right = CreateChild(parent, "BorderRight", typeof(RectTransform), typeof(Image));
        var rightRt = (RectTransform)right.transform;
        rightRt.anchorMin = new Vector2(1f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.pivot     = new Vector2(1f, 0.5f);
        rightRt.sizeDelta = new Vector2(1f, 0f);
        right.GetComponent<Image>().color = color;
        right.GetComponent<Image>().raycastTarget = false;
    }

    private static TMP_InputField BuildInputField(Transform parent, string name, string initialText, float width, float height)
    {
        var go = CreateChild(parent, name,
            typeof(RectTransform), typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);
        var bgImg = go.GetComponent<Image>();
        bgImg.color = BgPrimary;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = height;
        le.minWidth        = width;
        le.minHeight       = height;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        // Border edges (BorderTertiary)
        AddBorderEdges(go.transform, BorderTertiary);

        // Text Area with RectMask2D
        var textAreaGo = CreateChild(go.transform, "Text Area",
            typeof(RectTransform), typeof(RectMask2D));
        var taRt = (RectTransform)textAreaGo.transform;
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(8f, 4f);
        taRt.offsetMax = new Vector2(-8f, -4f);

        // Placeholder
        var placeholderGo = CreateChild(textAreaGo.transform, "Placeholder",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        StretchFill((RectTransform)placeholderGo.transform);
        var phT = placeholderGo.GetComponent<TextMeshProUGUI>();
        phT.text = "";
        phT.fontSize = 14;
        phT.color = new Color(TextSecondary.r, TextSecondary.g, TextSecondary.b, 0.7f);
        phT.alignment = TextAlignmentOptions.Center;

        // Text
        var textGo = CreateChild(textAreaGo.transform, "Text",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        StretchFill((RectTransform)textGo.transform);
        var tT = textGo.GetComponent<TextMeshProUGUI>();
        tT.text = initialText;
        tT.fontSize = 14;
        tT.color = TextPrimary;
        tT.alignment = TextAlignmentOptions.Center;

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport  = taRt;
        input.textComponent = tT;
        input.placeholder   = phT;
        input.text          = initialText;
        input.contentType   = TMP_InputField.ContentType.IntegerNumber;

        return input;
    }

    private static GameObject BuildDivider(Transform parent, string name, float width, float height)
    {
        var go = CreateChild(parent, name,
            typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);
        var img = go.GetComponent<Image>();
        img.color = BorderTertiary;
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = height;
        le.minWidth        = width;
        le.minHeight       = height;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;
        return go;
    }

    private static GameObject BuildSpacer(Transform parent, string name)
    {
        var go = CreateChild(parent, name,
            typeof(RectTransform), typeof(LayoutElement));
        var le = go.GetComponent<LayoutElement>();
        le.minWidth        = 0f;
        le.preferredWidth  = 0f;
        le.preferredHeight = 0f;
        le.flexibleWidth   = 1f;
        le.flexibleHeight  = 0f;
        return go;
    }

    private static void SetRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogError("[AnimatorPanelModuleBuilder] SerializedProperty not found: " + propName + " on " + so.targetObject.GetType().Name);
            return;
        }
        prop.objectReferenceValue = value;
    }

    // ---------------------------------------------------------------------
    // Task 9: Wire AnimatorPanelModule instance into UserPanel.prefab
    // ---------------------------------------------------------------------

    [MenuItem("PromeonLab/Build/Wire UserPanel With Animator")]
    public static void WireUserPanel()
    {
        var modulePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (modulePrefab == null)
        {
            Debug.LogError("[WireUserPanel] AnimatorPanelModule prefab not found at " + PrefabPath);
            return;
        }

        var userPanelRoot = PrefabUtility.LoadPrefabContents(UserPanelPath);
        if (userPanelRoot == null)
        {
            Debug.LogError("[WireUserPanel] Failed to load UserPanel prefab contents at " + UserPanelPath);
            return;
        }

        try
        {
            // Locate parent: prefer "Modules" / "ModulesSlot" child by recursive search; fall back to root.
            Transform modulesParent = FindChildRecursive(userPanelRoot.transform, "Modules")
                                   ?? FindChildRecursive(userPanelRoot.transform, "ModulesSlot")
                                   ?? userPanelRoot.transform;

            // Destroy any existing AnimatorPanelModule child under that parent.
            var existing = FindDirectChild(modulesParent, "AnimatorPanelModule");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject, allowDestroyingAssets: false);
            }

            // Instantiate the new module prefab as a child of the modules parent.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modulePrefab, modulesParent);
            if (instance == null)
            {
                Debug.LogError("[WireUserPanel] InstantiatePrefab returned null");
                return;
            }
            instance.name = "AnimatorPanelModule";
            instance.SetActive(false);

            // Wire UserPanel._bindings.
            var userPanel = userPanelRoot.GetComponent<UserPanel>();
            if (userPanel == null)
            {
                Debug.LogError("[WireUserPanel] UserPanel component not found on root of " + UserPanelPath);
                return;
            }

            var so = new SerializedObject(userPanel);
            var bindings = so.FindProperty("_bindings");
            if (bindings == null || !bindings.isArray)
            {
                Debug.LogError("[WireUserPanel] _bindings property not found or not an array on UserPanel");
                return;
            }

            int targetIndex = -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var element = bindings.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("EntryId");
                if (idProp != null && idProp.stringValue == AnimatorEntryId)
                {
                    targetIndex = i;
                    break;
                }
            }

            bool appended = targetIndex < 0;
            if (appended)
            {
                targetIndex = bindings.arraySize;
                bindings.arraySize = bindings.arraySize + 1;
            }

            var target = bindings.GetArrayElementAtIndex(targetIndex);
            var entryIdProp  = target.FindPropertyRelative("EntryId");
            var panelProp    = target.FindPropertyRelative("Panel");
            var navButtonProp = target.FindPropertyRelative("NavButton");
            if (entryIdProp == null || panelProp == null || navButtonProp == null)
            {
                Debug.LogError("[WireUserPanel] NavBarBinding fields (EntryId/Panel/NavButton) not found");
                return;
            }

            entryIdProp.stringValue        = AnimatorEntryId;
            panelProp.objectReferenceValue = instance;
            // For appended entries SerializedProperty.arraySize duplicates the previous element's references;
            // clear NavButton so the user wires it manually per spec section 6.
            // Existing entries preserve their NavButton (user may have wired it after a prior run).
            if (appended)
                navButtonProp.objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(userPanelRoot, UserPanelPath);

            Debug.Log("[WireUserPanel] Wired animator binding at index " + targetIndex
                      + "; parent=" + modulesParent.name
                      + "; bindings.arraySize=" + bindings.arraySize);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(userPanelRoot);
        }
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var hit = FindChildRecursive(root.GetChild(i), name);
            if (hit != null) return hit;
        }
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        return null;
    }

    // ---------------------------------------------------------------------
    // Task 10: Register "animator" entry in DefaultNavBarConfig
    // ---------------------------------------------------------------------

    [MenuItem("PromeonLab/Build/Register Animator NavBar Entry")]
    public static void RegisterAnimatorNavBarEntry()
    {
        var config = AssetDatabase.LoadAssetAtPath<NavBarConfig>(NavBarConfigPath);
        if (config == null)
        {
            Debug.LogError("[RegisterAnimatorNavBarEntry] NavBarConfig asset not found at " + NavBarConfigPath);
            return;
        }

        var so = new SerializedObject(config);
        var entries = so.FindProperty("_entries");
        if (entries == null || !entries.isArray)
        {
            Debug.LogError("[RegisterAnimatorNavBarEntry] _entries property not found or not an array");
            return;
        }

        int targetIndex = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var element = entries.GetArrayElementAtIndex(i);
            var idProp = element.FindPropertyRelative("Id");
            if (idProp != null && idProp.stringValue == AnimatorEntryId)
            {
                targetIndex = i;
                break;
            }
        }

        bool appended = targetIndex < 0;
        if (appended)
        {
            targetIndex = entries.arraySize;
            entries.arraySize = entries.arraySize + 1;
        }

        var target  = entries.GetArrayElementAtIndex(targetIndex);
        var idP     = target.FindPropertyRelative("Id");
        var modesP  = target.FindPropertyRelative("VisibleModes");
        var groupP  = target.FindPropertyRelative("ExclusiveGroup");
        if (idP == null || modesP == null || groupP == null)
        {
            Debug.LogError("[RegisterAnimatorNavBarEntry] Entry fields (Id/VisibleModes/ExclusiveGroup) not found");
            return;
        }

        idP.stringValue = AnimatorEntryId;

        if (appended)
        {
            // Defaults: visible in VrEditing + Sandbox, share the "center" group used by timeline/settings/assets.
            modesP.arraySize = 2;
            modesP.GetArrayElementAtIndex(0).intValue = (int)AppMode.VrEditing;
            modesP.GetArrayElementAtIndex(1).intValue = (int)AppMode.Sandbox;
            groupP.stringValue = "center";
        }
        // If the entry already existed, preserve user-edited VisibleModes/ExclusiveGroup values.

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Debug.Log("[RegisterAnimatorNavBarEntry] " + (appended ? "appended" : "verified existing")
                  + " entry id=" + AnimatorEntryId
                  + " at index " + targetIndex
                  + "; entries.arraySize=" + entries.arraySize);
    }

    // ---------------------------------------------------------------------
    // Sandbox: isolated Overlay Canvas scene for visual iteration
    // ---------------------------------------------------------------------

    [MenuItem("PromeonLab/Build/Sandbox/Create Sandbox Scene")]
    public static void CreateSandboxScene()
    {
        EnsureAssetFolder(SandboxSceneDir);

        // Create a fresh empty scene; mark as the single open scene so we can populate cleanly.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Main Camera ---
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.10f, 1f);
        cam.transform.position = new Vector3(0f, 1f, -10f);

        // --- EventSystem ---
        var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // --- SandboxCanvas (Screen Space Overlay) ---
        var canvasGo = new GameObject(SandboxCanvasName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, SandboxScenePath);
        if (saved)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sandbox] Scene created at " + SandboxScenePath);
        }
        else
        {
            Debug.LogError("[Sandbox] Failed to save scene at " + SandboxScenePath);
        }
    }

    [MenuItem("PromeonLab/Build/Sandbox/Build Animator UI In Sandbox")]
    public static void BuildAnimatorUiInSandbox()
    {
        BuildInActiveScene();
    }

    public static void BuildInActiveScene()
    {
        var canvasGo = GameObject.Find(SandboxCanvasName);
        if (canvasGo == null)
        {
            Debug.LogError("[Sandbox] SandboxCanvas not found in active scene. " +
                           "Run 'PromeonLab/Build/Sandbox/Create Sandbox Scene' first.");
            return;
        }

        // Idempotent rebuild
        var existing = canvasGo.transform.Find(SandboxRootName);
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var root = BuildRoot();
        if (root == null)
        {
            Debug.LogError("[Sandbox] BuildRoot returned null");
            return;
        }

        root.transform.SetParent(canvasGo.transform, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale    = Vector3.one;

        // Sandbox-specific overrides: visible by default, full-screen central panel.
        root.SetActive(true);
        var activeChild = root.transform.Find("ActiveStateRoot");
        var emptyChild  = root.transform.Find("EmptyStateRoot");
        if (activeChild != null) activeChild.gameObject.SetActive(true);
        if (emptyChild  != null) emptyChild.gameObject.SetActive(false);

        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot     = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = new Vector2(1400f, 760f);

        // Mark scene dirty so save indicator updates.
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Selection.activeGameObject = root;
        Debug.Log("[Sandbox] Built AnimatorPanelModule under " + SandboxCanvasName +
                  " size=" + rootRt.sizeDelta);
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ---------------------------------------------------------------------
    // One-shot migration: TimelineBtn -> AnimatorBtn binding swap in UserPanel
    // ---------------------------------------------------------------------
    [MenuItem("PromeonLab/Build/Wire AnimatorBtn From TimelineBtn")]
    public static void MigrateTimelineBtnToAnimator()
    {
        var root = PrefabUtility.LoadPrefabContents(UserPanelPath);
        if (root == null)
        {
            Debug.LogError("[MigrateTimelineBtnToAnimator] could not load " + UserPanelPath);
            return;
        }

        try
        {
            var userPanel = root.GetComponent<UserPanel>();
            if (userPanel == null)
            {
                Debug.LogError("[MigrateTimelineBtnToAnimator] UserPanel component not found on root.");
                return;
            }

            var so       = new SerializedObject(userPanel);
            var bindings = so.FindProperty("_bindings");
            if (bindings == null || !bindings.isArray)
            {
                Debug.LogError("[MigrateTimelineBtnToAnimator] _bindings array not found.");
                return;
            }

            int timelineIdx = -1;
            int animatorIdx = -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var el = bindings.GetArrayElementAtIndex(i);
                var id = el.FindPropertyRelative("EntryId").stringValue;
                if (id == "timeline") timelineIdx = i;
                else if (id == "animator") animatorIdx = i;
            }

            if (timelineIdx < 0 || animatorIdx < 0)
            {
                Debug.LogError($"[MigrateTimelineBtnToAnimator] expected both 'timeline' and 'animator' entries. timelineIdx={timelineIdx}, animatorIdx={animatorIdx}");
                return;
            }

            var timelineEl = bindings.GetArrayElementAtIndex(timelineIdx);
            var animatorEl = bindings.GetArrayElementAtIndex(animatorIdx);

            var timelineBtnRef   = timelineEl.FindPropertyRelative("NavButton").objectReferenceValue;
            var timelinePanelRef = timelineEl.FindPropertyRelative("Panel").objectReferenceValue;

            // Move button reference: animator <- timeline; timeline <- null
            animatorEl.FindPropertyRelative("NavButton").objectReferenceValue = timelineBtnRef;
            timelineEl.FindPropertyRelative("NavButton").objectReferenceValue = null;

            // Rename button + update label
            bool renamed       = false;
            bool labelChanged  = false;
            string oldLabel    = null;
            var btnComponent   = timelineBtnRef as Button;
            GameObject btnGo   = null;
            if (btnComponent != null)
            {
                btnGo = btnComponent.gameObject;
            }
            else if (timelineBtnRef is Component compRef)
            {
                btnGo = compRef.gameObject;
            }
            else if (timelineBtnRef is GameObject goRef)
            {
                btnGo = goRef;
            }

            if (btnGo != null)
            {
                btnGo.name = "AnimatorBtn";
                renamed    = true;

                var label = btnGo.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    oldLabel = label.text;
                    var lowered = oldLabel == null ? string.Empty : oldLabel.Trim().ToLowerInvariant();
                    if (lowered == "timeline")
                    {
                        label.text   = "Animator";
                        labelChanged = true;
                    }
                    else
                    {
                        Debug.Log($"[MigrateTimelineBtnToAnimator] label text is '{oldLabel}', not 'Timeline' — leaving as-is for user review.");
                    }
                }
                else
                {
                    Debug.Log("[MigrateTimelineBtnToAnimator] no TMP_Text label child found under button — check for sprite/icon manually.");
                }
            }
            else
            {
                Debug.LogWarning("[MigrateTimelineBtnToAnimator] timeline NavButton reference is null or not resolvable to a GameObject.");
            }

            // Destroy orphan AnimationModule panel (was timeline's Panel)
            bool orphanDestroyed = false;
            var orphanGo = timelinePanelRef as GameObject;
            if (orphanGo == null && timelinePanelRef is Component orphanComp)
                orphanGo = orphanComp.gameObject;

            if (orphanGo != null)
            {
                if (orphanGo.transform.parent != null)
                {
                    Debug.Log($"[MigrateTimelineBtnToAnimator] destroying orphan panel '{orphanGo.name}' under parent '{orphanGo.transform.parent.name}'.");
                    Object.DestroyImmediate(orphanGo);
                    orphanDestroyed = true;
                }
                else
                {
                    Debug.LogWarning("[MigrateTimelineBtnToAnimator] orphan panel has no parent — refusing to destroy.");
                }
            }
            else
            {
                Debug.Log("[MigrateTimelineBtnToAnimator] timeline Panel reference is null; nothing to destroy.");
            }

            // Remove timeline binding entry
            // Re-resolve timeline index in case the indices shifted (they didn't here, but be safe)
            int removeAt = -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var el = bindings.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("EntryId").stringValue == "timeline")
                {
                    removeAt = i;
                    break;
                }
            }
            bool bindingRemoved = false;
            if (removeAt >= 0)
            {
                bindings.DeleteArrayElementAtIndex(removeAt);
                bindingRemoved = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, UserPanelPath);

            Debug.Log(
                $"[MigrateTimelineBtnToAnimator] summary: " +
                $"buttonRenamed={renamed}, labelChanged={labelChanged} (oldText='{oldLabel}'), " +
                $"timelineBindingRemoved={bindingRemoved}, animationModuleDestroyed={orphanDestroyed}, " +
                $"finalBindingsSize={bindings.arraySize}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------------
    // One-shot migration: remove 'timeline' entry from NavBarConfig
    // ---------------------------------------------------------------------
    [MenuItem("PromeonLab/Build/Remove Timeline NavBarEntry")]
    public static void RemoveTimelineNavBarEntry()
    {
        var asset = AssetDatabase.LoadAssetAtPath<NavBarConfig>(NavBarConfigPath);
        if (asset == null)
        {
            var guids = AssetDatabase.FindAssets("t:NavBarConfig");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                asset = AssetDatabase.LoadAssetAtPath<NavBarConfig>(path);
                Debug.Log($"[RemoveTimelineNavBarEntry] default path missing, using found asset at {path}");
            }
        }
        if (asset == null)
        {
            Debug.LogError("[RemoveTimelineNavBarEntry] NavBarConfig asset not found.");
            return;
        }

        var so      = new SerializedObject(asset);
        var entries = so.FindProperty("_entries");
        if (entries == null || !entries.isArray)
        {
            Debug.LogError("[RemoveTimelineNavBarEntry] _entries array not found.");
            return;
        }

        int removeAt = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var el = entries.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("Id").stringValue == "timeline")
            {
                removeAt = i;
                break;
            }
        }

        if (removeAt < 0)
        {
            Debug.Log($"[RemoveTimelineNavBarEntry] no 'timeline' entry to remove, final size={entries.arraySize}");
            return;
        }

        entries.DeleteArrayElementAtIndex(removeAt);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[RemoveTimelineNavBarEntry] removed timeline entry, final size={entries.arraySize}");
    }
}
