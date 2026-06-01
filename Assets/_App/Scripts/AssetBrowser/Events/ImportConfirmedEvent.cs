// wizard → pipeline: the user confirmed (Confirmed=false means cancelled).
public struct ImportConfirmedEvent
{
    public bool             Confirmed;
    public string           FilePath;
    public string           DisplayName;
    public AssetType        ChosenType;
    public TerminalBoneAxis TerminalBonesAxis;       // leaf-bone orientation chosen in the wizard (Rig only)
    public bool             InvertTerminalBonesAxis; // invert toggle in the wizard
}
