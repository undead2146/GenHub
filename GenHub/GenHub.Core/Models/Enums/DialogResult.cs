namespace GenHub.Core.Models
{
    /// <summary>
    /// Represents the result of a dialog operation
    /// </summary>
    public enum DialogResult
    {
        /// <summary>
        /// The dialog was closed without a result
        /// </summary>
        None,
        
        /// <summary>
        /// The user clicked OK/Accept/Save
        /// </summary>
        OK,
        
        /// <summary>
        /// The user clicked Cancel/Close
        /// </summary>
        Cancel,
        
        /// <summary>
        /// The user clicked Yes
        /// </summary>
        Yes,
        
        /// <summary>
        /// The user clicked No
        /// </summary>
        No,
        
        /// <summary>
        /// The user clicked Delete
        /// </summary>
        Delete,
        
        /// <summary>
        /// The operation timed out
        /// </summary>
        Timeout
    }
}
