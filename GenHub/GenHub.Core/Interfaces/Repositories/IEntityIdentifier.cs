namespace GenHub.Core.Interfaces.Repositories
{
    /// <summary>
    /// Interface for entities that have an identifier
    /// </summary>
    /// <typeparam name="T">Type of the identifier</typeparam>
    public interface IEntityIdentifier<T>
    {
        /// <summary>
        /// Gets or sets the unique identifier for this entity
        /// </summary>
        T Id { get; set; }
    }
}
