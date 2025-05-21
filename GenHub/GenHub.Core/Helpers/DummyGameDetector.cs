using GenHub.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GenHub.Core.Models; // For IGameInstallation

namespace GenHub.Core.Helpers
{
    /// <summary>
    /// A dummy implementation of <see cref="IGameDetector"/> for testing or fallback scenarios.
    /// This detector does not perform any actual game detection.
    /// </summary>
    public class DummyGameDetector : IGameDetector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DummyGameDetector"/> class.
        /// </summary>
        public DummyGameDetector()
        {
            // Constructor can be empty or take ILogger if needed for consistency, though not used here.
        }

        /// <summary>
        /// Asynchronously "detects" game installations. In this dummy implementation,
        /// it always returns an empty collection.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests (not used in this dummy implementation).</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains an empty
        /// collection of <see cref="IGameInstallation"/> objects.
        /// </returns>
        public Task<IEnumerable<IGameInstallation>> DetectInstallationsAsync(CancellationToken cancellationToken = default)
        {
            // Return an empty list of installations.
            return Task.FromResult(Enumerable.Empty<IGameInstallation>());
        }
    }
}
