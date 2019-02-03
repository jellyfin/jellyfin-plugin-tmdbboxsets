using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Services;

namespace TMDbCollections.Api
{
    [Route("/TMDbCollections/Refresh", "POST", Summary = "Refreshes collection metadata for all movies")]
    public class RefreshMetadataRequest : IReturnVoid
    {
    }
    
    public class TMDbCollectionsService : IService
    {
        private readonly TMDbCollectionCreator _tmDbCollectionCreator;
        private readonly ILogger _logger;

        public TMDbCollectionsService(TMDbCollectionCreator tmDbCollectionCreator, ILogger logger)
        {
            _tmDbCollectionCreator = tmDbCollectionCreator;
            _logger = logger;
        }
        
        public void RefreshMetadata(RefreshMetadataRequest request)
        {
            // TODO
            _logger.LogInformation("Starting a manual refresh of TMDb collections");
            _tmDbCollectionCreator.ScanLibrary();
            _logger.LogInformation("Completed refresh of TMDb collections");
        }
    }
}