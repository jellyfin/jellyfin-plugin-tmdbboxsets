using System.Net.Mime;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.Api
{
    /// <summary>
    /// The TMDb collections api controller.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "DefaultAuthorization")]
    [Route("[controller]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class TMDbBoxSetsController : ControllerBase
    {
        private readonly TMDbBoxSetManager _tmDbBoxSetManager;
        private readonly ILogger<TMDbBoxSetsController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TMDbBoxSetsController"/>.
        /// </summary>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TMDbBoxSetsController}"/> interface.</param>
        /// <param name="boxsetLogger">Instance of the <see cref="ILogger{TMDbBoxSetManager}"/> interface.</param>
        public TMDbBoxSetsController(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<TMDbBoxSetsController> logger, ILogger<TMDbBoxSetManager> boxsetLogger)
        {
            _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, boxsetLogger);
            _logger = logger;
        }

        /// <summary>
        /// Scans all movies and creates box sets.
        /// </summary>
        /// <reponse code="204">Library scan and box set creation started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("Refresh")]
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