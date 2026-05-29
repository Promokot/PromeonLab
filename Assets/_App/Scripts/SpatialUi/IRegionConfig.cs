public interface IRegionConfig
{
    bool TryGetRegion(string moduleId, out string regionKey);
    bool IsVisibleInMode(string moduleId, AppMode mode);
    bool TryGetRegionDefault(string regionKey, out string moduleId);
}
