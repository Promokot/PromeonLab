using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AnimationSetupEditor
{
    private const string PREFAB_PATH =
        "Assets/_App/Subsystems/SpatialUi/Prefabs/Panels/UserPanel/UserPanel.prefab";
    private const string MARKER_PREFAB_PATH =
        "Assets/_App/Subsystems/AnimationAuthoring/UI/KeyframeMarker.prefab";

    [MenuItem("PromeonLab/Setup Animation UI")]
    public static void SetupAnimationUI()
    {
        EnsureKeyframeMarkerPrefab();

        var prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            var animModuleGO = FindDeep(prefabRoot.transform, "AnimationModule");
            if (animModuleGO == null)
            {
                Debug.LogError("[AnimSetup] AnimationModule not found in prefab hierarchy.");
                return;
            }

            RebuildAnimationModule(animModuleGO);
            WireTimelineBinding(prefabRoot, animModuleGO);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);
            Debug.Log("[AnimSetup] UserPanel prefab saved successfully.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.Refresh();
    }

    // ─── KeyframeMarker prefab ────────────────────────────────────────────────

    static void EnsureKeyframeMarkerPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MARKER_PREFAB_PATH) != null)
        {
            Debug.Log("[AnimSetup] KeyframeMarker prefab already exists.");
            return;
        }

        var go = new GameObject("KeyframeMarker");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta        = new Vector2(12, 12);
        rt.localPosition    = Vector3.zero;
        rt.localEulerAngles = new Vector3(0, 0, 45);

        var img = go.AddComponent<Image>();
        img.color         = new Color(1f, 0.75f, 0.1f, 1f);
        img.raycastTarget = false;

        PrefabUtility.SaveAsPrefabAsset(go, MARKER_PREFAB_PATH);
        Object.DestroyImmediate(go);
        Debug.Log("[AnimSetup] KeyframeMarker prefab created.");
    }

    // ─── AnimationModule rebuild ──────────────────────────────────────────────

    static void RebuildAnimationModule(GameObject root)
    {
        // Remove all MonoBehaviours except built-in Unity ones (keep Image, CanvasGroup).
        foreach (var mb in root.GetComponents<MonoBehaviour>())
        {
            var t = mb.GetType().Name;
            if (t != "Image" && t != "CanvasGroup")
                Object.DestroyImmediate(mb);
        }

        // Delete all existing children.
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        // Ensure Image has correct panel color.
        var bg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        // Add CanvasGroup if missing.
        if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();

        // Set inactive (shown only when user clicks the Timeline nav button).
        root.SetActive(false);

        // Add AnimationModule.
        var animModule = root.AddComponent<AnimationModule>();

        // ── Content (VerticalLayoutGroup, fills parent) ──────────────────────
        var content   = MakeRT(root.transform, "Content");
        FillParent(content);

        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 6;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        // ── TransportRow ─────────────────────────────────────────────────────
        var transportRow = MakeRT(content, "TransportRow");
        SetLayoutHeight(transportRow.gameObject, 40);

        var tHLG = transportRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tHLG.childControlWidth    = false;
        tHLG.childControlHeight   = true;
        tHLG.childForceExpandWidth  = false;
        tHLG.childForceExpandHeight = true;
        tHLG.spacing = 4;

        var rewindBtn    = MakeButton(transportRow, "RewindButton",    "◀◀");
        var playBtn      = MakeButton(transportRow, "PlayButton",       "▶");
        var stopBtn      = MakeButton(transportRow, "StopButton",       "■");
        SetFixedWidth(rewindBtn.transform, 50);
        SetFixedWidth(playBtn.transform,   50);
        SetFixedWidth(stopBtn.transform,   50);

        var frameLabel = MakeLabel(transportRow, "FrameLabel", "Fr: 0");
        var flLE = frameLabel.gameObject.AddComponent<LayoutElement>();
        flLE.flexibleWidth = 1;

        // ── ScrubberRow ──────────────────────────────────────────────────────
        var scrubberRow = MakeRT(content, "ScrubberRow");
        SetLayoutHeight(scrubberRow.gameObject, 34);

        var slider      = MakeSlider(scrubberRow, "Scrubber");
        var sliderLE    = slider.gameObject.AddComponent<LayoutElement>();
        sliderLE.flexibleWidth   = 1;
        sliderLE.preferredHeight = 28;

        // MarkersRoot overlays the Scrubber; uses ignoreLayout so it doesn't affect VLG.
        var markersRoot = MakeRT(scrubberRow, "MarkersRoot");
        FillParent(markersRoot);
        var mrLE = markersRoot.gameObject.AddComponent<LayoutElement>();
        mrLE.ignoreLayout = true;
        // Ensure Z = 0.
        markersRoot.localPosition = Vector3.zero;

        // ── KeyframeRow ──────────────────────────────────────────────────────
        var keyframeRow = MakeRT(content, "KeyframeRow");
        SetLayoutHeight(keyframeRow.gameObject, 40);

        var kHLG = keyframeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        kHLG.childControlWidth    = true;
        kHLG.childControlHeight   = true;
        kHLG.childForceExpandWidth  = true;
        kHLG.childForceExpandHeight = true;
        kHLG.spacing = 6;

        var setKeyBtn    = MakeButton(keyframeRow, "SetKeyButton",    "Set Key");
        var deleteKeyBtn = MakeButton(keyframeRow, "DeleteKeyButton", "Delete Key");

        // ── Wire SerializeField refs ─────────────────────────────────────────
        var so = new SerializedObject(animModule);
        so.FindProperty("_playButton").objectReferenceValue      = playBtn.GetComponent<Button>();
        so.FindProperty("_stopButton").objectReferenceValue      = stopBtn.GetComponent<Button>();
        so.FindProperty("_rewindButton").objectReferenceValue    = rewindBtn.GetComponent<Button>();
        so.FindProperty("_scrubber").objectReferenceValue        = slider;
        so.FindProperty("_frameLabel").objectReferenceValue      = frameLabel.GetComponent<TextMeshProUGUI>();
        so.FindProperty("_markersRoot").objectReferenceValue     = markersRoot;
        so.FindProperty("_setKeyButton").objectReferenceValue    = setKeyBtn.GetComponent<Button>();
        so.FindProperty("_deleteKeyButton").objectReferenceValue = deleteKeyBtn.GetComponent<Button>();

        var markerPrefabGO = AssetDatabase.LoadAssetAtPath<GameObject>(MARKER_PREFAB_PATH);
        if (markerPrefabGO != null)
            so.FindProperty("_markerPrefab").objectReferenceValue = markerPrefabGO.GetComponent<Image>();
        else
            Debug.LogWarning("[AnimSetup] KeyframeMarker prefab not found at " + MARKER_PREFAB_PATH);

        so.ApplyModifiedProperties();
        Debug.Log("[AnimSetup] AnimationModule rebuilt and wired.");
    }

    // ─── UserPanel binding ────────────────────────────────────────────────────

    static void WireTimelineBinding(GameObject prefabRoot, GameObject animModuleGO)
    {
        var userPanel = prefabRoot.GetComponent<UserPanel>();
        if (userPanel == null)
        {
            Debug.LogError("[AnimSetup] UserPanel component not found on prefab root.");
            return;
        }

        var timelineBtnGO = FindDeep(prefabRoot.transform, "TimelineBtn");
        if (timelineBtnGO == null)
        {
            Debug.LogError("[AnimSetup] TimelineBtn not found in prefab hierarchy.");
            return;
        }

        var so           = new SerializedObject(userPanel);
        var bindingsProp = so.FindProperty("_bindings");

        for (int i = 0; i < bindingsProp.arraySize; i++)
        {
            var elem    = bindingsProp.GetArrayElementAtIndex(i);
            var entryId = elem.FindPropertyRelative("EntryId").stringValue;
            if (entryId != "timeline") continue;

            elem.FindPropertyRelative("NavButton").objectReferenceValue = timelineBtnGO.GetComponent<Button>();
            elem.FindPropertyRelative("Panel").objectReferenceValue     = animModuleGO;
            so.ApplyModifiedProperties();
            Debug.Log("[AnimSetup] Timeline binding wired: NavButton=" + timelineBtnGO.name +
                      " Panel=" + animModuleGO.name);
            return;
        }

        Debug.LogWarning("[AnimSetup] No binding with EntryId='timeline' found in UserPanel._bindings.");
    }

    // ─── UI factory helpers ───────────────────────────────────────────────────

    static RectTransform MakeRT(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = Vector3.zero;  // Z = 0 always
        return rt;
    }

    static RectTransform MakeRT(RectTransform parent, string name) => MakeRT(parent.transform, name);

    static Slider MakeSlider(RectTransform parent, string name)
    {
        var go = MakeRT(parent, name);

        // Background
        var bgRT = MakeRT(go, "Background");
        FillParent(bgRT);
        var bgImg = bgRT.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
        bgImg.raycastTarget = false;

        // Fill Area
        var fillArea = MakeRT(go, "Fill Area");
        fillArea.anchorMin  = new Vector2(0, 0.25f);
        fillArea.anchorMax  = new Vector2(1, 0.75f);
        fillArea.offsetMin  = new Vector2(5, 0);
        fillArea.offsetMax  = new Vector2(-15, 0);
        fillArea.localPosition = Vector3.zero;

        var fillRT = MakeRT(fillArea, "Fill");
        fillRT.anchorMin   = Vector2.zero;
        fillRT.anchorMax   = new Vector2(0, 1);
        fillRT.sizeDelta   = new Vector2(10, 0);
        fillRT.localPosition = Vector3.zero;
        var fillImg = fillRT.gameObject.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.75f, 1f, 0.9f);
        fillImg.raycastTarget = false;

        // Handle Slide Area
        var handleArea = MakeRT(go, "Handle Slide Area");
        FillParent(handleArea);
        handleArea.offsetMin = new Vector2(10, 0);
        handleArea.offsetMax = new Vector2(-10, 0);
        handleArea.localPosition = Vector3.zero;

        var handleRT = MakeRT(handleArea, "Handle");
        handleRT.anchorMin   = Vector2.zero;
        handleRT.anchorMax   = new Vector2(0, 1);
        handleRT.sizeDelta   = new Vector2(20, 0);
        handleRT.localPosition = Vector3.zero;
        var handleImg = handleRT.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.85f, 1f, 1f);

        var slider = go.gameObject.AddComponent<Slider>();
        slider.fillRect     = fillRT;
        slider.handleRect   = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction    = Slider.Direction.LeftToRight;
        slider.minValue     = 0;
        slider.maxValue     = 120;
        slider.wholeNumbers = true;
        slider.value        = 0;

        return slider;
    }

    static GameObject MakeButton(RectTransform parent, string name, string label)
    {
        var go  = MakeRT(parent, name).gameObject;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.26f, 0.9f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.45f, 1f);
        colors.pressedColor     = new Color(0.12f, 0.12f, 0.2f, 1f);
        btn.colors = colors;

        var labelRT = MakeRT(go.transform, "Label");
        FillParent(labelRT);

        var tmp = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 14;
        tmp.color     = Color.white;

        return go;
    }

    static RectTransform MakeLabel(RectTransform parent, string name, string text)
    {
        var rt  = MakeRT(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize  = 13;
        tmp.color     = new Color(0.8f, 0.8f, 0.8f, 1f);
        return rt;
    }

    // ─── RectTransform helpers ────────────────────────────────────────────────

    static void FillParent(RectTransform rt)
    {
        rt.anchorMin     = Vector2.zero;
        rt.anchorMax     = Vector2.one;
        rt.offsetMin     = Vector2.zero;
        rt.offsetMax     = Vector2.zero;
        rt.localPosition = Vector3.zero;  // Z = 0
    }

    static void SetLayoutHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight       = h;
    }

    static void SetFixedWidth(Transform t, float w)
    {
        var le = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.minWidth       = w;
    }

    // ─── Hierarchy search ─────────────────────────────────────────────────────

    static GameObject FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
