using UnityEngine;

namespace U3Gear.Playground.Scripts.Water
{
    [RequireComponent(typeof(WaterBase))]
    [ExecuteInEditMode]
    public class SpecularLighting : MonoBehaviour
    {
        public Transform specularLight;
        private WaterBase _waterBase;
        private static readonly int WorldLightDir = Shader.PropertyToID("_WorldLightDir");


        public void Start()
        {
            _waterBase = (WaterBase) gameObject.GetComponent(typeof(WaterBase));
        }


        public void Update()
        {
            if (!_waterBase) _waterBase = (WaterBase) gameObject.GetComponent(typeof(WaterBase));

            if (specularLight && _waterBase.sharedMaterial)
                _waterBase.sharedMaterial.SetVector(WorldLightDir, specularLight.transform.forward);
        }
    }
}