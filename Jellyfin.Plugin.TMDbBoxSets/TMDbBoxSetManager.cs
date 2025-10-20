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

            return;
        }

        // Create the box set if it doesn't exist, but don't add anything to it on creation
        if (boxSet is null)
        {
            var tmdbCollectionName = GetTmdbCollectionName(movies);
            if (string.IsNullOrWhiteSpace(tmdbCollectionName))
            {
                _logger.LogError(
                    "Can't get a proper box set name for the movies {MovieNames}. Make sure is propertly assigned to the movie info.",
                    string.Join(", ", movies.Select(m => m.Name)));

                return;
            }

            if (Plugin.Instance.PluginConfiguration.StripCollectionKeywords)
            {
                tmdbCollectionName = _collectionRegex.Replace(tmdbCollectionName, string.Empty).Trim();
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
            _logger.LogInformation(
                "The movies {MovieNames} is/are already in their proper box set, {BoxSetName}",
                string.Join(", ", movies.Select(m => m.Name)),
                boxSet.Name);

            return;
        }

        await _collectionManager.AddToCollectionAsync(boxSet.Id, itemsToAdd).ConfigureAwait(false);
    }

    private List<Movie> GetMoviesFromLibrary()
    {
        var allMovies = new List<Movie>();

        // convert csv string of ids to Guid
        var libraryIds = Plugin.Instance.PluginConfiguration.LibraryIdsCSV
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .ToList();

        _logger.LogInformation("Filtering movies by library IDs: {LibraryIds}", string.Join(", ", libraryIds));

        foreach (var libraryId in libraryIds)
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
                HasTmdbId = true,
                ParentId = libraryId
            }).Select(m => m as Movie);

            // We are only interested in movies that belong to a TMDb collection
            var filteredMovies = movies.Where(m =>
                m.HasProviderId(MetadataProvider.TmdbCollection) &&
                File.Exists(m.Path) && // This should fix the creation of collections missing/non-existent files
                !string.IsNullOrWhiteSpace(m.GetProviderId(MetadataProvider.TmdbCollection))).ToList();

            allMovies.AddRange(filteredMovies);
        }

        return allMovies;
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

    private string GetTmdbCollectionName(List<Movie> movies)
    {
        var collectionNames = movies
            .Select(movie => movie.TmdbCollectionName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        var collectionNamesCount = collectionNames.Count;
        var moviesCount = movies.Count;

        if (moviesCount != collectionNamesCount)
        {
            _logger.LogWarning(
                "Not all the movies in the box set ({MovieCount}) has a Tmdb Collection Name assigned ({Count}): {MovieNames}",
                moviesCount,
                collectionNamesCount,
                string.Join(", ", movies.Select(m => m.Name)));
        }

        var firstCollectionName = collectionNames.FirstOrDefault();
        if (collectionNames.Any(x => x != firstCollectionName))
        {
            _logger.LogWarning(
                "Not all the Tmdb Collection Names are the same for the box set (using the first one): {MovieNames}",
                string.Join(", ", movies.Select(m => string.Join(" - ", m.Name, m.TmdbCollectionName))));
        }

        return firstCollectionName;
    }

    /// <summary>
    /// Scans the library.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    public async Task ScanLibrary(IProgress<double> progress)
    {
        var boxSets = GetAllBoxSetsFromLibrary();

        var movieCollections = GetMoviesFromLibrary()
            .GroupBy(m => m.GetProviderId(MetadataProvider.TmdbCollection))
            .ToArray();

        _logger.LogInformation("Found {Count} TMDb collection(s) across all movies", movieCollections.Length);
        int index = 0;
        foreach (var movieCollection in movieCollections)
        {
            progress?.Report(100.0 * index / movieCollections.Length);

            var tmdbCollectionId = movieCollection.Key;

            var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProvider.Tmdb) == tmdbCollectionId);
            await AddMoviesToCollection(movieCollection.Where(m => string.IsNullOrEmpty(m.PrimaryVersionId)).ToList(), tmdbCollectionId, boxSet).ConfigureAwait(false);
            index++;
        }

        progress?.Report(100);
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
        foreach (var tmdbCollectionId in tmdbCollectionIds)
        {
            var movieMatches = movies
                .Where(m => m.GetProviderId(MetadataProvider.TmdbCollection) == tmdbCollectionId && string.IsNullOrEmpty(m.PrimaryVersionId))
                .ToList();
            var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProvider.Tmdb) == tmdbCollectionId);

            AddMoviesToCollection(movieMatches, tmdbCollectionId, boxSet).GetAwaiter().GetResult();
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
