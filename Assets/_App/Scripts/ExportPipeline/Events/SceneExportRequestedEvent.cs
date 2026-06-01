/// <summary>
/// Published by ExportPanel when the user taps Export.
/// SceneExporter subscribes, runs the export, then publishes SceneExportedEvent.
/// </summary>
public struct SceneExportRequestedEvent
{
    public string FileName;
}
