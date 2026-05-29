using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent MonoBehaviour (lives on PersistentRoot, survives DontDestroyOnLoad). Owns the head
// fade and the async single-scene load. A re-entrancy guard drops Load calls while a transition is
// already running. onLoaded fires after the new scene is loaded+activated (its LifetimeScope has
// built), before the fade-in — ModeOrchestrator publishes ModeChangedEvent there.
public class SceneTransitionRunner : MonoBehaviour, ISceneTransition
{
    [SerializeField] private HeadFade _fade;

    public bool IsTransitioning { get; private set; }

    private CancellationTokenSource _cts;

    public void Load(string sceneName, Action onLoaded)
    {
        if (IsTransitioning || string.IsNullOrEmpty(sceneName)) return;
        IsTransitioning = true;
        _cts = new CancellationTokenSource();
        _ = RunAsync(sceneName, onLoaded, _cts.Token);
    }

    // Cold-boot helper: start fully black, load the first scene, fade in. Used by AppBootstrap.
    public void LoadInitial(string sceneName, Action onLoaded)
    {
        if (_fade != null) _fade.SetAlphaImmediate(1f);
        Load(sceneName, onLoaded);
    }

    private async System.Threading.Tasks.Task RunAsync(string sceneName, Action onLoaded, CancellationToken token)
    {
        try
        {
            if (_fade != null) await _fade.FadeAsync(1f, token);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
            {
                token.ThrowIfCancellationRequested();
                await System.Threading.Tasks.Task.Yield();
            }
            // Single load makes the new scene the active scene automatically; its LifetimeScope
            // built during the load. Now it is safe to announce the mode change.
            onLoaded?.Invoke();

            if (_fade != null) await _fade.FadeAsync(0f, token);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex) { Debug.LogError($"SceneTransitionRunner: load '{sceneName}' failed: {ex}"); }
        finally { IsTransitioning = false; }
    }

    private void OnDestroy() => _cts?.Cancel();
}
