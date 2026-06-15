using System.Collections.Generic;
using UnityEngine;

public class Replay<T> : ScriptableObject
{
    public int FailureFrame;
    public Replay<T> Other;
    public List<T> Frames;
    
    public void TrimToFirstInput(int i)
    {

    }

    public void AssignOther(Replay<T> otherReplay)
    {
        Other = otherReplay;
        otherReplay.Other = this;
    }
}
