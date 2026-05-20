public struct SceneOpenedEvent       { public string SceneId; }
public struct SceneModifiedEvent     { }
public struct SceneClosedEvent       { }
public struct AssetImportedEvent     { public string AssetId; }
public struct SelectionChangedEvent  { public string SelectedNodeId; public string[] SelectedNodeIds; }
public struct NodeRenamedEvent       { public string NodeId; public string NewName; }
public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
public struct FrameChangedEvent      { public int Frame; }
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
public struct SceneSelectedEvent          { public string SceneId; public string DisplayName; }
public struct PlayerSpawnRequestedEvent   { public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct AssetSpawnRequestedEvent    { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct KeyboardFocusEvent          { public TMPro.TMP_InputField Target; }
public struct PanelDetachedEvent { public string EntryId; }
public struct PanelLinkedEvent   { public string EntryId; }
public struct PanelClosedEvent   { public string EntryId; }
public struct AnimationKeyframeChangedEvent { public string NodeId; }
