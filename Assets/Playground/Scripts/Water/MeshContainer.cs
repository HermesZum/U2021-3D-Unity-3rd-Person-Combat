using UnityEngine;

namespace U3Gear.Playground.Scripts.Water
{
    public class MeshContainer
    {
        private readonly Mesh _mesh;
        private readonly Vector3[] _normals;
        private readonly Vector3[] _vertices;


        public MeshContainer(Mesh m)
        {
            _mesh = m;
            _vertices = m.vertices;
            _normals = m.normals;
        }


        public void Update()
        {
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
        }
    }
}