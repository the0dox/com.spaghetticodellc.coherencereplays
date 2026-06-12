using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using Unity.Properties;

namespace SpaghettiCode.CoherenceReplays.Utilities
{
    // add to serialized reference objects to select their impelmentation via the custom property drawer
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SelectImplementation : PropertyAttribute
    {
        // the underlying type the property will attempt to populate the decorated field of, note there is not safety check so ensure you use the correct typing!
        public Type FieldType{get; private set;}
        public bool ImplementationOnly {get; private set;}

        // constructor must supply a type
        public SelectImplementation(Type underlyingType, bool implementationOnly = false)
        {
            
            FieldType = underlyingType;
            ImplementationOnly = implementationOnly;
        } 
    }

}