using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using System.Linq;
using System.Collections;
using UnityEngine.Rendering;
using Unity.Collections;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

namespace ClothDynamics
{
    public partial class GPUClothDynamics
    {
        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
#endif
            Destroy();
            _finishedLoading = false;
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
#endif
            Destroy();
            _finishedLoading = false;
        }

#if UNITY_EDITOR
        public static GPUClothDynamics _copyObj = null;

        public void OnPlaymodeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                PlayerPrefs.SetFloat(this.GetInstanceID().ToString(), this._bufferScale);
                PlayerPrefs.Save();
            }
            else if (state != PlayModeStateChange.EnteredPlayMode && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                this._bufferScale = math.ceil(PlayerPrefs.GetFloat(this.GetInstanceID().ToString()));

                if (_meshProxy != null)
                {
                    var skinFileName = PlayerPrefs.GetString("CD_" + this.name + _meshProxy.name + "skinPrefab", "");
                    if (!string.IsNullOrEmpty(skinFileName))
                    {
                        TextAsset newPrefabAsset = Resources.Load(skinFileName) as TextAsset;
                        _skinPrefab = newPrefabAsset;
                    }
                }
                if (_usePreCache)
                {
                    var cacheFileName = PlayerPrefs.GetString("CD_" + this.name + "clothPrefab", "");
                    if (!string.IsNullOrEmpty(cacheFileName))
                    {
                        TextAsset newPrefabAsset = Resources.Load(cacheFileName) as TextAsset;
                        _preCacheFile = newPrefabAsset;
                    }
                }
            }
        }

        public void TransferData(bool copy = true)
        {
            if (copy)
            {
                _copyObj = this;
            }
            else if (_copyObj != null)
            {
                var obj = this;
                BindingFlags flags = /*BindingFlags.NonPublic | */BindingFlags.Public | BindingFlags.Instance/* | BindingFlags.Static*/;
                FieldInfo[] fields = obj.GetType().GetFields(flags);
                FieldInfo[] fieldsSource = _copyObj.GetType().GetFields(flags);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (fieldsSource[i].Name != fields[i].Name) Debug.Log("fieldsSource: " + fieldsSource[i].Name + ", Field: " + fields[i].Name);
                    if (fields[i].FieldType == typeof(bool) ||
                        fields[i].FieldType == typeof(float) ||
                        fields[i].FieldType == typeof(int) ||
                        fields[i].FieldType == typeof(Vector3) ||
                        fields[i].FieldType == typeof(WindZone) ||
                        fields[i].FieldType == typeof(PointConstraintType) ||
                        fields[i].FieldType == typeof(int[]) ||
                        fields[i].FieldType == typeof(DampingMethod) ||
                        fields[i].FieldType == typeof(Transform) ||
                        fields[i].FieldType == typeof(Transform[]) ||
                        fields[i].FieldType == typeof(float3) ||
                        fields[i].FieldType == typeof(List<GameObject>) ||
                        fields[i].FieldType == typeof(AnimationCurveData) ||
                        fields[i].FieldType == typeof(Camera) ||
                        fields[i].FieldType == typeof(UnityEngine.Object) ||
                        fields[i].FieldType == typeof(Light[])
                        )
                    {
                        fields[i].SetValue(obj, fieldsSource[i].GetValue(_copyObj));
                    }
                }
                //PropertyInfo[] properties = obj.GetType().GetProperties(flags);
                //foreach (PropertyInfo propertyInfo in properties)
                //{
                //	Debug.Log("Obj: " + obj.name + ", Property: " + propertyInfo.Name);
                //}
            }
        }
#endif
        private void SetCollisionFinderBuffers()
        {
            if (_collisionFinder._selfTrisDataBuffer == null) _collisionFinder._selfTrisDataBuffer = new ComputeBuffer(1, sizeof(int));

            _clothSolver.SetBool("_useTrisMesh", _collisionFinder._useTrisMesh);

            _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer_ID, _countContactBuffer);
            _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer2_ID, _countContactBuffer2);
            _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer3_ID, _countContactBuffer3);
#if !UNITY_EDITOR
			if (_predictiveContact)
#endif
            {
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _connectionInfoBuffer_ID, _objBuffers[0].connectionInfoBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _connectedVertsBuffer_ID, _objBuffers[0].connectedVertsBuffer);

                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _velocities_ID, _velocitiesBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _usedVoxelListBuffer_ID, _collisionFinder._usedVoxelListBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _counterPerVoxelBuffer2_ID, _collisionFinder._counterPerVoxelBuffer2);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _lastCounterPerVoxelBuffer2_ID, _collisionFinder._lastCounterPerVoxelBuffer2);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _pointPointContactBuffer_ID, _pointPointContactBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _pointPointContactBuffer2_ID, _pointPointContactBuffer2);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _countContactBuffer_ID, _countContactBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _countContactBuffer2_ID, _countContactBuffer2);
                _clothSolver.SetBuffer(_pointPointPredictiveContactKernel, _trisData_ID, _collisionFinder._selfTrisDataBuffer);

                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _deltaCount_ID, _deltaCounterBuffer);
                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _pointPointContactBuffer_ID, _pointPointContactBuffer);
                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                _clothSolver.SetBuffer(_selfContactCollisionsKernel, _frictions_ID, _frictionsBuffer);

                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _deltaCount_ID, _deltaCounterBuffer);
                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _pointPointContactBuffer2_ID, _pointPointContactBuffer2);
                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                _clothSolver.SetBuffer(_otherSpheresContactCollisions2Kernel, _frictions_ID, _frictionsBuffer);
            }
#if !UNITY_EDITOR
			else
#endif
            {
#if !UNITY_2020_1_OR_NEWER
				_clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _connectionInfoBuffer_ID, _objBuffers[0].connectionInfoBuffer);
				_clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _connectedVertsBuffer_ID, _objBuffers[0].connectedVertsBuffer);
