using System;
using System.IO;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Utilities
{
    [RequireComponent(typeof(Camera))]
    public class Screenshot : MonoBehaviour
    {
        [SerializeField] private int width = 256;
        [SerializeField] private int height = 256;
        [SerializeField] private string folder = "Screenshots";
        [SerializeField] private string filenamePrefix = "icon";
        [SerializeField] private bool ensureTransparentBackground;

        [ContextMenu("Take Screenshot")]
        public void TakeScreenshot()
        {
            folder = GetSafePath(folder.Trim('/'));
            filenamePrefix = GetSafeFilename(filenamePrefix);

            var dir = Application.dataPath + "/" + folder + "/";
            // ReSharper disable once StringLiteralTypo
            var filename = filenamePrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            var path = dir + filename;

            var cam = GetComponent<Camera>();

            // Create Render Texture with width and height.
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.DefaultHDR);

            // Assign Render Texture to camera.
            cam.targetTexture = rt;

            // save current background settings of the camera
            var clearFlags = cam.clearFlags;
            var backgroundColor = cam.backgroundColor;

            // make the background transparent when enabled
            if (ensureTransparentBackground)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(); // alpha is zero
            }

            // Render the camera's view to the Target Texture.
            cam.Render();

            // restore the camera's background settings if they were changed before rendering
            if (ensureTransparentBackground)
            {
                cam.clearFlags = clearFlags;
                cam.backgroundColor = backgroundColor;
            }

            // Save the currently active Render Texture so we can override it.
            var currentRT = RenderTexture.active;

            // ReadPixels reads from the active Render Texture.
            RenderTexture.active = cam.targetTexture;

            // Make a new texture and read the active Render Texture into it.
            var screenshot = new Texture2D(width, height, TextureFormat.ARGB32, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);

            // PNGs should be sRGB so convert to sRGB color space when rendering in linear.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var pixels = screenshot.GetPixels();
                for (var p = 0; p < pixels.Length; p++) pixels[p] = pixels[p].gamma;
                screenshot.SetPixels(pixels);
            }

            // Apply the changes to the screenshot texture.
            screenshot.Apply(false);

            // Save the screenshot.
            Directory.CreateDirectory(dir);
            var png = screenshot.EncodeToPNG();
            File.WriteAllBytes(path, png);

            // Remove the reference to the Target Texture so our Render Texture is garbage collected.
            cam.targetTexture = null;

            // Replace the original active Render Texture.
            RenderTexture.active = currentRT;

            Debug.Log("Screenshot saved to:\n" + path);
        }

        private static string GetSafePath(string path)
        {
            return string.Join("_", path.Split(Path.GetInvalidPathChars()));
        }

        private static string GetSafeFilename(string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}