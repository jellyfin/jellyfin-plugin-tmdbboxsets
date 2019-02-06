using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.Api
{
    [Route("/TMDbBoxSets/Refresh", "POST", Summary = "Scans all movies and creates box sets")]
    [Authenticated]
    public class RefreshMetadataRequest : IReturnVoid
    {
    }
    
    public class TMDbCollectionsService : IService
    {
        private readonly TMDbBoxSetManager _tmDbBoxSetManager;
        private readonly ILogger _logger;

        public TMDbCollectionsService(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger logger)
        {
            _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, logger);
            _logger = logger;
        }
        
        public void Post(RefreshMetadataRequest request)
        {
            _logger.LogInformation("Starting a manual refresh of TMDb collections");
            _tmDbBoxSetManager.ScanLibrary(null);
            _logger.LogInformation("Completed refresh of TMDb collections");
        }
    }
}