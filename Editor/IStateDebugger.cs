using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System;
using System.Text;
using SpaghettiCode.CoherenceReplays.Runtime;




#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SpaghettiCode.CoherenceReplays.Editor
{
    public interface IStateDebugger
    {
        public void ReadJson(string file, int player);
        public bool CanRead(string file);
        public void CompareJsons();

        public static IStateDebugger CreateStateDebugger()
        {
            try
            {
                Type genericLibrary = typeof(StateDebuggerDeep<,>);
                Type constructedLibrary = genericLibrary.MakeGenericType(ReplaySettings.instance.GetStateType(), ReplaySettings.instance.GetReplayType());
                return Activator.CreateInstance(constructedLibrary) as IStateDebugger;
            }
            catch (Exception e)
            {
                Debug.LogError("failed to create a generic state debugger exception, did you set up your replay and simulation types correctly? " + e);
                return new NullStateDebugger();
            }
        }
    }

    public class NullStateDebugger : IStateDebugger
    {
        public bool CanRead(string file)
        {
            return false;
        }

        public void CompareJsons()
        {
        }

        public void ReadJson(string file, int player)
        {
            throw new NotImplementedException();
        }
    }

    public class TestNullStateDebugger<TGameState> : IStateDebugger
    {
        public bool CanRead(string file)
        {
            return true;
        }

        public void CompareJsons()
        {
        }

        public TestNullStateDebugger()
        {
            Debug.Log("created debugger with generic argument: " + typeof(TGameState).Name);
        }

        public void ReadJson(string file, int player)
        {            
            string savePath = EditorUtility.SaveFilePanelInProject($"Save Debug Replay asset",  "New Debug Replay", "asset", $"Enter the name of this is replay", "Assets/Replays");
            var example = ScriptableObject.CreateInstance(ReplaySettings.instance.GetReplayType());
            example.name = "test replay asset";
            AssetDatabase.CreateAsset(example, savePath);
            AssetDatabase.SaveAssets();
        }
    }


    // this is the
    public class StateDebuggerDeep<TGameState, TReplay> : IStateDebugger where TReplay : Replay<TGameState> where TGameState : struct
    {
        private const string ASSETEXTENSION = "Assets/";
        private const int NUMPLAYERS = 2;
        // frames are arranged in two parallel arrays for simple comparision 
        private FrameSampleWrapper<TGameState>[][] _data = new FrameSampleWrapper<TGameState>[NUMPLAYERS][];
        // used to confirm there are no duplicate files being compared
        private readonly string[] _paths = new string[NUMPLAYERS];


        // reads one file for player of a given index
        // called for both player 1 and player 2 so that they may be compared
        public void ReadJson(string file, int player)
        {
            _paths[player] = file;
            var output = JsonConvert.DeserializeObject<SortedDictionary<long, FrameSampleWrapper<TGameState>>>(File.ReadAllText(file));
            _data[player] = new FrameSampleWrapper<TGameState>[output.Count];
            Debug.Log(_data[player].Length);
            int index = 0;
            foreach(KeyValuePair<long, FrameSampleWrapper<TGameState>> kvp in output)
            {
                try
                {
                    if(kvp.Key == 0)
                    {
                        Debug.Log($"found invalid message at head {kvp.Value} ignoring");
                        continue;
                    }
                    if(kvp.Value.InitialState == null)
                    {
                        Debug.Log($"head was found to have a null inital state {kvp.Value} ignoring");
                        continue;
                    }
                    else
                    {
                        _data[player][index] = kvp.Value;
                    }
                }
                catch(Exception e)
                {
                    Debug.LogWarning($"failed to deserialize at frame {index} {kvp.Key} ending operation\n Exception {e}");
                    _data[player][index] = null;
                    return;
                }
                index++;
            }
            Debug.Log($"resizing to index {index}");
            Array.Resize(ref _data[player], index);
        }


        public bool CanRead(string file)
        {
            return !_paths.Contains(file);
        }

        // called if a valid json is found
        // reads through the contents and compares out all of the state hashes, identifying the first point of inaccuracy  
        public void CompareJsons()
        {
            FindFirstCommonIndex(out int i, out int j);
            Debug.Assert(_data[0][i].Frame == _data[1][j].Frame, $"should produce the same frame, instead got {i} {j} : {_data[0][i].Frame} != {_data[1][j].Frame}");
            string savePath = EditorUtility.SaveFilePanelInProject($"Save Debug Replay asset",  "New Debug Replay", "asset", $"Enter the name of this is replay", ASSETEXTENSION);
            int insertIndex = savePath.LastIndexOf('.');
            var replay0 = ExportReplay(savePath.Insert(insertIndex, "_p0"), _data[0], i);
            var replay1 = ExportReplay(savePath.Insert(insertIndex, "_p1"), _data[1], j);
            replay0.FailureFrame = -1;
            replay1.FailureFrame = -1;
            bool desync = false;
            while(!desync && i < _data[0].Length && j < _data[1].Length)
            {
                Debug.Log($"comparing p1 {_data[0][i]} and p1 {_data[1][j]}");
                if(_data[0][i].Hash != null && !_data[0][i].Hash.Equals(_data[1][j].Hash))
                {
                    int minFailureFrame = Mathf.Min(i,j);
                    Debug.LogWarning($"Found Desync {minFailureFrame} frames into simluation long: {_data[0][i].Frame}\n p1 Hash: {_data[0][i].Hash} p2 Hash: {_data[1][j].Hash} ");
                    SetFailureFrame(replay0,  minFailureFrame);
                    SetFailureFrame(replay1,  minFailureFrame);
                    desync = true;
                }

                i++;
                j++;
            }
            if(!desync)
            {
                Debug.Log("No desyncs Found!");
            }
            replay0.AssignOther(replay1);
            EditorUtility.SetDirty(replay0);
            EditorUtility.SetDirty(replay1);
            AssetDatabase.SaveAssets();
        }

        private TReplay ExportReplay(string name, FrameSampleWrapper<TGameState>[] deseraizliedRecord, int startIndex)
        {
            var replay = ScriptableObject.CreateInstance<TReplay>();
            int inputLength = deseraizliedRecord.Length - startIndex;
            //InputValue[][] playerInputs = new InputValue[2][]{new InputValue[inputLength], new InputValue[inputLength]};
            replay.States = new ReplayFrame<TGameState>[inputLength];
            int playerIndex;
            for(int i = 0; i < replay.States.Length; i++)
            {
                var deserialiedFrame = deseraizliedRecord[i + startIndex];
                playerIndex = 0; 
                bool wasUpdated = deserialiedFrame.UpdatedInputs != null && deserialiedFrame.UpdatedInputs.Count > 0;
                if(wasUpdated)
                {
                    Debug.Log($"found updated inputs in {name} at {i}" );
                }
                foreach(var kvp in wasUpdated ? deserialiedFrame.UpdatedInputs : deserialiedFrame.InitialInputs)
                {
                    //playerInputs[playerIndex][i] = ParseInputValue(kvp.Value);
                    
                    playerIndex++;
                }
                replay.States[i] = new ReplayFrame<TGameState>();
                replay.States[i].InitalState = (TGameState)(wasUpdated ? deserialiedFrame.UpdatedState : deserialiedFrame.InitialState);
                replay.States[i].MetaState = CreateMetaState(wasUpdated, deserialiedFrame);
            }
            AssetDatabase.CreateAsset(replay, name);
            //replay.Inputs = new InputRecordTemplate[playerInputs.Length];
            //replay.InitalState = initalStates; 
            //replay.SetDefaultEnvrionmentSettings();
            //SaveInputRecords(name, ref playerInputs, replay, false);
            return replay;
        }

        // checks the first input entry to see if inputs are reveresed or not
        private bool DetermineReveresedInputs(Dictionary<string, string> deserializedInputs)
        {
            var firstToken = deserializedInputs.Values.First();
            string trimmedToken = firstToken.Substring(firstToken.IndexOf("Entity: ") + 7);
            trimmedToken = trimmedToken.Substring(0, trimmedToken.IndexOf(","));
            Debug.Log(trimmedToken);
            if(int.TryParse(trimmedToken, out int result))
            {
                return result != 0;
            }
            throw new ArgumentException($"unable to parse int from {trimmedToken}");
        }
        

        public ReplayMetaState CreateMetaState(bool wasUpdated, FrameSampleWrapper<TGameState> deserializedReference)
        {
            ReplayMetaState output = new ReplayMetaState();
            output.WasUpdated = wasUpdated;
            output.EventData = ParseEventData(deserializedReference);
            output.Hash = deserializedReference.Hash;
            output.GameFrame = deserializedReference.Frame;
            output.HasEvents = wasUpdated || !string.IsNullOrEmpty(output.EventData);
            return output;
        }

        /*
        private void SaveInputRecords(string name, ref InputValue[][] playerInputs, MultiplayerReplay replay, bool reversed)
        {
            for(int i = 0; i < playerInputs.Length; i++)
            {
                #if UNITY_EDITOR
                int index = reversed ? 1 - i : i;
                var newTemplate = ScriptableObject.CreateInstance<InputRecordTemplate>();
                newTemplate.SetTemplate(playerInputs[index]);
                // as of coherence 2.0 inputs appear to be saved in reverse order. 
                // (i.e. p1s inputs are serialized before p0) requiring us to save them in reverse order again
                replay.Inputs[index] = newTemplate;
                newTemplate.name = $"P{index} Inputs";
                AssetDatabase.AddObjectToAsset(newTemplate, name);
                #endif
            }
        }

        public InputValue ParseInputValue(string code)
        {
            code = code.Substring(code.LastIndexOf("[Input:") + 7);
            code = code.TrimEnd(']');
            if(int.TryParse(code, out int result))
            {
                return (InputValue)result;
            }
            throw new ArgumentException($"unable to parse int from {code}");
        }
        */
        
        // frames are arranged in consecutive order 
        // we use this to calculate the starting indexes for the player 1 and player 2 incremeters respectively
        // we identify which player is 'ahead' and have the other player's index 'catch' up by the difference
        private void FindFirstCommonIndex(out int i, out int j)
        {
            i = 0;
            Debug.Log($"{i}, {_data[0].Length} {_data[1].Length}");
            j = (int)(_data[0][0].Frame - _data[1][0].Frame);
            if(j < 0)
            {
                i = -j;
                j = 0;
            }
            Debug.Log("start indexes are " + i + " and " + j);
        }

        // as event data is very generic, we simply the type into a string of all event data printed that frame
        // returns all event data associated with json serialized frame sample
        private string ParseEventData(FrameSampleWrapper<TGameState> sample)
        {
            StringBuilder output = new StringBuilder();
            if(sample.AdditionalData.TryGetValue("Events", out JToken eventsJtoken))
            {
                foreach(JToken evt in eventsJtoken)
                {
                    string evtName = evt["Event"].ToString();
                    if(!evtName.Equals("InputSent") && !evtName.Equals("InputReceived"))
                    {
                        Debug.Log($"found unique event: w/ name {evtName} " + evt);
                        output.Append(evt);
                    }
                }
            }
            return output.ToString();
        }

        private static void SetFailureFrame(Replay<TGameState> target, int frame)
        {
            target.FailureFrame = frame;
            ReplayMetaState existingMetaState = target.States[frame].MetaState;
            existingMetaState.FirstDesync = true;
            target.States[frame].MetaState = existingMetaState;
        }
    }
    
    [System.Serializable]
    public class FrameSampleWrapper<TGameState> where TGameState : struct
    {
        public long Frame;

        public long AckFrame;

        public long ReceiveFrame;

        public long AckedAt;

        public long MispredictionFrame;

        public string Hash;

        public string Time;

        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long UpdatedAt;

        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ShouldPause;

        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TGameState? UpdatedState;

        //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, string> UpdatedInputs;

        public TGameState? InitialState;

        public Dictionary<string, string> InitialInputs;

        public string EventData;

        //public Dictionary<string, InputBufferState> InputBufferStates;
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set;}
    }
}