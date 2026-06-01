using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Rig. Slice A: loads the static imported mesh (no proxies),
// mirroring ObjectEntityFactory. Slice B adds BuildProxyRig (runtime proxy-bone construction).
public class RigEntityFactory
{
    private readonly GltfModelLoader _loader;

    public RigEntityFactory(GltfModelLoader loader) => _loader = loader;

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);
}
