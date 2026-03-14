using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClothDynamics
{
	[System.Serializable]
	public class GetDepthPos
	{
		[SerializeField] private float _distLimit = 0.1f;
		[SerializeField] private bool _debug = false;
		[SerializeField] private float _pointScale = 0.01f;

		private RenderTexture _unityDepthTexture;
		public RenderTexture _unityDepthTextureCopy;
		private GPUClothDynamics _clothSim;
		private Vector4 _depthPoint;
		private Vector3 _vertexPoint;
		private ComputeShader _depthScanCS;
		private RenderTexture _posBuffer;
		private ComputeBuffer _resultBuffer;
		private bool _runReadback = false;
		private Camera _mainCam;
		Material m_DepthCopyMat;
		internal int _newVertexId = 0;

		internal void Init(GPUClothDynamics clothSim, Camera mainCam)
		{
			_mainCam = mainCam;
			if (_mainCam != null)
			{
				if (_mainCam.actualRenderingPath == RenderingPath.Forward)
					_mainCam.depthTextureMode = DepthTextureMode.Depth;

				_clothSim = clothSim;
				if (_depthScanCS == null) _depthScanCS = Resources.Load("Shaders/Compute/DepthScan") as ComputeShader;

				if (UnityEngine.Object.FindObjectsOfType<GPUClothDynamics>().Length > 1)
					_depthScanCS = UnityEngine.Object.Instantiate(_depthScanCS);

				_posBuffer = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGBFloat);
				_posBuffer.filterMode = FilterMode.Point;
				_posBuffer.enableRandomWrite = true;
				_posBuffer.Create();

				if (_resultBuffer != null) _resultBuffer.Release();
				 _resultBuffer = new ComputeBuffer(2, sizeof(int));

				var depthCopyShader = Resources.Load("Shaders/DepthCopy") as Shader;
				m_DepthCopyMat = new Material(depthCopyShader);
			}
		}

		internal void Update()
		{
			if (_mainCam == null) return;

			_unityDepthTexture = Shader.GetGlobalTexture("_CameraDepthTexture") as RenderTexture;
			if (_unityDepthTexture != null)
			{
				Vector4 coordPos = new Vector4(InputEx.mousePosition.x / Screen.width, InputEx.mousePosition.y / Screen.height);
				//Debug.Log("coordPos " + coordPos.x + ", " + coordPos.y);

				int kernelIndex = 1;
				if (GraphicsSettings.currentRenderPipeline && GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
				{
					//Debug.Log("HDRP active");

					if (_unityDepthTextureCopy == null) { _unityDepthTextureCopy = new RenderTexture(_unityDepthTexture.width, _unityDepthTexture.height, 24, RenderTextureFormat.Depth); }
					//_depthCopyBuffer.Blit(_unityDepthTexture, _unityDepthTextureCopy);

					// Set the _MyDepthTex Shader Texture to our source depth texture to be copied
					m_DepthCopyMat.SetTexture("_MyDepthTex", _unityDepthTexture);

					// Do a Blit using the DepthCopy Material/Shader
					m_DepthCopyMat.SetVector("_screenSize", new Vector4(Screen.width, Screen.height, 0, 0));
					Graphics.Blit(null, _unityDepthTextureCopy, m_DepthCopyMat);

					_depthScanCS.SetTexture(kernelIndex, "_DepthTexture", _unityDepthTextureCopy);
				}
				else
				{
					_depthScanCS.SetTexture(kernelIndex, "_DepthTexture", _unityDepthTexture);
				}

				_depthScanCS.SetVector("coordPos", coordPos);
				_depthScanCS.SetTexture(kernelIndex, "posBuffer", _posBuffer);
				_depthScanCS.SetBuffer(kernelIndex, "resultBuffer", _resultBuffer);
				_depthScanCS.Dispatch(kernelIndex, 1, 1, 1);

				if (!_runReadback) _clothSim.StartCoroutine(Readback(coordPos));
			}
		}

		IEnumerator Readback(Vector4 coordPos)
		{
			if (_runReadback) yield break;
			_runReadback = true;

			float[] data = new float[1];
			if (_clothSim._supportsAsyncGPUReadback)
			{
				var request = UniversalAsyncGPUReadbackRequest.Request(_posBuffer);
				while (!request.done)
				{
					if (request.hasError) request = UniversalAsyncGPUReadbackRequest.Request(_posBuffer);
					yield return null;
				}
				var pData = request.GetData<Vector4>();
				data[0] = pData[0].x;
			}
			else
			{
				Color[] dataPos = GPUClothDynamics.GetDataFromRT(_posBuffer);
				data[0] = dataPos[0].r;
				//_posBuffer.GetData(data);
			}

			if (!float.IsNaN(data[0]))
			{
				//#if defined(UNITY_REVERSED_Z) //TODO?
				float depth = 1.0f - data[0];
				//#endif
				Vector4 point = new Vector4(coordPos.x * 2.0f - 1.0f, coordPos.y * 2.0f - 1.0f, depth * 2.0f - 1.0f, 1.0f);
				point = _mainCam.projectionMatrix.inverse * point;
				point = _mainCam.cameraToWorldMatrix * point;
				point /= point.w;
				_depthPoint = point;
				_depthPoint.w = _distLimit;

				int kernelIndex = 0;
				_depthScanCS.SetVector("cursorPos", _depthPoint);
				_depthScanCS.SetMatrix("localToWorldMatrix", _clothSim.transform.localToWorldMatrix);
				_depthScanCS.SetInt("vertexCount", _clothSim._numParticles);
				_depthScanCS.SetBuffer(kernelIndex, "resultBuffer", _resultBuffer);
				_depthScanCS.SetBuffer(kernelIndex, "vertexBuffer", _clothSim._objBuffers[0].positionsBuffer);
				_depthScanCS.Dispatch(kernelIndex, _clothSim._numParticles.GetComputeShaderThreads(128), 1, 1);

				int[] indexData = new int[2];
				if (_clothSim._supportsAsyncGPUReadback)
				{
					var request = UniversalAsyncGPUReadbackRequest.Request(_resultBuffer);
					while (!request.done)
					{
						if (request.hasError) request = UniversalAsyncGPUReadbackRequest.Request(_resultBuffer);
						yield return null;
					}
					var iData = request.GetData<int>();
					indexData[0] = iData[0];
					indexData[1] = iData[1];
				}
				else
				{
					_resultBuffer.GetData(indexData);
				}
				int vertexId = indexData[1];
				_clothSim._tempPointVertexId = vertexId;

				if (_debug)
				{
					var vertexData = new Vector3[_clothSim._objBuffers[0].positionsBuffer.count];
					_clothSim._objBuffers[0].positionsBuffer.GetData(vertexData);
					if (vertexId >= 0 && vertexId < vertexData.Length)
						_vertexPoint = _clothSim.transform.TransformPoint(vertexData[vertexId]);
				}

			}
			_runReadback = false;
		}

		internal void GetClosestPoint(Vector4 point)
		{
			int kernelIndex = 0;
			_depthScanCS.SetVector("cursorPos", point);
			_depthScanCS.SetMatrix("localToWorldMatrix", _clothSim.transform.localToWorldMatrix);
			_depthScanCS.SetInt("vertexCount", _clothSim._numParticles);
			_depthScanCS.SetBuffer(kernelIndex, "resultBuffer", _resultBuffer);
			_depthScanCS.SetBuffer(kernelIndex, "vertexBuffer", _clothSim._objBuffers[0].positionsBuffer);
			_depthScanCS.Dispatch(kernelIndex, _clothSim._numParticles.GetComputeShaderThreads(128), 1, 1);

			int[] indexData = new int[2];
			//if (_clothSim._supportsAsyncGPUReadback)
			//{
			//	var request = UniversalAsyncGPUReadbackRequest.Request(_resultBuffer);
			//	while (!request.done)
			//	{
			//		if (request.hasError) request = UniversalAsyncGPUReadbackRequest.Request(_resultBuffer);
			//		yield return null;
			//	}
			//	var iData = request.GetData<int>();
			//	indexData[0] = iData[0];
			//	indexData[1] = iData[1];
			//}
			//else
			//{
				_resultBuffer.GetData(indexData);
			//}
			_newVertexId = indexData[1];
		}


		internal void OnDrawGizmos()
		{
			if (_debug)
			{
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(_depthPoint, _pointScale);
				Gizmos.color = Color.blue;
				Gizmos.DrawWireSphere(_vertexPoint, _pointScale);
			}
		}

		internal void OnDestroy()
		{
			//_posBuffer?.Release();
			//_posBuffer = null;
			_resultBuffer?.Release();
			_resultBuffer = null;
			//_depthCopyBuffer?.Release();
			//_depthCopyBuffer = null;
		}
	}

}