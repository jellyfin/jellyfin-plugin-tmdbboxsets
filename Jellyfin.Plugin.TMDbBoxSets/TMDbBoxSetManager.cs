using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets;

/// <summary>
/// Class TMDbBoxSetManager.
/// </summary>
public class TMDbBoxSetManager : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly Timer _timer;
    private readonly HashSet<string> _queuedTmdbCollectionIds;
    private readonly ILogger<TMDbBoxSetManager> _logger;

    private readonly Regex _collectionRegex = new Regex(
        @"(( |( - ))+\(?\[?(colecci[oó]n|collection|f[ií]lmreihe|поредица|kolekce|系列|시리즈|samling|kolekcia|saga|מארז|კრებული|collectie|gyűjtemény|collezione|シリーズ|samlingen|مجموعه|kolekcja|coletânea|coleção|colecția|коллекция|รวมชุด|seri|кіноцикл|kolleksiyasi)\)?\]?)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbBoxSetManager"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbBoxSetManager}"/> interface.</param>
    public TMDbBoxSetManager(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<TMDbBoxSetManager> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _logger = logger;
        _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        _queuedTmdbCollectionIds = new HashSet<string>();
    }

    private async Task AddMoviesToCollection(List<Movie> movies, string tmdbCollectionId, BoxSet boxSet)
    {
        int minimumNumberOfMovies = Plugin.Instance.PluginConfiguration.MinimumNumberOfMovies;
        if (movies.Count < minimumNumberOfMovies)
        {
            _logger.LogInformation(
                "Minimum number of movies is {Count}, but there is/are only {MovieCount}: {MovieNames}",
                minimumNumberOfMovies,
                movies.Count,
                string.Join(", ", movies.Select(m => m.Name)));

            // If a box set exists but doesn't meet the minimum requirement, remove it
            if (boxSet is not null)
            {
                _logger.LogInformation(
                "Removing box set {BoxSetName} ({TmdbCollectionId}) as it no longer meets the minimum movie requirement",
                boxSet.Name,
                tmdbCollectionId);

                _libraryManager.DeleteItem(boxSet, new DeleteOptions
                {
                    DeleteFileLocation = true
                });
            }

            return;
        }

        // Create the box set if it doesn't exist, but don't add anything to it on creation
        if (boxSet is null)
        {
            var tmdbCollectionName = GetTmdbCollectionName(movies, tmdbCollectionId);
            if (string.IsNullOrWhiteSpace(tmdbCollectionName))
            {
                _logger.LogError(
                    "Can't get a proper box set name for the movies {MovieNames}. Make sure is propertly assigned to the movie info.",
                    string.Join(", ", movies.Select(m => m.Name)));

                return;
            }

            _logger.LogInformation("Box Set for {TmdbCollectionName} ({TmdbCollectionId}) does not exist. Creating it now!", tmdbCollectionName, tmdbCollectionId);
            boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
            {
                Name = tmdbCollectionName,
                ProviderIds = new Dictionary<string, string> { { MetadataProvider.Tmdb.ToString(), tmdbCollectionId } }
            }).ConfigureAwait(false);
        }

        var itemsToAdd = movies
            .Where(m => !boxSet.ContainsLinkedChildByItemId(m.Id))
            .Select(m => m.Id)
            .ToList();

        if (itemsToAdd.Count == 0)
        {
            _logger.LogDebug(
                "The movies {MovieNames} is/are already in their proper box set, {BoxSetName}",
                string.Join(", ", movies.Select(m => m.Name)),
                boxSet.Name);

            return;
        }

        await _collectionManager.AddToCollectionAsync(boxSet.Id, itemsToAdd).ConfigureAwait(false);
    }

    private List<Movie> GetMoviesFromLibrary()
    {
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            OrderBy = new List<(ItemSortBy, SortOrder)>
            {
                new(ItemSortBy.SortName, SortOrder.Ascending)
            },
            Recursive = true,
            HasTmdbId = true
        }).Select(m => m as Movie);

        IEnumerable<string> excludedLibraries = Plugin.Instance.PluginConfiguration.ExcludedLibraries ?? [];
        _logger.LogDebug("_Ignoring the following libraries during the scan: {LibraryNames}", string.Join(", ", excludedLibraries));

        IEnumerable<string> excludedTmdbCollections = Plugin.Instance.PluginConfiguration.ExcludedTmdbCollections ?? [];
        _logger.LogDebug("Excluding the following TMDb collections from the scan: {CollectionIds}", string.Join(", ", excludedTmdbCollections));

        // We are only interested in movies that belong to a TMDb collection
        // Any movies that the plugin should ignore are excluded here.
        return movies.Where(m =>
            m.HasProviderId(MetadataProvider.TmdbCollection) &&
            _libraryManager.GetLibraryOptions(m).Enabled &&
            !excludedTmdbCollections.Contains(m.GetProviderId(MetadataProvider.TmdbCollection)) &&
            !excludedLibraries.Contains(_libraryManager.GetCollectionFolders(m).FirstOrDefault()?.Name) &&
            !string.IsNullOrWhiteSpace(m.GetProviderId(MetadataProvider.TmdbCollection))).ToList();
    }

    private List<BoxSet> GetAllBoxSetsFromLibrary()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
            CollapseBoxSetItems = false,
            Recursive = true,
            HasTmdbId = true
        }).Select(b => b as BoxSet).ToList();
    }

    private IGrouping<string, Movie>[] BuildMergedMovieCollections(IGrouping<string, Movie>[] movieCollections)
    {
        // Build a mapping of secondary TMDb collection IDs to their primary (merge target) ID,
        // then re-group movies so that each merge group is processed as a single collection.
        var mergeGroups = Plugin.Instance.PluginConfiguration.MergeGroups ?? [];
        var secondaryToPrimary = mergeGroups
            .Where(mg => !string.IsNullOrWhiteSpace(mg.PrimaryTmdbCollectionId))
            .SelectMany(mg => (mg.SecondaryTmdbCollectionIds ?? [])
                .Select(secondaryId => (SecondaryId: secondaryId, PrimaryId: mg.PrimaryTmdbCollectionId)))
            .ToDictionary(x => x.SecondaryId, x => x.PrimaryId);
        var mergedMovieCollections = movieCollections
            .SelectMany(g => g.Select(m => (
                EffectiveCollectionId: secondaryToPrimary.TryGetValue(g.Key, out var primaryId) ? primaryId : g.Key,
                Movie: m)))
            .GroupBy(x => x.EffectiveCollectionId, x => x.Movie)
            .ToArray();

        return mergedMovieCollections;
    }

    /// <summary>
    /// Gets all TMDb collections discovered across library movies, regardless of exclusion settings.
    /// </summary>
    /// <returns>An enumerable of TMDb collection ID and name pairs.</returns>
    public IEnumerable<(string TmdbCollectionId, string CollectionName)> GetCandidateCollections()
    {
        return _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            Recursive = true,
            HasTmdbId = true
        })
        .OfType<Movie>()
        .Where(m => m.HasProviderId(MetadataProvider.TmdbCollection))
        .GroupBy(m => m.GetProviderId(MetadataProvider.TmdbCollection))
        .Select(g => (
            TmdbCollectionId: g.Key,
            CollectionName: g.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.TmdbCollectionName))?.TmdbCollectionName ?? g.Key))
        .OrderBy(c => c.CollectionName);
    }

    private string GetTmdbCollectionName(List<Movie> movies, string primaryTmdbCollectionId)
    {
        var config = Plugin.Instance.PluginConfiguration;
        var mergeGroups = config.MergeGroups ?? [];

        // 1. Custom name from merge group.
        var matchingMergeGroup = mergeGroups.FirstOrDefault(
            mg => mg.PrimaryTmdbCollectionId == primaryTmdbCollectionId);

        if (!string.IsNullOrWhiteSpace(matchingMergeGroup?.DisplayName))
        {
            _logger.LogDebug(
                "Using custom merge group display name {DisplayName} for TMDb collection {TmdbCollectionId}",
                matchingMergeGroup.DisplayName,
                primaryTmdbCollectionId);

            return matchingMergeGroup.DisplayName;
        }

        // 2. Use the TmdbCollectionName from a movie that belongs to the primary collection.
        var primaryCollectionName = movies
            .Where(m => m.GetProviderId(MetadataProvider.TmdbCollection) == primaryTmdbCollectionId
                && !string.IsNullOrWhiteSpace(m.TmdbCollectionName))
            .Select(m => m.TmdbCollectionName)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(primaryCollectionName))
        {
            if (config.StripCollectionKeywords)
            {
                primaryCollectionName = _collectionRegex.Replace(primaryCollectionName, string.Empty).Trim();
            }

            return primaryCollectionName;
        }

        // 3. Fall back to any non-empty collection name (e.g. from a secondary collection).
        var fallbackName = movies
            .Select(m => m.TmdbCollectionName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            _logger.LogWarning(
                "Could not find a collection name for the primary TMDb collection {TmdbCollectionId}. Falling back to a name from a secondary TMDb collection: {FallbackName}",
                primaryTmdbCollectionId,
                fallbackName);

            if (config.StripCollectionKeywords)
            {
                fallbackName = _collectionRegex.Replace(fallbackName, string.Empty).Trim();
            }
        }

        return fallbackName;
    }

    /// <summary>
    /// Scans the library to update the automatically created box sets based on TMDb collection IDs.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    public async Task ScanLibrary(IProgress<double> progress)
    {
        if (!ValidateMergeGroupConfig())
        {
            progress?.Report(100);
            return;
        }

        var movieCollections = GetMoviesFromLibrary()
            .GroupBy(m => m.GetProviderId(MetadataProvider.TmdbCollection))
            .ToArray();
        var boxSets = GetAllBoxSetsFromLibrary();

        // We need to get the updated boxsets after each method because those methods might have deleted some boxsets.
        // This would lead to errors in the next methods as they would try to access boxsets that no longer exist.
        RemoveBoxSetsWithIncorrectNames(boxSets);
        boxSets = GetAllBoxSetsFromLibrary();
        CleanupOrphanedBoxSets(boxSets, movieCollections);
        boxSets = GetAllBoxSetsFromLibrary();
        CleanupAfterConfigChanges(boxSets);
        boxSets = GetAllBoxSetsFromLibrary();
        var mergedMovieCollections = BuildMergedMovieCollections(movieCollections);

        _logger.LogDebug("Found {Count} TMDb collection(s) across all movies", movieCollections.Length);
        _logger.LogDebug("After merging, processing {Count} TMDb collection(s)", mergedMovieCollections.Length);

        int index = 0;
        foreach (var movieCollection in mergedMovieCollections)
        {
            progress?.Report(100.0 * index / mergedMovieCollections.Length);

            var tmdbCollectionId = movieCollection.Key;
            var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProvider.Tmdb) == tmdbCollectionId);

            await AddMoviesToCollection(
                movieCollection.Where(m => string.IsNullOrEmpty(m.PrimaryVersionId)).ToList(),
                tmdbCollectionId,
                boxSet).ConfigureAwait(false);
            index++;
        }

        progress?.Report(100);
    }

    private bool ValidateMergeGroupConfig()
    {
        var mergeGroups = Plugin.Instance.PluginConfiguration.MergeGroups ?? [];
        var duplicateIds = mergeGroups
            .SelectMany(mg => mg.SecondaryTmdbCollectionIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            _logger.LogError(
                "Invalid merge group configuration: TMDb collection/s {SecondaryTmdbCollectionIds} is/are used in more than one merge set. Aborting scan. Please fix the TMDb Box Sets plugin configuration and try again.",
                string.Join(", ", duplicateIds));
            return false;
        }

        return true;
    }

    private void OnLibraryManagerItemUpdated(object sender, ItemChangeEventArgs e)
    {
        // Only support movies at this time
        if (e.Item is not Movie movie || e.Item.LocationType == LocationType.Virtual)
        {
            return;
        }

        // TODO: look it up?
        var tmdbCollectionId = movie.GetProviderId(MetadataProvider.TmdbCollection);
        if (string.IsNullOrEmpty(tmdbCollectionId))
        {
            return;
        }

        _queuedTmdbCollectionIds.Add(tmdbCollectionId);

        // Restart the timer. After idling for 5 seconds it should trigger the callback. This is to avoid clobbering during a large library update.
        _timer.Change(5000, Timeout.Infinite);
    }

    private void OnTimerElapsed()
    {
        // Stop the timer until next update
        _timer.Change(Timeout.Infinite, Timeout.Infinite);

        var tmdbCollectionIds = _queuedTmdbCollectionIds.ToArray();
        // Clear the queue now, TODO what if it crashes? Should it be cleared after it's done?
        _queuedTmdbCollectionIds.Clear();

        var boxSets = GetAllBoxSetsFromLibrary();
        var movies = GetMoviesFromLibrary();
        var movieCollections = movies
            .GroupBy(m => m.GetProviderId(MetadataProvider.TmdbCollection))
            .ToArray();

        CleanupOrphanedBoxSets(boxSets, movieCollections);

        foreach (var tmdbCollectionId in tmdbCollectionIds)
        {
            var movieMatches = movies
                .Where(m => m.GetProviderId(MetadataProvider.TmdbCollection) == tmdbCollectionId && string.IsNullOrEmpty(m.PrimaryVersionId))
                .ToList();
            var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProvider.Tmdb) == tmdbCollectionId);

            AddMoviesToCollection(movieMatches, tmdbCollectionId, boxSet).GetAwaiter().GetResult();
        }
    }

    private void CleanupOrphanedBoxSets(List<BoxSet> boxSets, IGrouping<string, Movie>[] movieCollections)
    {
        // If a merge-box-set's primary TMDb collection no longer has any movies in the library the boxset will be deleted.
        // But if other TMDb collections that are part of the same merge group still have movies in the library, the boxset will be recreated later on.

        var mergeGroups = Plugin.Instance.PluginConfiguration.MergeGroups ?? [];
        var primaryCollectionIdsInMergeGroups = mergeGroups
            .Select(mg => mg.PrimaryTmdbCollectionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet();

        var nonPrimaryCollectionIdsInMergeGroups = mergeGroups
            .SelectMany(mg => mg.SecondaryTmdbCollectionIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id) && !primaryCollectionIdsInMergeGroups.Contains(id))
            .ToHashSet();

        foreach (var boxSet in boxSets)
        {
            if (!movieCollections.Any(mc => mc.Key == boxSet.GetProviderId(MetadataProvider.Tmdb)))
            {
                _logger.LogInformation(
                    "Removing orphaned box set {BoxSetName} ({TmdbCollectionId}) as there are no movies assigned to it anymore",
                    boxSet.Name,
                    boxSet.GetProviderId(MetadataProvider.Tmdb));
                _libraryManager.DeleteItem(boxSet, new DeleteOptions
                {
                    DeleteFileLocation = true
                });
            }
            else if (nonPrimaryCollectionIdsInMergeGroups.Contains(boxSet.GetProviderId(MetadataProvider.Tmdb)))
            {
                _logger.LogInformation(
                    "Removing box set {BoxSetName} ({TmdbCollectionId}) as its TMDb collection ID is a non-primary collection in a merge group and it shouldn't exist on its own",
                    boxSet.Name,
                    boxSet.GetProviderId(MetadataProvider.Tmdb));
                _libraryManager.DeleteItem(boxSet, new DeleteOptions
                {
                    DeleteFileLocation = true
                });
            }
        }
    }

    private void CleanupAfterConfigChanges(List<BoxSet> boxSets)
    {
        var mergeGroups = Plugin.Instance.PluginConfiguration.MergeGroups ?? [];
        var mergeGroupTmdbIds = mergeGroups
            .Select(mg => mg.PrimaryTmdbCollectionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
        var excludedTmdbCollections = Plugin.Instance.PluginConfiguration.ExcludedTmdbCollections ?? [];
        var excludedLibraries = Plugin.Instance.PluginConfiguration.ExcludedLibraries ?? [];

        foreach (var boxSet in boxSets)
        {
            List<string> allowedSecondaryIds;

            if (mergeGroupTmdbIds.Contains(boxSet.GetProviderId(MetadataProvider.Tmdb)))
            {
                allowedSecondaryIds = mergeGroups
                    .Where(mg => mg.PrimaryTmdbCollectionId == boxSet.GetProviderId(MetadataProvider.Tmdb))
                    .SelectMany(mg => mg.SecondaryTmdbCollectionIds ?? [])
                    .ToList();
            }
            else
            {
                allowedSecondaryIds = [boxSet.GetProviderId(MetadataProvider.Tmdb)];
            }

            var moviesInBoxSet = boxSet.GetRecursiveChildren()
                .OfType<Movie>()
                .ToList();

            foreach (var movie in moviesInBoxSet)
            {
                var movieTmdbCollectionId = movie.GetProviderId(MetadataProvider.TmdbCollection);

                if (!allowedSecondaryIds.Contains(movieTmdbCollectionId))
                {
                    _logger.LogInformation(
                        "Removing movie {MovieName} from box set {BoxSetName} as its TMDb collection ID {MovieTmdbCollectionId} is no longer in the allowed secondary collection IDs for this box set",
                        movie.Name,
                        boxSet.Name,
                        movieTmdbCollectionId);

                    _collectionManager.RemoveFromCollectionAsync(boxSet.Id, new List<Guid> { movie.Id }).GetAwaiter().GetResult();
                }
                else if (excludedTmdbCollections.Contains(movieTmdbCollectionId))
                {
                    // This is to make sure that if a TMDb collection is added to the excluded list after beeing added to a merge box set, any movies that belong to that TMDB collection are correctly removed from their merge box set.
                    _logger.LogInformation(
                        "Removing movie {MovieName} from box set {BoxSetName} as its TMDb collection ID {MovieTmdbCollectionId} is now in the list of excluded TMDb collections",
                        movie.Name,
                        boxSet.Name,
                        movieTmdbCollectionId);

                    _collectionManager.RemoveFromCollectionAsync(boxSet.Id, new List<Guid> { movie.Id }).GetAwaiter().GetResult();
                }
                else if (excludedLibraries.Contains(_libraryManager.GetCollectionFolders(movie).FirstOrDefault()?.Name))
                {
                    // This is to make sure that if a library is added to the excluded list after beeing added to a merge box set, any movies that belong to that library are correctly removed from their merge box set.
                    _logger.LogInformation(
                        "Removing movie {MovieName} from box set {BoxSetName} as its library {LibraryName} is now in the list of excluded libraries",
                        movie.Name,
                        boxSet.Name,
                        _libraryManager.GetCollectionFolders(movie).FirstOrDefault()?.Name);

                    _collectionManager.RemoveFromCollectionAsync(boxSet.Id, new List<Guid> { movie.Id }).GetAwaiter().GetResult();
                }
            }
        }
    }

    private void RemoveBoxSetsWithIncorrectNames(List<BoxSet> boxSets)
    {
        var mergeGroups = Plugin.Instance.PluginConfiguration.MergeGroups ?? [];

        foreach (var boxSet in boxSets)
        {
            var mergeGroup = mergeGroups.FirstOrDefault(mg => mg.PrimaryTmdbCollectionId == boxSet.GetProviderId(MetadataProvider.Tmdb));
            if (mergeGroup is not null)
            {
                if (!string.IsNullOrWhiteSpace(mergeGroup.DisplayName) && boxSet.Name != mergeGroup.DisplayName)
                {
                    _logger.LogInformation(
                        "Removing merged box set {BoxSetName} ({TmdbCollectionId}) as its name does not match the configured display name for its merge group. It will be recreated with the correct name later. Current name: {CurrentName}, Expected name: {ExpectedName}",
                        boxSet.Name,
                        boxSet.GetProviderId(MetadataProvider.Tmdb),
                        boxSet.Name,
                        mergeGroup.DisplayName);

                    _libraryManager.DeleteItem(boxSet, new DeleteOptions
                    {
                        DeleteFileLocation = true
                    });
                }

                continue;
            }
            else if (boxSet.HasProviderId(MetadataProvider.Tmdb))
            {
                var tmdbCollectionName = GetTmdbCollectionName(
                    boxSet.GetRecursiveChildren()
                        .OfType<Movie>()
                        .Where(m => m.HasProviderId(MetadataProvider.TmdbCollection))
                        .ToList(),
                    boxSet.GetProviderId(MetadataProvider.Tmdb));

                if (!string.IsNullOrWhiteSpace(tmdbCollectionName) && boxSet.Name != tmdbCollectionName)
                {
                    _logger.LogInformation(
                        "Removing box set {BoxSetName} ({TmdbCollectionId}) as its name does not match the TMDb collection name from its movies. It will be recreated with the correct name later. Current name: {CurrentName}, Expected name: {ExpectedName}",
                        boxSet.Name,
                        boxSet.GetProviderId(MetadataProvider.Tmdb),
                        boxSet.Name,
                        tmdbCollectionName);

                    _libraryManager.DeleteItem(boxSet, new DeleteOptions
                    {
                        DeleteFileLocation = true
                    });
                }
            }
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnLibraryManagerItemUpdated;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnLibraryManagerItemUpdated;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            _timer.Dispose();
        }
    }
}
