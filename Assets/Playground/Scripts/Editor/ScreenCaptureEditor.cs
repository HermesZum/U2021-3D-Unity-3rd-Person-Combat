using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Editor
{
    public class ScreenCaptureEditor : EditorWindow
    {
        private const string Directory = "Screenshots/Capture/";
        private static string _latestScreenshotPath = "";

        private GUIStyle _bigText;
        private bool _initDone;

        private void OnGUI()
        {
            if (!_initDone) InitStyles();

            GUILayout.Label("Screen Capture", _bigText);
            if (GUILayout.Button("Take a screenshot"))
            {
                TakeScreenshot();
                AssetDatabase.Refresh();
            }

            GUILayout.Label("Resolution: " + GetResolution());

            if (GUILayout.Button("Reveal in Explorer")) ShowFolder();
            GUILayout.Label("Directory: " + Directory);
        }

        private void InitStyles()
        {
            _initDone = true;
            _bigText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }

        [MenuItem("Tools/Screenshots/Open Window")]
        public static void ShowWindow()
        {
            GetWindow(typeof(ScreenCaptureEditor));
        }

        [MenuItem("Tools/Screenshots/Reveal in Explorer")]
        private static void ShowFolder()
        {
            if (File.Exists(_latestScreenshotPath))
            {
                EditorUtility.RevealInFinder(_latestScreenshotPath);
                return;
            }

            System.IO.Directory.CreateDirectory(Directory);
            EditorUtility.RevealInFinder(Directory);
        }

        [MenuItem("Tools/Screenshots/Take a Screenshot")]
        private static void TakeScreenshot()
        {
            System.IO.Directory.CreateDirectory(Directory);
            var currentTime = DateTime.Now;
            var filename = currentTime.ToString(CultureInfo.CurrentCulture).Replace('/', '-').Replace(':', '_') +
                           ".png";
            var path = Directory + filename;
            ScreenCapture.CaptureScreenshot(path);
            _latestScreenshotPath = path;
            Debug.Log($"Screenshot saved: <b>{path}</b> with resolution <b>{GetResolution()}</b>");
        }

        private static string GetResolution()
        {
            var size = Handles.GetMainGameViewSize();
            var sizeInt = new Vector2Int((int) size.x, (int) size.y);
            return $"{sizeInt.x.ToString()}x{sizeInt.y.ToString()}";
        }
    }
}