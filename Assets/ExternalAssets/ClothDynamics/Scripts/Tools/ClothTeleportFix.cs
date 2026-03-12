using System.Collections;
using UnityEngine;

namespace ClothDynamics
{
	public class ClothTeleportFix : MonoBehaviour
	{
		[SerializeField]
        private GPUClothDynamics[] _cds;
        [SerializeField]
        private GPUClothDynamicsV2[] _cdsV2;
        [SerializeField]
		private float _teleportDuration = 1.0f;
		//private WaitForSeconds _waitForSeconds = new WaitForSeconds(_teleportDuration);

		private void Awake()
		{
            if (_cds == null || _cds.Length < 1) _cds = GetComponentsInChildren<GPUClothDynamics>();
            if (_cdsV2 == null || _cdsV2.Length < 1) _cdsV2 = FindObjectsOfType<GPUClothDynamicsV2>();
        }

        public void OnTeleportEvent()
		{
			print("OnTeleportEvent() triggered!");
			foreach (var cd in _cds)
			{
				if (cd != null)
				{
					var saveMinBlend = cd._minBlend;
					cd._minBlend = 1;
					StartCoroutine(DelayBlendBack(cd, saveMinBlend));
				}
			}
            foreach (var cd in _cdsV2)
            {
                if (cd != null)
                {
					var meshes = cd._clothList;

					foreach (var item in meshes)
					{
						var skinning = item.GetComponent<ClothSkinningGPU>();
						if (skinning != null)
						{
							var saveMinBlend = skinning._minBlend;
							skinning._minBlend = 1;
							StartCoroutine(DelayBlendBack(skinning, saveMinBlend));
						}
                    }
                }
            }
        }

		private IEnumerator DelayBlendBack(GPUClothDynamics cd, float saveMinBlend)
		{
			//yield return _waitForSeconds;
			float blendTime = 0;
			var duration = _teleportDuration;
            while (blendTime < duration)
			{
				float step = blendTime / duration;
				cd._minBlend = Mathf.Lerp(cd._minBlend, saveMinBlend, step);
				blendTime += Time.deltaTime;
				yield return null;
			}
			cd._minBlend = saveMinBlend;
		}

        private IEnumerator DelayBlendBack(ClothSkinningGPU cd, float saveMinBlend)
        {
            //yield return _waitForSeconds;
            float blendTime = 0;
            var duration = _teleportDuration;
            while (blendTime < duration)
            {
                float step = blendTime / duration;
                cd._minBlend = Mathf.Lerp(cd._minBlend, saveMinBlend, step);
                blendTime += Time.deltaTime;
                yield return null;
            }
            cd._minBlend = saveMinBlend;
        }
    }
}