using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TMDbBoxSets.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TMDbBoxSets;

/// <summary>
/// Plugin class for TMDb box set management.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
        : base(appPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc/>
    public override string Name => "TMDb Box Sets";

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <inheritdoc/>
    public override string Description => "Automatically create movie box sets based on TMDb collections";

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public PluginConfiguration PluginConfiguration => Configuration;

    /// <inheritdoc/>
    public override Guid Id => new("bc4aad2e-d3d0-4725-a5e2-fd07949e5b42");

    /// <inheritdoc/>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "TMDb Box Sets",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
            }
        ];
    }
}
