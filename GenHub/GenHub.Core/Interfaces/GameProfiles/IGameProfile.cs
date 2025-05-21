
using GenHub.Core.Models;
using GenHub.Core.Models.SourceMetadata;
using GenHub.Core.Interfaces.Repositories;

namespace GenHub.Core.Interfaces
{
    public interface IGameProfile : IEntityIdentifier<string>
    {
        string Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string ExecutablePath { get; set; }
        string DataPath { get; set; }
        string IconPath { get; set; }
        string CoverImagePath { get; set; }
        string ColorValue { get; set; }
        string CommandLineArguments { get; set; }
        string VersionId { get; set; }
        bool IsDefaultProfile { get; set; }
        bool IsCustomProfile { get; set; }
        bool IsInstalled { get; set; }
        bool RunAsAdmin { get; set; }
        GameInstallationType SourceType { get; set; }
        BaseSourceMetadata? SourceSpecificMetadata { get; set; }        
        
        public GitHubSourceMetadata? GitHubMetadata => SourceSpecificMetadata as GitHubSourceMetadata;
        int DisplayOrder { get; set; }
    }
}
