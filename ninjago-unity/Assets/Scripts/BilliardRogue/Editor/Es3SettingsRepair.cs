#nullable enable

using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Easy Save resolves saved types only from the assemblies listed in ES3Defaults; RunState lives in the
    /// Simulation assembly, so it must be listed (TDD D8). Idempotent.
    /// </summary>
    public static class Es3SettingsRepair
    {
        const string DefaultsPath = "Assets/Libraries/Easy Save 3/Resources/ES3/ES3Defaults.asset";
        const string SimulationAssembly = "Nex.BilliardRogue.Simulation";

        public static string Run()
        {
            var defaults = AssetDatabase.LoadAssetAtPath<ScriptableObject>(DefaultsPath);
            var so = new SerializedObject(defaults);
            var names = so.FindProperty("settings.assemblyNames") ?? so.FindProperty("assemblyNames");
            for (var i = 0; i < names.arraySize; i++)
            {
                if (names.GetArrayElementAtIndex(i).stringValue == SimulationAssembly)
                {
                    return $"[Es3SettingsRepair] {SimulationAssembly} already listed";
                }
            }

            names.InsertArrayElementAtIndex(names.arraySize);
            names.GetArrayElementAtIndex(names.arraySize - 1).stringValue = SimulationAssembly;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(defaults);
            AssetDatabase.SaveAssets();
            var message = $"[Es3SettingsRepair] added {SimulationAssembly} to ES3Defaults.assemblyNames";
            Debug.Log(message);
            return message;
        }
    }
}
