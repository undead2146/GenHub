// filepath: z:\GenHub\GenHub\GenHub.Core\Interfaces\AppUpdate\IUpdateInstaller.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models.AppUpdate;
using GenHub.Core.Models.GitHub;

namespace GenHub.Core.Interfaces.AppUpdate
{
    /// <summary>
    /// Interface for platform-specific update installers
    /// </summary>
    public interface IUpdateInstaller
    {
        /// <summary>
        /// Installs an application update from the specified release
        /// </summary>
        /// <param name="release">The release to install</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InstallUpdateAsync(GitHubRelease release, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
