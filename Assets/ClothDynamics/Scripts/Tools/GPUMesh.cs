using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClothDynamics
{
    public class GPUMesh : GPUMeshData
    {
        [SerializeField] private bool _updateBufferEachFrame = false;
        private bool _init = false;
        public ComputeBuffer _meshVertsOut;
        private struct SVertOut
        {
            internal Vector3 pos;
            internal Vector3 norm;
            internal Vector4 tang;
        }
        private SVertOut[] _inVBO;
        private Mesh _mesh;

        public void OnEnable()
        {
            if (_init) return;

            _mesh = this.GetComponent<MeshFilter>().sharedMesh;

            _inVBO = new SVertOut[_mesh.vertexCount];
            var verts = _mesh.vertices; var normals = _mesh.normals; var tangents = _mesh.tangents;
            int length = _inVBO.Length;
            for (int i = 0; i < length; ++i)
            {
                _inVBO[i].pos = verts[i];
                if (i < normals.Length)
                    _inVBO[i].norm = normals[i];
                if (i < tangents.Length) _inVBO[i].tang = tangents[i];
            }

            _meshVertsOut = new ComputeBuffer(_mesh.vertexCount, Marshal.SizeOf(typeof(SVertOut)));
            _meshVertsOut.SetData(_inVBO);

            _init = true;
        }
        enum UpdateType
        {
            FixedUpdate = 0,
            Update = 1,
            LateUpdate = 2
        }
        [SerializeField] private UpdateType _updateType = UpdateType.LateUpdate;
        internal bool _updateSync = false;

        internal void UpdateSync()
        {
            if (_updateSync) MeshUpdate();
        }


        private void FixedUpdate()
        {
            if (_updateType == UpdateType.FixedUpdate)
                if (!_updateSync) MeshUpdate();
        }

        private void Update()
        {
            if (_updateType == UpdateType.Update)
                if (!_updateSync) MeshUpdate();
        }

        private void LateUpdate()
        {
            if (_updateType == UpdateType.LateUpdate)
                if (!_updateSync) MeshUpdate();
        }


        private void MeshUpdate()
        {
            if (_updateBufferEachFrame)
            {
                //TODO use the job system here.
                var verts = _mesh.vertices; var normals = _mesh.normals; var tangents = _mesh.tangents;
                int length = _inVBO.Length;
                for (int i = 0; i < length; ++i)
                {
                    _inVBO[i].pos = verts[i];
                    if (i < normals.Length)
                        _inVBO[i].norm = normals[i];
                    if (i < tangents.Length) _inVBO[i].tang = tangents[i];
                }
                _meshVertsOut.SetData(_inVBO);
            }
        }

        private void OnDisable()
        {
            _meshVertsOut.ClearBuffer();
        }

    }
}