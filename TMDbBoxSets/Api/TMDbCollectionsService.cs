using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;

namespace TMDbBoxSets.Api
{
    [Route("/TMDbCollections/Refresh", "POST", Summary = "Refreshes collection metadata for all movies")]
    public class RefreshMetadataRequest : IReturnVoid
    {
    }
    
    public class TMDbCollectionsService : IService
    {
        private readonly TMDbBoxSetManager _tmDbBoxSetManager;
        private readonly ILogger _logger;

        public TMDbCollectionsService(TMDbBoxSetManager tmDbBoxSetManager, ILogger logger)
        {
            _tmDbBoxSetManager = tmDbBoxSetManager;
            _logger = logger;
        }
        
        public void RefreshMetadata(RefreshMetadataRequest request)
        {
            // TODO
            _logger.LogInformation("Starting a manual refresh of TMDb collections");
            _tmDbBoxSetManager.ScanLibrary();
            _logger.LogInformation("Completed refresh of TMDb collections");
        }
    }
}