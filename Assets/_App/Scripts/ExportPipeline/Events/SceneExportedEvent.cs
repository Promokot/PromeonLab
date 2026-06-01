/// <summary>
/// Published by SceneExporter after an export attempt (success or failure).
/// ExportPanel subscribes to update its status label.
/// </summary>
public struct SceneExportedEvent
{
    public string Path;
    public bool   Success;
    public string Message;
}
