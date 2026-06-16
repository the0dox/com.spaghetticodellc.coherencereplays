/**
 * @file CustomEditor.cs
 * @description 
 * @author Alexander Theodore
 * @copyright 2024 All Rights Reserved.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using System;

namespace  SpaghettiCode.CoherenceReplays.Editor
{
    // low level class for custom editor windows
    public abstract class CustomToolEditor : EditorWindow
    {
        public abstract void OnGUI();
        public abstract void OnEnable();
        public abstract void OnDisable();

        public virtual void DrawBlockGUI(string label, SerializedProperty property)
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(label);
            EditorGUILayout.PropertyField(property);
            EditorGUILayout.EndHorizontal();
        }
    }

    public static class EditorHelper
    {
        public static TEditorType GetEditorWindow<TEditorType>() where TEditorType : CustomToolEditor
        {
            TEditorType newWindow = (TEditorType)GetEditorWindow(typeof(TEditorType));
            return newWindow;
        }
        
        public static CustomToolEditor GetEditorWindow(Type editorType)
        {
            var newWindow = EditorWindow.GetWindow(editorType) as CustomToolEditor;
            newWindow.ShowPopup();
            return newWindow;
        }
    }
}
