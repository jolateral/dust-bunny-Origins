using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClothDynamics
{
    public class RoundConeCollider : MonoBehaviour
    {
        public Transform otherSphere = null;
        internal Vector3 r1r2h = Vector3.one;
        internal bool _showGizmos = false;
        private void Awake()
        {
            if (this.GetComponent<Collider>())
                Destroy(this.GetComponent<Collider>());
            if (otherSphere != null)
            {
                if (otherSphere.GetComponent<Collider>())
                    Destroy(otherSphere.GetComponent<Collider>());
            }
            SetupCone();
        }
        void Update()
        {
            SetupCone();
        }

        private void SetupCone()
        {
            if (otherSphere != null)
            {
                var dir = otherSphere.position - this.transform.position;
                this.transform.up = dir.normalized;
                var r1 = this.transform.lossyScale.x * 0.5f;
                var r2 = otherSphere.lossyScale.x * 0.5f;
                var h = dir.magnitude;// Vector3.Distance(this.transform.position, otherSphere.position);
                r1r2h = new Vector3(r1, r2, h) * 0.5f;
            }
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {

			var selected = Selection.gameObjects.ToList();
			if (selected.Contains(this.gameObject) || selected.Contains(otherSphere.gameObject) || _showGizmos)
			{
				if (otherSphere != null)
                {
                    SetupCone();
                    var r1 = r1r2h.x * 2;
                    var r2 = r1r2h.y * 2;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(this.transform.position, r1);
                    Gizmos.DrawWireSphere(otherSphere.position, r2);
                    Gizmos.DrawLine(this.transform.position + this.transform.right * r1, otherSphere.position + this.transform.right * r2);
                    Gizmos.DrawLine(this.transform.position - this.transform.right * r1, otherSphere.position - this.transform.right * r2);
                    Gizmos.DrawLine(this.transform.position + this.transform.forward * r1, otherSphere.position + this.transform.forward * r2);
                    Gizmos.DrawLine(this.transform.position - this.transform.forward * r1, otherSphere.position - this.transform.forward * r2);
                }
            }
        }
#endif
    }
}