using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent MonoBehaviour (lives on PersistentRoot, survives DontDestroyOnLoad). Owns the head
// fade and the async single-scene load, driven by a frame-locked coroutine so the fade stays smooth
// in both directions. A re-entrancy guard drops Load calls while a transition is already running.
// onLoaded fires after the new scene is loaded+activated (its LifetimeScope has built), before the
// fade-in – ModeOrchestrator publishes ModeChangedEvent there.
public class SceneTransitionRunner : MonoBehaviour, ISceneTransition
{
    [SerializeField] private HeadFade _fade;

    public bool IsTransitioning { get; private set; }

    public void Load(string sceneName, Action onLoaded)
    {
        if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return;
        IsTransitioning = true;
        StartCoroutine(RunRoutine(sceneName, onLoaded));
    }

    // Cold-boot helper: start fully black, load the first scene, fade in. Used by AppBootstrap.
    public void LoadInitial(string sceneName, Action onLoaded)
    {
        if (_fade != null) _fade.SetAlphaImmediate(1f);
        Load(sceneName, onLoaded);
    }

    private IEnumerator RunRoutine(string sceneName, Action onLoaded)
    {
        if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(1f));

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op != null) yield return op;

        // Single load makes the new scene the active scene automatically; its LifetimeScope built
        // during the load. Now it is safe to announce the mode change.
        onLoaded?.Invoke();

        if (_fade != null) yield return StartCoroutine(_fade.FadeRoutine(0f));

        IsTransitioning = false;
    }
}
