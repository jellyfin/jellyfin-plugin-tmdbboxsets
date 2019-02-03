using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace TMDbCollections
{
    public class TMDbCollectionCreator : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly IProviderManager _providerManager;
        private readonly ILogger _logger;

        public TMDbCollectionCreator(ILibraryManager libraryManager, ICollectionManager collectionManager, IProviderManager providerManager, ILogger logger)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _providerManager = providerManager;
            _logger = logger;
        }

        private void AddMoviesToCollection(IReadOnlyCollection<Movie> movies, string tmdbCollectionId)
        {
            var boxSet = GetBoxSetFromLibrary(tmdbCollectionId).FirstOrDefault();
            AddMoviesToCollection(movies, tmdbCollectionId, boxSet);
        }
        
        private void AddMoviesToCollection(IReadOnlyCollection<Movie> movies, string tmdbCollectionId, BoxSet boxSet)
        {
            // Create the box set if it doesn't exist, but don't add anything to it on creation
            if (boxSet == null)
            {
                boxSet = _collectionManager.CreateCollection(new CollectionCreationOptions
                {
                    Name = movies.First().TmdbCollectionName,
                    ProviderIds = new Dictionary<string, string> {{MetadataProviders.Tmdb.ToString(), tmdbCollectionId}}
                });
            }

            var itemsToAdd = movies
                .Where(m => !boxSet.ContainsLinkedChildByItemId(m.Id))
                .Select(m => m.Id);
            
            _collectionManager.AddToCollection(boxSet.Id, itemsToAdd);
        }

        private IEnumerable<Movie> GetMoviesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(Movie).Name},
                IsVirtualItem = false,
                OrderBy = new[]
                {
                    new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                },
                Recursive = true,
                HasTmdbId = true
            }).Select(m => m as Movie).Where(m => m.HasProviderId(MetadataProviders.TmdbCollection));
        }
        
        private IEnumerable<BoxSet> GetBoxSetFromLibrary(string tmdbId)
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(BoxSet).Name},
                CollapseBoxSetItems = false,
                Recursive = true,
                HasAnyProviderId = new Dictionary<string, string> {{MetadataProviders.Tmdb.ToString(), tmdbId}}
            }).Select(b => b as BoxSet);
        }
        
        private IReadOnlyCollection<BoxSet> GetBoxSetsFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {typeof(BoxSet).Name},
                CollapseBoxSetItems = false,
                Recursive = true
            }).Select(b => b as BoxSet).Where(b => b.HasProviderId(MetadataProviders.Tmdb)).ToList();
        }

        public void ScanLibrary()
        {
            var existingMovies = GetMoviesFromLibrary();
            var boxSets = GetBoxSetsFromLibrary();

            var movieCollections = new Dictionary<string, List<Movie>>();
            foreach (Movie movie in existingMovies)
            {
                var tmdbCollectionId = movie.GetProviderId(MetadataProviders.TmdbCollection);
                if (string.IsNullOrWhiteSpace(tmdbCollectionId))
                {
                    continue;
                }

                if (!movieCollections.TryGetValue(tmdbCollectionId, out var movies))
                {
                    movies = new List<Movie>();
                    movieCollections.Add(tmdbCollectionId, movies);
                }
                movies.Add(movie);
            }

            foreach (var movieCollection in movieCollections)
            {
                var tmdbCollectionId = movieCollection.Key;
                var movies = movieCollection.Value;

                var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(MetadataProviders.Tmdb) == tmdbCollectionId);
                AddMoviesToCollection(movies, tmdbCollectionId, boxSet);
            }
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
            
            var movieMatches = GetMoviesFromLibrary()
                .Where(m => m.GetProviderId(MetadataProviders.TmdbCollection) == tmdbCollectionId).ToList();
            
            AddMoviesToCollection(movieMatches, tmdbCollectionId);
        }
        
        public void Dispose()
        {
            _libraryManager.ItemUpdated -= OnLibraryManagerItemUpdated;
        }

        public void Run()
        {
            _libraryManager.ItemUpdated += OnLibraryManagerItemUpdated;
        }
    }
}