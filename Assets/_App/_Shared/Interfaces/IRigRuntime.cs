using UnityEngine;

public interface IRigRuntime
{
    RigDefinition BuildFromSkinnedMesh(SkinnedMeshRenderer smr);
    void ApplyDefinition(RigDefinition definition, SkinnedMeshRenderer smr);
}
