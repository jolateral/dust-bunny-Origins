using UnityEngine;

/// <summary>
/// PaperItem.cs
/// 
/// This component represents a readable paper object in the game world.
/// When the player rolls onto it, it displays a full-screen white paper overlay
/// with text, freezes player movement, and waits for input to dismiss.
/// 
/// Similar to MemoryItem but shows a full paper overlay instead of UI text.
/// 
/// Usage:
/// 1. Create a GameObject with a Collider (trigger or solid)
/// 2. Tag it as "StickyObject" so it can be absorbed
/// 3. Add this PaperItem component
/// 4. Fill in the paperText field with your content
/// 5. The PaperUIManager will handle the display automatically
/// </summary>
public class PaperItem : MonoBehaviour
{
    [Header("Paper Content")]
    [Tooltip("The text that will be displayed on the paper overlay")]
    [TextArea(5, 15)]
    public string paperText = "This is a piece of paper you can read.";
    
    [Header("Visual Settings")]
    [Tooltip("Color of the paper object in the game world")]
    public Color paperColor = Color.white;
    
    [Tooltip("Should this paper be destroyed after being read?")]
    public bool destroyAfterReading = false;
    
    // Internal state
    private bool hasBeenRead = false;
    private Renderer objectRenderer;
    
    /// <summary>
    /// Initialize the paper object
    /// </summary>
    void Start()
    {
        // Get the renderer to apply the paper color
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            objectRenderer.material.color = paperColor;
        }
        
        // Ensure the object has the correct tag
        if (!CompareTag("StickyObject"))
        {
            Debug.LogWarning($"PaperItem '{name}' should have tag 'StickyObject' to be absorbed by player!");
        }
    }
    
    /// <summary>
    /// Called when player absorbs this object
    /// This is triggered by the AbsorbMechanic script
    /// </summary>
    public void OnAbsorbed()
    {
        // Only show the paper if it hasn't been read yet (or if it's reusable)
        if (!hasBeenRead || !destroyAfterReading)
        {
            // Tell the PaperUIManager to show this paper
            if (PaperUIManager.Instance != null)
            {
                PaperUIManager.Instance.ShowPaper(paperText);
                hasBeenRead = true;
            }
            else
            {
                Debug.LogError("PaperUIManager.Instance not found! Make sure PaperUIManager exists in the scene.");
            }
        }
    }
    
    /// <summary>
    /// Reset the paper so it can be read again
    /// Useful for testing or if you want reusable papers
    /// </summary>
    public void ResetPaper()
    {
        hasBeenRead = false;
    }
    
    /// <summary>
    /// Visualize the paper object in the editor
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(paperColor.r, paperColor.g, paperColor.b, 0.5f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}