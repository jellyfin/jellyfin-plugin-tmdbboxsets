using System.Collections.ObjectModel;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TMDbBoxSets.Configuration;

/// <summary>
/// Class holding the plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration" /> class.
    /// </summary>
    public PluginConfiguration()
    {
        MinimumNumberOfMovies = 2;
        StripCollectionKeywords = false;
    }

    /// <summary>
    /// Gets or sets the minimum number of movies a collection should have to be created.
    /// </summary>
    public int MinimumNumberOfMovies { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collection keywords should be stripped from the collection name.
    /// </summary>
    public bool StripCollectionKeywords { get; set; }

    /// <summary>
    /// Gets the list of library names to exclude from box set creation.
    /// </summary>
    public Collection<string> ExcludedLibraries { get; init; } = [];

    /// <summary>
    /// Gets the list of TMDb collection IDs to exclude from box set creation.
    /// </summary>
    public Collection<string> ExcludedTmdbCollections { get; init; } = [];

    /// <summary>
    /// Gets the list of staged merge groups.
    /// </summary>
    public Collection<MergeGroup> MergeGroups { get; init; } = [];
}
