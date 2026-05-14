using System;
using UnityEngine;

[Serializable]
public class IkChainRecord
{
    public string RootBone;
    public string EndBone;
    public string PoleBone;
    [Range(0f, 1f)]
    public float Weight = 1f;
}
