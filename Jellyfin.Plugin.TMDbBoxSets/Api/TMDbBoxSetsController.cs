using System;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.Api;

/// <summary>
/// The TMDb collections API controller.
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class TMDbBoxSetsController : ControllerBase, IDisposable
{
    private readonly TMDbBoxSetManager _tmDbBoxSetManager;
    private readonly ILogger<TMDbBoxSetsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbBoxSetsController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbBoxSetsController}"/> interface.</param>
    /// <param name="boxsetLogger">Instance of the <see cref="ILogger{TMDbBoxSetManager}"/> interface.</param>
    public TMDbBoxSetsController(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ILogger<TMDbBoxSetsController> logger,
        ILogger<TMDbBoxSetManager> boxsetLogger)
    {
        _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, boxsetLogger);
        _logger = logger;
    }

    /// <summary>
    /// Scans all movies and creates box sets.
    /// </summary>
    /// <response code="204">Library scan and box set creation started successfully. </response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("Refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RefreshMetadataRequest()
    {
        _logger.LogInformation("Starting a manual refresh of TMDb collections");
        await _tmDbBoxSetManager.ScanLibrary(null).ConfigureAwait(false);
        _logger.LogInformation("Completed refresh of TMDb collections");
        return NoContent();
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
            _tmDbBoxSetManager.Dispose();
        }
    }
}
