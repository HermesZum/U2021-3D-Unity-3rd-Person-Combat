using System.Collections.Generic;
using UnityEngine;

namespace U3Gear.Playground.Scripts.Water
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(WaterBase))]
    public class PlanarReflection : MonoBehaviour
    {
        public LayerMask reflectionMask;
        public bool reflectSkybox;
        public Color clearColor = Color.grey;
        public string reflectionSampler = "_ReflectionTex";
        public float clipPlaneOffset = 0.07F;
        private Dictionary<Camera, bool> _helperCameras;


        private Vector3 _oldPos;
        private Camera _reflectionCamera;
        private Material _sharedMaterial;


        public void Start()
        {
            _sharedMaterial = ((WaterBase) gameObject.GetComponent(typeof(WaterBase))).sharedMaterial;
        }


        public void LateUpdate()
        {
            _helperCameras?.Clear();
        }


        public void OnEnable()
        {
            Shader.EnableKeyword("WATER_REFLECTIVE");
            Shader.DisableKeyword("WATER_SIMPLE");
        }


        public void OnDisable()
        {
            Shader.EnableKeyword("WATER_SIMPLE");
            Shader.DisableKeyword("WATER_REFLECTIVE");
        }


        private Camera CreateReflectionCameraFor(Camera cam)
        {
            var reflectionName = gameObject.name + "Reflection" + cam.name;
            var go = GameObject.Find(reflectionName);

            if (!go) go = new GameObject(reflectionName, typeof(Camera));
            if (!go.GetComponent(typeof(Camera))) go.AddComponent(typeof(Camera));
            var reflectCamera = go.GetComponent<Camera>();

            reflectCamera.backgroundColor = clearColor;
            reflectCamera.clearFlags = reflectSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

            SetStandardCameraParameter(reflectCamera, reflectionMask);

            if (!reflectCamera.targetTexture) reflectCamera.targetTexture = CreateTextureFor(cam);

            return reflectCamera;
        }


        private static void SetStandardCameraParameter(Camera cam, LayerMask mask)
        {
            cam.cullingMask = mask & ~(1 << LayerMask.NameToLayer("Water"));
            cam.backgroundColor = Color.black;
            cam.enabled = false;
        }


        private static RenderTexture CreateTextureFor(Camera cam)
        {
            var rt = new RenderTexture(Mathf.FloorToInt(cam.pixelWidth * 0.5F),
                Mathf.FloorToInt(cam.pixelHeight * 0.5F), 24)
            {
                hideFlags = HideFlags.DontSave
            };
            return rt;
        }


        private void RenderHelpCameras(Camera currentCam)
        {
            _helperCameras ??= new Dictionary<Camera, bool>();

            if (!_helperCameras.ContainsKey(currentCam)) _helperCameras.Add(currentCam, false);
            if (_helperCameras[currentCam]) return;

            if (!_reflectionCamera) _reflectionCamera = CreateReflectionCameraFor(currentCam);

            RenderReflectionFor(currentCam, _reflectionCamera);

            _helperCameras[currentCam] = true;
        }


        public void WaterTileBeingRendered(Transform tr, Camera currentCam)
        {
            RenderHelpCameras(currentCam);

            if (_reflectionCamera && _sharedMaterial)
                _sharedMaterial.SetTexture(reflectionSampler, _reflectionCamera.targetTexture);
        }


        private void RenderReflectionFor(Camera cam, Camera reflectCamera)
        {
            if (!reflectCamera) return;

            if (_sharedMaterial && !_sharedMaterial.HasProperty(reflectionSampler)) return;

            reflectCamera.cullingMask = reflectionMask & ~(1 << LayerMask.NameToLayer("Water"));

            SaneCameraSettings(reflectCamera);

            reflectCamera.backgroundColor = clearColor;
            reflectCamera.clearFlags = reflectSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (reflectSkybox)
                if (cam.gameObject.GetComponent(typeof(Skybox)))
                {
                    var sb = (Skybox) reflectCamera.gameObject.GetComponent(typeof(Skybox));
                    if (!sb) sb = (Skybox) reflectCamera.gameObject.AddComponent(typeof(Skybox));
                    sb.material = ((Skybox) cam.GetComponent(typeof(Skybox))).material;
                }

            GL.invertCulling = true;

            var reflectiveSurface = transform; //waterHeight;

            var transform2 = cam.transform;
            var eulerA = transform2.eulerAngles;

            var transform1 = reflectCamera.transform;
            transform1.eulerAngles = new Vector3(-eulerA.x, eulerA.y, eulerA.z);
            transform1.position = transform2.position;

            var transform3 = reflectiveSurface.transform;
            var pos = transform3.position;
            pos.y = reflectiveSurface.position.y;
            var normal = transform3.up;
            var d = -Vector3.Dot(normal, pos) - clipPlaneOffset;
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            var reflection = Matrix4x4.zero;
            reflection = CalculateReflectionMatrix(reflection, reflectionPlane);
            _oldPos = cam.transform.position;
            var newPos = reflection.MultiplyPoint(_oldPos);

            reflectCamera.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

            var clipPlane = CameraSpacePlane(reflectCamera, pos, normal, 1.0f);

            var projection = cam.projectionMatrix;
            projection = CalculateObliqueMatrix(projection, clipPlane);
            reflectCamera.projectionMatrix = projection;

            var transform4 = reflectCamera.transform;
            transform4.position = newPos;
            var euler = cam.transform.eulerAngles;
            transform4.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);

            reflectCamera.Render();

            GL.invertCulling = false;
        }


        private static void SaneCameraSettings(Camera helperCam)
        {
            helperCam.depthTextureMode = DepthTextureMode.None;
            helperCam.backgroundColor = Color.black;
            helperCam.clearFlags = CameraClearFlags.SolidColor;
            helperCam.renderingPath = RenderingPath.Forward;
        }


        private static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane)
        {
            var q = projection.inverse * new Vector4(
                Sgn(clipPlane.x),
                Sgn(clipPlane.y),
                1.0F,
                1.0F
            );
            var c = clipPlane * (2.0F / Vector4.Dot(clipPlane, q));
            // third row = clip plane - fourth row
            projection[2] = c.x - projection[3];
            projection[6] = c.y - projection[7];
            projection[10] = c.z - projection[11];
            projection[14] = c.w - projection[15];

            return projection;
        }


        private static Matrix4x4 CalculateReflectionMatrix(Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = 1.0F - 2.0F * plane[0] * plane[0];
            reflectionMat.m01 = -2.0F * plane[0] * plane[1];
            reflectionMat.m02 = -2.0F * plane[0] * plane[2];
            reflectionMat.m03 = -2.0F * plane[3] * plane[0];

            reflectionMat.m10 = -2.0F * plane[1] * plane[0];
            reflectionMat.m11 = 1.0F - 2.0F * plane[1] * plane[1];
            reflectionMat.m12 = -2.0F * plane[1] * plane[2];
            reflectionMat.m13 = -2.0F * plane[3] * plane[1];

            reflectionMat.m20 = -2.0F * plane[2] * plane[0];
            reflectionMat.m21 = -2.0F * plane[2] * plane[1];
            reflectionMat.m22 = 1.0F - 2.0F * plane[2] * plane[2];
            reflectionMat.m23 = -2.0F * plane[3] * plane[2];

            reflectionMat.m30 = 0.0F;
            reflectionMat.m31 = 0.0F;
            reflectionMat.m32 = 0.0F;
            reflectionMat.m33 = 1.0F;

            return reflectionMat;
        }


        private static float Sgn(float a)
        {
            if (a > 0.0F) return 1.0F;
            if (a < 0.0F) return -1.0F;
            return 0.0F;
        }


        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offsetPos = pos + normal * clipPlaneOffset;
            var m = cam.worldToCameraMatrix;
            var cPos = m.MultiplyPoint(offsetPos);
            var cNormal = m.MultiplyVector(normal).normalized * sideSign;

            return new Vector4(cNormal.x, cNormal.y, cNormal.z, -Vector3.Dot(cPos, cNormal));
        }
    }
}