using U3Gear.Playground.Scripts.Utilities;
using UnityEditor;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Editor
{
    [CustomEditor(typeof(Screenshot))]
    public class ScreenshotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!GUILayout.Button("Take Screenshot")) return;
            ((Screenshot) serializedObject.targetObject).TakeScreenshot();
            AssetDatabase.Refresh();
        }
    }
}