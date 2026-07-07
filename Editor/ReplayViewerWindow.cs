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
using System;
using Object = UnityEngine.Object;
using UnityEditor.Callbacks;

// Custom tool used for displaying all elements of a simulation game state
// compares the left hand state (p_incoming) with the right hand state (baseline)
// if p_incoming does not equal the p_expected, the specific fields that do not match are highlighted red
namespace  SpaghettiCode.CoherenceReplays.Editor
{
    public class ReplayViewerWindow : CustomToolEditor, IReplayHarness
    {
        // big limitation is not knowing the concrete type of the user generated scriptable object,
        // thus we track it at the lowest possible level as a unity.engine object
        public Object ActiveSelection;

        // public accessors
        public event EventHandler<ReplayPlayedEvent> Updated {add => m_updated += value;  remove => m_updated -= value;}
        public IReplay CurrentReplay => m_currentReplay;

        // holds onto serialized properties for display in the editor
        // reference to self to display 
        private SerializedObject m_this;
        // serialized version of the currently inspected replay
        private SerializedObject m_serializedReplay;
        private SerializedProperty p_frame;	
        public SerializedProperty p_incoming;
        public SerializedProperty p_expected;
        public SerializedProperty p_activeSelection;
        
        // current frame being viewed
        public int m_frame;
        // if true, can display main body of the gui
        private bool _display;
        // the simple hash of incoming used to determine if there is a desync if not = to baseline
        private string m_incomingHash;
        // the simple hash of baseline used to determine if there is a desync if not = to incoming
        private string m_expectedHash;
        // what replay am I currently viewing?
        private IReplay m_currentReplay;
        // collects all mismached props between baseline and incoming
        private HashSet<string> m_mismatchedProps;
        // shows which props to display when the user says "show mismatches only"
        private HashSet<string> m_mismatchedBaselineProps;
        // tracks scroll view position of window
        Vector2 scrollPosition = Vector2.zero;
        // if true display a hash failure
        private FailureState m_failureState;
        // if true, only display mismatched properties
        public bool desyncOnly = true;
        // if true, show meta data events
        public bool displayEvents = true;
        // if true allow deeper frame viewing with scrubbing etc.
        bool advancedReplayControls;
        // the replay meta states, what to show independent of the replay inital state
        private ReplayMetaState[] updatedSide = new ReplayMetaState[2];

        // used to trim down property paths when comparing p_expected to incomming state. We only care about seralizied properties deeper than:
        // Data.States.Array[].InitalState. so we trim the first 3 '.' dots in the structure
        private static readonly int RELATIVEPROPERTYDEPTH = 4;
        // event for when this replay is updated. Other replay related objects can listen to this to get sync with the current frame
        private EventHandler<ReplayPlayedEvent> m_updated;

        // can be set to another replay player to match its display
        private IReplayHarness m_otherPlayer;
        [Flags]
        private enum FailureState {None, HashFailure = 1 << 1, MismatchedProps = 1 << 2, FullDesync = HashFailure | MismatchedProps}

        // clean on disable
        public override void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            m_this.Dispose();
            p_incoming?.Dispose();
            p_expected?.Dispose();
            m_serializedReplay?.Dispose();
            AttachToHarness(null);
        }

        // when this tool is created, subscribe to editor selection, and create seralized objects
        public override void OnEnable()
        {
            m_this = new SerializedObject(this);
            p_frame = m_this.FindProperty("m_frame");
            p_activeSelection = m_this.FindProperty("ActiveSelection");
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }    


        // called whenever a selection is changed in editor, checks to see if the selected object is a replay, and then opens it
        private void OnSelectionChanged()
        {
            SetSampleDisplay(Selection.activeObject);
            Repaint();
        }


        // takes a unity object as an argument, checks to see if it can be displayed as a replay
        public void SetSampleDisplay(Object newObj)
        {
            _display = false;
            ActiveSelection = newObj;
            m_currentReplay = ActiveSelection as IReplay;
            if(m_currentReplay != null)
            {
                DisplayFrameOfReplay(m_currentReplay.FailureFrame);  
            }
            AttachToHarness(ActiveSelection as IReplayHarness);
        }

        // attaches to another harness, updating display if the other harness updates its representation
        public void AttachToHarness(IReplayHarness other)
        {
            if(m_otherPlayer != null)
            {
                m_otherPlayer.Updated -= OnReplayUpdated;
            }
            m_otherPlayer = other;
            if(m_otherPlayer != null)
            {
                m_otherPlayer.Updated += OnReplayUpdated;
            }
        }


