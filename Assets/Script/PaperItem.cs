using UnityEngine;

/// <summary>
/// PaperItem.cs - IMAGE VERSION (No Paper Color Property)
/// 
/// This component represents a readable paper object in the game world.
/// When the player rolls onto it, it displays a full-screen image overlay
/// (your PNG) with optional text, freezes player movement, and waits for input to dismiss.
/// 
/// The 3D object's appearance is controlled by its material, not by code.
/// 
/// Usage:
/// 1. Create a GameObject with a Collider (trigger or solid)
/// 2. Tag it as "StickyObject" so it can be absorbed
/// 3. Add this PaperItem component
/// 4. Assign your PNG image to the paperSprite field
/// 5. (Optional) Fill in the paperText field with content to overlay on the image
/// 6. The PaperUIManager will handle the display automatically
/// </summary>
public class PaperItem : MonoBehaviour
{
    [Header("Paper Content")]
    [Tooltip("The image that will be displayed when this paper is read (assign your PNG here)")]
    public Sprite paperSprite;
    
    [Tooltip("(Optional) Text that will be displayed on top of the image")]
    [TextArea(5, 15)]
    public string paperText = "";
    
    [Header("Gameplay Settings")]
    [Tooltip("Should this paper be destroyed after being read?")]
    public bool destroyAfterReading = false;
    
    // Internal state
    private bool hasBeenRead = false;
    
    /// <summary>
    /// Initialize the paper object
    /// </summary>
    void Start()
    {
        // Ensure the object has the correct tag
        if (!CompareTag("StickyObject"))
        {
            Debug.LogWarning($"PaperItem '{name}' should have tag 'StickyObject' to be absorbed by player!");
        }
        
        // Warn if no sprite is assigned
        if (paperSprite == null)
        {
            Debug.LogWarning($"PaperItem '{name}' has no paperSprite assigned! It will use the default image.");
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
                // If this paper has a custom sprite, use it
                if (paperSprite != null)
                {
                    // Show with custom image
                    PaperUIManager.Instance.ShowPaper(paperText, paperSprite);
                }
                else
                {
                    // Show with default image (set in PaperUIManager)
                    PaperUIManager.Instance.ShowPaper(paperText);
                }
                
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
    /// Shows a semi-transparent white cube gizmo
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f); // Semi-transparent white
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}