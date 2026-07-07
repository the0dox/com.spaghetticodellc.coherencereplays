using System;
using System.Collections.Generic;
using Coherence.Toolkit;
using SpaghettiCode.CoherenceReplays.Runtime;
using UnityEngine;

public abstract class Replay<T> : ScriptableObject, IReplay
{
    IReplay IReplay.Other => Other;
    int IReplay.FailureFrame => FailureFrame;

    public int FailureFrame;
    public Replay<T> Other;
    public ReplayFrame<T>[] States;
    public int Duration => Mathf.Min(Other.States.Length, States.Length);

    public void TrimToFirstInput(int i)
    {

    }

    public void AssignOther(Replay<T> otherReplay)
    {
        Other = otherReplay;
        otherReplay.Other = this;
    }

    public int NextEventIndex(int startingIndex)
    {
        startingIndex = Mathf.Clamp(startingIndex + 1, 0, Duration);
        for(int i = startingIndex; i < Duration; i++)
        {
            if(States[i].MetaState.HasEvents)
            {
                return i;
            }
        }
        return Duration;
    }

    public int PreviousEventIndex(int startingIndex)
    {
        startingIndex = Mathf.Clamp(startingIndex - 1, 0, Duration);
        for(int i = startingIndex; i >= 0; i--)
        {
            if(States[i].MetaState.HasEvents)
            {
                return i;
            }
        }
        return 0;
    }

    public abstract void LoadReplayInSingleplayer(GameObject ClientPrefab, CoherenceInputSimulation<T> Simulator);
}

[System.Serializable]
public class ReplayFrame<T>
{
    public T InitalState;
    public ReplayMetaState MetaState;
}

[System.Serializable]
public struct ReplayMetaState
{
    public bool HasEvents;
    public bool WasUpdated;
    public bool FirstDesync;
    public string EventData;
    public long GameFrame;
    public string Hash;
}

public interface IReplay
{
    public string name {get;}
    public int Duration {get;}
    public IReplay Other {get;}
    public int FailureFrame {get;}
    public int PreviousEventIndex(int startingIndex);
    public int NextEventIndex(int startingIndex);

}

// used by components and editors that can 'play' replays
public interface IReplayHarness
{
    public IReplay CurrentReplay {get;}
    public event EventHandler<ReplayPlayedEvent> Updated;
    public void AttachToHarness(IReplayHarness Other);
    public void SetReplay(IReplay replayAsset);
}

public interface IReplayPlayer : IReplayHarness
{
    public int Frame {get;}
    public bool Play {get; set;}
    public void AdvanceFrame();
    public void SetFrame(int frame);
}

public class ReplayPlayedEvent : EventArgs
{
    public IReplay CurrentReplay;
    public int Frame;
}