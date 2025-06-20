using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using GenHub.Core.Interfaces.AdvancedLauncher;
using GenHub.Core.Models.AdvancedLauncher;
using GenHub.Core.Models.Results;

namespace GenHub.Features.AdvancedLauncher.Services
{
    /// <summary>
    /// Service for parsing command line arguments for advanced launcher functionality
    /// </summary>
    public class LauncherArgumentParser : ILauncherArgumentParser
    {
        private readonly ILogger<LauncherArgumentParser> _logger;

        public LauncherArgumentParser(ILogger<LauncherArgumentParser> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Parses command line arguments into launch parameters
        /// </summary>
        public OperationResult<LaunchParameters> ParseArguments(string[] args)
        {
            try
            {
                _logger.LogDebug("Parsing {ArgumentCount} command line arguments", args?.Length ?? 0);

                if (args == null || args.Length == 0)
                {
                    return OperationResult<LaunchParameters>.Succeeded(new LaunchParameters());
                }

                var parameters = new LaunchParameters();
                
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i].ToLowerInvariant();
                    
                    switch (arg)
                    {
                        case "--launch-profile":
                        case "-lp":
                            if (i + 1 < args.Length)
                            {
                                parameters.ProfileName = args[++i];
                                parameters.Action = "launch";
                            }
                            break;

                        case "--profile-id":
                        case "-pid":
                            if (i + 1 < args.Length)
                            {
                                parameters.ProfileId = args[++i];
                                parameters.Action = "launch";
                            }
                            break;

                        case "--quick-launch":
                        case "-q":
                            parameters.Mode = LaunchMode.Quick;
                            parameters.ShowLaunchDialog = false;
                            break;

                        case "--validate":
                        case "-v":
                            parameters.Mode = LaunchMode.Validate;
                            parameters.Action = "validate";
                            break;

                        case "--diagnostic":
                        case "-d":
                            parameters.Mode = LaunchMode.Diagnostic;
                            parameters.Verbose = true;
                            break;

                        case "--quiet":
                            parameters.QuietMode = true;
                            break;

                        case "--run-as-admin":
                        case "--admin":
                            parameters.RunAsAdmin = true;
                            break;

                        case "--skip-validation":
                            parameters.SkipValidation = true;
                            break;

                        case "--args":
                        case "--arguments":
                            if (i + 1 < args.Length)
                            {
                                parameters.CustomArguments = args[++i];
                            }
                            break;

                        case "--working-dir":
                        case "--wd":
                            if (i + 1 < args.Length)
                            {
                                parameters.WorkingDirectory = args[++i];
                            }
                            break;

                        case "--delay":
                            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int delaySeconds))
                            {
                                parameters.LaunchDelay = TimeSpan.FromSeconds(delaySeconds);
                                i++;
                            }
                            break;

                        case "--create-shortcut":
                            parameters.CreateShortcut = true;
                            break;

                        case "--register-protocol":
                            parameters.RegisterProtocol = true;
                            parameters.Action = "register-protocol";
                            break;

                        case "--verbose":
                            parameters.Verbose = true;
                            break;

                        case "--help":
                        case "-h":
                        case "/?":
                            parameters.Action = "help";
                            break;

                        case "--env":
                            if (i + 1 < args.Length)
                            {
                                var envVar = args[++i];
                                var parts = envVar.Split('=', 2);
                                if (parts.Length == 2)
                                {
                                    parameters.EnvironmentVariables[parts[0]] = parts[1];
                                }
                            }
                            break;

                        default:
                            if (arg.StartsWith("--"))
                            {
                                // Custom parameter
                                var key = arg[2..];
                                var value = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[++i] : "true";
                                parameters.CustomParameters[key] = value;
                            }
                            else if (!arg.StartsWith("-") && string.IsNullOrEmpty(parameters.ProfileName) && string.IsNullOrEmpty(parameters.ProfileId))
                            {
                                // Assume it's a profile name if no profile is set yet
                                parameters.ProfileName = args[i];
                                parameters.Action = "launch";
                            }
                            break;
                    }
                }

                var validationResult = ValidateParameters(parameters);
                if (!validationResult.Success)
                {
                    return OperationResult<LaunchParameters>.Failed(validationResult.Message ?? "Invalid parameters");
                }

                _logger.LogDebug("Successfully parsed arguments: Action={Action}, ProfileId={ProfileId}, Mode={Mode}", 
                    parameters.Action, parameters.ProfileId, parameters.Mode);