#endif
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _frictions_ID, _frictionsBuffer);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _usedVoxelListBuffer_ID, _collisionFinder._usedVoxelListBuffer);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _counterPerVoxelBuffer2_ID, _collisionFinder._counterPerVoxelBuffer2);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _lastCounterPerVoxelBuffer2_ID, _collisionFinder._lastCounterPerVoxelBuffer2);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _voxelDataBuffer_ID, _collisionFinder._voxelDataBuffer2);
                _clothSolver.SetBuffer(_satisfyVertexCollisionsKernel, _trisData_ID, _collisionFinder._selfTrisDataBuffer);
            }

            _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer_ID, _countContactBuffer);
            _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer2_ID, _countContactBuffer2);
            _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer3_ID, _countContactBuffer3);
        }

        private void SetupComputeBuffers(bool init = true)
        {
            if (init)
            {
                // create the compute buffers
                _velocitiesBuffer = new ComputeBuffer(_numParticles, sizeof(float) * 4);
                _frictionsBuffer = new ComputeBuffer(_numParticles, sizeof(float));
                _deltaPositionsUIntBuffer = new ComputeBuffer(_numParticles, sizeof(uint) * 3);
                _deltaPositionsUIntBuffer2 = new ComputeBuffer(8, sizeof(uint) * 4);
                _deltaCounterBuffer = new ComputeBuffer(_numParticles, sizeof(int));
                _distanceConstraintsBuffer = new ComputeBuffer(_numDistanceConstraints, sizeof(float) + sizeof(int) * 2);
                _bendingConstraintsBuffer = new ComputeBuffer(_numBendingConstraints, sizeof(float) + sizeof(int) * 4);
                //_gridCenterBuffer = new ComputeBuffer(1, sizeof(float) * 4);
                _gridCenterBuffer = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                _gridCenterBuffer.filterMode = FilterMode.Point;
                _gridCenterBuffer.enableRandomWrite = true;
                _gridCenterBuffer.Create();

                // fill buffers with initial data
                _velocitiesBuffer.SetData(_velocities);
                _frictionsBuffer.SetData(_frictions);
                _deltaPositionsUIntBuffer.SetData(new UInt3Struct[_numParticles]);
                _deltaCounterBuffer.SetData(new int[_numParticles]);
                _distanceConstraintsBuffer.SetData(_distanceConstraints);
                _bendingConstraintsBuffer.SetData(_bendingConstraints);

                // identify the kernels
                _applyExternalForcesKernel = _clothSolver.FindKernel("ApplyExternalForces");
                _dampVelocitiesKernel = _clothSolver.FindKernel("DampVelocities");
                _applyExplicitEulerKernel = _clothSolver.FindKernel("ApplyExplicitEuler");
                _projectConstraintDeltasKernel = _clothSolver.FindKernel("ProjectConstraintDeltas");
                _averageConstraintDeltasKernel = _clothSolver.FindKernel("AverageConstraintDeltas");
                _updatePositionsKernel = _clothSolver.FindKernel("UpdatePositions");
                _updatePositions2Kernel = _clothSolver.FindKernel("UpdatePositions2");
                _updatePositions2BlendsKernel = _clothSolver.FindKernel("UpdatePositions2Blends");
                _updatePositionsNoSkinningKernel = _clothSolver.FindKernel("UpdatePositionsNoSkinning");
                _satisfySphereCollisionsKernel = _clothSolver.FindKernel("SatisfySphereCollisions");
                _satisfyVertexCollisionsKernel = _clothSolver.FindKernel("SatisfyVertexCollisions");
                _satisfySDFCollisionsKernel = _clothSolver.FindKernel("SatisfySDFCollisions");
                _satisfyPointConstraintsKernel = _clothSolver.FindKernel("SatisfyPointConstraints");
                _updateWorldTransformKernel = _clothSolver.FindKernel("UpdateWorldTransform");
                _updateInverseWorldTransformKernel = _clothSolver.FindKernel("UpdateInverseWorldTransform");
                _csNormalsKernel = _clothSolver.FindKernel("CSNormals");
                _bendingConstraintDeltasKernel = _clothSolver.FindKernel("BendingConstraintDeltas");
                _skinningHDKernel = _clothSolver.FindKernel("SkinningHD");

                _calcCubeCenterKernel = _clothSolver.FindKernel("CalcCubeCenter");
                _calcCubeCenter2Kernel = _clothSolver.FindKernel("CalcCubeCenter2");
                _calcCubeCenterFastKernel = _clothSolver.FindKernel("CalcCubeCenterFast");

                _selfContactCollisionsKernel = _clothSolver.FindKernel("SelfContactCollisions");
                _collidersContactCollisionsKernel = _clothSolver.FindKernel("CollidersContactCollisions");
                _otherSpheresContactCollisions2Kernel = _clothSolver.FindKernel("OtherSpheresContactCollisions");

                _countContactStartKernel = _clothSolver.FindKernel("CountContactStart");
                _pointPointPredictiveContactKernel = _clothSolver.FindKernel("PointPointPredictiveContact");
                _pointPointPredictiveContactCollidersKernel = _clothSolver.FindKernel("PointPointPredictiveContactColliders");
                _countContactSetupKernel = _clothSolver.FindKernel("CountContactSetup");

                _transferDuplicateVertexDataKernel = _clothSolver.FindKernel("TransferDuplicateVertexData");

                _surfacePushKernel = _clothSolver.FindKernel("SurfacePush");
                _surfacePushCollidersKernel = _clothSolver.FindKernel("SurfacePushColliders");
                _surfacePushDQSKernel = _clothSolver.FindKernel("SurfacePushDQS");
                _surfacePushCollidersDQSKernel = _clothSolver.FindKernel("SurfacePushCollidersDQS");
                _surfacePushSkinningKernel = _clothSolver.FindKernel("SurfacePushSkinning");
                _surfacePushSkinningBlendsKernel = _clothSolver.FindKernel("SurfacePushSkinningBlends");
                _surfacePushCollidersSkinningKernel = _clothSolver.FindKernel("SurfacePushCollidersSkinning");
                _surfacePushCollidersSkinningBlendsKernel = _clothSolver.FindKernel("SurfacePushCollidersSkinningBlends");

                _computeCenterOfMassKernel = _clothSolver.FindKernel("ComputeCenterOfMass");
                _finishCenterOfMassKernel = _clothSolver.FindKernel("FinishCenterOfMass");
                _sumAllMassAndMatrixKernel = _clothSolver.FindKernel("SumAllMassAndMatrix");
                _finishMatrixCalcKernel = _clothSolver.FindKernel("FinishMatrixCalc");
                _applyBackIntoVelocitiesKernel = _clothSolver.FindKernel("ApplyBackIntoVelocities");
                _clearCenterOfMassKernel = _clothSolver.FindKernel("ClearCenterOfMass");

                _clothSolver.SetVector(_windVec_ID, Vector4.zero);
            }

            _clothSolver.SetBool("_useSelfCollisionTriangles", _selfCollisionTriangles);

            // set uniform data for kernels       
            if (!_useCollidableObjectsList) { _numCollidableSpheres = 0; _numCollidableSDFs = 0; }
            _clothSolver.SetInt(_numCollidableSpheres_ID, _numCollidableSpheres);
            _clothSolver.SetInt(_numCollidableSDFs_ID, _numCollidableSDFs);

            if (_useCollisionFinder && _collisionFinder != null) _clothSolver.SetInt("_gridCount", _collisionFinder._gridCount);
            _clothSolver.SetInt(_numParticles_ID, _numParticles);
            _clothSolver.SetInt("_numDistanceConstraints", _numDistanceConstraints);
            _clothSolver.SetInt("_numBendingConstraints", _numBendingConstraints);
            //_clothSolver.SetInt("_numAllConstraints", _numDistanceConstraints + _numBendingConstraints);

            // bind buffer data to each kernel
            _clothSolver.SetBuffer(_updateWorldTransformKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_updateWorldTransformKernel, _velocities_ID, _velocitiesBuffer);

            _clothSolver.SetBuffer(_applyExternalForcesKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_applyExternalForcesKernel, _velocities_ID, _velocitiesBuffer);
            _clothSolver.SetBuffer(_dampVelocitiesKernel, _velocities_ID, _velocitiesBuffer);

            _clothSolver.SetBuffer(_applyExplicitEulerKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_applyExplicitEulerKernel, _projectedPositions_ID, _projectedPositionsBuffer);
            _clothSolver.SetBuffer(_applyExplicitEulerKernel, _velocities_ID, _velocitiesBuffer);

            if (_duplicateVerticesBuffer != null)
            {
                _clothSolver.SetBuffer(_transferDuplicateVertexDataKernel, "_duplicateVerticesBuffer", _duplicateVerticesBuffer);
                _clothSolver.SetBuffer(_transferDuplicateVertexDataKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            }

            _clothSolver.SetBuffer(_updateInverseWorldTransformKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_updateInverseWorldTransformKernel, _velocities_ID, _velocitiesBuffer);

            _clothSolver.SetBuffer(_projectConstraintDeltasKernel, _projectedPositions_ID, _projectedPositionsBuffer);
            _clothSolver.SetBuffer(_projectConstraintDeltasKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
            _clothSolver.SetBuffer(_projectConstraintDeltasKernel, _deltaCount_ID, _deltaCounterBuffer);
            _clothSolver.SetBuffer(_projectConstraintDeltasKernel, "_distanceConstraints", _distanceConstraintsBuffer);

            _clothSolver.SetBuffer(_bendingConstraintDeltasKernel, _projectedPositions_ID, _projectedPositionsBuffer);
            _clothSolver.SetBuffer(_bendingConstraintDeltasKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
            _clothSolver.SetBuffer(_bendingConstraintDeltasKernel, _deltaCount_ID, _deltaCounterBuffer);
            _clothSolver.SetBuffer(_bendingConstraintDeltasKernel, "_bendingConstraints", _bendingConstraintsBuffer);

            _clothSolver.SetBuffer(_averageConstraintDeltasKernel, _projectedPositions_ID, _projectedPositionsBuffer);
            _clothSolver.SetBuffer(_averageConstraintDeltasKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
            _clothSolver.SetBuffer(_averageConstraintDeltasKernel, _deltaCount_ID, _deltaCounterBuffer);

            if (_skinTypeCloth == SkinTypes.DualQuaternionSkinner)
            {
                var dqs = _skinComponent as DualQuaternionSkinner;
                if (dqs.gameObject.activeInHierarchy)
                {
                    _clothSolver.SetInt(_skinned_tex_width_ID, dqs ? dqs._textureWidth : 4);
                    _clothSolver.SetTexture(_updatePositionsKernel, _skinned_data_1_ID, dqs ? dqs._rtSkinnedData_1 : _dummyTex);
                    _clothSolver.SetBuffer(_updatePositionsKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                    _clothSolver.SetBuffer(_updatePositionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.SetBuffer(_updatePositionsKernel, _velocities_ID, _velocitiesBuffer);
                    _clothSolver.SetBuffer(_updatePositionsKernel, _frictions_ID, _frictionsBuffer);
                }
                else _skinTypeCloth = SkinTypes.NoSkinning;
            }
            else if (_skinTypeCloth == SkinTypes.GPUSkinning)
            {
                var skinning = _skinComponent as GPUSkinning;
                if (skinning.gameObject.activeInHierarchy)
                {
                    bool morph = false;
                    if (this.GetComponent<GPUBlendShapes>().ExistsAndEnabled(out MonoBehaviour monoMorph))
                        morph = true;

                    if (morph)
                    {
                        if (skinning._meshVertsOut != null) _clothSolver.SetBuffer(_updatePositions2BlendsKernel, _meshVertsOut_ID, skinning._meshVertsOut);
                        _clothSolver.SetBuffer(_updatePositions2BlendsKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                        _clothSolver.SetBuffer(_updatePositions2BlendsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                        _clothSolver.SetBuffer(_updatePositions2BlendsKernel, _velocities_ID, _velocitiesBuffer);
                        _clothSolver.SetBuffer(_updatePositions2BlendsKernel, _frictions_ID, _frictionsBuffer);
                    }

                    if (skinning._meshVertsOut != null) _clothSolver.SetBuffer(_updatePositions2Kernel, _meshVertsOut_ID, skinning._meshVertsOut);
                    _clothSolver.SetBuffer(_updatePositions2Kernel, _positions_ID, _objBuffers[0].positionsBuffer);
                    _clothSolver.SetBuffer(_updatePositions2Kernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.SetBuffer(_updatePositions2Kernel, _velocities_ID, _velocitiesBuffer);
                    _clothSolver.SetBuffer(_updatePositions2Kernel, _frictions_ID, _frictionsBuffer);
                }
                else _skinTypeCloth = SkinTypes.NoSkinning;
            }
            if (_skinTypeCloth == SkinTypes.NoSkinning)
            {
                _clothSolver.SetBuffer(_updatePositionsNoSkinningKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                _clothSolver.SetBuffer(_updatePositionsNoSkinningKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_updatePositionsNoSkinningKernel, _velocities_ID, _velocitiesBuffer);
                _clothSolver.SetBuffer(_updatePositionsNoSkinningKernel, _frictions_ID, _frictionsBuffer);
            }

            //calculate and set the work group size
            _numGroups_Vertices = _numParticles.GetComputeShaderThreads(_workGroupSize);
            _numGroups_DistanceConstraints = _numDistanceConstraints.GetComputeShaderThreads(_workGroupSize);
            _numGroups_BendingConstraints = _numBendingConstraints.GetComputeShaderThreads(_workGroupSize);
            //_numGroups_AllConstraints = (_numDistanceConstraints + _numBendingConstraints).GetComputeShaderThreads(_workGroupSize);

            if (_useGarmentMesh && !_onlyAtStart && _colors != null)
            {
                for (int i = 0; i < _distanceConstraints.Length; i++)
                {
                    var edge = _distanceConstraints[i].edge;
                    var dist = (transform.TransformPoint(_positions[edge.startIndex]) - transform.TransformPoint(_positions[edge.endIndex])).magnitude;
                    _distanceConstraints[i].restLength = _useGarmentMesh && _colors[edge.startIndex].g + _colors[edge.endIndex].g > 1.5f && dist > _garmentSeamLength ? _garmentSeamLength : dist;
                }
                _distanceConstraintsBuffer.SetData(_distanceConstraints);
            }

            if (init == false && _useCollisionFinder && _collisionFinder != null) SetCollisionFinderBuffers();
        }

        private void SetupCollisionComputeBuffers()
        {
            if (_spheres == null) _spheres = new List<GameObject>();
            if (_sdfObjs == null) _sdfObjs = new List<GameObject>();
            _spheres.Clear();
            _sdfObjs.Clear();

            Extensions.CleanupList(ref _collidableObjects);
            _collidableObjects = _collidableObjects.Distinct().ToList();
            int length = _collidableObjects.Count;
            _lastCollidableObjectsCount = length;
            int countUsed = 0;
            for (int j = 0; j < length; j++)
            {
                if (!_collidableObjects[j].activeInHierarchy) continue;

                Collider collider = _collidableObjects[j].GetComponent<Collider>();

                if (collider != null && _useDefaultSphereCollision && collider.GetType() == typeof(SphereCollider) && !collider.GetComponent<RoundConeCollider>())
                {
                    var lossyScale = collider.transform.lossyScale;
                    if (math.abs(lossyScale.x - lossyScale.y) < DELTA_SCALE && math.abs(lossyScale.y - lossyScale.z) < DELTA_SCALE)
                        if (_spheres != null) _spheres.Add(_collidableObjects[j]);
                        else
                            _sdfObjs.Add(_collidableObjects[j]);
                }
                else
                {
                    _sdfObjs.Add(_collidableObjects[j]);
                }
                countUsed++;
            }

            if (countUsed == 0 && _useCollidableObjectsList) Debug.Log("<color=blue>CD: </color><color=orange>Collidable Objects are not used, check whether they are active in the hierarchy!</color>");

            _numCollidableSpheres = _spheres.Count;
            _collidableSpheres = new CollidableSphereStruct[_numCollidableSpheres];
            for (int i = 0; i < _numCollidableSpheres; i++)
            {
                SetupSphereCollider(i);
                int tdNum = InitializeDiffFrames(_spheres[i].transform.position, _spheres[i].transform.lossyScale, _spheres[i].transform.rotation);
                UpdateDiffFrames(_spheres[i].transform, Time.deltaTime, tdNum, out _collidableSpheres[i].posVel, out _collidableSpheres[i].rotVel);
            }
            if (_collidableSpheresBuffer != null) _collidableSpheresBuffer.Release();
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CollidableSphereStruct));
            _collidableSpheresBuffer = new ComputeBuffer(Mathf.Max(1, _numCollidableSpheres), size);

            if (_numCollidableSpheres > 0)
            {
                _collidableSpheresBuffer.SetData(_collidableSpheres);
                _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _frictions_ID, _frictionsBuffer);
            }

            _numCollidableSDFs = _sdfObjs.Count;
            _collidableSDFs = new CollidableSDFStruct[_numCollidableSDFs];
            for (int i = 0; i < _numCollidableSDFs; i++)
            {
                //if (i >= _collidableSDFs.Length || i >= _sdfObjs.Count || _sdfObjs[i] == null) break;
                SetupSDFCollider(i);
                int tdNum = InitializeDiffFrames(_sdfObjs[i].transform.position, _sdfObjs[i].transform.lossyScale, _sdfObjs[i].transform.rotation);
                UpdateDiffFrames(_sdfObjs[i].transform, Time.deltaTime, tdNum, out _collidableSDFs[i].posVel, out _collidableSDFs[i].rotVel);
            }
            if (_collidableSDFsBuffer != null) _collidableSDFsBuffer.Release();
            size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CollidableSDFStruct));
            _collidableSDFsBuffer = new ComputeBuffer(Mathf.Max(1, _numCollidableSDFs), size);

            if (_numCollidableSDFs > 0)
            {
                _collidableSDFsBuffer.SetData(_collidableSDFs);
                _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _collidableSDFs_ID, _collidableSDFsBuffer);
                _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _frictions_ID, _frictionsBuffer);
            }

            AddPointConstraints();
            if (_pointConstraintsBuffer != null) _pointConstraintsBuffer.Release();
            if (_pointConstraintsVecBuffer != null) _pointConstraintsVecBuffer.Release();

            if (_numPointConstraints > 0)
            {
                _pointConstraintsBuffer = new ComputeBuffer(_numPointConstraints, sizeof(int));
                _pointConstraintsBuffer.SetData(_pointConstraints);
                _pointConstraintsVecBuffer = new ComputeBuffer(_numPointConstraints, sizeof(float) * 4);
                _pointConstraintsVecBuffer.SetData(_pointConstraintsVec);

                _clothSolver.SetInt(_numPointConstraints_ID, _numPointConstraints);
                _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, "_pointConstraints", _pointConstraintsBuffer);
                _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, "_pointConstraintsVec", _pointConstraintsVecBuffer);
                _numGroups_PointConstraints = _numPointConstraints.GetComputeShaderThreads(_workGroupSize);
            }

            if (!_useCollidableObjectsList) { _numCollidableSpheres = 0; _numCollidableSDFs = 0; }
            _clothSolver.SetInt(_numCollidableSpheres_ID, _numCollidableSpheres);
            _clothSolver.SetInt(_numCollidableSDFs_ID, _numCollidableSDFs);


            if (_usePredictiveContactColliders)
            {
                _pointPointContactBuffer3 = new ComputeBuffer(Mathf.Max(1, _numParticles * _collidableObjects.Count), sizeof(float) * 4 + sizeof(int) * 4);

                if (_countContactBuffer == null)
                {
                    _countContactBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer.SetData(new int[] { 1, 1, 1 });
                    _countContactBuffer2 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer2.SetData(new int[] { 1, 1, 1 });
                    _countContactBuffer3 = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
                    _countContactBuffer3.SetData(new int[] { 1, 1, 1 });
                }

                if (_debugBuffers)
                {
                    Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " start value: _pointPointContactBuffer3 " + _pointPointContactBuffer3.count + "</color>");
                }
                _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer_ID, _countContactBuffer);
                _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer2_ID, _countContactBuffer2);
                _clothSolver.SetBuffer(_countContactStartKernel, _countContactBuffer3_ID, _countContactBuffer3);
                _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer_ID, _countContactBuffer);
                _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer2_ID, _countContactBuffer2);
                _clothSolver.SetBuffer(_countContactSetupKernel, _countContactBuffer3_ID, _countContactBuffer3);

                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _velocities_ID, _velocitiesBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _pointPointContactBuffer3_ID, _pointPointContactBuffer3);
                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _countContactBuffer3_ID, _countContactBuffer3);
                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                _clothSolver.SetBuffer(_pointPointPredictiveContactCollidersKernel, _collidableSDFs_ID, _collidableSDFsBuffer);

                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _deltaPosAsInt_ID, _deltaPositionsUIntBuffer);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _deltaCount_ID, _deltaCounterBuffer);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _pointPointContactBuffer3_ID, _pointPointContactBuffer3);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _collidableSDFs_ID, _collidableSDFsBuffer);
                _clothSolver.SetBuffer(_collidersContactCollisionsKernel, _frictions_ID, _frictionsBuffer);
            }
        }

        private void UpdateCollisionComputeBuffers(float dt)
        {
            if (_numCollidableSpheres > 0)
            {
                for (int i = 0; i < _numCollidableSpheres; i++)
                {
                    SetupSphereCollider(i);
                    int tdNum = 1 + i;
                    UpdateDiffFrames(_spheres[i].transform, dt, tdNum, out _collidableSpheres[i].posVel, out _collidableSpheres[i].rotVel);
                }
                _collidableSpheresBuffer.SetData(_collidableSpheres);

#if UNITY_EDITOR //for Debugging
                if (_debugMode)
                {
                    _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _collidableSpheres_ID, _collidableSpheresBuffer);
                    _clothSolver.SetBuffer(_satisfySphereCollisionsKernel, _frictions_ID, _frictionsBuffer);
                }
#endif
            }
            if (_numCollidableSDFs > 0)
            {
                for (int i = 0; i < _numCollidableSDFs; i++)
                {
                    //if (i >= _collidableSDFs.Length || i >= _sdfObjs.Count || _sdfObjs[i] == null) break;
                    SetupSDFCollider(i);
                    int tdNum = 1 + _numCollidableSpheres + i;
                    UpdateDiffFrames(_sdfObjs[i].transform, dt, tdNum, out _collidableSDFs[i].posVel, out _collidableSDFs[i].rotVel);
                }
                _collidableSDFsBuffer.SetData(_collidableSDFs);

#if UNITY_EDITOR //for Debugging
                if (_debugMode)
                {
                    _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _collidableSDFs_ID, _collidableSDFsBuffer);
                    _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _frictions_ID, _frictionsBuffer);
                    _clothSolver.SetBuffer(_satisfySDFCollisionsKernel, _velocities_ID, _velocitiesBuffer);
                }
#endif
            }

#if UNITY_EDITOR //for Debugging
            if (_debugMode)
            {
                if (_numPointConstraints > 0)
                {
                    _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, _positions_ID, _objBuffers[0].positionsBuffer);
                    _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, _projectedPositions_ID, _projectedPositionsBuffer);
                    _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, "_pointConstraints", _pointConstraintsBuffer);
                    _clothSolver.SetBuffer(_satisfyPointConstraintsKernel, "_pointConstraintsVec", _pointConstraintsVecBuffer);
                }
            }
