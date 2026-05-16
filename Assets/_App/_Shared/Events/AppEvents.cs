public struct SceneOpenedEvent       { public string SceneId; }
public struct SceneModifiedEvent     { }
public struct SceneClosedEvent       { }
public struct AssetImportedEvent     { public string AssetId; }
public struct SelectionChangedEvent  { public string SelectedNodeId; public string[] SelectedNodeIds; }
public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
public struct FrameChangedEvent      { public int Frame; }
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
public struct SceneSelectedEvent          { public string SceneId; public string DisplayName; }
public struct PlayerSpawnRequestedEvent   { public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct AssetSpawnRequestedEvent    { public ILabAsset Asset; public UnityEngine.Vector3 Position; public UnityEngine.Quaternion Rotation; }
public struct KeyboardFocusEvent          { public TMPro.TMP_InputField Target; }
