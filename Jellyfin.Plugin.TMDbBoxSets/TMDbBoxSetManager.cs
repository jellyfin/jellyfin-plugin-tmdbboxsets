using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets
{
    public class TMDbBoxSetManager : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly Timer _timer;
        private readonly HashSet<string> _queuedTmdbCollectionIds;
        private readonly ILogger _logger; // TODO logging

        public TMDbBoxSetManager(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger logger)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _queuedTmdbCollectionIds = new HashSet<string>();
        }

        private void AddMoviesToCollection(IReadOnlyCollection<Movie> movies, string tmdbCollectionId, BoxSet boxSet)
        {
            int minimumNumberOfMovies = Plugin.Instance.PluginConfiguration.MinimumNumberOfMovies;
            if (movies.Count < minimumNumberOfMovies)
            {
                _logger.LogInformation("Minimum number of movies is {Count}, but there is/are only {MovieCount}: {MovieNames}",
                    minimumNumberOfMovies, movies.Count, string.Join(", ", movies.Select(m => m.Name)));
                return;
            }

            // Create the box set if it doesn't exist, but don't add anything to it on creation
            if (boxSet == null)
            {
                var tmdbCollectionName = movies.First().TmdbCollectionName;
                if (Plugin.Instance.PluginConfiguration.StripCollectionKeywords)
                {
                    tmdbCollectionName = tmdbCollectionName.Replace("Collection", String.Empty).Trim();
                }

                _logger.LogInformation("Box Set for {TmdbCollectionName} ({TmdbCollectionId}) does not exist. Creating it now!", tmdbCollectionName, tmdbCollectionId);
                boxSet = _collectionManager.CreateCollection(new CollectionCreationOptions
                {
                    Name = tmdbCollectionName,
                    ProviderIds = new Dictionary<string, string> {{MetadataProviders.Tmdb.ToString(), tmdbCollectionId}}
                });
            }

            var itemsToAdd = movies
                .Where(m => !boxSet.ContainsLinkedChildByItemId(m.Id))
                .Select(m => m.Id)
                .ToList();

            if (!itemsToAdd.Any())
            {
                _logger.LogInformation("The movies {MovieNames} is/are already in their proper box set, {BoxSetName}",
                    string.Join(", ", movies.Select(m => m.Name)), boxSet.Name);
                return;
            }
            
            _collectionManager.AddToCollection(boxSet.Id, itemsToAdd);
        }

        private IReadOnlyCollection<Movie> GetMoviesFromLibrary()
        {
            var movies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(Movie).Name},
                IsVirtualItem = false,
                OrderBy = new List<ValueTuple<string, SortOrder>>
                {
                    new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                },
                Recursive = true,
                HasTmdbId = true
            }).Select(m => m as Movie);

            // We are only interested in movies that belong to a TMDb collection
            return movies.Where(m =>
                m.HasProviderId(MetadataProviders.TmdbCollection) &&
                !string.IsNullOrWhiteSpace(m.GetProviderId(MetadataProviders.TmdbCollection))).ToList();
        }

        private IReadOnlyCollection<BoxSet> GetAllBoxSetsFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(BoxSet).Name},
                CollapseBoxSetItems = false,
                Recursive = true,
                HasTmdbId = true
            }).Select(b => b as BoxSet).ToList();
        }

        public void ScanLibrary(IProgress<double> progress)
        {
            var boxSets = GetAllBoxSetsFromLibrary();

            var movieCollections = GetMoviesFromLibrary()
                .GroupBy(m => m.GetProviderId(MetadataProviders.TmdbCollection))
                .ToArray();

            _logger.LogInformation("Found {Count} TMDb collection(s) across all movies", movieCollections.Length);
            int index = 0;
            foreach (var movieCollection in movieCollections)
            {
                progress?.Report(100.0 * index / movieCollections.Length);

                var tmdbCollectionId = movieCollection.Key;

                var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProviders.Tmdb) == tmdbCollectionId);
                AddMoviesToCollection(movieCollection.ToList(), tmdbCollectionId, boxSet);
                index++;
            }

            progress?.Report(100);
        }

        private void OnLibraryManagerItemUpdated(object sender, ItemChangeEventArgs e)
        {
            // Only support movies at this time
            if (!(e.Item is Movie movie) || e.Item.LocationType == LocationType.Virtual)
            {
                return;
            }

            // TODO: look it up?
            var tmdbCollectionId = movie.GetProviderId(MetadataProviders.TmdbCollection);
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
                    .Where(m => m.GetProviderId(MetadataProviders.TmdbCollection) == tmdbCollectionId)
                    .ToList();
                var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProviders.Tmdb) == tmdbCollectionId);

                AddMoviesToCollection(movieMatches, tmdbCollectionId, boxSet);
            }
        }

        public void Dispose()
        {
            _libraryManager.ItemUpdated -= OnLibraryManagerItemUpdated;
        }

        public Task RunAsync()
        {
            _libraryManager.ItemUpdated += OnLibraryManagerItemUpdated;

            return Task.CompletedTask;
        }
    }
}