#endif
        }

        private void SetupSphereCollider(int i)
        {
            var lossyScale = _spheres[i].transform.lossyScale;
            var sCollider = _spheres[i].GetComponent<SphereCollider>();
            if (sCollider != null)
            {
                _collidableSpheres[i].center = _spheres[i].transform.position + Vector3.Scale(lossyScale, sCollider.center);
                //Debug.DrawRay(_collidableSpheres[i].center, Vector3.up * 0.1f, Color.white);
                _collidableSpheres[i].radius = lossyScale.x * sCollider.radius;
                var cfc = _spheres[i].GetComponent<ClothFrictionCollider>();
                _collidableSpheres[i].friction = cfc ? 1.0f - cfc.friction : 1;
            }
        }

        private void SetupSDFCollider(int i)
        {
            int sdfType = _sdfObjs[i].GetComponent<CylinderCollider>() ? 4 :
                          _sdfObjs[i].GetComponent<RoundConeCollider>() ? 3 :
                          _sdfObjs[i].GetComponent<BoxCollider>() ? 0 :
                          _sdfObjs[i].GetComponent<CapsuleCollider>() ? 1 :
                          _sdfObjs[i].GetComponent<SphereCollider>() ? 2 :
                          5;
            Collider c = sdfType == 0 ? _sdfObjs[i].GetComponent<BoxCollider>() :
                        sdfType == 1 ? (Collider)_sdfObjs[i].GetComponent<CapsuleCollider>() :
                        sdfType == 2 ? (Collider)_sdfObjs[i].GetComponent<SphereCollider>() :
                        null;
            var lossyScale = _sdfObjs[i].transform.lossyScale;
            Quaternion extraRot = Quaternion.identity;
            Vector4 extent = sdfType == 0 ? (c == null ? Vector3.one * 0.5f : ((BoxCollider)c).size / 2f)
                        : sdfType == 1 ? (c == null ? Vector3.one * 0.5f : SetupCapsule(c, out extraRot))
                        : sdfType == 2 ? (c == null ? Vector3.one * 0.5f : Vector3.one * ((SphereCollider)c).radius * 0.5f)
                        : sdfType == 3 ? _sdfObjs[i].GetComponent<RoundConeCollider>().r1r2h
                        : Vector3.one * 0.5f;
            _collidableSDFs[i].center = _sdfObjs[i].transform.position;
            if (sdfType < 3 && c != null)
            {
                _collidableSDFs[i].center += _sdfObjs[i].transform.TransformVector(sdfType == 0 ? ((BoxCollider)c).center :
                                                                                   sdfType == 1 ? (((CapsuleCollider)c).center + Vector3.up * 0.5f) :
                                                                                   sdfType == 2 ? ((SphereCollider)c).center : Vector3.zero);
            }

            if (_debugMode && sdfType == 3) _sdfObjs[i].GetComponent<RoundConeCollider>()._showGizmos = _debugMode;

            //Debug.DrawRay(_collidableSDFs[i].center, Vector3.up * 0.1f, Color.white);

            extent = sdfType == 3 ? extent : Vector4.Scale(lossyScale, extent);
            var cfc = _sdfObjs[i].GetComponent<ClothFrictionCollider>();
            extent.w = cfc ? 1.0f - cfc.friction : 1;
            if (sdfType == 5) { extent.y = _usePlaneScaleY ? extent.y : 0.0f; sdfType = 6; }
            if (sdfType == 4) sdfType = 5;
            if (sdfType == 3) sdfType = 4;
            if (sdfType == 2 && math.abs(lossyScale.x - lossyScale.y) < DELTA_SCALE && math.abs(lossyScale.y - lossyScale.z) < DELTA_SCALE) sdfType = 3;
            _collidableSDFs[i].extent = extent;
            _collidableSDFs[i].rotation = QuatToVec(_sdfObjs[i].transform.rotation * extraRot);
            _collidableSDFs[i].sdfType = sdfType;
        }

        private Vector3 SetupCapsule(Collider c, out Quaternion rot)
        {
            rot = Quaternion.identity;
            var collider = ((CapsuleCollider)c);
            var vec = new Vector3(collider.radius, (collider.height - 1) * 0.25f, collider.radius);
            if (collider.direction == 0)
            {
                rot.eulerAngles = new Vector3(0, 0, 90);
            }
            if (collider.direction == 2)
            {
                rot.eulerAngles = new Vector3(90, 0, 0);
            }
            return vec;
        }

        internal override void SetSecondUVsForVertexID(Mesh mesh)
        {
#if UNITY_2019 //Vertex ID might already work with 2020
            //TODO this needs also the shaders to be adjusted to UV reading for vertex ID
            if (GraphicsSettings.currentRenderPipeline)
            {
                var uv2 = new Vector2[mesh.vertexCount];
                for (int i = 0; i < uv2.Length; i++)
                {
                    uv2[i] = new Vector2(i, 0);
                }
                switch (_vertexIdsToUvId)
                {
                    case 2:
                        mesh.uv2 = uv2;
                        break;
                    case 3:
                        mesh.uv3 = uv2;
                        break;
                    case 4:
                        mesh.uv4 = uv2;
                        break;
                    case 5:
                        mesh.uv5 = uv2;
                        break;
                    case 6:
                        mesh.uv6 = uv2;
                        break;
                    case 7:
                        mesh.uv7 = uv2;
                        break;
                    default:
                        mesh.uv8 = uv2;
                        break;
                }
            }
#endif
        }

        internal override ComputeBuffer GetPositionsBuffer()
        {
            return _objBuffers[0].positionsBuffer;
        }

        internal override ComputeBuffer GetNormalsBuffer()
        {
            return _objBuffers[0].normalsBuffer;
        }

        private void SetClothShader(Renderer mr)
        {
            if (mr != null)
            {
                for (int i = 0; i < mr.materials.Length; i++)
                {
                    var material = mr.materials[i];
                    var cacheShader = material.shader;
                    //Debug.Log(this.name + " " + material.shader.name + " " + material.shader.name.ToLower().Contains("cloth"));
                    if (_shader == null && !material.shader.name.ToLower().Contains("cloth"))
                    {
                        if (GraphicsSettings.currentRenderPipeline)
                        {
                            //var smoothness = material.GetFloat("_Smoothness");
                            if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
                            {
                                material.shader = Resources.Load("Shaders/SRP/Lit_Graph_Cloth", typeof(Shader)) as Shader; // Shader.Find("Shader Graphs/Lit_Graph_Cloth");
                            }
                            else // assuming here we only have HDRP or URP options here
                            {
#if UNITY_2020_1_OR_NEWER
                                material.shader = Resources.Load("Shaders/SRP/URP_Lit_Graph_Cloth", typeof(Shader)) as Shader;
#else
								material.shader = Resources.Load("Shaders/SRP/URP_Lit_Cloth_2019", typeof(Shader)) as Shader;
#endif
                            }
                            //material.SetFloat("_SmoothnessRemapMax", smoothness);//TODO this is just a workaround
                        }
                        else
                        {
                            material.shader = Resources.Load("Shaders/ClothSurfaceShader", typeof(Shader)) as Shader; // Shader.Find("ClothDynamics/ClothSurfaceShader");
                        }
                        _shader = material.shader;
                        if (cacheShader != material.shader) material.SetFloat("_EmissiveIntensity", 0);
                    }
                    if (_shader != null) material.shader = _shader;
                    material.EnableKeyword("USE_BUFFERS");
                    if (_useTransferData) material.EnableKeyword("USE_TRANSFER_DATA");
                }
            }
        }

        private IEnumerator DelayVoxelPosState()
        {
            yield return null;
            yield return null; //TODO needed?
            _calcVoxelPosRoughly = _saveVoxelPosState;
            _useLOD = _saveLodState;
            _useFrustumClipping = _saveClippingState;
            SetupComputeBuffers(false); //TODO needed?

            for (int i = 0; i < 60; i++)
                yield return null; //TODO needed?
            _updateBufferSize = false;
        }

        private IEnumerator ReadbackVertexData()
        {
            if (_runReadbackVertices) yield break;
            _runReadbackVertices = true;

            _readbackVertexEveryX = math.max(1, _readbackVertexEveryX);

            Vector3 scale = Vector3.one * _readbackVertexScale;

            int length = 0;

            if (_supportsAsyncGPUReadback)
            {
                var request = UniversalAsyncGPUReadbackRequest.Request(_objBuffers[0].positionsBuffer);
                while (!request.done)
                {
                    if (request.hasError) request = UniversalAsyncGPUReadbackRequest.Request(_objBuffers[0].positionsBuffer);
                    yield return null;
                }
                var dataPos = request.GetData<Vector3>();

                length = dataPos.Length / _readbackVertexEveryX;
                if (_vertexColliders == null)
                {
                    _readbackVertexParent = new GameObject("ReadbackVertexData").transform;
                    _vertexColliders = new GameObject[length];
                }

                for (int i = 0; i < length; i++)
                {
                    SetVertexCollider(i);
                    var pos = dataPos[i * _readbackVertexEveryX];
                    if (i < _vertexColliders.Length) _vertexColliders[i].transform.position = pos;
                }
            }
            else
            {
                if (_dataBufferPos == null) _dataBufferPos = new Vector3[_objBuffers[0].positionsBuffer.count];
                _objBuffers[0].positionsBuffer.GetData(_dataBufferPos);//Warning this is slow, use AsyncGPUReadback

                length = _dataBufferPos.Length / _readbackVertexEveryX;
                if (_vertexColliders == null)
                {
                    _readbackVertexParent = new GameObject("ReadbackVertexData").transform;
                    _vertexColliders = new GameObject[length];
                }

                for (int i = 0; i < length; i++)
                {
                    SetVertexCollider(i);
                    var pos = _dataBufferPos[i * _readbackVertexEveryX];
                    if (i < _vertexColliders.Length) _vertexColliders[i].transform.position = pos;
                }
            }

            if (_vertexColliders[0].transform.localScale.x != scale.x)
                for (int i = 0; i < length; i++)
                    _vertexColliders[i].transform.localScale = scale;

            _runReadbackVertices = false;
        }

        private void SetVertexCollider(int i)
        {
            if (i < _vertexColliders.Length && _vertexColliders[i] == null)
            {
                _vertexColliders[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _vertexColliders[i].transform.localScale *= _readbackVertexScale;
                _vertexColliders[i].transform.parent = _readbackVertexParent;
                Destroy(_vertexColliders[i].GetComponent<Renderer>());
                //_vertexColliders[i].AddComponent<Rigidbody>().isKinematic = true;
            }
        }

        private void SmartDamping()
        {
            if (_centerMassBuffer != null) _centerMassBuffer.Release();
            _centerMassBuffer = new ComputeBuffer(8, sizeof(float) * 4);

            _clothSolver.SetInt(_dispatchDim_x_ID, _numGroups_Vertices);

            _clothSolver.SetBuffer(_computeCenterOfMassKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_computeCenterOfMassKernel, _velocities_ID, _velocitiesBuffer);
            _clothSolver.SetBuffer(_computeCenterOfMassKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.Dispatch(_computeCenterOfMassKernel, _numGroups_Vertices, 1, 1);

            _clothSolver.SetBuffer(_finishCenterOfMassKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.Dispatch(_finishCenterOfMassKernel, 1, 1, 1);

            _clothSolver.SetBuffer(_sumAllMassAndMatrixKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.SetBuffer(_sumAllMassAndMatrixKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_sumAllMassAndMatrixKernel, _velocities_ID, _velocitiesBuffer);
            _clothSolver.Dispatch(_sumAllMassAndMatrixKernel, _numGroups_Vertices, 1, 1);

            _clothSolver.SetBuffer(_finishMatrixCalcKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.Dispatch(_finishMatrixCalcKernel, 1, 1, 1);

            _clothSolver.SetFloat(_dampingStiffness_ID, _dampingStiffness * 0.01f);
            _clothSolver.SetBuffer(_applyBackIntoVelocitiesKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.SetBuffer(_applyBackIntoVelocitiesKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_applyBackIntoVelocitiesKernel, _velocities_ID, _velocitiesBuffer);
            _clothSolver.Dispatch(_applyBackIntoVelocitiesKernel, _numGroups_Vertices, 1, 1);

            _clothSolver.SetBuffer(_clearCenterOfMassKernel, _centerMassBuffer_ID, _centerMassBuffer);
            _clothSolver.Dispatch(_clearCenterOfMassKernel, _centerMassBuffer.count, 1, 1);
        }

        private void DispatchVoxelCubeCenter(int numParticles, RenderTexture gridCenterBuffer, ComputeBuffer positionsBuffer, ComputeBuffer deltaPosUIntBuffer)
        {
            int dispatchDim_x = numParticles.GetComputeShaderThreads(512);
            _clothSolver.SetInt(_dispatchDim_x_ID, dispatchDim_x);
            _clothSolver.SetInt(_numParticles_ID, numParticles);
            int kernel = _calcVoxelPosRoughly ? _calcCubeCenterFastKernel : _calcCubeCenterKernel;
            _clothSolver.SetBuffer(kernel, _positions_ID, positionsBuffer);
            if (_calcVoxelPosRoughly)
                //_clothSolver.SetBuffer(kernel, _gridCenterBuffer_ID, gridCenterBuffer);
                _clothSolver.SetTexture(kernel, _gridCenterBuffer_ID, gridCenterBuffer);
            else
                _clothSolver.SetBuffer(kernel, _deltaPosAsIntX_ID, deltaPosUIntBuffer);

            _clothSolver.Dispatch(kernel, dispatchDim_x, 1, 1);

            if (_calcVoxelPosRoughly == false)
            {
                _clothSolver.SetBuffer(_calcCubeCenter2Kernel, _deltaPosAsIntX_ID, deltaPosUIntBuffer);
                //_clothSolver.SetBuffer(calcCubeCenter2Kernel, _gridCenterBuffer_ID, gridCenterBuffer);
                _clothSolver.SetTexture(_calcCubeCenter2Kernel, _gridCenterBuffer_ID, gridCenterBuffer);
                _clothSolver.Dispatch(_calcCubeCenter2Kernel, 1, 1, 1);
            }
        }

        private IEnumerator ReadbackVoxelCubeCenter()
        {
            if (_runReadback) yield break;
            _runReadback = true;

            float4 cPos;

            if (_supportsAsyncGPUReadback)
            {
                var request = UniversalAsyncGPUReadbackRequest.Request(_gridCenterBuffer);
                while (!request.done)
                {
                    if (request.hasError) request = UniversalAsyncGPUReadbackRequest.Request(_gridCenterBuffer);
                    yield return null;
                }
                var dataPos = request.GetData<float4>();
                cPos = dataPos[0];
            }
            else
            {
                //_gridCenterBuffer.GetData(dataPos);
                Color[] dataPos = GetDataFromRT(_gridCenterBuffer);
                cPos = new float4(dataPos[0].r, dataPos[0].g, dataPos[0].b, dataPos[0].a);
            }

            if (!float.IsNaN(cPos.x))
            {
                //var minPos = dataPos[1];
                //var maxPos = dataPos[2];
                //minPos = math.min(minPos, maxPos);
                //maxPos = math.max(minPos, maxPos);
                //if (math.distancesq(minPos.xyz, maxPos.xyz) > float.Epsilon && math.lengthsq(maxPos.xyz - minPos.xyz) < _voxelCubeScale * _voxelCubeScale)
                //{
                //    _minPos = minPos;
                //    _maxPos = maxPos;
                //}
                float3 pos = float3.zero;
                if (_calcVoxelPosRoughly)
                    pos = cPos.xyz / math.max(1.0f, cPos.w) + _gridOffset;
                else
                    pos = cPos.xyz + _gridOffset;
                //var pos = ((float3)dataPos[0].xyz * 0.001f - 1000) / math.max(1.0f, dataPos[0].w) + _gridOffset;
                float voxelSize = _voxelCubeScale / _voxelCubeSteps;
                float scaler = voxelSize;
                _voxelCubePos = (Vector3)math.round(pos / scaler) * scaler;
            }
            else
            {
                //TODO reset and restart everything
                _runSim = false;
            }

            //OnAnimator();

            _runReadback = false;
        }

        internal void GetVoxelCubePosQuick()
        {
            if (_clothSolver == null) _clothSolver = Resources.Load("Shaders/Compute/PBDClothSolver") as ComputeShader;
            var mf = this.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = this.gameObject.AddComponent<MeshFilter>();
                if (this.GetComponent<SkinnedMeshRenderer>())
                    mf.sharedMesh = this.GetComponent<SkinnedMeshRenderer>().sharedMesh;
            }
            if (mf && mf.sharedMesh)
            {
                var copyBounds = mf.sharedMesh.bounds;
                if (Application.isPlaying && _scaleBoundingBox > 0)
                {
                    mf.sharedMesh.RecalculateBounds();
                    var b = mf.sharedMesh.bounds;
                    var maxSize = math.max(b.size.x, math.max(b.size.y, b.size.z));
                    b.size = Vector3.one * maxSize;
                    mf.sharedMesh.bounds = b;
                }

                var mesh = mf.sharedMesh;
                if (mesh)
                {
                    var numParticles = mesh.vertexCount;
                    var vertices = mesh.vertices;
                    var gridCenterBufferCopy = new ComputeBuffer(16, sizeof(float) * 4);
                    var gridCenterBuffer = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    gridCenterBuffer.filterMode = FilterMode.Point;
                    gridCenterBuffer.enableRandomWrite = true;
                    gridCenterBuffer.Create();
                    var positionsBuffer = new ComputeBuffer(numParticles, sizeof(float) * 3);
                    var deltaPosUIntBuffer = new ComputeBuffer(4, sizeof(uint) * 4);

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = this.transform.TransformPoint(vertices[i]);
                    }

                    positionsBuffer.SetData(vertices);

                    _calcCubeCenterKernel = _clothSolver.FindKernel("CalcCubeCenter");
                    _calcCubeCenter2Kernel = _clothSolver.FindKernel("CalcCubeCenter2");
                    _calcCubeCenterFastKernel = _clothSolver.FindKernel("CalcCubeCenterFast");
                    var copyTexToBufferKernel = _clothSolver.FindKernel("CopyTexToBuffer");

                    DispatchVoxelCubeCenter(numParticles, gridCenterBuffer, positionsBuffer, deltaPosUIntBuffer);

                    _clothSolver.SetBuffer(copyTexToBufferKernel, _gridCenterBufferCopy_ID, gridCenterBufferCopy);
                    _clothSolver.SetTexture(copyTexToBufferKernel, _gridCenterBuffer_ID, gridCenterBuffer);
                    _clothSolver.Dispatch(copyTexToBufferKernel, gridCenterBufferCopy.count, 1, 1);

                    float4[] dataPos = new float4[gridCenterBufferCopy.count];
                    gridCenterBufferCopy.GetData(dataPos);
                    //Color[] dataPos = new Color[1];// GetDataFromRT(gridCenterBuffer);

                    var cPos = dataPos[0];// new float4(dataPos[0].r, dataPos[0].g, dataPos[0].b, dataPos[0].a);
                                          //var minPos = dataPos[1];
                                          //var maxPos = dataPos[2];
                                          //minPos = math.min(minPos, maxPos);
                                          //maxPos = math.max(minPos, maxPos);

                    var posNoOffset = cPos.xyz / (_calcVoxelPosRoughly ? (float)math.max(1.0f, cPos.w) : 1.0f);
                    if (!float.IsNaN(cPos.x))
                    {
                        //if (math.distancesq(minPos.xyz, maxPos.xyz) > float.Epsilon && math.lengthsq(maxPos.xyz - minPos.xyz) < _voxelCubeScale * _voxelCubeScale)
                        //{
                        //    _minPos = minPos;
                        //    _maxPos = maxPos;
                        //}
                        var pos = posNoOffset + _gridOffset;
                        float voxelSize = _voxelCubeScale / _voxelCubeSteps;
                        float scaler = voxelSize;
                        _voxelCubePos = _useCustomVoxelCenter ? _customVoxelCenter.position : (Vector3)math.round(pos / scaler) * scaler;

                        var boundVoxelCube = new Bounds(_voxelCubePos, Vector3.one * _voxelCubeScale);
                        var meshBounds = new Bounds(this.transform.TransformPoint(mesh.bounds.center), this.transform.TransformVector(mesh.bounds.size));

                        if (_voxelCubeScale <= 0 || !boundVoxelCube.Contains(meshBounds.min) || !boundVoxelCube.Contains(meshBounds.max))
                        {
                            _gridOffset = (float3)meshBounds.center - posNoOffset;
                            var size = meshBounds.size;
                            var maxSize = math.max(size.x, math.max(size.y, size.z));
                            float multiplyScale = _collisionFinder?._gridCount >= 256 ? 2.6f : 1.6f;
                            if (_voxelCubeScale < maxSize) _voxelCubeScale = multiplyScale * maxSize;
                        }
                    }
                    gridCenterBuffer?.Release();
                    gridCenterBufferCopy.ClearBuffer();
                    positionsBuffer.ClearBuffer();
                    deltaPosUIntBuffer.ClearBuffer();
                }

                mf.sharedMesh.bounds = copyBounds;
            }
            else Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + ": Mesh or MeshFilter are missing!</color>");
        }

        internal static Color[] GetDataFromRT(RenderTexture gridCenterBuffer)
        {
            var texture = new Texture2D(gridCenterBuffer.width, gridCenterBuffer.height, TextureFormat.RGBAFloat, false);
            RenderTexture.active = gridCenterBuffer;
            texture.ReadPixels(new Rect(0, 0, gridCenterBuffer.width, gridCenterBuffer.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            var dataPos = texture.GetPixels(0);
            DestroyImmediate(texture);
            return dataPos;
        }

        private void CalcConnections(Mesh pMesh, ObjectBuffers obj, int index)
        {
            //var pMesh = _mesh;
            Vector3[] vertices = pMesh.vertices;
            int[] faces = pMesh.triangles;
            int lastCount = 0;// verts.Count;
            List<Vector2Int> connectionInfo = new List<Vector2Int>();
            List<int> connectedVerts = new List<int>();
            Dictionary<Vector3, List<int>> dictTris = new Dictionary<Vector3, List<int>>();

            for (int f = 0; f < faces.Length; f += 3)
            {
                if (dictTris.ContainsKey(vertices[faces[f]]))
                {
                    var list = dictTris[vertices[faces[f]]];
                    list.Add(lastCount + faces[f + 1]);
                    list.Add(lastCount + faces[f + 2]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f]], new List<int>(new[] {
                                                lastCount + faces [f + 1],
                                                lastCount + faces [f + 2]
                                            }));
                }
                if (dictTris.ContainsKey(vertices[faces[f + 1]]))
                {
                    var list = dictTris[vertices[faces[f + 1]]];
                    list.Add(lastCount + faces[f + 2]);
                    list.Add(lastCount + faces[f]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f + 1]], new List<int>(new[] {
                                                lastCount + faces [f + 2],
                                                lastCount + faces [f]
                                            }));
                }
                if (dictTris.ContainsKey(vertices[faces[f + 2]]))
                {
                    var list = dictTris[vertices[faces[f + 2]]];
                    list.Add(lastCount + faces[f]);
                    list.Add(lastCount + faces[f + 1]);
                }
                else
                {
                    dictTris.Add(vertices[faces[f + 2]], new List<int>(new[] {
                                                lastCount + faces [f],
                                                lastCount + faces [f + 1]
                                            }));
                }
            }
            int currentNumV = vertices.Length;
            int maxVertexConnection = 0;
            float[] minDistArray = new float[currentNumV];
            float minDistance = float.MaxValue;
            float maxDistance = 0;
            for (int n = 0; n < currentNumV; n++)
            {
                if (!dictTris.ContainsKey(vertices[n])) continue;
                var list = dictTris[vertices[n]];
                int start = connectedVerts.Count;
                float dist = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    connectedVerts.Add(list[i]);
                    float d = Vector3.Distance(vertices[n], vertices[list[i]]);
                    if (n != list[i] || d > float.Epsilon)
                        dist = Mathf.Min(dist, d);
                }
                int end = connectedVerts.Count;
                maxVertexConnection = Mathf.Max(maxVertexConnection, end - start);
                connectionInfo.Add(new Vector2Int(start, end));
                minDistArray[n] = dist;
                minDistance = Mathf.Min(dist, minDistance);
                maxDistance = Mathf.Max(dist, maxDistance);
            }
            //Debug.Log("maxVertexConnection: " + maxVertexConnection);
            //Debug.Log("minDistance: " + minDistance + " maxDistance: " + maxDistance);

            obj.connectionInfoBuffer = new ComputeBuffer(connectionInfo.Count, sizeof(int) * 2);
            obj.connectionInfoBuffer.SetData(connectionInfo.ToArray());
            obj.connectedVertsBuffer = new ComputeBuffer(connectedVerts.Count, sizeof(int));
            obj.connectedVertsBuffer.SetData(connectedVerts.ToArray());

            obj.normalsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            obj.normalsBuffer.SetData(pMesh.normals);
            obj.positionsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            obj.positionsBuffer.SetData(vertices);

            //var _colorCache = new int[64];
            //var _colorsBuffer = new int[vertices.Length];
            //int maxValue = 0;
            //for (int n = 0; n < currentNumV; n++)
            //{
            //    for (int j = 0; j < 64; ++j)
            //        _colorCache[j] = 0;
            //    int pointColor = _colorsBuffer[n];
            //    Vector2Int info = connectionInfo[n];
            //    int start = info.x;
            //    int end = info.y;
            //    for (int c = start; c < end; ++c)
            //    {
            //        int neighbourColor = _colorsBuffer[connectedVerts[c]];
            //        _colorCache[neighbourColor] = 1;
            //        if (pointColor == neighbourColor)
            //        {
            //            for (int k = 0; k < 64; ++k)
            //            {
            //                if (_colorCache[k] > 0)
            //                {
            //                    pointColor = k + 1;
            //                    pointColor = pointColor % 64;
            //                }
            //                else
            //                {
            //                    pointColor = k;
            //                    break;
            //                }
            //            }
            //        }
            //    }
            //    maxValue = math.max(maxValue, pointColor);
            //    _colorsBuffer[n] = pointColor;
            //}
            //_mapIndices = new Vector2Int[maxValue + 1];
            //Dictionary<int, int> mapColors = new Dictionary<int, int>();
            //for (int i = 0; i < _colorsBuffer.Length; i++)
            //{
            //    int pointColor = _colorsBuffer[i];
            //    mapColors.Add(i, pointColor);
            //}
            //var mapList = mapColors.OrderBy(x => x.Value).ToList();
            //var listValues = mapList.Select(x => x.Value).ToArray();
            //_colorKeyList = mapList.Select(x => x.Key).ToArray();
            //int lastPointColor = 0;
            //for (int i = 0; i < listValues.Length; i++)
            //{
            //    int pointColor = listValues[i];
            //    if (pointColor != lastPointColor)
            //    {
            //        lastPointColor = pointColor;
            //        _mapIndices[pointColor].y = _mapIndices[pointColor].x = _mapIndices[pointColor - 1].y;
            //    }
            //    _mapIndices[pointColor].y++;
            //}
            //obj.colorsBuffer = new ComputeBuffer(vertices.Length, sizeof(int));
            //obj.colorsBuffer.SetData(_colorKeyList);

            if (index == 0)
            {
                _connectionInfo = connectionInfo;
                _connectedVerts = connectedVerts;

                Vector4[] projectedPosData = new Vector4[vertices.Length];
                for (int n = 0; n < projectedPosData.Length; n++)
                {
                    projectedPosData[n] = vertices[n];
                    projectedPosData[n].w = minDistArray[n] * 0.5f;
                }
                _projectedPositionsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 4);
                _projectedPositionsBuffer.SetData(projectedPosData);
            }
        }

        private void WeldMesh()
        {
            if (_weldVertices && !_weldDone)
            {
                CheckPreCache();

                var vertexCount = _mesh.vertexCount;
                if (!_usePreCache || _preCacheFile == null) { WeldVerticesOld(_mesh, out _); _weldDone = true; }
                if (_usePreCache)
                {
                    PreCacheMeshData(_mesh);
                    if (vertexCount == _mesh.vertexCount) Debug.Log("<color=blue>CD: </color><color=orange>" + this.name + " mesh has the same vertex count after welding! Change the 'Weld Threshold' or disable 'Weld Vertices'!</color>");
                }
                if (_debugMode)
                {
                    _debugTimer.Stop();
                    _debugTimespan = _debugTimer.Elapsed;
                    Debug.Log(String.Format("<color=blue> CD: </color>WeldVertices {0:00}:{1:00}:{2:00}", _debugTimespan.Minutes, _debugTimespan.Seconds, _debugTimespan.Milliseconds / 10));
                    _debugTimer.Restart();
                }
            }
        }

        private void CheckPreCache(bool weld = false)
        {
            if (_usePreCache && _preCacheFile != null)
            {
                var stepCount = PlayerPrefs.GetString("CD_" + this.name + "clothPrefab_positionsCount", "");
                if (!string.IsNullOrEmpty(stepCount))
                {
                    TextAsset asset = _preCacheFile as TextAsset;
                    _bufferPreCache = asset.bytes;

                    var split = stepCount.Split(' ');

                    if (split.Length > 2 && int.TryParse(split[0], out int bStep) && bStep < _bufferPreCache.Length)
                    {
                        int positionsCount = BitConverter.ToInt32(_bufferPreCache, bStep);
                        bool useProxy = _useMeshProxy && _meshProxy != null;
                        if ((int.TryParse(split[1], out int obLength) && (useProxy ? 2 : 1) != obLength) ||
                            (int.TryParse(split[2], out int useGM) && (_useGarmentMesh ? 1 : 0) != useGM) ||
                            ((weld && positionsCount != _mesh.vertexCount) || (!weld && positionsCount == _mesh.vertexCount)))
                        {
                            _preCacheFile = null;
                            if (weld) WeldMesh();
                        }
                    }
                }
            }
        }

        private void PreCacheMeshData(Mesh aMesh)
        {
            var path = Path.Combine(Application.dataPath, "ClothDynamics/Resources/PreCache/");

            if (_preCacheFile == null)//Save
            {
                string file = Path.GetDirectoryName(path) + "/" + name + "_preCache.bytes";
#if UNITY_EDITOR
                _overwritePreCache = true;

                if (!Directory.Exists(path)) Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(file))
                {
                    if (!EditorUtility.DisplayDialog("PreCache data with the same name exists", "Do you want to overwrite the existing data \"" + Path.GetFileName(file) + "\" ?", "Yes", "No"))
                    {
                        _overwritePreCache = false;

                        var assetPath = AssetDatabase.GenerateUniqueAssetPath(file);
                        if (EditorUtility.DisplayDialog("PreCache data new file", "Do you want to create a new file \"" + Path.GetFileName(assetPath) + "\" ?", "Yes", "No"))
                        {
                            _overwritePreCache = true;
                            file = assetPath;
                        }
                    }
                }
#endif
                if (_overwritePreCache)
                {
                    Debug.Log("<color=blue>CD: </color>write data to " + file);

                    var verts = aMesh.vertices;
                    var tris = aMesh.triangles;
                    var normals = aMesh.normals;
                    var uv = aMesh.uv;

                    if (_byteDataPreCache == null) _byteDataPreCache = new List<byte>();
                    _byteDataPreCache.Clear();

                    byte[] trianglesCount = BitConverter.GetBytes(tris.Length);
                    _byteDataPreCache.AddRange(trianglesCount);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(tris[i]));
                    }

                    byte[] verticesCount = BitConverter.GetBytes(verts.Length);
                    _byteDataPreCache.AddRange(verticesCount);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        for (int n = 0; n < 3; n++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(verts[i][n]));
                        }
                    }

                    byte[] normalsCount = BitConverter.GetBytes(normals.Length);
                    _byteDataPreCache.AddRange(normalsCount);
                    for (int i = 0; i < normals.Length; i++)
                    {
                        for (int n = 0; n < 3; n++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(normals[i][n]));
                        }
                    }

                    byte[] uvCount = BitConverter.GetBytes(uv.Length);
                    _byteDataPreCache.AddRange(uvCount);
                    for (int i = 0; i < uv.Length; i++)
                    {
                        for (int n = 0; n < 2; n++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(uv[i][n]));
                        }
                    }

                    //					///WRITE//
                    //					System.IO.FileStream wFile;
                    //					wFile = new FileStream(file, FileMode.Create, FileAccess.Write);
                    //					wFile.Write(_byteDataPreCache.ToArray(), 0, _byteDataPreCache.Count);
                    //					wFile.Close();

                    //#if UNITY_EDITOR
                    //					AssetDatabase.Refresh();
                    //#endif
                }

                //TextAsset newPrefabAsset = Resources.Load("PreCache/" + name + "_preCache") as TextAsset;
                //_clothPreCache = newPrefabAsset;

                //PlayerPrefs.SetString("CD_" + this.name + "clothPrefab", "PreCache/" + name + "_preCache");
                //PlayerPrefs.Save();
            }
            else
            {
                if (_debugMode) Debug.Log("<color=blue>CD: </color>Read mesh preCache " + name);
                ///READ//
                TextAsset asset = Resources.Load("PreCache/" + _preCacheFile.name) as TextAsset;
                _bufferPreCache = asset.bytes;
                _bStepPreCache = 0;

                int trianglesCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache);
                var tris = new int[trianglesCount];
                for (int i = 0; i < trianglesCount; i++)
                {
                    tris[i] = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                }

                var parentLocalScale = Vector3.one;
                if (this.transform.parent != null && this.transform.parent.parent != null) parentLocalScale = this.transform.parent.parent.localScale;

                int vertsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                var verts = new Vector3[vertsCount];
                for (int i = 0; i < vertsCount; i++)
                {
                    verts[i] = new Vector3(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                    if (_scaleCacheByParent) verts[i] = Vector3.Scale(verts[i], parentLocalScale);
                }

                int normalsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                var normals = new Vector3[normalsCount];
                for (int i = 0; i < normalsCount; i++)
                {
                    normals[i] = new Vector3(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                }

                int uvCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                var uv = new Vector2[uvCount];
                for (int i = 0; i < uvCount; i++)
                {
                    uv[i] = new Vector2(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                }

                aMesh.triangles = tris;
                aMesh.vertices = verts;
                aMesh.normals = normals;
                aMesh.uv = uv;

                aMesh.RecalculateBounds();
                //aMesh.RecalculateNormals();
                aMesh.MarkDynamic();
            }
        }

        private void PreCacheClothData(List<Mesh> meshList)
        {
            var path = Path.Combine(Application.dataPath, "ClothDynamics/Resources/PreCache/");

            if (_preCacheFile == null)//Save
            {
                string file = Path.GetDirectoryName(path) + "/" + name + "_preCache.bytes";

                if (!_weldVertices)
                {
#if UNITY_EDITOR
                    _overwritePreCache = true;

                    if (!Directory.Exists(path)) Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (File.Exists(file))
                    {
                        if (!EditorUtility.DisplayDialog("Pre-Cache data with the same name exists", "Do you want to overwrite the existing data \"" + Path.GetFileName(file) + "\" ?", "Yes", "No"))
                        {
                            _overwritePreCache = false;

                            var assetPath = AssetDatabase.GenerateUniqueAssetPath(file);
                            if (EditorUtility.DisplayDialog("PreCache data new file", "Do you want to create a new file \"" + Path.GetFileName(assetPath) + "\" ?", "Yes", "No"))
                            {
                                _overwritePreCache = true;
                                file = assetPath;
                            }
                        }
                    }
#endif
                }
                if (_overwritePreCache)
                {
                    if (!_weldVertices) Debug.Log("<color=blue>CD: </color>write data to " + file);

                    if (_byteDataPreCache == null) _byteDataPreCache = new List<byte>();
                    if (!_weldVertices) _byteDataPreCache.Clear();

#if UNITY_EDITOR
                    PlayerPrefs.SetString("CD_" + this.name + "clothPrefab_positionsCount", _byteDataPreCache.Count.ToString() + " " + _objBuffers.Length + " " + (_useGarmentMesh ? 1 : 0));
                    PlayerPrefs.Save();
#endif
                    byte[] positionsCount = BitConverter.GetBytes(_positions.Length);
                    _byteDataPreCache.AddRange(positionsCount);
                    for (int i = 0; i < _positions.Length; i++)
                    {
                        for (int n = 0; n < 3; n++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(_positions[i][n]));
                        }
                    }

                    byte[] velocitiesCount = BitConverter.GetBytes(_velocities.Length);
                    _byteDataPreCache.AddRange(velocitiesCount);
                    for (int i = 0; i < _velocities.Length; i++)
                    {
                        for (int n = 0; n < 4; n++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(_velocities[i][n]));
                        }
                    }

                    byte[] frictionsCount = BitConverter.GetBytes(_frictions.Length);
                    _byteDataPreCache.AddRange(frictionsCount);
                    for (int i = 0; i < _frictions.Length; i++)
                    {
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_frictions[i]));

                    }

                    for (int j = 0; j < _objBuffers.Length; j++)
                    {
                        Mesh pMesh = meshList[j];
                        ObjectBuffers obj = _objBuffers[j];
                        int index = j;

                        Vector2Int[] connectionInfo = new Vector2Int[obj.connectionInfoBuffer.count];
                        obj.connectionInfoBuffer.GetData(connectionInfo);

                        int[] connectedVerts = new int[obj.connectedVertsBuffer.count];
                        obj.connectedVertsBuffer.GetData(connectedVerts);

                        byte[] connectionInfoCount = BitConverter.GetBytes(connectionInfo.Length);
                        _byteDataPreCache.AddRange(connectionInfoCount);
                        for (int i = 0; i < connectionInfo.Length; i++)
                        {
                            for (int n = 0; n < 2; n++)
                            {
                                _byteDataPreCache.AddRange(BitConverter.GetBytes(connectionInfo[i][n]));
                            }
                        }

                        byte[] connectedVertsCount = BitConverter.GetBytes(connectedVerts.Length);
                        _byteDataPreCache.AddRange(connectedVertsCount);
                        for (int i = 0; i < connectedVerts.Length; i++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(connectedVerts[i]));
                        }

                        if (index == 0)
                        {
                            Vector4[] projectedPosData = new Vector4[_projectedPositionsBuffer.count];
                            _projectedPositionsBuffer.GetData(projectedPosData);

                            byte[] projectedPosDataCount = BitConverter.GetBytes(projectedPosData.Length);
                            _byteDataPreCache.AddRange(projectedPosDataCount);
                            for (int i = 0; i < projectedPosData.Length; i++)
                            {
                                for (int n = 0; n < 4; n++)
                                {
                                    _byteDataPreCache.AddRange(BitConverter.GetBytes(projectedPosData[i][n]));
                                }
                            }
                        }
                    }

                    if (_useGarmentMesh)
                    {
                        byte[] colorsCount = BitConverter.GetBytes(_colors.Length);
                        _byteDataPreCache.AddRange(colorsCount);
                        for (int i = 0; i < _colors.Length; i++)
                        {
                            for (int n = 0; n < 4; n++)
                            {
                                _byteDataPreCache.AddRange(BitConverter.GetBytes(_colors[i][n]));
                            }
                        }
                    }
                    else _byteDataPreCache.AddRange(BitConverter.GetBytes(0));

                    byte[] distanceConstraintsCount = BitConverter.GetBytes(_distanceConstraints.Length);
                    _byteDataPreCache.AddRange(distanceConstraintsCount);
                    for (int i = 0; i < _distanceConstraints.Length; i++)
                    {
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_distanceConstraints[i].edge.startIndex));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_distanceConstraints[i].edge.endIndex));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_distanceConstraints[i].restLength));
                    }

                    byte[] bendingConstraintsCount = BitConverter.GetBytes(_bendingConstraints.Length);
                    _byteDataPreCache.AddRange(bendingConstraintsCount);
                    for (int i = 0; i < _bendingConstraints.Length; i++)
                    {
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_bendingConstraints[i].index0));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_bendingConstraints[i].index1));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_bendingConstraints[i].index2));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_bendingConstraints[i].index3));
                        _byteDataPreCache.AddRange(BitConverter.GetBytes(_bendingConstraints[i].restAngle));
                    }

                    if (_duplicateVerticesBuffer != null)
                    {
                        var newDupVerts = new int[_duplicateVerticesBuffer.count * 2];
                        _duplicateVerticesBuffer.GetData(newDupVerts);

                        byte[] newDupVertsCount = BitConverter.GetBytes(newDupVerts.Length);
                        _byteDataPreCache.AddRange(newDupVertsCount);
                        for (int i = 0; i < newDupVerts.Length; i++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(newDupVerts[i]));
                        }
                    }
                    else _byteDataPreCache.AddRange(BitConverter.GetBytes(0));

                    if (_showWeldEdges)
                    {
                        byte[] weldEdgesCount = BitConverter.GetBytes(_weldEdges.Count);
                        _byteDataPreCache.AddRange(weldEdgesCount);
                        int count = _weldEdges.Count;
                        for (int i = 0; i < count; i++)
                        {
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(_weldEdges[i].startIndex));
                            _byteDataPreCache.AddRange(BitConverter.GetBytes(_weldEdges[i].endIndex));
                        }
                    }
                    else _byteDataPreCache.AddRange(BitConverter.GetBytes(0));

                    ///WRITE//
                    System.IO.FileStream wFile;
                    wFile = new FileStream(file, FileMode.Create, FileAccess.Write);
                    wFile.Write(_byteDataPreCache.ToArray(), 0, _byteDataPreCache.Count);
                    wFile.Close();

