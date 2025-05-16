using System.Net.Mime;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System;
using Jellyfin.Plugin.AutoCollections.Configuration;
using System.Text.Json.Serialization;

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
        }        /// <summary>
        /// Creates Auto collections.
        /// </summary>
        /// <reponse code="204">Auto Collection started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("AutoCollections")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> AutoCollectionsRequest()
        {
            _logger.LogInformation("Generating Auto Collections");
            await _syncAutoCollectionsManager.ExecuteAutoCollectionsNoProgress();
            _logger.LogInformation("Completed");
            return NoContent();
        }
        
        /// <summary>
        /// Exports the Auto Collections configuration to JSON.
        /// </summary>
        /// <response code="200">Returns the configuration as a JSON file.</response>
        /// <returns>A <see cref="FileContentResult"/> containing the configuration.</returns>
        [HttpGet("Export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        public ActionResult ExportConfiguration()
        {
            _logger.LogInformation("Exporting Auto Collections configuration");
            
            // Get the current plugin configuration
            var config = Plugin.Instance!.Configuration;
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            var json = JsonSerializer.Serialize(config, options);
            
            // Return as a downloadable file
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "auto-collections-config.json");
        }
        
        /// <summary>
        /// Imports Auto Collections configuration from JSON.
        /// </summary>
        /// <response code="200">Configuration imported successfully.</response>
        /// <response code="400">Invalid configuration file.</response>
        /// <returns>A <see cref="IActionResult"/> indicating success or failure.</returns>
        [HttpPost("Import")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportConfiguration()
        {
            _logger.LogInformation("Importing Auto Collections configuration");
            
            try
            {                // Read the JSON configuration from the request body
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();
                
                // Remove any JSON comments if present (like in the example file)
                json = RemoveJsonComments(json);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                // Deserialize the configuration
                var importedConfig = JsonSerializer.Deserialize<PluginConfiguration>(json, options);
                
                if (importedConfig == null)
                {
                    return BadRequest("Invalid configuration file");
                }
                  // Fix any expressions with typos and validate
                if (importedConfig.ExpressionCollections != null)
                {
                    foreach (var collection in importedConfig.ExpressionCollections)
                    {
                        // Common typo fixes
                        collection.Expression = collection.Expression
                            .Replace("TITEL", "TITLE")
                            .Replace("GENERE", "GENRE");
                        
                        // Try to parse the expression
                        bool isValid = collection.ParseExpression();
                        
                        if (!isValid && collection.ParseErrors.Count > 0)
                        {
                            // Log errors but don't reject the whole import
                            _logger.LogWarning($"Expression errors in '{collection.CollectionName}': {string.Join(", ", collection.ParseErrors)}");
                        }
                    }
                }
                  // Update the plugin configuration
                // Copy values to the existing configuration
                var currentConfig = Plugin.Instance!.Configuration;
                currentConfig.TitleMatchPairs = importedConfig.TitleMatchPairs;
                currentConfig.ExpressionCollections = importedConfig.ExpressionCollections;
                
                // Save the updated configuration
                Plugin.Instance.SaveConfiguration();
                
                _logger.LogInformation("Configuration imported successfully");
                return Ok(new { Success = true, Message = "Configuration imported successfully" });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing configuration");
                return BadRequest($"Invalid JSON format: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing configuration");                return BadRequest($"Error importing configuration: {ex.Message}");
            }
        }
        
        private string RemoveJsonComments(string json)
        {
            // Remove single-line comments (// ...)
            var lineCommentRegex = new System.Text.RegularExpressions.Regex(@"\/\/.*?$", System.Text.RegularExpressions.RegexOptions.Multiline);
            return lineCommentRegex.Replace(json, string.Empty);
        }
    }
}