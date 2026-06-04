// Published synchronously by ModeOrchestrator BEFORE the Single scene load (i.e. while the outgoing
// scene + its LifetimeScope are still alive). Consumers that must act on the still-loaded outgoing
// scene (e.g. SceneAutoSaver capturing a snapshot) hook this instead of ModeChangedEvent, which fires
// only AFTER the load – by which point the outgoing scene scope has already been disposed.
public struct ModeExitingEvent { public AppMode From; public AppMode To; }
