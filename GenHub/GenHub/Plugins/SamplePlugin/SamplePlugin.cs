// SamplePlugin.cs
using GenHub.Core.Interfaces.Content;
using GenHub.Core.Models.Content;
using GenHub.Core.Models.Enums;
using GenHub.Core.Models.Manifest;
using GenHub.Core.Models.Results;
using GenHub.Features.Content.Services.ContentProviders;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Composition;

namespace SamplePlugin;

[Export(typeof(IContentProvider))]
public class SampleContentProvider : BaseContentProvider
{
    public SampleContentProvider(
        IContentValidator contentValidator,
        ILogger<SampleContentProvider> logger)
        : base(contentValidator, logger)
    {
    }

    public override string SourceName => "Sample Plugin";
    public override string Description => "A sample plugin demonstrating the plugin architecture";

    protected override IContentDiscoverer Discoverer => new SampleDiscoverer();
    protected override IContentResolver Resolver => new SampleResolver();
    protected override IContentDeliverer Deliverer => new HttpContentDeliverer(); // Reuse existing
}