                return OperationResult<LaunchParameters>.Succeeded(parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing command line arguments");
                return OperationResult<LaunchParameters>.Failed($"Failed to parse arguments: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that the provided launch parameters are valid
        /// </summary>
        public OperationResult ValidateParameters(LaunchParameters parameters)
        {
            try
            {
                var errors = new System.Collections.Generic.List<string>();

                if (parameters.Action == "launch")
                {
                    if (string.IsNullOrEmpty(parameters.ProfileId) && string.IsNullOrEmpty(parameters.ProfileName))
                    {
                        errors.Add("Profile ID or profile name must be specified for launch action");
                    }
                }

                if (parameters.LaunchDelay.HasValue && parameters.LaunchDelay.Value.TotalSeconds < 0)
                {
                    errors.Add("Launch delay cannot be negative");
                }

                if (parameters.LaunchDelay.HasValue && parameters.LaunchDelay.Value.TotalMinutes > 10)
                {
                    errors.Add("Launch delay cannot exceed 10 minutes");
                }

                if (!string.IsNullOrEmpty(parameters.WorkingDirectory) && !System.IO.Directory.Exists(parameters.WorkingDirectory))
                {
                    errors.Add($"Working directory does not exist: {parameters.WorkingDirectory}");
                }

                if (errors.Any())
                {
                    var errorMessage = string.Join("; ", errors);
                    _logger.LogWarning("Parameter validation failed: {Errors}", errorMessage);
                    return OperationResult.Failed(errorMessage);
                }

                return OperationResult.Succeeded();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating parameters");
                return OperationResult.Failed($"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds command line arguments from launch parameters
        /// </summary>
        public string BuildArgumentString(LaunchParameters parameters)
        {
            try
            {
                var args = new System.Collections.Generic.List<string>();

                if (!string.IsNullOrEmpty(parameters.ProfileId))
                {
                    args.Add($"--profile-id \"{parameters.ProfileId}\"");
                }
                else if (!string.IsNullOrEmpty(parameters.ProfileName))
                {
                    args.Add($"--launch-profile \"{parameters.ProfileName}\"");
                }

                switch (parameters.Mode)
                {
                    case LaunchMode.Quick:
                        args.Add("--quick-launch");
                        break;
                    case LaunchMode.Validate:
                        args.Add("--validate");
                        break;
                    case LaunchMode.Diagnostic:
                        args.Add("--diagnostic");
                        break;
                }

                if (parameters.QuietMode)
                    args.Add("--quiet");

                if (parameters.RunAsAdmin)
                    args.Add("--run-as-admin");

                if (parameters.SkipValidation)
                    args.Add("--skip-validation");

                if (!string.IsNullOrEmpty(parameters.CustomArguments))
                    args.Add($"--args \"{parameters.CustomArguments}\"");

                if (!string.IsNullOrEmpty(parameters.WorkingDirectory))
                    args.Add($"--working-dir \"{parameters.WorkingDirectory}\"");

                if (parameters.LaunchDelay.HasValue)
                    args.Add($"--delay {(int)parameters.LaunchDelay.Value.TotalSeconds}");

                if (parameters.CreateShortcut)
                    args.Add("--create-shortcut");

                if (parameters.RegisterProtocol)
                    args.Add("--register-protocol");

                if (parameters.Verbose)
                    args.Add("--verbose");

                foreach (var envVar in parameters.EnvironmentVariables)
                {
                    args.Add($"--env \"{envVar.Key}={envVar.Value}\"");
                }

                foreach (var customParam in parameters.CustomParameters)
                {
                    if (customParam.Value == "true")
                        args.Add($"--{customParam.Key}");
                    else
                        args.Add($"--{customParam.Key} \"{customParam.Value}\"");
                }

                return string.Join(" ", args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building argument string");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets help text for command line usage
        /// </summary>
        public string GetHelpText()
        {
            return @"
GenHub Advanced Launcher - Command Line Usage

USAGE:
    GenHub.exe [OPTIONS] [PROFILE_NAME]

OPTIONS:
    --launch-profile, -lp <name>     Launch the specified profile by name
    --profile-id, -pid <id>          Launch the specified profile by ID
    --quick-launch, -q               Launch directly without showing UI
    --validate, -v                   Validate profile without launching
    --diagnostic, -d                 Launch with diagnostic information
    --quiet                          Suppress output messages
    --run-as-admin, --admin          Request administrative privileges
    --skip-validation                Skip pre-launch validation
    --args <arguments>               Custom arguments to pass to game
    --working-dir, --wd <path>       Set working directory for game
    --delay <seconds>                Delay before launching (max 600)
    --create-shortcut                Create desktop shortcut after launch
    --register-protocol              Register genhub:// protocol handler
    --verbose                        Show detailed output
    --env <VAR=value>                Set environment variable
    --help, -h, /?                   Show this help message

EXAMPLES:
    GenHub.exe ""My Profile""                    Launch profile by name
    GenHub.exe --quick-launch --profile-id abc  Quick launch by ID
    GenHub.exe --validate ""My Profile""         Validate profile only
    GenHub.exe --register-protocol             Register URL protocol
    GenHub.exe --diagnostic --verbose          Show diagnostics

PROTOCOL URLS:
    genhub://launch/profile/<id>     Launch profile by ID
    genhub://create-shortcut/<id>    Create shortcut for profile
    genhub://manage-shortcuts        Open shortcut manager
";
        }
    }
}
