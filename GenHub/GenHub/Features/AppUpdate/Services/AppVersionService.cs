using System;
using System.Reflection;
using GenHub.Core.Interfaces.AppUpdate;

namespace GenHub.Features.AppUpdate.Services
{
    public class AppVersionService : IAppVersionService
    {
        private readonly string _version;

        public AppVersionService()
        {
            // Try to get the version from the entry assembly first (GenHub.Windows)
            var entryAssembly = Assembly.GetEntryAssembly();
            var version = entryAssembly?.GetName().Version;
            
            if (version != null && version.Major > 0)
            {
                _version = $"{version.Major}.{version.Minor}.{version.Build}";
            }
            else
            {
                // Fallback to GenHub assembly version
                var thisAssembly = Assembly.GetExecutingAssembly();
                version = thisAssembly.GetName().Version;
                
                if (version != null)
                {
                    _version = $"{version.Major}.{version.Minor}.{version.Build}";
                }
                else
                {
                    _version = "0.0.0"; // Hard-coded fallback - changed from "Unknown"
                }
            }
        }

        public string GetCurrentVersion()
        {
            return _version;
        }
    }
}
