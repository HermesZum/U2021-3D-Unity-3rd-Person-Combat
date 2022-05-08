using System;
using System.Collections.Generic;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Water
{
    [ExecuteInEditMode] // Make water live-update even when not in play mode
    public class Water : MonoBehaviour
    {
        public enum WaterMode
        {
            Simple = 0,
            Reflective = 1,
            Refractive = 2
        }

        private static bool _insideWater;


        public WaterMode waterMode = WaterMode.Refractive;
        public bool disablePixelLights = true;
        public int textureSize = 256;
        public float clipPlaneOffset = 0.07f;
        public LayerMask reflectLayers = -1;
        public LayerMask refractLayers = -1;
        private WaterMode _hardwareWaterSupport = WaterMode.Refractive;
        private int _oldReflectionTextureSize;
        private int _oldRefractionTextureSize;


        private readonly Dictionary<Camera, Camera>
            _reflectionCameras = new Dictionary<Camera, Camera>(); // Camera -> Camera table

        private RenderTexture _reflectionTexture;

        private readonly Dictionary<Camera, Camera>
            _refractionCameras = new Dictionary<Camera, Camera>(); // Camera -> Camera table

        private RenderTexture _refractionTexture;
        private static readonly int WaveSpeed = Shader.PropertyToID("WaveSpeed");
        private static readonly int WaveScale = Shader.PropertyToID("_WaveScale");
        private static readonly int WaveOffset = Shader.PropertyToID("_WaveOffset");
        private static readonly int WaveScale4 = Shader.PropertyToID("_WaveScale4");
        private static readonly int ReflectionTex = Shader.PropertyToID("_ReflectionTex");
        private static readonly int RefractionTex = Shader.PropertyToID("_RefractionTex");


        // This just sets up some matrices in the material; for really
        // old cards to make water texture scroll.
        private void Update()
        {
            if (!GetComponent<Renderer>()) return;
            var mat = GetComponent<Renderer>().sharedMaterial;
            if (!mat) return;

            var waveSpeed = mat.GetVector(WaveSpeed);
            var waveScale = mat.GetFloat(WaveScale);
            var waveScale4 = new Vector4(waveScale, waveScale, waveScale * 0.4f, waveScale * 0.45f);

            // Time since level load, and do intermediate calculations with doubles
            var t = Time.timeSinceLevelLoad / 20.0;
            var offsetClamped = new Vector4(
                (float) Math.IEEERemainder(waveSpeed.x * waveScale4.x * t, 1.0),
                (float) Math.IEEERemainder(waveSpeed.y * waveScale4.y * t, 1.0),
                (float) Math.IEEERemainder(waveSpeed.z * waveScale4.z * t, 1.0),
                (float) Math.IEEERemainder(waveSpeed.w * waveScale4.w * t, 1.0)
            );

            mat.SetVector(WaveOffset, offsetClamped);
            mat.SetVector(WaveScale4, waveScale4);
        }


        // Cleanup all the objects we possibly have created
        private void OnDisable()
        {
            if (_reflectionTexture)
            {
                DestroyImmediate(_reflectionTexture);
                _reflectionTexture = null;
            }

            if (_refractionTexture)
            {
                DestroyImmediate(_refractionTexture);
                _refractionTexture = null;
            }

            foreach (var kvp in _reflectionCameras) DestroyImmediate(kvp.Value.gameObject);
            _reflectionCameras.Clear();
            foreach (var kvp in _refractionCameras) DestroyImmediate(kvp.Value.gameObject);
            _refractionCameras.Clear();
        }


        // This is called when it's known that the object will be rendered by some
        // camera. We render reflections / refractions and do other updates here.
        // Because the script executes in edit mode, reflections for the scene view
        // camera will just work!
        public void OnWillRenderObject()
        {
            if (!enabled || !GetComponent<Renderer>() || !GetComponent<Renderer>().sharedMaterial ||
                !GetComponent<Renderer>().enabled)
                return;

            var cam = Camera.current;
            if (!cam) return;

            // Safeguard from recursive water reflections.
            if (_insideWater) return;
            _insideWater = true;

            // Actual water rendering mode depends on both the current setting AND
            // the hardware support. There's no point in rendering refraction textures
            // if they won't be visible in the end.
            _hardwareWaterSupport = FindHardwareWaterSupport();
            var mode = GetWaterMode();

            CreateWaterObjects(cam, out var reflectionCamera, out var refractionCamera);

            // find out the reflection plane: position and normal in world space
            var transform1 = transform;
            var pos = transform1.position;
            var normal = transform1.up;

            // Optionally disable pixel lights for reflection/refraction
            var oldPixelLightCount = QualitySettings.pixelLightCount;
            if (disablePixelLights) QualitySettings.pixelLightCount = 0;

            UpdateCameraModes(cam, reflectionCamera);
            UpdateCameraModes(cam, refractionCamera);

            RenderReflection(mode, normal, pos, cam, reflectionCamera);

            RenderRefraction(mode, refractionCamera, cam, pos, normal);

            // Restore pixel light count
            if (disablePixelLights) QualitySettings.pixelLightCount = oldPixelLightCount;

            // Setup shader keywords based on water mode
            switch (mode)
            {
                case WaterMode.Simple:
                    Shader.EnableKeyword("WATER_SIMPLE");
                    Shader.DisableKeyword("WATER_REFLECTIVE");
                    Shader.DisableKeyword("WATER_REFRACTIVE");
                    break;
                case WaterMode.Reflective:
                    Shader.DisableKeyword("WATER_SIMPLE");
                    Shader.EnableKeyword("WATER_REFLECTIVE");
                    Shader.DisableKeyword("WATER_REFRACTIVE");
                    break;
                case WaterMode.Refractive:
                    Shader.DisableKeyword("WATER_SIMPLE");
                    Shader.DisableKeyword("WATER_REFLECTIVE");
                    Shader.EnableKeyword("WATER_REFRACTIVE");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _insideWater = false;
        }

        private void RenderRefraction(WaterMode mode, Camera refractionCamera, Camera cam, Vector3 pos, Vector3 normal)
        {
            // Render refraction
            if (mode < WaterMode.Refractive) return;
            refractionCamera.worldToCameraMatrix = cam.worldToCameraMatrix;

            // Setup oblique projection matrix so that near plane is our reflection
            // plane. This way we clip everything below/above it for free.
            var clipPlane = CameraSpacePlane(refractionCamera, pos, normal, -1.0f);
            refractionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

            // Set custom culling matrix from the current camera
            refractionCamera.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;

            refractionCamera.cullingMask = ~(1 << 4) & refractLayers.value; // never render water layer
            refractionCamera.targetTexture = _refractionTexture;
            var transform2 = refractionCamera.transform;
            var transform3 = cam.transform;
            transform2.position = transform3.position;
            transform2.rotation = transform3.rotation;
            refractionCamera.Render();
            GetComponent<Renderer>().sharedMaterial.SetTexture(RefractionTex, _refractionTexture);
        }

        private void RenderReflection(WaterMode mode, Vector3 normal, Vector3 pos, Camera cam, Camera reflectionCamera)
        {
            // Render reflection if needed
            if (mode >= WaterMode.Reflective)
            {
                // Reflect camera around reflection plane
                var d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
                var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

                var reflection = Matrix4x4.zero;
                CalculateReflectionMatrix(ref reflection, reflectionPlane);
                var oldPos = cam.transform.position;
                var newPos = reflection.MultiplyPoint(oldPos);
                reflectionCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

                // Setup oblique projection matrix so that near plane is our reflection
                // plane. This way we clip everything below/above it for free.
                var clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
                reflectionCamera.projectionMatrix = cam.CalculateObliqueMatrix(clipPlane);

                // Set custom culling matrix from the current camera
                reflectionCamera.cullingMatrix = cam.projectionMatrix * cam.worldToCameraMatrix;

                reflectionCamera.cullingMask = ~(1 << 4) & reflectLayers.value; // never render water layer
                reflectionCamera.targetTexture = _reflectionTexture;
                var oldCulling = GL.invertCulling;
                GL.invertCulling = !oldCulling;
                var transform2 = reflectionCamera.transform;
                transform2.position = newPos;
                var euler = cam.transform.eulerAngles;
                transform2.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);
                reflectionCamera.Render();
                reflectionCamera.transform.position = oldPos;
                GL.invertCulling = oldCulling;
                GetComponent<Renderer>().sharedMaterial.SetTexture(ReflectionTex, _reflectionTexture);
            }
        }

        private static void UpdateCameraModes(Camera src, Camera dest)
        {
            if (dest == null) return;
            // set water camera to clear the same way as current camera
            dest.clearFlags = src.clearFlags;
            dest.backgroundColor = src.backgroundColor;
            if (src.clearFlags == CameraClearFlags.Skybox)
            {
                var sky = src.GetComponent<Skybox>();
                var mySky = dest.GetComponent<Skybox>();
                if (!sky || !sky.material)
                {
                    mySky.enabled = false;
                }
                else
                {
                    mySky.enabled = true;
                    mySky.material = sky.material;
                }
            }

            // update other values to match current camera.
            // even if we are supplying custom camera&projection matrices,
            // some of values are used elsewhere (e.g. skybox uses far plane)
            dest.farClipPlane = src.farClipPlane;
            dest.nearClipPlane = src.nearClipPlane;
            dest.orthographic = src.orthographic;
            dest.fieldOfView = src.fieldOfView;
            dest.aspect = src.aspect;
            dest.orthographicSize = src.orthographicSize;
        }


        // On-demand create any objects we need for water
        private void CreateWaterObjects(Camera currentCamera, out Camera reflectionCamera, out Camera refractionCamera)
        {
            var mode = GetWaterMode();

            reflectionCamera = null;
            refractionCamera = null;

            WaterModeReflective(currentCamera, ref reflectionCamera, mode);

            WaterModeRefractive(currentCamera, ref refractionCamera, mode);
        }

        private void WaterModeRefractive(Camera currentCamera, ref Camera refractionCamera, WaterMode mode)
        {
            if (mode < WaterMode.Refractive) return;
            {
                // Refraction render texture
                if (!_refractionTexture || _oldRefractionTextureSize != textureSize)
                {
                    if (_refractionTexture) DestroyImmediate(_refractionTexture);
                    _refractionTexture = new RenderTexture(textureSize, textureSize, 16)
                    {
                        name = "__WaterRefraction" + GetInstanceID(),
                        isPowerOfTwo = true,
                        hideFlags = HideFlags.DontSave
                    };
                    _oldRefractionTextureSize = textureSize;
                }

                // Camera for refraction
                _refractionCameras.TryGetValue(currentCamera, out refractionCamera);
                if (refractionCamera) return;
                var go =
                    new GameObject(
                        "Water Refr Camera id" + GetInstanceID() + " for " + currentCamera.GetInstanceID(),
                        typeof(Camera), typeof(Skybox));
                refractionCamera = go.GetComponent<Camera>();
                refractionCamera.enabled = false;
                var transform1 = refractionCamera.transform;
                var transform2 = transform;
                transform1.position = transform2.position;
                transform1.rotation = transform2.rotation;
                refractionCamera.gameObject.AddComponent<FlareLayer>();
                go.hideFlags = HideFlags.HideAndDontSave;
                _refractionCameras[currentCamera] = refractionCamera;
            }
        }

        private void WaterModeReflective(Camera currentCamera, ref Camera reflectionCamera, WaterMode mode)
        {
            if (mode >= WaterMode.Reflective)
            {
                // Reflection render texture
                if (!_reflectionTexture || _oldReflectionTextureSize != textureSize)
                {
                    if (_reflectionTexture) DestroyImmediate(_reflectionTexture);
                    _reflectionTexture = new RenderTexture(textureSize, textureSize, 16);
                    _reflectionTexture.name = "__WaterReflection" + GetInstanceID();
                    _reflectionTexture.isPowerOfTwo = true;
                    _reflectionTexture.hideFlags = HideFlags.DontSave;
                    _oldReflectionTextureSize = textureSize;
                }

                // Camera for reflection
                _reflectionCameras.TryGetValue(currentCamera, out reflectionCamera);
                if (reflectionCamera) return;
                var go = new GameObject(
                    "Water Refl Camera id" + GetInstanceID() + " for " + currentCamera.GetInstanceID(),
                    typeof(Camera), typeof(Skybox));
                reflectionCamera = go.GetComponent<Camera>();
                reflectionCamera.enabled = false;
                var transform1 = reflectionCamera.transform;
                var transform2 = transform;
                transform1.position = transform2.position;
                transform1.rotation = transform2.rotation;
                reflectionCamera.gameObject.AddComponent<FlareLayer>();
                go.hideFlags = HideFlags.HideAndDontSave;
                _reflectionCameras[currentCamera] = reflectionCamera;
            }
        }

        private WaterMode GetWaterMode()
        {
            return _hardwareWaterSupport < waterMode ? _hardwareWaterSupport : waterMode;
        }

        private WaterMode FindHardwareWaterSupport()
        {
            if (!GetComponent<Renderer>()) return WaterMode.Simple;

            var mat = GetComponent<Renderer>().sharedMaterial;
            if (!mat) return WaterMode.Simple;

            var mode = mat.GetTag("WATERMODE", false);
            return mode switch
            {
                "Refractive" => WaterMode.Refractive,
                "Reflective" => WaterMode.Reflective,
                _ => WaterMode.Simple
            };
        }

        // Given position/normal of the plane, calculates plane in camera space.
        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offsetPos = pos + normal * clipPlaneOffset;
            var m = cam.worldToCameraMatrix;
            var cPos = m.MultiplyPoint(offsetPos);
            var cNormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
        }

        // Calculates reflection matrix around the given plane
        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = 1F - 2F * plane[0] * plane[0];
            reflectionMat.m01 = -2F * plane[0] * plane[1];
            reflectionMat.m02 = -2F * plane[0] * plane[2];
            reflectionMat.m03 = -2F * plane[3] * plane[0];

            reflectionMat.m10 = -2F * plane[1] * plane[0];
            reflectionMat.m11 = 1F - 2F * plane[1] * plane[1];
            reflectionMat.m12 = -2F * plane[1] * plane[2];
            reflectionMat.m13 = -2F * plane[3] * plane[1];

            reflectionMat.m20 = -2F * plane[2] * plane[0];
            reflectionMat.m21 = -2F * plane[2] * plane[1];
            reflectionMat.m22 = 1F - 2F * plane[2] * plane[2];
            reflectionMat.m23 = -2F * plane[3] * plane[2];

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }
    }
}