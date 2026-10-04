using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.TMDbBoxSets.Configuration;

/// <summary>
/// Defines a staged merge group mapping multiple TMDb collections into one box set.
/// </summary>
public class MergeGroup
{
    /// <summary>
    /// Gets or sets the user-facing display name for this merge group.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary TMDb collection ID used for metadata retrieval (e.g. posters).
    /// Defaults to the first secondary collection ID.
    /// </summary>
    public string PrimaryTmdbCollectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the secondary TMDb collection IDs that form this merge group.
    /// </summary>
    public Collection<string> SecondaryTmdbCollectionIds { get; init; } = [];
}
