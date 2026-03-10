using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
	[DefaultExecutionOrder(15210)] //When using Final IK
	public class ClothUpdateAnimator : MonoBehaviour
	{
		private enum UpdateModes
		{
			OnAnimatorIK,
			FixedUpdate,
			LateUpdate,
			FixedLateUpdate,
			SyncOff
		}
		[Tooltip("The Update Modes lets ClothUpdateAnimator run the simulation and the gpu skinners in the right order. You can choose when the sim update happens (LateUpdate is the default). SyncOff turns off the update order and all the components can run by their own update method. If you use OnAnimatorIK the script will activate the IK Pass in the highest layer (if no IK Pass was found).")]
		[SerializeField] private UpdateModes _updateModes = UpdateModes.LateUpdate;
		[Tooltip("This collects all the cloth simulations in the child objects automatically.")]
		private GPUClothDynamics[] _clothSims;
		[Tooltip("This collects all the gpu skinners in the child objects automatically.")]
		private GPUSkinnerBase[] _gpuSkinners;

		private WaitForSeconds _waitForSeconds = new WaitForSeconds(1);
		private bool _readyToRun = false;
		private Coroutine _coroutine = null;

		IEnumerator Start()
		{
			if (_updateModes == UpdateModes.SyncOff) yield break;
			yield return _waitForSeconds;

			var animator = GetComponent<Animator>();

			if (_gpuSkinners == null || _gpuSkinners.Length < 1) _gpuSkinners = GetComponentsInChildren<GPUSkinnerBase>();
			if (_clothSims == null || _clothSims.Length < 1) _clothSims = GetComponentsInChildren<GPUClothDynamics>();
			bool found = false;
			foreach (var _gpuSkinner in _gpuSkinners)
			{
				if (_gpuSkinner == null || !_gpuSkinner.gameObject.activeInHierarchy)
				{
					continue;
				}
				_gpuSkinner._updateSync = true;
				found = true;
			}
			foreach (var clothSim in _clothSims)
			{
				if (clothSim == null || !clothSim.gameObject.activeInHierarchy)
				{
					continue;
				}
				clothSim._updateSync = true;
				found = true;
			}
			if (!found) this.enabled = false;
#if UNITY_EDITOR
			else if (_updateModes == UpdateModes.OnAnimatorIK) ModifyLayers(animator.runtimeAnimatorController);
#endif
			_readyToRun = true;
		}

		void FixedUpdate()
		{
			if (_updateModes == UpdateModes.FixedUpdate) UpdateSync();
		}

		void OnAnimatorIK()
		{
			if (_updateModes == UpdateModes.OnAnimatorIK) UpdateSync();
		}

		void LateUpdate()
		{
			if (_updateModes == UpdateModes.LateUpdate) UpdateSync();
		}

		private void UpdateSync()
		{
			if (_readyToRun)
			{
				foreach (var gpuSkinner in _gpuSkinners) gpuSkinner.UpdateSync();
				foreach (var clothSim in _clothSims) clothSim.UpdateSync();
			}
		}

#if UNITY_EDITOR
		public void ModifyLayers(RuntimeAnimatorController runtimeController)
		{
			if (runtimeController == null)
			{
				Debug.LogErrorFormat("RuntimeAnimatorController must not be null.");
				return;
			}

			var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(runtimeController));
			if (controller == null)
			{
				Debug.LogErrorFormat("AnimatorController must not be null.");
				return;
			}
			UnityEditor.Animations.AnimatorControllerLayer[] layers = controller.layers;
			bool found = false;
			foreach (var item in layers)
			{
				if (item.iKPass == true)
					found = true;
			}
			if (!found)
			{
				layers[layers.Length - 1].iKPass = true;
				controller.layers = layers;
			}
		}
#endif

		private void OnEnable()
		{
			if (_updateModes == UpdateModes.FixedLateUpdate) _coroutine = StartCoroutine(RunLateFixedUpdate());
		}
		private void OnDisable()
		{
			if (_coroutine != null) StopCoroutine(_coroutine);
		}

		private IEnumerator RunLateFixedUpdate()
		{
			while (Application.isPlaying)
			{
				yield return new WaitForFixedUpdate();
				LateFixedUpdate();
			}
		}

		private void LateFixedUpdate()
		{
			UpdateSync();
		}


	}
}