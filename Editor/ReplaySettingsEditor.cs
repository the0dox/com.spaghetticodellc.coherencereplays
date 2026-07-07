using System.Collections.Generic;
using SpaghettiCode.CoherenceReplays.Runtime;
using UnityEditor;

namespace SpaghettiCode.CoherenceReplays.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEditor.Search;
    using UnityEngine;

    class MyCustomSettingsProvider : SettingsProvider
    {
        private static Editor s_CachedEditor;

        // Defines the menu location inside Edit > Project Settings
        public MyCustomSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) 
            : base(path, scope) { }

        // Creates the embedded inspector viewer when the user opens this specific menu
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            ReplaySettings settings = ReplaySettings.instance;
            if (settings != null)
            {
                s_CachedEditor = Editor.CreateEditor(settings);
            }
        }

        // Draws the ScriptableObject configuration fields directly into the settings layout
        public override void OnGUI(string searchContext)
        {
            if (s_CachedEditor != null)
            {
                // Optional styling layout matching official Unity presentation 
                EditorGUILayout.Space(10);
                
                // Updates internal data variables and prints fields to the screen
                s_CachedEditor.serializedObject.Update();
                s_CachedEditor.OnInspectorGUI();
                s_CachedEditor.serializedObject.ApplyModifiedProperties();

                if(GUILayout.Button("Select Simulation Class"))
                {        
                    var context = SearchService.CreateContext("t:monoscript");
                    SearchService.ShowPicker(context, selectHandler: (a,b) => {
                        var type = GetTypeFromFile(a);
                        ReplaySettings.instance.StateClass = type.Name;
                        ReplaySettings.instance.UserAssembly = type.Assembly.GetName().Name;
                        });
                }

                if(GUILayout.Button("Select Replay Class"))
                {        
                    var context = SearchService.CreateContext("t:monoscript");
                    SearchService.ShowPicker(context, selectHandler: (a,b) => {
                        ReplaySettings.instance.ReplayClass = GetTypeFromFile(a).Name;
                        });
                }
            }
        }

        public static Type GetTypeFromFile(SearchItem e)
        {
            MonoScript monoScript =  (MonoScript)e.ToObject();
            return monoScript.GetClass();
        }

        public override void OnDeactivate()
        {
            ReplaySettings.instance.OnInspectorClosed();
            base.OnDeactivate();
        }

        // Registers the customized settings drawer loop into the global Unity registry
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new MyCustomSettingsProvider("Project/coherenceReplays", SettingsScope.Project);

            // Sets up standard string matching terms for the global settings search bar
            provider.keywords = new string[] { "Replays", "Extensions", "Json", "Debugging", "Coherence" };
            return provider;
        }
    }

}