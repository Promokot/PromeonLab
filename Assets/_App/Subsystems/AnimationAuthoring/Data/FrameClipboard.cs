using System.Collections.Generic;

public class FrameClipboard
{
    public string                    OwnerNodeId;
    public int                       SourceFrame;
    public List<FrameClipboardEntry> Entries = new();

    public bool IsEmpty => Entries == null || Entries.Count == 0;
}
