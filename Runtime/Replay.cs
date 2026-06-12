using UnityEngine;

public class Replay<T> : ScriptableObject
{
    public int FailureFrame;
    public Replay<T> Other;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TrimToFirstInput(int i)
    {

    }

    public void AssignOther(Replay<T> otherReplay)
    {
        Other = otherReplay;
        otherReplay.Other = this;
    }
}
