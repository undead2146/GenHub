namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents the variant of the game executable
    /// </summary>
    public enum GameVariant
    {
        /// <summary>
        /// Unknown game variant
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// Base Generals game
        /// </summary>
        Generals = 1,
        
        /// <summary>
        /// Zero Hour expansion
        /// </summary>
        ZeroHour = 2,
        
        /// <summary>
        /// Game utility like world builder
        /// </summary>
        Utility = 3
    }
}
