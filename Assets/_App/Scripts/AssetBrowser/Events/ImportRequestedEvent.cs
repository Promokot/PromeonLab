// pipeline → wizard: a file was picked; show the wizard with this suggestion.
public struct ImportRequestedEvent
{
    public string    FilePath;
    public string    SuggestedName;
    public AssetType SuggestedType;
}
