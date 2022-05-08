using UnityEngine;

namespace U3Gear.Playground.Scripts.Water
{
    [ExecuteInEditMode]
    public class WaterTile : MonoBehaviour
    {
        public PlanarReflection reflection;
        public WaterBase waterBase;


        public void Start()
        {
            AcquireComponents();
        }


#if UNITY_EDITOR
        public void Update()
        {
            AcquireComponents();
        }
#endif


        public void OnWillRenderObject()
        {
            if (reflection) reflection.WaterTileBeingRendered(transform, Camera.current);
            if (waterBase) waterBase.WaterTileBeingRendered(transform, Camera.current);
        }


        private void AcquireComponents()
        {
            if (!reflection)
                reflection = transform.parent
                    ? transform.parent.GetComponent<PlanarReflection>()
                    : transform.GetComponent<PlanarReflection>();

            if (!waterBase)
                waterBase = transform.parent
                    ? transform.parent.GetComponent<WaterBase>()
                    : transform.GetComponent<WaterBase>();
        }
    }
}