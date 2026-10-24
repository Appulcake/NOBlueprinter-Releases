using System;

namespace Blueprinter;

public static class Api
{
    public enum State
    {
        NotStarted,
        LoadingBundles,
        ApplyingPatches,
        ApplyingOps,
        Ready,
        Failed
    }

    public static State CurrentState { get; internal set; } = State.NotStarted;
    public static event Action<State> StateChanged;

    internal static void SetState(State state)
    {
        if (CurrentState == state)
            return;

        Plugin.Log?.LogDebug($"[Api] State changed: {CurrentState} -> {state}");
        CurrentState = state;
        StateChanged?.Invoke(state);
    }
}