#if UNITY_EDITOR
                    AssetDatabase.Refresh();
#endif
                }

                TextAsset newPrefabAsset = Resources.Load("PreCache/" + Path.GetFileNameWithoutExtension(file)) as TextAsset;
                _preCacheFile = newPrefabAsset;

                PlayerPrefs.SetString("CD_" + this.name + "clothPrefab", "PreCache/" + Path.GetFileNameWithoutExtension(file));
                PlayerPrefs.Save();
            }
            else
            {
                if (_debugMode) Debug.Log("<color=blue>CD: </color>Read cloth preCache " + name);
                ///READ//
                if (!_weldVertices)
                {
                    TextAsset asset = Resources.Load("PreCache/" + _preCacheFile.name) as TextAsset;
                    _bufferPreCache = asset.bytes;
                    _bStepPreCache = -4;
                }
                if (_bufferPreCache == null)
                {
                    _usePreCache = false;
                    return;
                }

                var parentLocalScale = Vector3.one;
                if (this.transform.parent != null && this.transform.parent.parent != null) parentLocalScale = this.transform.parent.parent.localScale;

                int positionsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                //#if UNITY_EDITOR
                //				PlayerPrefs.SetString("CD_" + this.name + "clothPrefab_positionsCount", _bStepPreCache);
                //				PlayerPrefs.Save();
                //#endif
                for (int i = 0; i < positionsCount; i++)
                {
                    _positions[i] = new Vector3(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                    if (_scaleCacheByParent) _positions[i] = Vector3.Scale(_positions[i], parentLocalScale);
                }

                int velocitiesCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                for (int i = 0; i < velocitiesCount; i++)
                {
                    _velocities[i] = new Vector4(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                }

                int frictionsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                for (int i = 0; i < frictionsCount; i++)
                {
                    _frictions[i] = BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4);
                }

                for (int j = 0; j < _objBuffers.Length; j++)
                {
                    Mesh pMesh = meshList[j];
                    ObjectBuffers obj = _objBuffers[j];
                    int index = j;

                    int connectionInfoCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    Vector2Int[] connectionInfo = new Vector2Int[connectionInfoCount];
                    for (int i = 0; i < connectionInfoCount; i++)
                    {
                        connectionInfo[i] = new Vector2Int(BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4));
                    }
                    int connectedVertsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    int[] connectedVerts = new int[connectedVertsCount];
                    for (int i = 0; i < connectedVertsCount; i++)
                    {
                        connectedVerts[i] = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    }

                    obj.connectionInfoBuffer = new ComputeBuffer(connectionInfoCount, sizeof(int) * 2);
                    obj.connectionInfoBuffer.SetData(connectionInfo);
                    obj.connectedVertsBuffer = new ComputeBuffer(connectedVertsCount, sizeof(int));
                    obj.connectedVertsBuffer.SetData(connectedVerts);

                    Vector3[] vertices = pMesh.vertices;
                    obj.normalsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                    obj.normalsBuffer.SetData(pMesh.normals);
                    obj.positionsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                    obj.positionsBuffer.SetData(vertices);

                    if (index == 0)
                    {
                        _connectionInfo = connectionInfo.ToList();
                        _connectedVerts = connectedVerts.ToList();

                        int projectedPosDataCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                        Vector4[] projectedPosData = new Vector4[projectedPosDataCount];
                        for (int i = 0; i < projectedPosDataCount; i++)
                        {
                            projectedPosData[i] = new Vector4(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                            if (_scaleCacheByParent)
                            {
                                Vector4 scale = parentLocalScale;
                                scale.w = 1;
                                projectedPosData[i] = Vector4.Scale(projectedPosData[i], scale);
                            }
                        }
                        _projectedPositionsBuffer = new ComputeBuffer(projectedPosDataCount, sizeof(float) * 4);
                        _projectedPositionsBuffer.SetData(projectedPosData);
                    }

                }

                int colorsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                if (_useGarmentMesh)
                {
                    for (int i = 0; i < colorsCount; i++)
                    {
                        _colors[i] = new Color(BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4));
                    }
                }

                int distanceConstraintsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                _numDistanceConstraints = distanceConstraintsCount;
                _distanceConstraints = new DistanceConstraintStruct[distanceConstraintsCount];
                for (int i = 0; i < distanceConstraintsCount; i++)
                {
                    _distanceConstraints[i].edge.startIndex = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _distanceConstraints[i].edge.endIndex = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _distanceConstraints[i].restLength = BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4);
                    if (_scaleCacheByParent)
                        _distanceConstraints[i].restLength = _distanceConstraints[i].restLength * parentLocalScale.y;//Normally Y is better than mag
                }

                int bendingConstraintsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                _numBendingConstraints = bendingConstraintsCount;
                _bendingConstraints = new BendingConstraintStruct[bendingConstraintsCount];
                for (int i = 0; i < bendingConstraintsCount; i++)
                {
                    _bendingConstraints[i].index0 = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _bendingConstraints[i].index1 = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _bendingConstraints[i].index2 = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _bendingConstraints[i].index3 = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    _bendingConstraints[i].restAngle = BitConverter.ToSingle(_bufferPreCache, _bStepPreCache += 4);
                }

                int newDupVertsCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                if (newDupVertsCount > 0)
                {
                    var newDupVerts = new int[newDupVertsCount];
                    for (int i = 0; i < newDupVertsCount; i++)
                    {
                        newDupVerts[i] = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                    }
                    _duplicateVerticesBuffer = new ComputeBuffer(newDupVertsCount / 2, sizeof(int) * 2);
                    _duplicateVerticesBuffer.SetData(newDupVerts);
                }

                int weldEdgesCount = BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4);
                if (_showWeldEdges)
                {
                    _weldEdges = new List<Edge>();
                    for (int i = 0; i < weldEdgesCount; i++)
                    {
                        _weldEdges.Add(new Edge(BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4), BitConverter.ToInt32(_bufferPreCache, _bStepPreCache += 4)));
                    }
                }
            }
        }

        private void SetupSkinningHD()
        {
            if (!_useMeshProxy || _meshProxy == null) return;

            Matrix4x4 initTransform = Matrix4x4.identity;// _meshProxy.transform.localToWorldMatrix;

            //var wpos = _positions;
            //for (int i = 0; i < _numParticles; i++)
            //{
            //	wpos[i] = transform.TransformPoint(wpos[i]);
            //}

            List<Vector3> positionVec = new List<Vector3>(_mesh.vertices);
            List<int> tris = new List<int>(_mesh.triangles);

            var dictTris = new Dictionary<Vector3, List<int>>();//dictTris.Clear ();
            for (int f = 0; f < tris.Count; f += 3)
            {
                if (dictTris.ContainsKey(positionVec[tris[f]]))
                {
                    var list = dictTris[positionVec[tris[f]]];
                    list.Add(tris[f + 1]);
                    list.Add(tris[f + 2]);
                }
                else
                {
                    dictTris.Add(positionVec[tris[f]], new List<int>(new[] {
                                            tris [f + 1],
                                            tris [f + 2]
                                        }));
                }
                if (dictTris.ContainsKey(positionVec[tris[f + 1]]))
                {
                    var list = dictTris[positionVec[tris[f + 1]]];
                    list.Add(tris[f + 2]);
                    list.Add(tris[f]);
                }
                else
                {
                    dictTris.Add(positionVec[tris[f + 1]], new List<int>(new[] {
                                            tris [f + 2],
                                            tris [f]
                                        }));
                }
                if (dictTris.ContainsKey(positionVec[tris[f + 2]]))
                {
                    var list = dictTris[positionVec[tris[f + 2]]];
                    list.Add(tris[f]);
                    list.Add(tris[f + 1]);
                }
                else
                {
                    dictTris.Add(positionVec[tris[f + 2]], new List<int>(new[] {
                                            tris [f],
                                            tris [f + 1]
                                        }));
                }
            }

            List<Vector2Int> connectionInfoTet = new List<Vector2Int>();
            List<int> connectedVertsTet = new List<int>();

            int maxVertexConnectionLow = 0;
            for (int n = 0; n < positionVec.Count; n++)
            {
                int start = connectedVertsTet.Count;
                if (dictTris.ContainsKey(positionVec[n]))
                {
                    var list = dictTris[positionVec[n]];
                    for (int i = 0; i < list.Count; i++)
                    {
                        connectedVertsTet.Add(list[i]);
                    }
                }
                int end = connectedVertsTet.Count;
                maxVertexConnectionLow = Mathf.Max(maxVertexConnectionLow, end - start);
                connectionInfoTet.Add(new Vector2Int(start, end));
            }

            if (_debugMode) Debug.Log("<color=blue>CD: </color>positionVec: " + positionVec.Count);

            _connectionInfoTetBuffer = new ComputeBuffer(connectionInfoTet.Count, sizeof(int) * 2);
            _connectionInfoTetBuffer.SetData(connectionInfoTet.ToArray());
            _connectedVertsTetBuffer = new ComputeBuffer(connectedVertsTet.Count, sizeof(int));
            _connectedVertsTetBuffer.SetData(connectedVertsTet.ToArray());

            Mesh meshHD = _meshProxy.GetComponent<MeshFilter>()?.mesh;
            if (meshHD == null) { Debug.Log("<color=blue>CD: </color><color=orange>Missing mesh data in proxy object " + _meshProxy.name + "!</color>"); return; }

            SetSecondUVsForVertexID(meshHD);

            var name = this.transform.name + "_" + _meshProxy.name;
            var path = Path.Combine(Application.dataPath, "ClothDynamics/Resources/");

            var verts = meshHD.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = transform.InverseTransformPoint(_meshProxy.transform.TransformPoint(verts[i]));
            }
            FindControls(_debugMode, this.name + _meshProxy.name, out _vertexBlendsBuffer, out _bonesStartMatrixBuffer, verts, meshHD.vertexCount, positionVec, tris, connectionInfoTet, connectedVertsTet, _weightsCurve._curve, 4, _weightsToleranceDistance, _scaleWeighting, initTransform, _skinPrefab, name, path, _minRadius);

            _startVertexBuffer = new ComputeBuffer(meshHD.vertexCount, sizeof(float) * 3);
            _startVertexBuffer.SetData(meshHD.vertices);

            //var smr = _meshProxy.GetComponent<SkinnedMeshRenderer>();
            //SetClothShader(smr);

            var mr = _meshProxy.GetComponent<MeshRenderer>();
            SetClothShader(mr);

            _mpbHD = new MaterialPropertyBlock();

            int maxVerts = _objBuffers[1].positionsBuffer.count;
            _numGroups_VerticesHD = maxVerts.GetComputeShaderThreads(_workGroupSize);
            _clothSolver.SetInt(_maxVerts_ID, maxVerts);
            _clothSolver.SetBuffer(_skinningHDKernel, _positions_ID, _objBuffers[0].positionsBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_vertexBufferHD", _objBuffers[1].positionsBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_startVertexBuffer", _startVertexBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_bonesStartMatrixBuffer", _bonesStartMatrixBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_vertexBlendsBuffer", _vertexBlendsBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_connectionInfoTetBuffer", _connectionInfoTetBuffer);
            _clothSolver.SetBuffer(_skinningHDKernel, "_connectedVertsTetBuffer", _connectedVertsTetBuffer);

            _mr.enabled = false;
        }

        private void ComputeSkinningHD()
        {
            if (!_useMeshProxy || _meshProxy == null) return;

            int maxVerts = _objBuffers[1].positionsBuffer.count;
            _clothSolver.SetInt(_maxVerts_ID, maxVerts);
            _clothSolver.SetMatrix(_worldToLocalMatrixHD_ID, _meshProxy.transform.worldToLocalMatrix);//TODO set in skin shader?
            _clothSolver.Dispatch(_skinningHDKernel, _numGroups_VerticesHD, 1, 1);

            ComputeNormals(1);
            _mpbHD.SetBuffer(_mpb_normalsBuffer_ID, _objBuffers[1].normalsBuffer);
            _mpbHD.SetBuffer(_mpb_positionsBuffer_ID, _objBuffers[1].positionsBuffer);
            Renderer mr = _meshProxy.GetComponent<MeshRenderer>();
            //if(mr == null) mr = _meshProxy.GetComponent<SkinnedMeshRenderer>();
            if (mr != null) mr.SetPropertyBlock(_mpbHD);
        }

        static internal void FindControls(bool debugMode, string fileName, out ComputeBuffer vertexBlendsBuffer, out ComputeBuffer bonesStartMatrixBuffer, Vector3[] verts, int numRealVertices, List<Vector3> positions, List<int> tris, List<Vector2Int> connectionInfoTet, List<int> connectedVertsTet,
                   AnimationCurve weightsCurve, int searchNum, float weightsToleranceDistance, float scaleWeighting, Matrix4x4 initTransMat, UnityEngine.Object skinPrefab, string name, string path, float minRadius)
        {
            List<Blends> vertexBlends = new List<Blends>();
            List<Matrix4x4> bonesStartMatrix = new List<Matrix4x4>();

            if (skinPrefab != null)
            {
                ///READ//
                TextAsset asset = Resources.Load(skinPrefab.name) as TextAsset;
                if (asset != null)
                {
                    byte[] buffer = asset.bytes;
                    int trisCount = BitConverter.ToInt32(buffer, 0);
                    int vertsCount = BitConverter.ToInt32(buffer, 4);
                    //Debug.Log("trisCount " + trisCount);
                    //Debug.Log("tris.Count " + tris.Count);
                    if (trisCount != tris.Count || vertsCount != positions.Count)
                    {
                        skinPrefab = null;
                    }
                }
                else
                {
                    skinPrefab = null;
                }
            }

            if (skinPrefab == null)
            {
#if UNITY_EDITOR
                int lod = 0; //TODO?
                UnityEditor.EditorUtility.DisplayProgressBar("Create Skinning Files", "Writing Skinning Files: " + name + " ...", 0.25f + lod / 10.0f);
#endif

                //if (true) { 
                Dictionary<Vector3, int> checkDoubles = new Dictionary<Vector3, int>();
                KDTree tree = new KDTree(3);
                for (int i = 0; i < tris.Count; i++)
                {//tris to get only surface vertices (no verts inside the volume)
                    if (!checkDoubles.ContainsKey(positions[tris[i]]))
                    {
                        checkDoubles.Add(positions[tris[i]], tris[i]);
                        tree.insert(new double[] {
                                    positions [tris [i]].x,
                                    positions [tris [i]].y,
                                    positions [tris [i]].z
                                }, tris[i]);
                    }
                }

                HashSet<int> posCheck = new HashSet<int>();

                var keys = checkDoubles.Keys.ToArray();
                //var values = checkDoubles.Values.ToArray();
                for (int i = keys.Length - 1; i >= 0; --i)
                {
                    var vec = keys[i];
                    posCheck.Clear();
                    tree.nearestAddNbrs(new double[] {
                                            vec.x,
                                            vec.y,
                                            vec.z
                                        }, 16, ref posCheck);

                    for (int n = 0; n < posCheck.Count; n++)
                    {
                        int num = posCheck.ElementAt(n);
                        var vec2 = positions[num];
                        if (vec.x != vec2.x && vec.y != vec2.y && vec.z != vec2.z)
                        {
                            //if(values[i] != num)
                            if ((vec2 * 100 - vec * 100).sqrMagnitude < weightsToleranceDistance && checkDoubles.ContainsKey(vec2))
                                checkDoubles.Remove(vec2);
                        }
                    }
                }
                tree = null;
                tree = new KDTree(3);
                var keys2 = checkDoubles.Keys.ToArray();
                for (int i = 0; i < keys2.Length; i++)
                {
                    tree.insert(new double[] {
                                            keys2 [i].x,
                                            keys2 [i].y,
                                            keys2 [i].z
                                        }, checkDoubles[keys2[i]]);
                }
                checkDoubles.Clear();
                //visTest = keys2;
                if (debugMode) Debug.Log("<color=blue>CD: </color>skinTreeNodes " + tree.m_count);
                for (int i = 0; i < positions.Count; i++)
                {
                    //radiusTet.Add (0);
                    bonesStartMatrix.Add(Matrix4x4.identity);
                }

                for (int i = 0; i < numRealVertices; i++)
                {
                    posCheck.Clear();
                    tree.nearestAddNbrs(new double[] {
                            verts[i].x,
                            verts[i].y,
                            verts[i].z
                        }, searchNum, ref posCheck);

                    int checkCount = posCheck.Count;
                    if (debugMode && checkCount < 4) Debug.Log("checkCount < 4");
                    int[] index = new int[checkCount];
                    float[] sqrMaxRadius = new float[checkCount];
                    for (int n = 0; n < checkCount; n++)
                        sqrMaxRadius[n] = Mathf.Max(float.Epsilon, minRadius);// float.PositiveInfinity;
                    for (int n = 0; n < checkCount; n++)
                    {
                        int num = index[n] = posCheck.ElementAt(n);
                        int start = connectionInfoTet[num].x;
                        int end = connectionInfoTet[num].y;
                        for (int j = start; j < end; j++)
                        {
                            if (connectedVertsTet[j] == num) continue;
                            float dist = (positions[connectedVertsTet[j]] - positions[num]).sqrMagnitude;
                            sqrMaxRadius[n] = Mathf.Max(sqrMaxRadius[n], dist * scaleWeighting);//dist*60);
                        }
                    }

                    List<KeyValuePair<int, float>> orderWeights = new List<KeyValuePair<int, float>>();
                    for (int n = 0; n < checkCount; n++)
                    {
                        int num = index[n];
                        float dist = (positions[num] - verts[i]).sqrMagnitude;
                        if (dist <= sqrMaxRadius[n])
                            orderWeights.Add(new KeyValuePair<int, float>(num, dist / sqrMaxRadius[n]));

                        //radiusTet [num] = Mathf.Sqrt (sqrMaxRadius [n]);
                    }
                    orderWeights = orderWeights.OrderBy(x => x.Value).ToList();
                    if (orderWeights.Count > 3)
                        orderWeights.RemoveRange(3, orderWeights.Count - 3);
                    if (orderWeights.Count > 4)
                        if (debugMode) Debug.Log("<color=blue>CD: </color>orderWeights.Count > 4");
                    if (orderWeights.Count < 1)
                    {
                        orderWeights.Add(new KeyValuePair<int, float>(index[0], 1));
                        if (debugMode) Debug.Log("<color=blue>CD: </color>orderWeights.Count < 1");
                    }

                    float[] weights = new float[4];
                    float[] indices = new float[4];
                    float sumWeight = 0;
                    for (int n = 0; n < 4; n++)
                    {
                        indices[n] = 0;
                        weights[n] = 0;
                        if (n < orderWeights.Count)
                        {
                            indices[n] = orderWeights[n].Key;
                            weights[n] = 1 - Mathf.Clamp01(weightsCurve.Evaluate(orderWeights[n].Value));
                        }
                        sumWeight += weights[n];
                    }
                    float sumWeight2 = 0;
                    if (sumWeight > 0)
                    {
                        for (int n = 0; n < 4; n++)
                        {
                            weights[n] = weights[n] / sumWeight;
                            //weights [n] = weightsCurve.Evaluate (weights [n]);
                            sumWeight2 += weights[n];
                        }
                    }
                    if (sumWeight2 > 0)
                    {
                        for (int n = 0; n < 4; n++)
                        {
                            weights[n] = weights[n] / sumWeight2;
                        }
                    }
                    else
                          if (debugMode) Debug.Log("<color=blue>CD: </color>sumWeight2 == 0");

                    if (weights[0] == 0.0f && weights[1] == 0.0f && weights[2] == 0.0f && weights[3] == 0.0f)
                        if (debugMode) Debug.Log("<color=blue>CD: </color>weights == 0");

                    if (weights[0] + weights[1] + weights[2] + weights[3] < 0.999f)
                        if (debugMode) Debug.Log("<color=blue>CD: </color>weights != 1");

                    Blends blend = new Blends();
                    blend.bones = new Vector4(indices[0], indices[1], indices[2], indices[3]);
                    blend.weights = new Vector4(weights[0], weights[1], weights[2], weights[3]);
                    //blend.weights[0] = blend.weights[0] == 1 ? 0.0f : blend.weights[0];

                    vertexBlends.Add(blend);
                    //if(i == 21161)
                    //Debug.Log("temp");
                    for (int n = 0; n < 4; n++)
                    {
                        if (weights[n] > 0)
                        {
                            int num = (int)indices[n];
                            int start = connectionInfoTet[num].x;
                            int end = connectionInfoTet[num].y - 1;
                            Vector3 normal = Vector3.zero;

                            for (int j = start; j < end; j += 2)
                            {
                                var a = positions[connectedVertsTet[j]] * 100 - positions[num] * 100;
                                var b = positions[connectedVertsTet[j + 1]] * 100 - positions[num] * 100;
                                normal += Vector3.Cross(a, b);
                            }
                            int neighbour = connectedVertsTet[start];
                            var right = (positions[neighbour] * 100 - positions[num] * 100).normalized;// right needs to be 90 degree ? to normal so cross!
                            normal = normal.normalized;
                            right = Vector3.Cross(normal, right).normalized;
                            Vector3 up = Vector3.Cross(right, normal).normalized;

                            Matrix4x4 mat = Matrix4x4.identity;
                            mat.SetColumn(0, right);
                            mat.SetColumn(1, up);
                            mat.SetColumn(2, normal);
                            mat.SetColumn(3, new Vector4(positions[num].x, positions[num].y, positions[num].z, 1));

                            bonesStartMatrix[num] = mat.inverse * initTransMat;
                        }
                    }
                }
                string file = Path.GetDirectoryName(path) + "/" + name + "_skin.bytes";

                bool overwrite = false;
#if UNITY_EDITOR
                overwrite = true;

                if (!Directory.Exists(path)) Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(file))
                {
                    if (!EditorUtility.DisplayDialog("Skin data with the same name exists", "Do you want to overwrite the existing skin data \"" + Path.GetFileName(file) + "\" ?", "Yes", "No"))
                    {
                        overwrite = false;

                        var assetPath = AssetDatabase.GenerateUniqueAssetPath(file);
                        if (EditorUtility.DisplayDialog("Skin data new file", "Do you want to create a new file \"" + Path.GetFileName(assetPath) + "\" ?", "Yes", "No"))
                        {
                            overwrite = true;
                            file = assetPath;
                        }
                    }
                }
#endif
                if (overwrite)
                {
                    Debug.Log("<color=blue>CD: </color>write skin data to " + file);
                    List<byte> byteData = new List<byte>();

                    byteData.AddRange(BitConverter.GetBytes(tris.Count));
                    byteData.AddRange(BitConverter.GetBytes(positions.Count));

                    byte[] vertexBlendsCount = BitConverter.GetBytes(vertexBlends.Count);
                    byteData.AddRange(vertexBlendsCount);
                    for (int i = 0; i < vertexBlends.Count; i++)
                    {
                        for (int n = 0; n < 4; n++)
                        {
                            byteData.AddRange(BitConverter.GetBytes(vertexBlends[i].bones[n]));
                        }
                        for (int n = 0; n < 4; n++)
                        {
                            byteData.AddRange(BitConverter.GetBytes(vertexBlends[i].weights[n]));
                        }
                    }
                    byte[] bonesStartMatrixCount = BitConverter.GetBytes(bonesStartMatrix.Count);
                    byteData.AddRange(bonesStartMatrixCount);
                    for (int i = 0; i < bonesStartMatrix.Count; i++)
                    {
                        for (int n = 0; n < 16; n++)
                        {
                            byteData.AddRange(BitConverter.GetBytes(bonesStartMatrix[i][n]));
                        }
                    }
                    ///WRITE//
                    System.IO.FileStream wFile;
                    wFile = new FileStream(file, FileMode.Create, FileAccess.Write);
                    wFile.Write(byteData.ToArray(), 0, byteData.Count);
                    wFile.Close();
#if UNITY_EDITOR
                    AssetDatabase.Refresh();
#endif
                }

                TextAsset newPrefabAsset = Resources.Load(name + "_skin") as TextAsset;
                skinPrefab = newPrefabAsset;

                PlayerPrefs.SetString("CD_" + fileName + "skinPrefab", name + "_skin");
                PlayerPrefs.Save();

            }
            else
            {
                PlayerPrefs.SetString("CD_" + fileName + "skinPrefab", skinPrefab.name);
                PlayerPrefs.Save();

                if (debugMode) Debug.Log("<color=blue>CD: </color>Read skinPrefab " + name);
                ///READ//
                TextAsset asset = Resources.Load(skinPrefab.name) as TextAsset;
                byte[] buffer = asset.bytes;

                int bStep = 8;//skip the first two
                int vertexBlendsCount = BitConverter.ToInt32(buffer, bStep);
                for (int i = 0; i < vertexBlendsCount; i++)
                {
                    Blends blend = new Blends();
                    blend.bones = new Vector4(BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4));
                    blend.weights = new Vector4(BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4), BitConverter.ToSingle(buffer, bStep += 4));
                    vertexBlends.Add(blend);
                }
                int bonesStartMatrixCount = BitConverter.ToInt32(buffer, bStep += 4);
                for (int i = 0; i < bonesStartMatrixCount; i++)
                {
                    Matrix4x4 tempM = Matrix4x4.identity;
                    for (int n = 0; n < 16; n++)
                    {
                        tempM[n] = BitConverter.ToSingle(buffer, bStep += 4);
                    }
                    bonesStartMatrix.Add(tempM);
                }

            }

            bonesStartMatrixBuffer = new ComputeBuffer(bonesStartMatrix.Count, 64);
            bonesStartMatrixBuffer.SetData(bonesStartMatrix.ToArray());

            vertexBlendsBuffer = new ComputeBuffer(vertexBlends.Count, 8 * sizeof(float));
            vertexBlendsBuffer.SetData(vertexBlends.ToArray());
