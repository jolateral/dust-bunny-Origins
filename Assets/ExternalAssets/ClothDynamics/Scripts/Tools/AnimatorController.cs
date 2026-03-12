using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.Rendering.VirtualTexturing;

namespace ClothDynamics
{
    public class AnimatorController : MonoBehaviour
    {
        [SerializeField] private bool _paused = false;

        private void Start()
        {
            GetComponent<Animator>().speed = _paused ? 0 : 1;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GetComponent<Animator>().speed = _paused ? 1 : 0;
                _paused = !_paused;
            }
        }

        public GPUClothDynamicsV2 _cdV2;

        void OnAnimatorMove()
        {
            GetComponent<Animator>().ApplyBuiltinRootMotion();
            if(_cdV2!=null) _cdV2._solver.OnAnimatorMove();
        }

    }

}