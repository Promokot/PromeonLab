using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAssetLibrary
{
    IReadOnlyList<ILabAsset> Assets { get; }

    Task LoadAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    void Add(ILabAsset asset);
    void Remove(string id);
}
