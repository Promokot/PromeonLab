using GLTFast;

internal static class _GltfApiSmoke
{
    // Compile-only proof that GLTFast.GltfImport resolves from _App.Runtime.
    public static System.Type Probe() => typeof(GltfImport);
}