#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        static internal void WeldVerticesOld(Mesh aMesh, float aMaxDelta = 1e-10f)
        {
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            var colors = aMesh.colors;
            List<int> newVerts = new List<int>();
            int[] map = new int[verts.Length];
            // create mapping and filter duplicates.
            int newVertsCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                //var n = normals[i];
                //var uv = uvs[i];
                bool duplicate = false;

                for (int i2 = 0; i2 < newVertsCount; i2++)
                {
                    int a = newVerts[i2];
                    if (
                        (verts[a] - p).sqrMagnitude <= aMaxDelta)
                    {
                        map[i] = i2;
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    map[i] = newVertsCount;// newVerts.Count;
                    newVerts.Add(i);
                    newVertsCount++;
                }
            }
            // create new vertices
            int count = newVertsCount;
            var verts2 = new Vector3[count];
            var normals2 = new Vector3[count];
            var uvs2 = new Vector2[count];
            var colors2 = new Color[count];

            for (int i = 0; i < count; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                if (a < normals.Length)
                    normals2[i] = normals[a];
                if (a < uvs.Length)
                    uvs2[i] = uvs[a];
                if (colors != null && a < colors.Length)
                    colors2[i] = colors[a];
            }
            // map the triangle to the new vertices
            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }
            //aMesh.Clear();
            aMesh.triangles = tris;
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.uv = uvs2;
            if (colors != null && colors.Length == verts.Length)
                aMesh.colors = colors2;

            aMesh.RecalculateBounds();
            //aMesh.RecalculateNormals();
            aMesh.MarkDynamic();
        }

        public static Mesh WeldVerticesOld(Mesh aMesh, out List<int> newVerts)
        {
            //using (var dataArray = Mesh.AcquireReadOnlyMeshData(aMesh))
            //{
            Debug.Log("WeldVertices " + aMesh.name);
            //var data = dataArray[0];
            //var verts = new NativeArray<Vector3>(aMesh.vertexCount, Allocator.TempJob);
            //var normals = new NativeArray<Vector3>(aMesh.vertexCount, Allocator.TempJob);
            //var uvs = new NativeArray<Vector2>(aMesh.vertexCount, Allocator.TempJob);
            //var tris = new NativeArray<int>(aMesh.triangles.Length, Allocator.TempJob);
            //data.GetVertices(verts);
            //data.GetNormals(normals);
            //if (aMesh.uv.Length == aMesh.vertexCount) data.GetUVs(0, uvs);
            //data.GetIndices(tris, 0, true);

            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            var tris = aMesh.triangles;
            var colors = aMesh.colors;

            Dictionary<Vector3, int> duplicateHashTable = new Dictionary<Vector3, int>();
            newVerts = new List<int>();
            int[] map = new int[verts.Length];

            int newVertsCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                if (!duplicateHashTable.ContainsKey(verts[i]))
                {
                    duplicateHashTable.Add(verts[i], newVertsCount);
                    map[i] = newVertsCount;
                    newVerts.Add(i);
                    newVertsCount++;
                }
                else
                {
                    map[i] = duplicateHashTable[verts[i]];
                }
            }

            var verts2 = new Vector3[newVertsCount];
            var normals2 = new Vector3[newVertsCount];
            var uvs2 = new Vector2[newVertsCount];
            var colors2 = new Color[newVertsCount];
            
            for (int i = 0; i < newVertsCount; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                if (a < normals.Length)
                    normals2[i] = normals[a];
                if (a < uvs.Length)
                    uvs2[i] = uvs[a];
                if (a < colors.Length)
                    colors2[i] = colors[a];
            }


            for (int i = 0; i < tris.Length; i++) tris[i] = map[tris[i]];

            aMesh.triangles = tris;
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.uv = uvs2;
            aMesh.colors = colors2;

            //verts.Dispose();
            //normals.Dispose();
            //uvs.Dispose();
            //tris.Dispose();

            aMesh.RecalculateBounds();
            //aMesh.RecalculateNormals();
            //}
            return aMesh;
        }

        public static Mesh WeldVertices(Mesh aMesh, out List<int> newVerts, float aMaxDelta = 1e-10f)
        {
            //var meshBounds = aMesh.bounds;
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            Dictionary<int3, int> duplicateHashTable = new Dictionary<int3, int>();
            newVerts = new List<int>();
            int[] map = new int[verts.Length];

            float3 distanceToleranceSize = 1.0f / aMaxDelta;// (float3)meshBounds.size;

            int newVertsCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var iVertex = (int3)((float3)verts[i] * distanceToleranceSize);
                if (!duplicateHashTable.ContainsKey(iVertex))
                {
                    duplicateHashTable.Add(iVertex, newVertsCount);
                    map[i] = newVertsCount;
                    newVerts.Add(i);
                    newVertsCount++;
                }
                else
                {
                    map[i] = duplicateHashTable[iVertex];
                }
            }

            var verts2 = new Vector3[newVertsCount];
            var normals2 = new Vector3[newVertsCount];
            var uvs2 = new Vector2[newVertsCount];
            for (int i = 0; i < newVertsCount; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                if (a < normals.Length)
                    normals2[i] = normals[a];
                if (a < uvs.Length)
                    uvs2[i] = uvs[a];
            }

            var tris = aMesh.triangles;
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = map[tris[i]];
            }
            aMesh.triangles = tris;
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.uv = uvs2;

            aMesh.RecalculateBounds();
            //aMesh.RecalculateNormals();

            return aMesh;
        }
        public static Mesh WeldVerticesAndTris(Mesh aMesh, out List<int> newVerts, HashSet<int> ignoreList = null, float aMaxDelta = 1e-10f)
        {
            //var meshBounds = aMesh.bounds;
            var verts = aMesh.vertices;
            var normals = aMesh.normals;
            var uvs = aMesh.uv;
            var tris = aMesh.triangles;
            Dictionary<int3, int> duplicateHashTable = new Dictionary<int3, int>();
            newVerts = new List<int>();
            int[] map = new int[verts.Length];

            float3 distanceToleranceSize = 1.0f / aMaxDelta;// (float3)meshBounds.size;
                                                            //float3 minDistanceToleranceSize = 1.0f / 1e-08f;// (float3)meshBounds.size;

            int newVertsCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var ignore = ignoreList == null ? false : ignoreList.Contains(i);
                var iVertex = (int3)((float3)verts[i] * distanceToleranceSize);
                if (ignore || !duplicateHashTable.ContainsKey(iVertex))
                {
                    if (!ignore) duplicateHashTable.Add(iVertex, newVertsCount);
                    map[i] = newVertsCount;
                    newVerts.Add(i);
                    newVertsCount++;
                }
                else
                {
                    map[i] = duplicateHashTable[iVertex];
                }
            }
            var verts2 = new Vector3[newVertsCount];
            var normals2 = new Vector3[newVertsCount];
            var uvs2 = new Vector2[newVertsCount];
            for (int i = 0; i < newVertsCount; i++)
            {
                int a = newVerts[i];
                verts2[i] = verts[a];
                if (a < normals.Length)
                    normals2[i] = normals[a];
                if (a < uvs.Length)
                    uvs2[i] = uvs[a];
            }

            var trisDoubles = new HashSet<int3>();
            var tris2 = new List<int>();
            var faceCount = tris.Length / 3;
            for (int i = 0; i < faceCount; i++)
            {
                var t0 = map[tris[i * 3 + 0]];
                var t1 = map[tris[i * 3 + 1]];
                var t2 = map[tris[i * 3 + 2]];

                if (math.distancesq(verts2[t0], verts2[t1]) < aMaxDelta || math.distancesq(verts2[t1], verts2[t2]) < aMaxDelta || math.distancesq(verts2[t2], verts2[t0]) < aMaxDelta)
                    continue;

                int[] t = new int[] { t0, t1, t2 };
                Array.Sort(t);

                if (trisDoubles.Add(new int3(t[0], t[1], t[2])))
                {
                    tris2.Add(t0);
                    tris2.Add(t1);
                    tris2.Add(t2);
                }
            }
            aMesh.triangles = tris2.ToArray();
            aMesh.vertices = verts2;
            aMesh.normals = normals2;
            aMesh.uv = uvs2;

            //aMesh.RecalculateBounds();
            //aMesh.RecalculateNormals();

            return aMesh;
        }


        static float PlaneDotCoord(Plane pp, Vector3 pv)
        {
            return pp.normal.x * pv.x + pp.normal.y * pv.y + pp.normal.z * pv.z + pp.distance;// + octree.camCullDist;
        }

        static bool SweptSpherePlaneIntersect(out float t0, out float t1, Plane plane, Vector3 pos, float radius, Vector3 sweepDir)
        {
            float b_dot_n = PlaneDotCoord(plane, pos);
            float d_dot_n = -Mathf.Abs(Vector3.Dot(plane.normal, sweepDir));

            t0 = 0.0f;
            t1 = 1e32f;

            //if (d_dot_n == 0.0f)
            //{
            //	if (b_dot_n <= radius)
            //		return true;
            //	else
            //		return false;
            //}
            //else
            //{
            float tmp0 = (radius - b_dot_n) / d_dot_n;
            float tmp1 = (-radius - b_dot_n) / d_dot_n;
            t0 = Mathf.Min(tmp0, tmp1);
            t1 = Mathf.Max(tmp0, tmp1);
            return true;
            //}
        }

        static bool TestSphere(Vector3 pos, float radius, Plane[] camPlanes)
        {

            bool inside = true;
            for (int i = 0; i < 4 && inside; i++)
                inside &= (PlaneDotCoord(camPlanes[i], pos) + radius) >= 0.0f;
            return inside;
        }

        public static bool TestSweptSphere(Vector3 pos, float radius, Vector3 sweepDir, Plane[] planes)
        {
            //  algorithm -- get all 12 intersection points of the swept sphere with the view frustum
            //  for all points > 0, displace sphere along the sweep direction.  if the displaced sphere
            //  is inside the frustum, return true.  else, return false
            int cnt = 0;
            float a, b;
            bool inFrustum = false;

            for (int i = 0; i < 4; i++)
            {
                if (SweptSpherePlaneIntersect(out a, out b, planes[i], pos, radius, sweepDir))
                {
                    if (a >= 0.0f)
                        _displacements[cnt++] = a;
                    if (b >= 0.0f)
                        _displacements[cnt++] = b;
                }
            }

            for (int i = 0; i < cnt; i++)
            {
                Vector3 displPos = pos + sweepDir * _displacements[i];
                float displRadius = radius * 1.1f;
                inFrustum |= TestSphere(displPos, displRadius, planes);
            }
            return inFrustum;
        }

        public Vector4 QuatToVec(Quaternion rot)
        {
            Vector4 rotVec;
            rotVec.x = rot.x;
            rotVec.y = rot.y;
            rotVec.z = rot.z;
            rotVec.w = rot.w;
            return rotVec;
        }

        private Vector3 QuatToVec3(Quaternion q)
        {
            Vector3 v;
            v.x = q.x;
            v.y = q.y;
            v.z = q.z;
            return v;
        }

        private Vector3 DiffPos(Vector3 position, Vector3 prevPosition, float dt)
        {
            return (position - prevPosition) / dt;
        }

        private Vector3 DiffRot(Quaternion rotation, Quaternion prevRotation, float dt)
        {
            return QuatToVec3(rotation * Quaternion.Inverse(prevRotation)) * 2.0f / dt;
        }

        private int InitializeDiffFrames(Vector3 position, Vector3 scale, Quaternion rotation)
        {
            TransformDynamics td = new TransformDynamics();
            td.frame = new TransformDynamics.TransformPerFrame(position, rotation, scale);
            td.prevFrame = td.frame;
            td.velocity = Vector3.zero;
            td.rotVelocity = Vector3.zero;
            td.posAcceleration = Vector3.zero;
            td.rotAcceleration = Vector3.zero;
            _tds.Add(td);
            return _tds.Count - 1;
        }

        private void UpdateDiffFrames(float dt, int tdNum = 0)
        {
            UpdateDiffFrames(this.transform, dt, tdNum, out Vector3 posVel, out Vector3 rotVel);
            _clothSolver.SetVector(_posCloth_ID, this.transform.position);
            _clothSolver.SetVector(_posVel_ID, posVel);
            _clothSolver.SetVector(_rotVel_ID, rotVel);
        }

        private void UpdateDiffFrames(Transform t, float dt, int tdNum, out Vector3 posVel, out Vector3 rotVel)
        {
            var td = _tds[tdNum];

            Vector3 position = t.position;
            Quaternion rotation = t.rotation;
            Vector3 scale = t.lossyScale;

            td.prevFrame = td.frame;
            td.frame.position = position;
            td.frame.rotation = rotation;
            td.frame.scale = scale;

            td.velocity = DiffPos(td.frame.position, td.prevFrame.position, dt);
            td.rotVelocity = DiffRot(td.frame.rotation, td.prevFrame.rotation, dt);

            //Debug.Log(t.name + " velocity:" + td.velocity.x + ", " + td.velocity.y + ", " + td.velocity.z);
            //Debug.Log(t.name + " rotVelocity:" + td.rotVelocity.x + ", " + td.rotVelocity.y + ", " + td.rotVelocity.z);

            posVel = td.velocity;
            rotVel = td.rotVelocity;

            _tds[tdNum] = td;
        }

        private float GetWindTurbulence(float time, float windPulseFrequency, float windPulseMagnitude)
        {
            return Mathf.Clamp(Mathf.Clamp01(Mathf.PerlinNoise(time * windPulseFrequency, 0.0f)) * 2 - 1, -1, 1) * windPulseMagnitude;
        }

        public void ExportMesh(bool useProxy = false)
        {
            ExportMesh(this, _mesh, _meshProxy, int2.zero, _applyTransformAtExport, useProxy, this.transform, _objBuffers);
        }

        public Mesh GetMesh(bool useProxy = false)
        {
            return GetMesh(this, _mesh, _meshProxy, int2.zero, _applyTransformAtExport, useProxy, this.transform, _objBuffers);
        }

        public static void ExportMesh(MonoBehaviour mono, Mesh exportMesh, GameObject meshProxy, int2 bufferRange, bool applyTransformAtExport = false, bool useProxy = false, Transform t = null, ObjectBuffers[] objBuffers = null, ComputeBuffer posBuffer = null, ComputeBuffer normBuffer = null, int version = 1)
        {
            Mesh newMesh = GetMesh(mono, exportMesh, meshProxy, bufferRange, applyTransformAtExport, useProxy, t, objBuffers, posBuffer, normBuffer, version);

            if (newMesh != null)
            {
#if UNITY_EDITOR

                var clothPath = Directory.GetParent(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(mono))));
                if (version == 2) clothPath = clothPath.Parent;
                string path = clothPath + "/Export/Cloth" + (useProxy ? "Proxy" : "") + "Mesh_" + mono.name + ".asset";

                bool exported = false;
                if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

                path = path.Replace('\\', '/');
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                }

                if (System.IO.File.Exists(path))
                {
                    if (EditorUtility.DisplayDialog((useProxy ? "Proxy " : "") + "Mesh Exists", "Do you want to overwrite the existing mesh \"" + Path.GetFileName(path) + "\" ?", "Yes", "No"))
                    {
                        if (AssetDatabase.Contains(newMesh)) { AssetDatabase.SaveAssets(); }
                        else { AssetDatabase.CreateAsset(newMesh, path); }
                        exported = true;
                    }
                }
                else
                {
                    if (AssetDatabase.Contains(newMesh)) { AssetDatabase.SaveAssets(); }
                    else { AssetDatabase.CreateAsset(newMesh, path); }
                    exported = true;
                }

                if (exported)
                {
                    Debug.Log("<color=blue>CD: </color><color=orange>" + mono.name + " exported to " + path + "</color>");
                    //AssetDatabase.SaveAssets();
                }
