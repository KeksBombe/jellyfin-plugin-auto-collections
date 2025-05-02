using System.Net.Mime;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.SmartCollections.Api
{
    /// <summary>
    /// The Smart Collections api controller.
    /// </summary>
    [ApiController]
    [Route("SmartCollections")]
    [Produces(MediaTypeNames.Application.Json)]


    public class SmartCollectionsController : ControllerBase
    {
        private readonly SmartCollectionsManager _syncSmartCollectionsManager;
        private readonly ILogger<SmartCollectionsManager> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="SmartCollectionsController"/>.

        public SmartCollectionsController(
            IProviderManager providerManager,
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            ILogger<SmartCollectionsManager> logger,
            IApplicationPaths applicationPaths
        )
        {
            _syncSmartCollectionsManager = new SmartCollectionsManager(providerManager, collectionManager, libraryManager, logger, applicationPaths);
            _logger = logger;
        }

        /// <summary>
        /// Creates smart collections.
        /// </summary>
        /// <reponse code="204">Smart Collection started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("SmartCollections")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult SmartCollectionsRequest()
        {
            _logger.LogInformation("Generating Smart Collections");
            _syncSmartCollectionsManager.ExecuteSmartCollectionsNoProgress();
            _logger.LogInformation("Completed");
            return NoContent();
        }
    }
}