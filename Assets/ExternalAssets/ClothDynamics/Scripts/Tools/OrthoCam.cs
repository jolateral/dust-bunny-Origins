using UnityEngine;
using System.Collections.Generic;

namespace ClothDynamics
{
    public class OrthoCam : MonoBehaviour
    {

        public float m_speed = 5.0f;

        public float sensitivityX = 0.1f;
        public float sensitivityY = 0.1f;
        public float sensitivityZ = 0.1f;

        public float minimumX = -360;
        public float maximumX = 360;

        public float minimumY = -89F;
        public float maximumY = 89;

        private float rotationY = 0F;
        private float rotationX = 0F;
        private float rotationZ = 0F;

        void Start()
        {
            rotationY = transform.localEulerAngles.x;
            transform.localEulerAngles = new Vector3(rotationY, transform.localEulerAngles.y, 0);
        }

        void Update()
        {

            float speed = m_speed;

            if (Input.GetKey(KeyCode.Space)) speed *= 10.0f;

            Vector3 move = new Vector3(0, 0, 0);

            float deltaTime = Time.deltaTime;

            ////move left
            //if (Input.GetKey(KeyCode.A))
            //    move = new Vector3(-1, 0, 0) * deltaTime * speed;

            ////move right
            //if (Input.GetKey(KeyCode.D))
            //    move = new Vector3(1, 0, 0) * deltaTime * speed;

            ////move forward
            //if (Input.GetKey(KeyCode.W))
            //    move = new Vector3(0, 0, 1) * deltaTime * speed;

            ////move back
            //if (Input.GetKey(KeyCode.S))
            //    move = new Vector3(0, 0, -1) * deltaTime * speed;

            ////move up
            //if (Input.GetKey(KeyCode.E))
            //    move = new Vector3(0, -1, 0) * deltaTime * speed;

            ////move down
            //if (Input.GetKey(KeyCode.Q))
            //    move = new Vector3(0, 1, 0) * deltaTime * speed;


            transform.Translate(move);

            if (Input.GetMouseButton(2))
            {
                rotationX += Input.GetAxis("Mouse X") * sensitivityX;
                rotationX = Mathf.Clamp(rotationX, minimumX, maximumX);

                rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
                rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);

                transform.localPosition = new Vector3(rotationX, rotationY, 1);
            }

            rotationZ += Input.mouseScrollDelta.y * sensitivityZ;

            this.GetComponent<Camera>().orthographicSize -= rotationZ;

            rotationZ = 0;
        }
    }

}
