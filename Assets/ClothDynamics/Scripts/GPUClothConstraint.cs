using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClothDynamics
{
	public class GPUClothConstraint : MonoBehaviour
	{
		public GPUClothDynamics _clothSim;
		[Range(0.0f, 1.0f)]
		public float _intensity = 1.0f;
		[SerializeField] private float _distLimit = 0.1f;
		private bool _findVertex = false;
		[SerializeField] private bool _findNearestVertex = true;
		[SerializeField] private bool _gpuSearch = false;
		public int _vertexId;
		[SerializeField] private bool _debug = false;
		[SerializeField] private float _debugPointScale = 0.01f;
		private Vector3 _vertexPoint;
		private WaitForSeconds _waitForSeconds = new WaitForSeconds(1);
		private float lastDist;
		private int nearest;

		private void OnEnable()
		{
#if UNITY_ANDROID || UNITY_IPHONE
			_gpuSearch = false;
#endif
			if (_clothSim != null)
			{
				List<GPUClothConstraint> list = new List<GPUClothConstraint>();
				if (_clothSim._transformConstraints != null)
					list = _clothSim._transformConstraints.ToList();
				list.Add(this);
				_clothSim._transformConstraints = list.Distinct().ToArray();
			}
		}

		private void FindVertex(GPUClothDynamics cloth)
		{
			Vector4 pos = this.transform.position;
			if (_gpuSearch)
			{
				pos.w = _distLimit;
				cloth._getDepthPos.GetClosestPoint(pos);
				_vertexId = cloth._getDepthPos._newVertexId;
			}
			else
			{
				var vertexData = new Vector3[_clothSim._objBuffers[0].positionsBuffer.count];
				_clothSim._objBuffers[0].positionsBuffer.GetData(vertexData);

				float lastDist = float.MaxValue;
				int nearest = 0;
				var point = _clothSim.transform.InverseTransformPoint(pos);
				for (int i = 0; i < vertexData.Length; i++)
				{
					var dist = Vector3.Distance(vertexData[i], point);
					if (dist < lastDist)
					{
						lastDist = dist;
						nearest = i;
					}
				}
				_vertexId = nearest;
			}
			if (_debug) Debug.Log("FindVertex " + _vertexId);
		}

		private void Update()
		{
			if (_clothSim != null && _clothSim._finishedLoading)
			{
				if (_findNearestVertex)
				{
					if (_intensity > 0 && !_findVertex)
					{
						FindVertex(_clothSim);
						_findVertex = true;
					}
					else if (_intensity == 0)
					{
						_findVertex = false;
					}
				}
				if (_debug)
				{
					var vertexData = new Vector3[_clothSim._objBuffers[0].positionsBuffer.count];
					_clothSim._objBuffers[0].positionsBuffer.GetData(vertexData);
					if (_vertexId >= 0 && _vertexId < vertexData.Length)
						_vertexPoint = _clothSim.transform.TransformPoint(vertexData[_vertexId]);
				}
			}
		}

		private void OnDrawGizmos()
		{
			if (_debug)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawWireSphere(_vertexPoint, _debugPointScale);
			}
		}
	}
}
