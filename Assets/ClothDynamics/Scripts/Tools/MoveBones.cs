using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
	public class MoveBones : MonoBehaviour
	{
		[SerializeField] private Transform[] _boneControls;
		[SerializeField] private bool _keyboardInput = false;
		[SerializeField] private float _speed = 1;
		[SerializeField] private float _range = 0.005f;
		[SerializeField] private bool _close = false;
		private Transform[] _bones;
		private SkinnedMeshRenderer _skin;
		private Vector3[] _savePos;
		private Vector3[] _startPos;

		void Awake()
		{
			_skin = this.GetComponent<SkinnedMeshRenderer>();
			_bones = _skin.bones;

			if (_keyboardInput)
			{
				_savePos = new Vector3[_bones.Length];
				for (int i = 0; i < _bones.Length; i++)
				{
					_savePos[i] = _bones[i].position;
				}

				_startPos = new Vector3[_bones.Length];
				for (int i = 0; i < _bones.Length; i++)
				{
					_startPos[i] = _bones[0].position - Vector3.forward * 0.001f * i;
				}
			}
		}

		void Update()
		{
			if (_keyboardInput)
			{
				if (Input.GetKeyDown(KeyCode.M))
				{
					_close = !_close;
					StopAllCoroutines();
					StartCoroutine(MoveToStartPos(_close));
				}
				if (Input.GetKey(KeyCode.N))
				{
					for (int i = 0; i < _boneControls.Length; i++)
					{
						_boneControls[i].position += Mathf.Sin(Time.time * _speed) * Vector3.forward * _range;
					}
				}
			}

			for (int i = 0; i < _boneControls.Length; i++)
			{
				if (i < _bones.Length)
				{
					_bones[i].position = _boneControls[i].position;
					_bones[i].rotation = _boneControls[i].rotation;
				}
			}
		}

		IEnumerator MoveToStartPos(bool close = true)
		{

			if (close)
			{
				while (Vector3.Distance(_boneControls[_boneControls.Length - 1].position, _startPos[_boneControls.Length - 1]) > 0.001f)
				{
					for (int i = 0; i < _boneControls.Length; i++)
						_boneControls[i].position = Vector3.Lerp(_boneControls[i].position, _startPos[i], Time.deltaTime * _speed);
					yield return null;
				}
			}
			else
			{
				while (Vector3.Distance(_boneControls[_boneControls.Length - 1].position, _savePos[_boneControls.Length - 1]) > 0.001f)
				{
					for (int i = 0; i < _boneControls.Length; i++)
						_boneControls[i].position = Vector3.Lerp(_boneControls[i].position, _savePos[i], Time.deltaTime * _speed);
					yield return null;
				}
			}
		}
	}
}