using System.Collections.Generic;
using System.Threading.Tasks;
using GenHub.Core.Models.UI;

namespace GenHub.Core.Interfaces.UI
{
    /// <summary>
    /// Service for handling file and folder selection dialogs
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Opens a file picker dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="fileTypes">Dictionary of file type descriptions and their extensions (e.g. "Image Files" -> "*.png;*.jpg")</param>
        /// <param name="allowMultiple">Whether multiple files can be selected</param>
        /// <returns>List of selected file paths or empty list if canceled</returns>
        Task<IReadOnlyList<string>> PickFilesAsync(string title, Dictionary<string, string> fileTypes, bool allowMultiple = false);
        
        /// <summary>
        /// Opens a folder picker dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="allowMultiple">Whether multiple folders can be selected</param>
        /// <returns>List of selected folder paths or empty list if canceled</returns>
        Task<IReadOnlyList<string>> PickFoldersAsync(string title, bool allowMultiple = false);
        
        /// <summary>
        /// Opens an image file picker dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected image file path or null if canceled</returns>
        Task<string?> PickImageFileAsync(string title);
        
        /// <summary>
        /// Opens an executable file picker dialog
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <returns>Selected executable file path or null if canceled</returns>
        Task<string?> PickExecutableFileAsync(string title);

        /// <summary>
        /// Shows a message box
        /// </summary>
        /// <param name="title">Message box title</param>
        /// <param name="message">Message box content</param>
        /// <param name="buttons">Buttons to display on the message box</param>
        /// <param name="icon">Icon to display on the message box</param>
        /// <returns>Result of the message box interaction</returns>
        Task<MessageBoxResult> ShowMessageBoxAsync(
            string title,
            string message,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None);

        /// <summary>
        /// Shows a confirmation dialog with custom button texts.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Dialog message.</param>
        /// <param name="primaryButtonText">Text for the primary action button (e.g., "Save", "Discard", "Yes").</param>
        /// <param name="secondaryButtonText">Text for the secondary action button (e.g., "Cancel", "Keep Editing", "No").</param>
        /// <returns>MessageBoxResult.Yes if the primary button is clicked, MessageBoxResult.No if the secondary button is clicked.</returns>
        Task<MessageBoxResult> ShowConfirmationDialogAsync(
            string title,
            string message,
            string primaryButtonText,
            string secondaryButtonText);
    }
}

