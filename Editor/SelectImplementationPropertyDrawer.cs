using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.Overlays;
using SpaghettiCode.CoherenceReplays.Utilities;

namespace SpaghettiCode.CoherenceReplays.Editor
{
    // code by Aleksander Trępała https://medium.com/@trepala.aleksander/serializereference-in-unity-b4ee10274f48
    // custom drawer that shows a series of implementations that can populate a field decorated by the select implementation attribute
    // user can select implementations in editor and edit values directly
    [CustomPropertyDrawer(typeof(SelectImplementation))]
    public class SelectImplementationPropertyDrawer : PropertyDrawer
    {
        // a temporary cache of types derived from select implementation attribute that the user can pick from
        private Type[] _implementations;
        // which type is currently selected by the user to popualte the object
        private int _implementationTypeIndex;
        // if the user requested to refresh types this frame
        private bool _refreshDirty;
        public float RectSeparation;

        // draws the property
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            RectSeparation = EditorGUIUtility.fieldWidth * 1.1f;
            Rect RefreshRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, RectSeparation, EditorGUIUtility.singleLineHeight);
            Rect SelectionRect = new Rect(RefreshRect.xMax, position.y, position.width - RefreshRect.xMax, EditorGUIUtility.singleLineHeight);
            Rect PropRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.yMax);
            
            // ensure cache exists
            if(_implementations == null || _refreshDirty)
            {
                _refreshDirty = false;
                Refresh();
            }
            
            if(IsBroken(property))
            {
                EditorGUI.LabelField(position, String.Format("Broken Managed Reference: [id:{0}] {1}", property.managedReferenceId, property.managedReferenceFullTypename));
                if(GUI.Button(SelectionRect, "Remove"))
                {
                    Reset(property);
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            // ensure the index is set to the current underlying type
            _implementationTypeIndex = GetCurrentTypeIndex(property);
            // create a dropdown of all items in the cache
            _implementationTypeIndex = EditorGUI.Popup(SelectionRect,
                _implementationTypeIndex, _implementations.Select(impl => impl.Name).ToArray());
            
            // if the dropdown was used this frame, change the underlying type to the new index
            if(EditorGUI.EndChangeCheck() || property.managedReferenceValue == null)
            {   
                property.managedReferenceValue = Activator.CreateInstance(_implementations[_implementationTypeIndex]);
                Debug.Log("managed ref value for index " + _implementationTypeIndex + ": " + property.managedReferenceValue.GetType());
            }

            // allow the user to manually refresh the cache if needed
            if(GUI.Button(RefreshRect, "Refresh"))
            {
                _refreshDirty = true;
            }
            if(!(attribute as SelectImplementation).ImplementationOnly)
            {
                EditorGUI.indentLevel = 1;
                EditorGUI.PropertyField(PropRect, property, true);
                EditorGUI.indentLevel = 0;
            }
            // if the underlying type is editable, allow editing here
            property.serializedObject.ApplyModifiedProperties();
        }

        // prevent unnecessary padding
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if(property.isExpanded)
            {
                return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
            else
            {
                return 2 * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        // refreshes the type cache using the attribute decorator
        public void Refresh()
        {
            _implementations = GetImplementations((attribute as SelectImplementation).FieldType)
                .Where(impl => !impl.IsSubclassOf(typeof(UnityEngine.Object))).ToArray();
        }

        // returns the current index the underlying object is actually set to
        private int GetCurrentTypeIndex(SerializedProperty property)
        {
            if(property.managedReferenceValue == null)
            {
                return 0;
            }
            for(int i = 0; i < _implementations.Length; i++)
            {
                if(_implementations[i].Equals(property.managedReferenceValue.GetType()) )
                {
                    //Debug.Log($"found type at index {i}");
                    return i;
                }
            }
            Debug.LogWarning("unable to find type, please refresh implementations");
            return 0;
        }

        // helper function that returns all types that implement interface type
        public static Type[] GetImplementations(Type interfaceType)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes());
            return types.Where(p => interfaceType.IsAssignableFrom(p) && !p.IsAbstract).ToArray();
        }

        public void Reset(SerializedProperty property)
        {
            Debug.Log(property.name + " reset");
            SerializationUtility.ClearManagedReferenceWithMissingType(property.serializedObject.targetObject, property.managedReferenceId);
            property.boxedValue = null;
            property.serializedObject.ApplyModifiedProperties();
        }

        public bool IsBroken(SerializedProperty property)
        {
            if(!SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
            {
                return false;   
            }
            foreach(var brokenType in SerializationUtility.GetManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
            {
                if(brokenType.referenceId == property.managedReferenceId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}