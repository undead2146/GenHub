using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Core.Interfaces.DesktopShortcuts
{
    /// <summary>
    /// Service for building command line arguments for shortcuts
    /// </summary>
    public interface IShortcutCommandBuilder
    {
        /// <summary>
        /// Builds command line arguments for a shortcut configuration
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <returns>Command line argument string</returns>
        string BuildCommandLine(ShortcutConfiguration configuration);

        /// <summary>
        /// Builds a protocol URL for a shortcut configuration
        /// </summary>
        /// <param name="configuration">Shortcut configuration</param>
        /// <returns>Protocol URL string</returns>
        string BuildProtocolUrl(ShortcutConfiguration configuration);

        /// <summary>
        /// Validates that a command line is properly formatted
        /// </summary>
        /// <param name="commandLine">Command line to validate</param>
        /// <returns>Validation result</returns>
        OperationResult ValidateCommandLine(string commandLine);
    }
}
