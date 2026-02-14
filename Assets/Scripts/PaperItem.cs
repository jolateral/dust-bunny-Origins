using UnityEngine;

/// <summary>
/// PaperItem.cs - UPDATED VERSION WITH MULTI-PIECE SUPPORT
/// 
/// This component represents a readable paper object in the game world.
/// Now supports two modes:
/// 
/// SINGLE-PIECE MODE:
/// - Leave multiPieceData empty
/// - Assign paperSprite for the complete image
/// - Works exactly like before
/// 
/// MULTI-PIECE MODE:
/// - Assign a MultiPiecePaperData ScriptableObject
/// - Set the pieceIndex (0, 1, 2, 3, 4 for a 5-piece puzzle)
/// - Assign the piece sprite (shows fragment in correct position)
/// - All pieces with same multiPieceData share collection progress
/// 
/// Usage:
/// 1. Create GameObject with Collider, tag as "StickyObject"
/// 2. Add this PaperItem component
/// 3. For single piece: Assign paperSprite only
/// 4. For multi-piece: Create MultiPiecePaperData asset, assign it, set pieceIndex, assign piece sprite
/// </summary>
public class PaperItem : MonoBehaviour
{
    [Header("Paper Mode")]
    [Tooltip("Leave empty for single-piece paper. Assign for multi-piece puzzle.")]
    public MultiPiecePaperData multiPieceData;
    
    [Tooltip("For multi-piece papers: Which piece is this? (0-based index)")]
    public int pieceIndex = 0;
    
    [Header("Single-Piece Paper Content")]
    [Tooltip("For single-piece: The complete image. For multi-piece: The piece sprite.")]
    public Sprite paperSprite;
    
    [Tooltip("(Optional) Text for single-piece papers only")]
    [TextArea(5, 15)]
    public string paperText = "";
    
    [Header("Gameplay Settings")]
    [Tooltip("Should this paper be destroyed after being read? (Single-piece only)")]
    public bool destroyAfterReading = false;
    
    // Internal state
    private bool hasBeenRead = false;
    
    /// <summary>
    /// Determine if this is a multi-piece paper
    /// </summary>
    private bool IsMultiPiece()
    {
        return multiPieceData != null;
    }
    
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
        
        // Validation for multi-piece setup
        if (IsMultiPiece())
        {
            if (pieceIndex < 0 || pieceIndex >= multiPieceData.totalPieces)
            {
                Debug.LogError($"PaperItem '{name}': pieceIndex {pieceIndex} is out of range! Should be 0-{multiPieceData.totalPieces - 1}");
            }
            
            if (paperSprite == null)
            {
                Debug.LogWarning($"PaperItem '{name}': Multi-piece paper has no piece sprite assigned!");
            }
        }
        else
        {
            // Single-piece validation
            if (paperSprite == null)
            {
                Debug.LogWarning($"PaperItem '{name}': Single-piece paper has no paperSprite assigned!");
            }
        }
    }
    
    /// <summary>
    /// Called when player absorbs this object
    /// This is triggered by the AbsorbMechanic script
    /// </summary>
    public void OnAbsorbed()
    {
        if (IsMultiPiece())
        {
            HandleMultiPieceAbsorb();
        }
        else
        {
            HandleSinglePieceAbsorb();
        }
    }
    
    /// <summary>
    /// Handle absorption of a multi-piece paper fragment
    /// </summary>
    private void HandleMultiPieceAbsorb()
    {
        // Check if this piece was already collected
        if (multiPieceData.IsPieceCollected(pieceIndex))
        {
            Debug.Log($"Piece {pieceIndex} of {multiPieceData.paperID} was already collected!");
            return;
        }
        
        // Mark this piece as collected
        multiPieceData.CollectPiece(pieceIndex);
        
        // Show the puzzle UI with current progress
        if (PaperUIManager.Instance != null)
        {
            // Get all collected piece sprites
            Sprite[] collectedSprites = GetCollectedPieceSprites();
            
            // Show the multi-piece UI
            PaperUIManager.Instance.ShowMultiPiecePaper(
                multiPieceData,
                collectedSprites
            );
        }
        else
        {
            Debug.LogError("PaperUIManager.Instance not found!");
        }
    }
    
    /// <summary>
    /// Get sprites for all collected pieces of this puzzle
    /// Used to display the puzzle with revealed pieces
    /// </summary>
    private Sprite[] GetCollectedPieceSprites()
    {
        // Find all PaperItem objects that belong to this multi-piece paper
        PaperItem[] allPaperItems = FindObjectsOfType<PaperItem>();
        
        // Create array to hold sprites (indexed by piece number)
        Sprite[] sprites = new Sprite[multiPieceData.totalPieces];
        
        foreach (PaperItem item in allPaperItems)
        {
            // Skip if not part of this puzzle
            if (item.multiPieceData != multiPieceData)
                continue;
            
            // Skip if this piece hasn't been collected
            if (!multiPieceData.IsPieceCollected(item.pieceIndex))
                continue;
            
            // Add this piece's sprite to the array
            if (item.pieceIndex >= 0 && item.pieceIndex < sprites.Length)
            {
                sprites[item.pieceIndex] = item.paperSprite;
            }
        }
        
        return sprites;
    }
    
    /// <summary>
    /// Handle absorption of a single-piece paper (original behavior)
    /// </summary>
    private void HandleSinglePieceAbsorb()
    {
        // Only show if not already read (or if reusable)
        if (!hasBeenRead || !destroyAfterReading)
        {
            if (PaperUIManager.Instance != null)
            {
                // Show single-piece paper (original behavior)
                if (paperSprite != null)
                {
                    PaperUIManager.Instance.ShowPaper(paperText, paperSprite);
                }
                else
                {
                    PaperUIManager.Instance.ShowPaper(paperText);
                }
                
                hasBeenRead = true;
            }
            else
            {
                Debug.LogError("PaperUIManager.Instance not found!");
            }
        }
    }
    
    /// <summary>
    /// Reset the paper so it can be read again
    /// For multi-piece papers, this only resets the local read state,
    /// not the shared collection progress
    /// </summary>
    public void ResetPaper()
    {
        hasBeenRead = false;
    }
    
    /// <summary>
    /// Visualize the paper object in the editor
    /// Color-coded: White for single-piece, Yellow for multi-piece
    /// </summary>
    void OnDrawGizmos()
    {
        if (IsMultiPiece())
        {
            // Yellow for multi-piece papers
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        }
        else
        {
            // White for single-piece papers
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        }
        
        Gizmos.DrawCube(transform.position, transform.localScale);
        
        // Draw piece index for multi-piece papers
        #if UNITY_EDITOR
        if (IsMultiPiece())
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Piece {pieceIndex}"
            );
        }
        #endif
    }
}