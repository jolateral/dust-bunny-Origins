using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClothDynamics
{
    public class ClothFrictionCollider : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("This is currently a multiplier of the global static friction per object.")]
        public float friction;
    }
}