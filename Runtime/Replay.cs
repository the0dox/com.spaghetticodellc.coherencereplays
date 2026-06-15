using System.Collections.Generic;
using UnityEngine;

public class Replay<T> : ScriptableObject
{
    public int FailureFrame;
    public Replay<T> Other;
    public ReplayFrame<T>[] States;

    public void TrimToFirstInput(int i)
    {

    }

    public void AssignOther(Replay<T> otherReplay)
    {
        Other = otherReplay;
        otherReplay.Other = this;
    }
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