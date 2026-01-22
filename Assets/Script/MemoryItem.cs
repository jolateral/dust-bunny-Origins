using UnityEngine;

public class MemoryItem : MonoBehaviour
{
    [TextArea(3, 10)]
    public string memoryText = "I remember this...";
    public Color textColor = Color.white;
}