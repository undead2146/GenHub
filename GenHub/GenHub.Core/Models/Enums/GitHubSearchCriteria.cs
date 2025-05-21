namespace GenHub.Core.Models.Enums
{
    /// <summary>
    /// Search criteria for GitHub workflows
    /// </summary>
    public enum GitHubSearchCriteria
    {
        /// <summary>
        /// Search all fields
        /// </summary>
        All,
        
        /// <summary>
        /// Search by workflow number
        /// </summary>
        WorkflowNumber,
        
        /// <summary>
        /// Search by commit message
        /// </summary>
        CommitMessage,
        
        /// <summary>
        /// Search by pull request number
        /// </summary>
        PullRequestNumber
    }
}
