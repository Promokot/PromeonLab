// wizard → pipeline: the user confirmed (Confirmed=false means cancelled).
public struct ImportConfirmedEvent
{
    public bool      Confirmed;
    public string    FilePath;
    public string    DisplayName;
    public AssetType ChosenType;
}
