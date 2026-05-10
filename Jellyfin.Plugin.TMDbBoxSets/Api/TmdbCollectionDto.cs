namespace Jellyfin.Plugin.TMDbBoxSets.Api;

/// <summary>
/// Represents a TMDb collection discovered across library movies.
/// </summary>
/// <param name="TmdbCollectionId">The TMDb collection ID.</param>
/// <param name="CollectionName">The collection display name.</param>
public record TmdbCollectionDto(string TmdbCollectionId, string CollectionName);