        // called whenever the other harness updates its representation, matches the represetnation on this editor
        private void OnReplayUpdated(object sender, ReplayPlayedEvent e)
        {
            if(m_currentReplay != e.CurrentReplay)
            {
                m_currentReplay = e.CurrentReplay;
            }
            DisplayFrameOfReplay(e.Frame);
        }

        // displays a frame of the currently selected simulation frame
        public void DisplayFrameOfReplay(int newFrame)
        {
            if(m_currentReplay != null)
            {
                m_frame = Mathf.Clamp(newFrame, 0, m_currentReplay.Duration - 1);
                m_serializedReplay?.Dispose();
                m_serializedReplay = new SerializedObject(ActiveSelection);
                var tempObj = m_currentReplay.Other as Object;
                SerializedProperty incommingReplayFrame = m_serializedReplay.FindProperty("States").GetArrayElementAtIndex(m_frame);
                SerializedProperty expectedReplayFrame = new SerializedObject(tempObj).FindProperty("States").GetArrayElementAtIndex(m_frame);
                p_incoming = incommingReplayFrame.FindPropertyRelative("InitalState");
                p_expected = expectedReplayFrame.FindPropertyRelative("InitalState");
                updatedSide[0] = (ReplayMetaState)incommingReplayFrame.FindPropertyRelative("MetaState").boxedValue;
                updatedSide[1] = (ReplayMetaState)expectedReplayFrame.FindPropertyRelative("MetaState").boxedValue;
                Debug.Log("found valid Replay", ActiveSelection);
                _display = true;
                DetermineMismatchedProperties();
                DetermineFailureState();
                m_this.Update();
                m_updated?.Invoke(this, new ReplayPlayedEvent(){Frame = m_frame, CurrentReplay = m_currentReplay});
            }
        }

        // finds the hash and compares them between the both
        public void DetermineFailureState()
        {
            m_failureState = FailureState.None;
            m_incomingHash = updatedSide[0].Hash;
            m_expectedHash = updatedSide[1].Hash;
            if(!m_expectedHash.Equals(m_incomingHash))
            {
                m_failureState |= FailureState.HashFailure;
            }
            if(m_mismatchedProps.Count > 0)
            {
                m_failureState |= FailureState.MismatchedProps;
            }
            switch(m_failureState)
            {
                case FailureState.HashFailure:
                    Debug.LogWarning($"Hash Failure Detected, but no serialized properties were mismatched. Is your game state object serialized correctly?", ActiveSelection);
                break;  
                case FailureState.MismatchedProps:
                    Debug.LogWarning($"Detected {m_mismatchedProps.Count}(s) mismatched properties, But hashes don't report failure, did you set up your ComputeHash Function Properly?", ActiveSelection);
                break;  
            }
        }

