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
        LibraryIdsCSV = string.Empty;
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
    /// Gets or sets the list of library ids to filter by.
    /// </summary>
    /// <remarks>Only collections containing movies from these libraries will be created.</remarks>
    /// <value>The list of library ids to filter by.</value>
    public string LibraryIdsCSV { get; set; }
}
