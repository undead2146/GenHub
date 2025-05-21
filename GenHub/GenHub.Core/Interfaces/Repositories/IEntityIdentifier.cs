namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Interface for entities that have an identifier
    /// </summary>
    /// <typeparam name="TKey">The type of the entity identifier</typeparam>
    public interface IEntityIdentifier<TKey>
    {
        /// <summary>
        /// Gets the identifier of the entity
        /// </summary>
        TKey Id { get; }
    }
}
