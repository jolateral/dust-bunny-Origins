using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MultiPiecePaperData.cs
/// 
/// ScriptableObject that stores data about a multi-piece paper puzzle.
/// This allows multiple PaperItem objects to share collection state.
/// 
/// Usage:
/// 1. Right-click in Project > Create > Multi-Piece Paper Data
/// 2. Set the total number of pieces
/// 3. Assign the complete image sprite
/// 4. Assign this to all PaperItem pieces that belong to this puzzle
/// </summary>
[CreateAssetMenu(fileName = "NewMultiPiecePaper", menuName = "Multi-Piece Paper Data")]
public class MultiPiecePaperData : ScriptableObject
{
    [Header("Paper Identity")]
    [Tooltip("Unique identifier for this multi-piece paper")]
    public string paperID = "Paper_001";
    
    [Header("Puzzle Configuration")]
    [Tooltip("Total number of pieces in this puzzle")]
    public int totalPieces = 5;
    
    [Tooltip("The complete image shown when all pieces are collected")]
    public Sprite completeSprite;
    
    [Header("Optional Text")]
    [Tooltip("Text shown when viewing the paper (can be empty)")]
    [TextArea(5, 15)]
    public string paperText = "";
    
    // Runtime data - tracks which pieces have been collected
    [System.NonSerialized]
    private HashSet<int> collectedPieces = new HashSet<int>();
    
    /// <summary>
    /// Mark a piece as collected
    /// </summary>
    /// <param name="pieceIndex">The index of the piece (0-based)</param>
    public void CollectPiece(int pieceIndex)
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
        
        collectedPieces.Add(pieceIndex);
        Debug.Log($"Collected piece {pieceIndex + 1}/{totalPieces} of {paperID}");
    }
    
    /// <summary>
    /// Check if a specific piece has been collected
    /// </summary>
    public bool IsPieceCollected(int pieceIndex)
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
        
        return collectedPieces.Contains(pieceIndex);
    }
    
    /// <summary>
    /// Get the number of pieces collected so far
    /// </summary>
    public int GetCollectedCount()
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
        
        return collectedPieces.Count;
    }
    
    /// <summary>
    /// Check if all pieces have been collected
    /// </summary>
    public bool IsComplete()
    {
        return GetCollectedCount() >= totalPieces;
    }
    
    /// <summary>
    /// Get the list of collected piece indices
    /// </summary>
    public List<int> GetCollectedPieces()
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
        
        return new List<int>(collectedPieces);
    }
    
    /// <summary>
    /// Reset all collected pieces (useful for testing or New Game)
    /// </summary>
    public void ResetProgress()
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
        
        collectedPieces.Clear();
        Debug.Log($"Reset progress for {paperID}");
    }
    
    /// <summary>
    /// Called when the ScriptableObject is loaded
    /// Ensures collectedPieces is initialized
    /// </summary>
    void OnEnable()
    {
        if (collectedPieces == null)
            collectedPieces = new HashSet<int>();
    }
}