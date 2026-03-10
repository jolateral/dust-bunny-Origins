
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace ClothDynamics
{

	public class GlobalObjectLOD : MonoBehaviour
	{
		[SerializeField] private float _distanceThreshold = 10;
		private Camera _cam;
		private GPUClothDynamics[] _cds;

		void OnEnable()
		{
			_cam = Camera.main;
#if UNITY_2020_1_OR_NEWER
			_cds = FindObjectsOfType<GPUClothDynamics>(true);
#else
			var list = Resources.FindObjectsOfTypeAll<GPUClothDynamics>().Where(x => x.gameObject.scene != null);
			if (list != null && list.Count() > 0) _cds = list.ToArray();
#endif
		}

		void Update()
		{
			if (_cam != null)
			{
				int length = _cds.Length;
				float threshold = _distanceThreshold * _distanceThreshold;
				for (int i = 0; i < length; i++)
				{
					var cd = _cds[i];
					var dist = math.distancesq(cd.transform.position, _cam.transform.position);
					if (threshold < dist)
					{
						cd.enabled = false;
					}
					else
					{
						cd.enabled = true;
					}
				}
			}
		}
	}
}
