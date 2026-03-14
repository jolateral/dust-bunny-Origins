using System.Collections;
using UnityEngine;

namespace ClothDynamics
{
    public class StressTest : MonoBehaviour
    {
        public float _speed = 50;
        public Vector3 _axis = Vector3.right;
        private int _dir = 1;
        private float _rotationSum = 0;
        private float _deltaRotation = 0;
        private float _rotation = Mathf.PI * 0.01f;
        private float _2pi = 2 * Mathf.PI;
        private bool _init = false;
        //public Material _testMaterail;

        private IEnumerator Start()
        {
            //    _testMaterail.SetFloat("_CullMode", 1);
            //    _testMaterail.SetFloat("_CullModeForward", 1);
            yield return new WaitForSeconds(1);
            _init = true;
        }

        void Update()
        {
            if (_init)
            {
                if (_rotationSum <= _2pi)
                {
                    _deltaRotation = _rotation * _rotationSum / _2pi;
                }
                else if (_rotationSum >= 6 * Mathf.PI)
                {
                    _deltaRotation = _rotation * (8 * Mathf.PI - _rotationSum) / _2pi;
                }
                else
                {
                    _deltaRotation = _rotation;
                }
                _deltaRotation *= Time.deltaTime * _speed;
                transform.Rotate(_axis, _dir * _deltaRotation * Mathf.Rad2Deg);
                transform.position -= _dir * Vector3.right * 0.0001f;
                _rotationSum += _rotation * Time.deltaTime * _speed;
                if (_rotationSum > 8 * Mathf.PI)
                {
                    _dir *= -1;
                    _rotationSum = 0;
                }
            }
        }
    }
}