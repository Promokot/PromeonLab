public struct SceneOpenedEvent       { public string SceneId; }
public struct SceneModifiedEvent     { }
public struct SceneClosedEvent       { }
public struct AssetImportedEvent     { public string AssetId; }
public struct SelectionChangedEvent  { public string SelectedNodeId; }
public struct ModeChangedEvent       { public AppMode PreviousMode; public AppMode CurrentMode; }
public struct FrameChangedEvent      { public int Frame; }
public struct PlaybackStateChangedEvent { public bool IsPlaying; public int Frame; }
public struct ErrorOccurredEvent     { public ErrorLevel Level; public string Message; }