#endif
            }
        }

        public static Mesh GetMesh(MonoBehaviour mono, Mesh exportMesh, GameObject meshProxy, int2 bufferRange, bool applyTransformAtExport = false, bool useProxy = false, Transform t = null, ObjectBuffers[] objBuffers = null, ComputeBuffer posBuffer = null, ComputeBuffer normBuffer = null, int version = 1)
        {
            Mesh newMesh = useProxy ? null : exportMesh;

            if (newMesh == null || !Application.isPlaying)
            {
                Mesh mesh = null;
                if (useProxy)
                    mesh = meshProxy.GetComponent<MeshFilter>() ? meshProxy.GetComponent<MeshFilter>().sharedMesh : meshProxy.GetComponent<SkinnedMeshRenderer>() ? meshProxy.GetComponent<SkinnedMeshRenderer>().sharedMesh : null;
                else
                    mesh = mono.GetComponent<MeshFilter>() ? mono.GetComponent<MeshFilter>().sharedMesh : mono.GetComponent<SkinnedMeshRenderer>() ? mono.GetComponent<SkinnedMeshRenderer>().sharedMesh : null;

                if (mesh != null)
                {
                    newMesh = new Mesh();
                    newMesh.vertices = mesh.vertices;
                    newMesh.triangles = mesh.triangles;
                    newMesh.normals = mesh.normals;
                    if (mesh.colors != null) newMesh.colors = mesh.colors;
                    if (mesh.tangents != null) newMesh.tangents = mesh.tangents;
                    if (mesh.uv != null) newMesh.uv = mesh.uv;
                    if (mesh.uv2 != null) newMesh.uv2 = mesh.uv2;
                    if (mesh.uv3 != null) newMesh.uv3 = mesh.uv3;
                    if (mesh.uv4 != null) newMesh.uv4 = mesh.uv4;
                    if (mesh.uv5 != null) newMesh.uv5 = mesh.uv5;
                    if (mesh.uv6 != null) newMesh.uv6 = mesh.uv6;
                    if (mesh.uv7 != null) newMesh.uv7 = mesh.uv7;
                    if (mesh.uv8 != null) newMesh.uv8 = mesh.uv8;
                }
            }
            if (newMesh != null)
            {
                Vector3[] positions = newMesh.vertices;
                Vector3[] normals = newMesh.normals;

                if (Application.isPlaying)
                {
                    if (math.lengthsq(bufferRange) > 0 && bufferRange.y == newMesh.vertexCount)
                    {
                        if (posBuffer != null)
                        {
                            positions = new Vector3[newMesh.vertexCount];
                            posBuffer.GetData(positions, 0, bufferRange.x, bufferRange.y);
                        }
                        if (normBuffer != null)
                        {
                            normals = new Vector3[newMesh.vertexCount];
                            normBuffer.GetData(normals, 0, bufferRange.x, bufferRange.y);
                        }
                    }
                    else if (objBuffers != null)
                    {
                        positions = new Vector3[newMesh.vertexCount];
                        normals = new Vector3[newMesh.vertexCount];

                        objBuffers[useProxy && objBuffers.Length > 1 ? 1 : 0].positionsBuffer.GetData(positions);
                        objBuffers[useProxy && objBuffers.Length > 1 ? 1 : 0].normalsBuffer.GetData(normals);
                    }
                }

                var pos = positions;
                if (applyTransformAtExport)
                {
                    for (int i = 0; i < pos.Length; i++)
                    {
                        pos[i] = t.TransformVector(pos[i]) - t.localPosition;
                    }
                }
                newMesh.vertices = pos;
                newMesh.normals = normals;
                //newMesh.RecalculateNormals();
                newMesh.RecalculateBounds();

                //GameObject go = new GameObject("Exported Mesh");
                //go.AddComponent<MeshFilter>().sharedMesh = newMesh;
                //go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));

            }
            return newMesh;
        }



        public bool CheckShaderKeyword(string strKeyword = "#define USE_UNITY_2019 1", bool isOn = true)
        {
#if UNITY_EDITOR
            int line_to_edit = 1; // Warning: 1-based indexing!

            var scriptFile = MonoScript.FromMonoBehaviour(this);
            if (scriptFile == null)
                return true;

            var path = AssetDatabase.GetAssetPath(scriptFile);
            if (string.IsNullOrEmpty(path))
                return true;

            var clothPath = Directory.GetParent(Path.GetDirectoryName(path));
            string filePath = clothPath + "/Resources/Shaders/Compute/PBDClothSolver.compute";

            // Read the appropriate line from the file.
            string lineToWrite = null;
            using (StreamReader reader = new StreamReader(filePath))
            {
                for (int i = 1; i <= line_to_edit; ++i)
                    lineToWrite = reader.ReadLine();
            }
            if (lineToWrite != null)
            {
                if (lineToWrite.Contains("//"))
                    return !isOn;
                else
                    return isOn;
            }
#endif
            return true;
        }

        void SetShaderKeyword(string strKeyword = "#define USE_UNITY_2019 1", bool turnOn = true)
        {

#if UNITY_EDITOR
            if (CheckShaderKeyword(strKeyword, isOn: turnOn) == false)
            {
                var scriptFile = MonoScript.FromMonoBehaviour(this);
                if (scriptFile == null)
                    return;

                var path = AssetDatabase.GetAssetPath(scriptFile);
                if (string.IsNullOrEmpty(path))
                    return;

                var clothPath = Directory.GetParent(Path.GetDirectoryName(path));
                string filePath = clothPath + "/Resources/Shaders/Compute/PBDClothSolver.compute";

                // Read the old file.
                string[] lines = File.ReadAllLines(filePath);

                // Write the new file over the old file.
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    for (int currentLine = 0; currentLine < lines.Length; ++currentLine)
                    {
                        if (currentLine == 0)
                        {
                            writer.WriteLine((turnOn ? "" : "//") + strKeyword);
                        }
                        else
                        {
                            writer.WriteLine(lines[currentLine]);
                        }
                    }
                }
                AssetDatabase.Refresh();
            }
