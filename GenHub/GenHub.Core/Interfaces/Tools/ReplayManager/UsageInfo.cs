using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Tools.ReplayManager;

namespace GenHub.Core.Interfaces.Tools.ReplayManager;

/// <summary>
/// Represents usage information for upload limits.
/// </summary>
/// <param name="UsedBytes">The number of bytes used in the current week.</param>
/// <param name="LimitBytes">The maximum allowed bytes per week.</param>
/// <param name="ResetDate">The date and time when the usage resets.</param>
public readonly record struct UsageInfo(long UsedBytes, long LimitBytes, DateTime ResetDate);