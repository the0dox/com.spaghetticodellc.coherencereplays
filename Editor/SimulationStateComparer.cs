/**
 * @file SimulationStateComparer.cs
 * @description 
 * @author Alexander Theodore
 * @copyright 2024 All Rights Reserved.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System;
using Object = UnityEngine.Object;
using SpaghettiCode.CoherenceReplays.Editor;
using SpaghettiCode.CoherenceReplays.Runtime;
using System.Runtime.InteropServices;

// Custom tool used for displaying all elements of a simulation game state
// compares the left hand state (incoming) with the right hand state (baseline)
// if incoming does not equal the baseline, the specific fields that do not match are highlighted red
namespace  SpaghettiCode.CoherenceReplays.Editor
{
    public class DeepStateComparer : CustomToolEditor
    {
        // referene to the tester object that serializes the state	
        // the frame that is being tested
        private SerializedProperty p_frame;	
        // used for scroll view
        Vector2 scrollPosition = Vector2.zero;
        private HashSet<string> m_mismatchedProps;
        private HashSet<string> m_mismatchedBaselineProps;
        private SerializedObject m_target;
        private SerializedObject m_this;
        public Object Context;
        public SerializedProperty incoming;
        public SerializedProperty baseline;
        public int frame;
        private SerializedProperty p_context;
        private bool _display;
        private string incomingHash;
        private string baselineHash;
        public bool hashFail;
        public bool desyncOnly = true;
        public bool displayEvents = true;
        bool advancedReplayControls;
        IReplay CurrentReplay;
        ReplayMetaState[] updatedSide = new ReplayMetaState[2];

        // used to trim down property paths when comparing baseline to incomming state. We only care about seralizied properties deeper than:
        // Data.States.Array[]. so we trim the first 3 '.' dots in the structure
        private static readonly int RELATIVEPROPERTYDEPTH = 3;
        
        public override void OnDisable()
        {
            m_this.Dispose();
            incoming?.Dispose();
            baseline?.Dispose();
            Selection.selectionChanged -= OnSelectionChanged;
        }

        // when this tool is created, find the local state tester and serialize this object
        public override void OnEnable()
        {
            m_this = new SerializedObject(this);
            p_frame = m_this.FindProperty("frame");
            p_context = m_this.FindProperty("Context");
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            Debug.Log("openning " + Selection.activeObject);
            SetSampleDisplay(Selection.activeObject as ScriptableObject);
        }

        public void SetSampleDisplay(Object newObj)
        {
            _display = false;
            Context = null;
            if(newObj != null)
            {
                Context = newObj;
                DisplayFrameOfReplay((Context as IReplay).FailureFrame);  
            }
        }

        public void DisplayFrameOfReplay(int newFrame)
        {
            CurrentReplay = Context as IReplay;
            if(CurrentReplay != null)
            {
                newFrame = Mathf.Clamp(newFrame, 0, CurrentReplay.Duration);
                m_target?.Dispose();
                m_target = new SerializedObject(Context);
                var tempObj = CurrentReplay.Other as Object;
                incoming = m_target.FindProperty("States").GetArrayElementAtIndex(newFrame);
                baseline = new SerializedObject(tempObj).FindProperty("States").GetArrayElementAtIndex(newFrame);
                Debug.Log("found valid Replay", Context);

                _display = true;

                Debug.Log(incoming.name);
                foreach(SerializedProperty prop in incoming)
                {
                    Debug.Log("inc prop name" + prop.displayName);
                }
                Debug.Log(baseline.name);
                foreach(SerializedProperty prop in baseline)
                {
                    Debug.Log("base prop name" + prop.displayName);
                }
                DetermineMismatchedProperties();
                HashCompare();
            }
            
        }

        public void HashCompare()
        {
            try
            {
                baselineHash = baseline.FindPropertyRelative("MetaState").FindPropertyRelative("Hash").stringValue;
                incomingHash = incoming.FindPropertyRelative("MetaState").FindPropertyRelative("Hash").stringValue;
                hashFail = !baselineHash.Equals(incomingHash);
            }
            catch(NullReferenceException)
            {
                baselineHash = string.Empty;
                incomingHash = string.Empty;
                hashFail = false;
            }
        }

        private void DetermineMismatchedProperties()
        {
            m_this.Update();
            bool firstFound = false;    
            float currentHeight = 0;
            m_mismatchedProps = new HashSet<string>();
            m_mismatchedBaselineProps = new HashSet<string>();
            foreach(SerializedProperty property in incoming.Copy())
            {
                var baselineProperty = GetBaselineProperty(property);
                if(!SerializedProperty.DataEquals(property, baselineProperty))
                {
                    m_mismatchedProps.Add(property.propertyPath);
                    m_mismatchedBaselineProps.Add(baselineProperty.propertyPath);
                    if(!firstFound)
                    {
                        firstFound = true;
                        scrollPosition = new Vector2(0, currentHeight);
                    }
                }
                currentHeight += EditorGUI.GetPropertyHeight(property);
            }
            Debug.Log(m_mismatchedProps.Count + " found mismatched props");
        }

        // on gui draws the content of both states
        public override void OnGUI()
        {		
            m_this.Update();
            EditorGUILayout.BeginVertical("Box");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(p_context);
            if(EditorGUI.EndChangeCheck())
            {
                SetSampleDisplay(Context);
            }

            //if(advancedReplayControls)
            {
            //    FrameControlAdvanced();
            }
            //else
            {
                FrameControlSimple();
            }

            m_this.ApplyModifiedProperties();

            if(hashFail && GUILayout.Button("Print Contents"))
            {
                Debug.Log($"Printing tostring contents of each state\nbaseline:{baseline}\nactual{incoming}");
            }
            if(_display)
            {
                EditorGUILayout.LabelField($"Comparing frame {p_frame.longValue}", EditorStyles.largeLabel);
            }
            else
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.HelpBox($"Select a replay object for deep comparision", MessageType.Info);
                return;
            }
            if(m_mismatchedProps.Count > 0)
            {
                EditorGUILayout.HelpBox($"Detected {m_mismatchedProps.Count}(s) mismatched properties", MessageType.Warning);
            }
            desyncOnly = EditorGUILayout.Toggle("Hide Matches?", desyncOnly);
            displayEvents = EditorGUILayout.Toggle("Show Events?", displayEvents);
            if(displayEvents)
            {
                UpdatedNotification();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Baseline Game State",style: EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Hash: {baselineHash}" ,style: EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Actual Game State",style: EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Hash: {incomingHash}" ,style: EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if(_display)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);	
                
                var p1 = baseline.Copy();
                var p2 = incoming.Copy();
                
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical("Box");
                foreach(SerializedProperty property in p1)
                {
                    if(!desyncOnly || m_mismatchedBaselineProps.Contains(property.propertyPath))
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: false);
                    }
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical("Box");
                foreach(SerializedProperty property in p2)
                {
                    if(m_mismatchedProps.Contains(property.propertyPath))
                    {
                        GUI.backgroundColor = Color.red;
                        EditorGUILayout.PropertyField(property, includeChildren: false);
                    }
                    else if(!desyncOnly)
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: false);
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndVertical();



                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Select an object that can be viewed in the state comparer");
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // displays which side was updated
        public void UpdatedNotification()
        {
            if(!updatedSide[0].HasEvents && !updatedSide[1].HasEvents)
            {
                return;    
            }

            string message = "";
            if(updatedSide[0].HasEvents)
            {
                message = "Baseline ";
                if(!string.IsNullOrEmpty(updatedSide[0].EventData))
                {
                    EditorGUILayout.HelpBox($"Baseline Events: " + updatedSide[0].EventData, MessageType.Info);
                }
            }
            if(updatedSide[1].HasEvents)
            {
                message += "Incomming ";
                if(!string.IsNullOrEmpty(updatedSide[1].EventData))
                {
                    EditorGUILayout.HelpBox($"Incomming Events: " + updatedSide[1].EventData, MessageType.Info);
                }
            }
            if(updatedSide[0].WasUpdated)
            {
                EditorGUILayout.HelpBox("Baseline Was updated during simulation this frame", MessageType.Info);
            }
            if(updatedSide[1].WasUpdated)
            {
                EditorGUILayout.HelpBox("Incoming was updated during simulation this frame", MessageType.Info);
            }  
        }

        // simply the frame the player is locked too
        private void FrameControlSimple()
        {
            GUI.enabled = false;
            frame = EditorGUILayout.IntField("Frame", frame);
            GUI.enabled = true;
        }

        // give a more advanced control of scrubing through a replay
        private void FrameControlAdvanced()
        {
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            if(advancedReplayControls)
            
            if(GUILayout.Button("<<", style: EditorStyles.toolbarButton))
            {
                frame = CurrentReplay.PreviousEventIndex(frame);
            }
            
            if(GUILayout.Button("<", style: EditorStyles.toolbarButton))
            {
                frame--;
            }

            frame = EditorGUILayout.IntField("Frame", frame);

            if(GUILayout.Button(">", style: EditorStyles.toolbarButton))
            {
                frame++;
            }

            if(GUILayout.Button(">>", style: EditorStyles.toolbarButton))
            {
                frame = CurrentReplay.NextEventIndex(frame);
            }
            
            EditorGUILayout.EndHorizontal();
            if(EditorGUI.EndChangeCheck())
            {
                DisplayFrameOfReplay(frame);
            }
        }

        // takes a serialized property and finds the baseline equivalent property
        private SerializedProperty GetBaselineProperty(SerializedProperty incoming)
        {
            string baseLinePath = incoming.propertyPath;
            for(int i = 0; i < RELATIVEPROPERTYDEPTH; i++)
            {
                baseLinePath = baseLinePath.Substring(baseLinePath.IndexOf('.') + 1);
            }
            Debug.Assert(baseline.FindPropertyRelative(baseLinePath) != null, $"baseline property {baseLinePath} not found");
            return baseline.FindPropertyRelative(baseLinePath); 
        }

    }

    // non-generic helper class
    public static class SimulationStateComparerHelper
    {
        // create a menu button that opens this window
        [MenuItem("coherence/Replays/Replay Viewer")]
        public static void ShowWindow()
        {
            var window = EditorHelper.GetEditorWindow<DeepStateComparer>();
        }

        [RuntimeInitializeOnLoadMethod()]
        public static void SetListener()
        {
            //StateTester.SampleFailed += (sender, args) => PromptError(sender as StateTester);
        }

        // prompts the user to open the state tester
        private static void PromptError()
        {
            if(!EditorApplication.isPaused)
            {
                if(EditorUtility.DisplayDialog("State Desync Detected", "State tester detected that there is a desync in your multiplayer simulation?", "Open State Comparer", "Close"))
                {
                    ShowWindow();
                }
            }
        }
    }
}