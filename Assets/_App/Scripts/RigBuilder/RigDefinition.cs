using System;
using System.Collections.Generic;

[Serializable]
public class RigDefinition
{
    public int SchemaVersion = 1;
    public string AssetId;
    public TerminalBoneAxis TerminalBonesAxis;     // per-rig leaf-bone orientation; Auto = legacy/default
    public bool             InvertTerminalBonesAxis; // flip the chosen X/Y/Z axis (ignored when Auto)
    public List<BoneRecord> Bones = new();
    public List<IkChainRecord> IkChains = new();
}
