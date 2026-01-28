using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TMDbBoxSets.Configuration;

/// <summary>
/// Class holding the plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the minimum number of movies a collection should have to be created.
    /// </summary>
    public int MinimumNumberOfMovies { get; set; } = 2;

    /// <summary>
    /// Gets or sets a value indicating whether collection keywords should be stripped from the collection name.
    /// </summary>
    public bool StripCollectionKeywords { get; set; }

    /// <summary>
    /// Gets or sets the list of library ids to filter by.
    /// </summary>
    /// <remarks>Only collections containing movies from these libraries will be created.</remarks>
    /// <value>The list of library ids to filter by.</value>
    [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Configuration model is serialized/deserialized. It does not work with IEnumerable/IReadOnlyList")]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Configuration model is serialized/deserialized.")]
    public List<string> LibraryIds { get; set; } = [];
}
