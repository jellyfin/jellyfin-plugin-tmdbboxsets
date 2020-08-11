using System.Net.Mime;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.Api
{
    /// <summary>
    /// The TMDb collections api controller.
    /// </summary>
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public class TMDbCollectionsController : ControllerBase
    {
        private readonly TMDbBoxSetManager _tmDbBoxSetManager;
        private readonly ILogger<TMDbCollectionsController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TMDbCollectionsController"/>.
        /// </summary>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TMDbCollectionsController}"/> interface.</param>
        /// <param name="boxsetLogger">Instance of the <see cref="ILogger{TMDbBoxSetManager}"/> interface.</param>
        public TMDbCollectionsController(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<TMDbCollectionsController> logger, ILogger<TMDbBoxSetManager> boxsetLogger)
        {
            _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, boxsetLogger);
            _logger = logger;
        }

        /// <summary>
        /// Scans all movies and creates box sets.
        /// </summary>
        /// <returns></returns>
        [HttpPost("TMDbBoxSets/Refresh")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult RefreshMetadataRequest()
        {
            _logger.LogInformation("Starting a manual refresh of TMDb collections");
            _tmDbBoxSetManager.ScanLibrary(null);
            _logger.LogInformation("Completed refresh of TMDb collections");
            return NoContent();
        }
    }
}