        // compares the baseline and incoming state by each property marking which props are mismatched
        private void DetermineMismatchedProperties()
        {
            m_this.Update();
            bool firstFound = false;    
            float currentHeight = 0;
            m_mismatchedProps = new HashSet<string>();
            m_mismatchedBaselineProps = new HashSet<string>();
            foreach(SerializedProperty property in p_incoming.Copy())
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

        // takes a serialized property and finds the p_expected equivalent property
        private SerializedProperty GetBaselineProperty(SerializedProperty p_incoming)
        {
            string baseLinePath = p_incoming.propertyPath;
            for(int i = 0; i < RELATIVEPROPERTYDEPTH; i++)
            {
                baseLinePath = baseLinePath.Substring(baseLinePath.IndexOf('.') + 1);
            }
            Debug.Assert(p_expected.FindPropertyRelative(baseLinePath) != null, $"p_expected property {baseLinePath} not found");
            return p_expected.FindPropertyRelative(baseLinePath); 
        }


        // on gui draws the content of both states
        public override void OnGUI()
        {		
            m_this.Update();
            EditorGUILayout.BeginVertical("Box");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(p_activeSelection);
            if(EditorGUI.EndChangeCheck())
            {
                SetSampleDisplay(ActiveSelection);
            }

            EditorGUI.BeginChangeCheck();
            //if(advancedReplayControls)
            {
            //    FrameControlAdvanced();
            }
            //else
            {
                FrameControlSimple();
            }
            if(EditorGUI.EndChangeCheck())
            {
                DisplayFrameOfReplay(m_frame);
            }

            m_this.ApplyModifiedProperties();

            if(_display)
            {
                EditorGUILayout.LabelField($"Comparing frame #{p_frame.longValue} [{updatedSide[0].GameFrame}]", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.HelpBox($"Select a replay object for deep comparision", MessageType.Info);
                return;
            }
            switch(m_failureState)
            {
                case FailureState.HashFailure:
                    EditorGUILayout.HelpBox($"Desync detected", MessageType.Error);
                    EditorGUILayout.HelpBox($"Hash Failure Detected, but no serialized properties were mismatched. Is your game state object serialized correctly?", MessageType.Warning);
                break;  
                case FailureState.MismatchedProps:
                    EditorGUILayout.HelpBox($"Desync detected with {m_mismatchedProps.Count}(s) mismatched properties", MessageType.Error);
                    EditorGUILayout.HelpBox($"Detected {m_mismatchedProps.Count}(s) mismatched properties, But hashes don't report failure, did you set up your ComputeHash Function Properly?", MessageType.Warning);
                break;  
                case FailureState.FullDesync:
                    EditorGUILayout.HelpBox($"Desync detected with {m_mismatchedProps.Count}(s) mismatched properties", MessageType.Error);
                break;  
            }
            desyncOnly = EditorGUILayout.Toggle("Hide Matches?", desyncOnly);
            displayEvents = EditorGUILayout.Toggle("Show Events?", displayEvents);
            if(displayEvents)
            {
                UpdatedNotification();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField($"Actual Game State ({m_currentReplay.name})",style: EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Hash: {m_incomingHash}" ,style: EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField($"Expected Game State ({m_currentReplay.Other.name})",style: EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Hash: {m_expectedHash}" ,style: EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if(_display)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);	
                
                var actual = p_incoming.Copy();
                var expected = p_expected.Copy();
                
                EditorGUILayout.BeginHorizontal();
                
                EditorGUILayout.BeginVertical("Box");
                foreach(SerializedProperty property in actual)
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

                EditorGUILayout.BeginVertical("Box");
                foreach(SerializedProperty property in expected)
                {
                    if(!desyncOnly || m_mismatchedBaselineProps.Contains(property.propertyPath))
                    {
                        EditorGUILayout.PropertyField(property, includeChildren: false);
                    }
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
                    EditorGUILayout.HelpBox($"Actual Events: " + updatedSide[0].EventData, MessageType.Info);
                }
            }
            if(updatedSide[1].HasEvents)
            {
                message += "Incomming ";
                if(!string.IsNullOrEmpty(updatedSide[1].EventData))
                {
                    EditorGUILayout.HelpBox($"Expected Events: " + updatedSide[1].EventData, MessageType.Info);
                }
            }
            if(updatedSide[0].WasUpdated)
            {
                EditorGUILayout.HelpBox("Actual Was updated during simulation this frame", MessageType.Info);
            }
            if(updatedSide[1].WasUpdated)
            {
                EditorGUILayout.HelpBox("Expected was updated during simulation this frame", MessageType.Info);
            }  
        }

        // simple gui that allows the user to manipulate the viewed frame
        private void FrameControlSimple()
        {
            m_frame = EditorGUILayout.IntField("Frame", m_frame);
        }

        // give a more advanced control of scrubing through a replay
        private void FrameControlAdvanced()
        {
            EditorGUILayout.BeginHorizontal();

            if(advancedReplayControls)
            
            if(GUILayout.Button("<<", style: EditorStyles.toolbarButton))
            {
                m_frame = m_currentReplay.PreviousEventIndex(m_frame);
            }
            
            if(GUILayout.Button("<", style: EditorStyles.toolbarButton))
            {
                m_frame--;
            }

            m_frame = EditorGUILayout.IntField("Frame", m_frame);

            if(GUILayout.Button(">", style: EditorStyles.toolbarButton))
            {
                m_frame++;
            }

            if(GUILayout.Button(">>", style: EditorStyles.toolbarButton))
            {
                m_frame = m_currentReplay.NextEventIndex(m_frame);
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }

    // non-generic helper class
    public static class SimulationStateComparerHelper
    {
        // create a menu button that opens this window
        [MenuItem("coherence/Replays/Replay Viewer")]
        public static ReplayViewerWindow ShowWindow()
        {
            return EditorHelper.GetEditorWindow<ReplayViewerWindow>();
        }

        [RuntimeInitializeOnLoadMethod()]
        public static void SetListener()
        {
            //StateTester.SampleFailed += (sender, args) => PromptError(sender as StateTester);
        }

        // whenever a scriptable object is 'opened' check to see if it is a replay, if so open the state viewer
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            // Convert the generic instance ID back to a Unity Object
            var targetObject = EditorUtility.EntityIdToObject(instanceID);

            // Check if the opened asset matches your ScriptableObject type
            if (targetObject is IReplay customData)
            {
                // Execute your custom logic here
                Debug.Log($"Successfully intercepted 'Open' for: {customData.name}");
                
                // Example custom action: Open a specific custom Editor Window
                ShowWindow()?.SetSampleDisplay(targetObject);

                // Return true to tell Unity you handled the opening behavior
                return true; 
            }

            // Return false to let Unity fall back to its default asset opening behavior
            return false;
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