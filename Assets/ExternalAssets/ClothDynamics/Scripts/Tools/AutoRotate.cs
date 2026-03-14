using UnityEngine;

namespace ClothDynamics
{
    public class AutoRotate : MonoBehaviour
    {
        public Vector3 speed = Vector3.zero;
        public Space space = Space.World;

        void Update()
        {
            transform.Rotate(speed * Time.deltaTime, space);
        }
    }
}