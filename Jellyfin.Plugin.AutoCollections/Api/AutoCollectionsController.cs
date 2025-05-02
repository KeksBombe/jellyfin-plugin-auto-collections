using System.Net.Mime;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.AutoCollections.Api
{
    /// <summary>
    /// The Auto Collections api controller.
    /// </summary>
    [ApiController]
    [Route("AutoCollections")]
    [Produces(MediaTypeNames.Application.Json)]


    public class AutoCollectionsController : ControllerBase
    {
        private readonly AutoCollectionsManager _syncAutoCollectionsManager;
        private readonly ILogger<AutoCollectionsManager> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="AutoCollectionsController"/>.

        public AutoCollectionsController(
            IProviderManager providerManager,
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            ILogger<AutoCollectionsManager> logger,
            IApplicationPaths applicationPaths
        )
        {
            _syncAutoCollectionsManager = new AutoCollectionsManager(providerManager, collectionManager, libraryManager, logger, applicationPaths);
            _logger = logger;
        }

        /// <summary>
        /// Creates Auto collections.
        /// </summary>
        /// <reponse code="204">Auto Collection started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("AutoCollections")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult AutoCollectionsRequest()
        {
            _logger.LogInformation("Generating Auto Collections");
            _syncAutoCollectionsManager.ExecuteAutoCollectionsNoProgress();
            _logger.LogInformation("Completed");
            return NoContent();
        }
    }
}