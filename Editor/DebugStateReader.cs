/**
 * @file DebugStateReader.cs
 * @description 
 * @author Alexander Theodore
 * @copyright 2024 All Rights Reserved.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using System.IO;

namespace  SpaghettiCode.CoherenceReplays.Editor
{
    // tool for comparing state dumps from rollback netcode
    public class DebugStateReader
    {
        // number of players 
        public const int NUMPLAYERS = 2;
        private static readonly string assetPath = "/Assets";

        // prompts the user to open a json file in the project folder
        [MenuItem("coherence/Replays/Import Json")]
        public static void Open()
        {
            string file = Application.dataPath.Substring(0, Application.dataPath.Length - assetPath.Length);
            IStateDebugger reader = IStateDebugger.CreateStateDebugger();
            // repeat for two players
            for(int i = 0; i < NUMPLAYERS; i++)
            {
                file = EditorUtility.OpenFilePanel($"Select Debug file for player {i+1}", file, "json");
                if(!File.Exists(file))
                {
                    Debug.LogError($"File {file} not found. State Debugger Terminated");
                    return;
                }
                else if(!file.EndsWith(".json"))
                {
                    Debug.LogError($"{file} is not a .json file. State Debugger Terminated");
                    return;
                }
                else if(!reader.CanRead(file))
                {
                    Debug.LogError($"{file} already registered. State Debugger Terminated");
                    return;
                }
                else
                {
                    Debug.Log($"opening {file}");
                    reader.ReadJson(file, i);
                }
            }
            reader.CompareJsons();
        }
    }


}