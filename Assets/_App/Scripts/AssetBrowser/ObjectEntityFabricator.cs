using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Per-type runtime construction helper for Object: loads the imported glTF mesh. Thin wrapper over the
// shared low-level GltfModelImporter. Mirrors ReferenceEntityFabricator / RigEntityFabricator.
public class ObjectEntityFabricator
{
    private readonly GltfModelImporter _loader;

    public ObjectEntityFabricator(GltfModelImporter loader) => _loader = loader;

    public Task<GameObject> CreateAsync(string absolutePath, Vector3 position, Quaternion rotation, CancellationToken ct)
        => _loader.LoadAsync(absolutePath, position, rotation, ct);
}
