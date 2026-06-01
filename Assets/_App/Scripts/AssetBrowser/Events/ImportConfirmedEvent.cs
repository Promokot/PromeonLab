// wizard → pipeline: the user confirmed (Confirmed=false means cancelled).
public struct ImportConfirmedEvent
{
    public bool             Confirmed;
    public string           FilePath;
    public string           DisplayName;
    public AssetType        ChosenType;
    public TerminalBoneAxis TerminalAxis;   // leaf-bone orientation chosen in the wizard (Rig only)
}
