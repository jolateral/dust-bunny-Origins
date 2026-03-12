using UnityEngine;

namespace ClothDynamics
{
    [CreateAssetMenu(fileName = "AnimationCurveData", menuName = "ScriptableObjects/AnimationCurveData", order = 1)]
    public class AnimationCurveData : ScriptableObject
    {

        public AnimationCurve _curve;
    }
}