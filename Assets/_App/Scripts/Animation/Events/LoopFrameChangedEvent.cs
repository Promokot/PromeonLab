// Published by AnimationAuthoring's background loop as its integer frame advances. Display-only:
// the timeline playhead follows it for the selected owner. Kept separate from FrameChangedEvent,
// which is tied to the transport clock and drives clip sampling.
public struct LoopFrameChangedEvent
{
    public string OwnerNodeId;
    public int    Frame;
}
