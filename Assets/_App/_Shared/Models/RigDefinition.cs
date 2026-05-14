using System;
using System.Collections.Generic;

[Serializable]
public class RigDefinition
{
    public int SchemaVersion = 1;
    public string AssetId;
    public List<BoneRecord> Bones = new();
    public List<IkChainRecord> IkChains = new();
}