#endif
        }

        internal static void WeldVerticesOld(Mesh mesh, object threshold)
        {
            throw new NotImplementedException();
        }
    }

    public static class Extensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }

        public static void ClearBuffer(this ComputeBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
            buffer = null;
        }
        public static void ClearBuffer(this GraphicsBuffer buffer)
        {
            if (buffer != null)
                buffer.Release();
            buffer = null;
        }

        public static void Clear(this AssetBundle ab)
        {
            if (ab != null) ab.Unload(true);
        }

        public static void Clear(this AssetBundle[] abs)
        {
            if (abs != null)
            {
                for (int i = 0; i < abs.Length; i++)
                {
                    if (abs[i] != null) abs[i].Unload(true);
                }
            }
        }

        public static bool ExistsAndEnabled(this MonoBehaviour comp, out MonoBehaviour outComp)
        {
            if (comp != null && comp.enabled)
            {
                outComp = comp;
                return true;
            }
            outComp = null;
            return false;
        }

        public static int GetComputeShaderThreads(this int count, int threads = 64)
        {
            return (count + threads - 1) / threads;
        }
        public static int GetComputeShaderThreadsSave(this int count, int threads = 64)
        {
            return math.max(1, (count + threads - 1) / threads);
        }

        public static void CleanupList<T>(ref T[] array)
        {
            if (array != null)
            {
                var list = array.ToList();
                CleanupList<T>(ref list);
                array = list.ToArray();
            }
        }

        public static void CleanupList<T>(ref List<T> list)
        {
            if (list != null)
            {
                int count = list.Count - 1;
                for (int i = count; i >= 0; --i)
                {
                    if (list[i] == null || list[i].ToString() == "null")
                        list.RemoveAt(i);
                }
                if (list.Count > 1) list = list.Distinct().ToList();
            }
        }

        //public static List<T> CleanupList<T>(this List<T> list)
        //{
        //    int count = list.Count - 1;
        //    for (int i = count; i >= 0; --i)
        //    {
        //        if (list[i] == null)
        //            list.RemoveAt(i);
        //    }
        //    return list;
        //}
    }

    public static class GraphicsUtilities
    {
        private static Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();
        private static Dictionary<string, ComputeShader> _computeShaders = new Dictionary<string, ComputeShader>();

        public static Material LoadMaterial(string path)
        {
            var shader = LoadAsset(path, type: 1) as Shader;
            Material mat = shader != null ? new Material(shader) : null;
            try { return mat; } catch { Debug.LogError("Failed to load Shader " + path); }
            return null;
        }

        public static ComputeShader LoadComputeShaderAt(string path, bool forceUnpacked = false)
        {
            var cs = LoadAsset(path, type: 0, forceUnpacked) as ComputeShader;
            try { return cs; } catch { Debug.LogError("Failed to load Compute Shader " + path); }
            return null;
        }

        public static UnityEngine.Object LoadAsset(string resource, int type = 0, bool forceUnpacked = false)
        {
#if ASSET_UNPACKED
            bool unpacked = true;
#else
            bool unpacked = false;
#endif
            if (forceUnpacked || unpacked)
            {
                //Debug.Log("Resources.Load " + resource);
                if (type == 0)
                {
                    ComputeShader cs = null;
                    if (_computeShaders.TryGetValue(resource, out ComputeShader out_cs))
                    {
                        cs = out_cs;
                    }
                    else
                    {
                        cs = Resources.Load<ComputeShader>(resource);
                        _computeShaders.Add(resource, cs);
                    }
                    return cs;
                }
                else
                {
                    Shader shader = null;
                    if (_shaders.TryGetValue(resource, out Shader outShader))
                    {
                        shader = outShader;
                    }
                    else
                    {
                        shader = Resources.Load<Shader>(resource);
                        _shaders.Add(resource, shader);
                    }
                    return shader;
                }
            }
            else
            {
                var split = resource.Split('/');
                var fileName = split[split.Length - 1];
                var platform = "StandaloneWindows64";
#if UNITY_64 && UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                platform = "StandaloneWindows64";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                platform = "StandaloneOSX";
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                platform = "StandaloneLinux64";
#elif !UNITY_EDITOR && (UNITY_ANDROID || UNITY_LUMIN || UNITY_SWITCH)
                platform = "Android";
#elif !UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS)
                platform = "iOS";
#endif
                var path = resource.Replace(fileName, "") + platform + "/" + fileName.ToLower() + "Asset";
                var txt = Resources.Load<TextAsset>(path);
                if (txt == null)
                {
                    Debug.Log("Failed to load TextAsset " + path + " !");
                    return null;
                }
                var bundle = AssetBundle.LoadFromMemory(txt.bytes);
                if (bundle == null)
                {
                    Debug.Log("Failed to load AssetBundle " + fileName + "!");
                    return null;
                }
                UnityEngine.Object cs = null;
                string pathCam = path;// + "_" + cam;
                if (type == 0)
                {
                    if (_computeShaders.TryGetValue(pathCam, out ComputeShader outCS))
                    {
                        cs = outCS;
                    }
                    else
                    {
                        cs = bundle.LoadAsset<UnityEngine.Object>(fileName) as ComputeShader;
                        _computeShaders.Add(pathCam, (ComputeShader)cs);
                    }
                }
                else
                {
                    if (_shaders.TryGetValue(pathCam, out Shader outShader))
                    {
                        cs = outShader;
                    }
                    else
                    {
                        cs = bundle.LoadAsset<UnityEngine.Object>(fileName) as Shader;
                        _shaders.Add(pathCam, (Shader)cs);
                    }
                }
                bundle.Unload(false);
                return cs;
            }
        }


    }
}