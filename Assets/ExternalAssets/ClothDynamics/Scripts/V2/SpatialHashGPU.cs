using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace ClothDynamics
{

    [DefaultExecutionOrder(15200)] //When using Final IK
    public class SpatialHashGPU
    {
        private const int BLOCK_SIZE = 256;
        public RadixSortGPU _radixsort;
        private float _spacing;
        private int _tableSize;
        private GPUClothDynamicsV2 _dynamics;
        private AssetBundle _myLoadedAssetBundle = null;

        public SpatialHashGPU(GPUClothDynamicsV2 dynamics, float particleDiameter, int maxNumObjects)
        {
            _dynamics = dynamics;
            //m_cs = Resources.Load<ComputeShader>("Shaders/Compute/V2/SpatialHashGPU");
            _cs = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/SpatialHashGPU");

            _spacing = particleDiameter * _dynamics._globalSimParams.hashCellSizeScalar;
            _tableSize = 2 * maxNumObjects;

            if (neighbors != null) neighbors.Release();
            if (cellStart != null) cellStart.Release();
            if (cellEnd != null) cellEnd.Release();
            neighbors = new ComputeBuffer(maxNumObjects * _dynamics._globalSimParams.maxNumNeighbors, sizeof(int));
            cellStart = new ComputeBuffer(_tableSize, sizeof(int));
            cellEnd = new ComputeBuffer(_tableSize, sizeof(int));

            _radixsort = new RadixSortGPU(512, Mathf.NextPowerOfTwo(maxNumObjects).GetComputeShaderThreads(512));
        }

        public void SetInitialPositions(Vector3[] data, ComputeBuffer sphereDataBuffer)
        {
            //Debug.Log("sphereDataBuffer.count init " + sphereDataBuffer.count);
            int count = data.Length + sphereDataBuffer.count;
            Debug.Log("SetInitialPositions count " + count);
            var data2 = new CollisionMeshesGPU.sData[sphereDataBuffer.count];
            sphereDataBuffer.GetData(data2);
            var dataCombined = new Vector3[count];

            int firstLength = data.Length;
            for (int i = 0; i < count; i++)
            {
                if (i < firstLength)
                    dataCombined[i] = data[i];
                else
                    dataCombined[i] = data2[i - firstLength].pr.xyz;

            }
            initialPositions = new ComputeBuffer(count, sizeof(float) * 3);
            initialPositions.SetData(dataCombined);
        }

        // particles that are initially close won't generate collision in the future
        public void SetInitialPositions(ComputeBuffer positions, ComputeBuffer sphereDataBuffer)
        {
            //Debug.Log("sphereDataBuffer.count init " + sphereDataBuffer.count);
            int count = positions.count + sphereDataBuffer.count;

            var data = new Vector3[positions.count];
            positions.GetData(data);

            var data2 = new CollisionMeshesGPU.sData[sphereDataBuffer.count];
            sphereDataBuffer.GetData(data2);

            var dataCombined = new Vector3[count];

            int firstLength = data.Length;
            for (int i = 0; i < count; i++)
            {
                if (i < firstLength)
                    dataCombined[i] = data[i];
                else
                    dataCombined[i] = data2[i - firstLength].pr.xyz;

            }
            initialPositions = new ComputeBuffer(count, sizeof(float) * 3);
            initialPositions.SetData(dataCombined);
        }

        public void Hash(ComputeBuffer positions, ComputeBuffer sphereDataBuffer)
        {
            HashParams param;
            param.numObjects = positions.count + (sphereDataBuffer.count > 1 ? sphereDataBuffer.count : 0);
            param.cellSpacing = _spacing;
            param.cellSpacing2 = _spacing * _spacing;
            param.tableSize = _tableSize;
            param.maxNumNeighbors = (uint)_dynamics._globalSimParams.maxNumNeighbors;
            param.particleDiameter2 = _dynamics._globalSimParams.particleDiameter * _dynamics._globalSimParams.particleDiameter;

            HashObjects(_radixsort.buffer, cellStart, cellEnd, neighbors, positions, initialPositions, sphereDataBuffer, param);
        }

        private void HashObjects(ComputeBuffer particleHashIndex, ComputeBuffer cellStart, ComputeBuffer cellEnd, ComputeBuffer neighbors, ComputeBuffer positions, ComputeBuffer initialPositions, ComputeBuffer sphereDataBuffer, HashParams param)
        {
            int numClothParticles = positions.count;
            int numObjects = param.numObjects;
            int numObjectsThread = numObjects.GetComputeShaderThreads(BLOCK_SIZE);

            _cs.SetInt("_numClothParticles", numClothParticles);
            _cs.SetInt("params_numObjects", numObjects);
            _cs.SetFloat("params_cellSpacing", param.cellSpacing);
            _cs.SetFloat("params_cellSpacing2", param.cellSpacing2);
            _cs.SetInt("params_tableSize", param.tableSize);
            _cs.SetInt("params_maxNumNeighbors", (int)param.maxNumNeighbors);
            _cs.SetFloat("params_particleDiameter2", param.particleDiameter2);

            int ComputeParticleHash_Kernel = 0;
            _cs.SetBuffer(ComputeParticleHash_Kernel, "particleHashIndex", particleHashIndex);
            _cs.SetBuffer(ComputeParticleHash_Kernel, "positions", positions);
            _cs.SetBuffer(ComputeParticleHash_Kernel, "_sphereDataBuffer", sphereDataBuffer);
            _cs.SetBuffer(ComputeParticleHash_Kernel, "cellStart", cellStart);
            _cs.Dispatch(ComputeParticleHash_Kernel, numObjectsThread, 1, 1);

            _radixsort.Run();

            int FindCellStart_Kernel = 1;
            _cs.SetBuffer(FindCellStart_Kernel, "cellStart", cellStart);
            _cs.SetBuffer(FindCellStart_Kernel, "cellEnd", cellEnd);
            _cs.SetBuffer(FindCellStart_Kernel, "particleHashIndex", particleHashIndex);
            _cs.Dispatch(FindCellStart_Kernel, numObjectsThread, 1, 1);

            int CacheNeighbors_Kernel = 2;
            _cs.SetBuffer(CacheNeighbors_Kernel, "neighbors", neighbors);
            _cs.SetBuffer(CacheNeighbors_Kernel, "particleHashIndex", particleHashIndex);
            _cs.SetBuffer(CacheNeighbors_Kernel, "cellStart", cellStart);
            _cs.SetBuffer(CacheNeighbors_Kernel, "cellEnd", cellEnd);
            _cs.SetBuffer(CacheNeighbors_Kernel, "positions", positions);
            _cs.SetBuffer(CacheNeighbors_Kernel, "_sphereDataBuffer", sphereDataBuffer);
            _cs.SetBuffer(CacheNeighbors_Kernel, "originalPositions", initialPositions);
            _cs.Dispatch(CacheNeighbors_Kernel, numObjectsThread, 1, 1);

        }

        public void OnDestroy()
        {
            neighbors.ClearBuffer();
            initialPositions.ClearBuffer();
            cellStart.ClearBuffer();
            cellEnd.ClearBuffer();
            if (_radixsort != null) _radixsort.Cleanup();
            _myLoadedAssetBundle.Clear();
        }

        public ComputeBuffer neighbors;
        ComputeBuffer initialPositions;
        ComputeBuffer cellStart;
        ComputeBuffer cellEnd;

        struct HashParams
        {
            public int numObjects;
            public uint maxNumNeighbors;
            public float cellSpacing;
            public float cellSpacing2;
            public int tableSize;
            public float particleDiameter2;
        }

        private ComputeShader _cs;

    }
}