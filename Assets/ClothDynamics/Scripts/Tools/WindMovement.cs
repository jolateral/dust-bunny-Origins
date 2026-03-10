using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
    public class WindMovement : MonoBehaviour
    {
        [SerializeField] private Vector3 _speed;
        [SerializeField] private Vector3 _angle;
        [SerializeField] private Vector3 _offset;

        private void Start()
        {
            if (_offset.magnitude == 0)
                _offset = this.transform.localRotation.eulerAngles;
        }

        void Update()
        {
            this.transform.localRotation = Quaternion.Euler(Mathf.Cos(Time.time * _speed.x) * _angle.x + _offset.x, Mathf.Sin(Time.time * _speed.y) * _angle.y + _offset.y, Mathf.Sin(Time.time * _speed.z) * _angle.z + _offset.z);
        }
    }
}