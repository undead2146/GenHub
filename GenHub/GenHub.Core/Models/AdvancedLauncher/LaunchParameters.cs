using System;
using System.Collections.Generic;

namespace GenHub.Core.Models.AdvancedLauncher
{
    /// <summary>
    /// Represents parsed command line parameters for advanced launcher functionality
    /// </summary>
    public class LaunchParameters
    {
        /// <summary>
        /// Gets or sets the profile ID to launch
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the profile name to launch (alternative to ProfileId)
        /// </summary>
        public string? ProfileName { get; set; }

        /// <summary>
        /// Gets or sets the launch mode
        /// </summary>
        public LaunchMode Mode { get; set; } = LaunchMode.Normal;

        /// <summary>
        /// Gets or sets whether to skip pre-launch validation
        /// </summary>
        public bool SkipValidation { get; set; }

        /// <summary>
        /// Gets or sets whether to run in quiet mode (minimal output)
        /// </summary>
        public bool QuietMode { get; set; }

        /// <summary>
        /// Gets or sets whether to request administrative privileges
        /// </summary>
        public bool RunAsAdmin { get; set; }

        /// <summary>
        /// Gets or sets custom command line arguments to pass to the game
        /// </summary>
        public string? CustomArguments { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the game launch
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets environment variables to set for the game process
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        /// <summary>
        /// Gets or sets whether to show the launch dialog before launching
        /// </summary>
        public bool ShowLaunchDialog { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional delay before launching the game
        /// </summary>
        public TimeSpan? LaunchDelay { get; set; }

        /// <summary>
        /// Gets or sets whether to create a desktop shortcut after successful launch
        /// </summary>
        public bool CreateShortcut { get; set; }

        /// <summary>
        /// Gets or sets whether to register the protocol handler
        /// </summary>
        public bool RegisterProtocol { get; set; }

        /// <summary>
        /// Gets or sets whether to show verbose output for diagnostics
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets the action to perform (launch, validate, create-shortcut, etc.)
        /// </summary>
        public string Action { get; set; } = "launch";

        /// <summary>
        /// Gets or sets additional custom parameters
        /// </summary>
        public Dictionary<string, string> CustomParameters { get; set; } = new();

        /// <summary>
        /// Creates a copy of the launch parameters
        /// </summary>
        /// <returns>A new instance with copied values</returns>
        public LaunchParameters Clone()
        {
            return new LaunchParameters
            {
                ProfileId = this.ProfileId,
                ProfileName = this.ProfileName,
                Mode = this.Mode,
                SkipValidation = this.SkipValidation,
                QuietMode = this.QuietMode,
                RunAsAdmin = this.RunAsAdmin,
                CustomArguments = this.CustomArguments,
                WorkingDirectory = this.WorkingDirectory,
                EnvironmentVariables = new Dictionary<string, string>(this.EnvironmentVariables),
                ShowLaunchDialog = this.ShowLaunchDialog,
                LaunchDelay = this.LaunchDelay,
                CreateShortcut = this.CreateShortcut,
                RegisterProtocol = this.RegisterProtocol,
                Verbose = this.Verbose,
                Action = this.Action,
                CustomParameters = new Dictionary<string, string>(this.CustomParameters)
            };
        }
    }
}
