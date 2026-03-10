using UnityEngine;

namespace ClothDynamics
{
    public class FollowObject : MonoBehaviour
    {
        public Transform _followObj = null;
        public Vector3 _offset = Vector3.zero;
        public bool _followPos = false;
        void FixedUpdate()
        {
            var dir = (_followObj.position + _offset) - this.transform.position;
            transform.rotation = Quaternion.LookRotation(dir);
            if(_followPos)
            {
                transform.position = _followObj.position;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_followObj != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(this.transform.position, _followObj.position + _offset);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(this.transform.position, this.transform.position + this.transform.forward);
            }
        }
    }
}