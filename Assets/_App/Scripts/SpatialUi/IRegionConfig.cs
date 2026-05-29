public interface IRegionConfig
{
    bool TryGetRegion(string moduleId, out string regionKey);
    bool IsVisibleInMode(string moduleId, AppMode mode);
}
