using UnityEngine;

namespace ClothDynamics
{
    public class Mover : MonoBehaviour
    {
        [SerializeField] private float _force = 1;
        enum UpdateType
        {
            FixedUpdate = 0,
            Update = 1,
            LateUpdate = 2
        }
        [SerializeField] private UpdateType _updateType = UpdateType.FixedUpdate;


        private void FixedUpdate()
        {
            if (_updateType == UpdateType.FixedUpdate)
                TransformUpdate();
        }

        private void Update()
        {
            if (_updateType == UpdateType.Update)
                TransformUpdate();
        }

        private void LateUpdate()
        {
            if (_updateType == UpdateType.LateUpdate)
                TransformUpdate();
        }

        void TransformUpdate()
        {
            if (Input.GetKey(KeyCode.LeftArrow) && !Input.GetKey(KeyCode.LeftShift))
                this.transform.position += Vector3.forward * _force * Time.deltaTime;
            if (Input.GetKey(KeyCode.RightArrow) && !Input.GetKey(KeyCode.LeftShift))
                this.transform.position -= Vector3.forward * _force * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftArrow) && Input.GetKey(KeyCode.LeftShift))
                this.transform.localRotation *= Quaternion.Euler(0, _force * Time.deltaTime * 100, 0);
            if (Input.GetKey(KeyCode.RightArrow) && Input.GetKey(KeyCode.LeftShift))
                this.transform.localRotation *= Quaternion.Euler(0, -_force * Time.deltaTime * 100, 0);

            if (Input.GetKey(KeyCode.UpArrow) && !Input.GetKey(KeyCode.LeftShift))
                this.transform.position += Vector3.right * _force * Time.deltaTime;
            if (Input.GetKey(KeyCode.UpArrow) && Input.GetKey(KeyCode.LeftShift))
                this.transform.position += Vector3.up * _force * Time.deltaTime;

            if (Input.GetKey(KeyCode.DownArrow) && !Input.GetKey(KeyCode.LeftShift))
                this.transform.position -= Vector3.right * _force * Time.deltaTime;
            if (Input.GetKey(KeyCode.DownArrow) && Input.GetKey(KeyCode.LeftShift))
                this.transform.position -= Vector3.up * _force * Time.deltaTime;
        }
    }
}