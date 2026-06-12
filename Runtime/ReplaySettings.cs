using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

namespace SpaghettiCode.CoherenceReplays.Runtime
{
    // singleton for saving user preferences in with coherence replays
    // required allowing the state debugger to find the correct game state
    public class ReplaySettings : ScriptableSingleton<ReplaySettings>
    {
        // the assembly where user definied types reside
        public string UserAssembly;
        // the name of the game state created by the user 
        public string StateClass;
        public string ReplayClass;
        
        // returns the user generated game state type for replay tools
        public Type GetStateType()
        {
            var assembly = Assembly.Load(GetAssemblyString());
            Type output = assembly.GetType(StateClass, true);
            if(output == null)
            {
                Debug.LogError($"unable to generate type from stateclass parameter: {StateClass}");
            }
            return output;
        }

        public Type GetReplayType()
        {
            var assembly = Assembly.Load(GetAssemblyString());
            Type output = assembly.GetType(ReplayClass, true);
            if(output == null)
            {
                Debug.LogError($"unable to generate type from replay parameter: {ReplayClass}");
            }
            return output;
        }

        // returns the user definied assembly where the game state resides, or default if not provided
        private string GetAssemblyString()
        {
            return String.IsNullOrEmpty(UserAssembly) ? "Assembly-CSharp" : UserAssembly;
        }
    }
}
