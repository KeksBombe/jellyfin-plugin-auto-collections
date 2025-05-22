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
using System.Collections.Generic;
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
        [HttpGet("ExportConfiguration")] // Changed route
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces("application/json")]
        public ActionResult ExportConfiguration()
        {
            _logger.LogInformation("Exporting Auto Collections configuration");
            
            // Get the current plugin configuration
            var config = Plugin.Instance!.Configuration;

            // Create an anonymous object with only the desired properties
            var exportData = new
            {
                TitleMatchPairs = config.TitleMatchPairs,
                ExpressionCollections = config.ExpressionCollections
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            var json = JsonSerializer.Serialize(exportData, options); // Serialize the anonymous object
            
            // Return as a downloadable file
            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", "auto-collections-config.json");
        }
        
        /// <summary>
        /// Imports Auto Collections configuration from JSON.
        /// </summary>
        /// <response code="200">Configuration imported successfully.</response>
        /// <response code="400">Invalid configuration file.</response>
        /// <returns>A <see cref="IActionResult"/> indicating success or failure.</returns>
        [HttpPost("ImportConfiguration")] // Changed route
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ImportConfiguration() // This is the OVERWRITE method
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
                _logger.LogError(ex, "Error importing configuration");
                return BadRequest(new { Message = $"Error importing configuration: {ex.Message}" }); // Return object for consistency
            }
        }

        /// <summary>
        /// Adds (merges) Auto Collections configuration from JSON to the existing configuration.
        /// </summary>
        /// <response code="200">Configuration added successfully.</response>
        /// <response code="400">Invalid configuration file or error during merge.</response>
        /// <returns>A <see cref="IActionResult"/> indicating success or failure.</returns>
        [HttpPost("AddConfiguration")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddConfiguration()
        {
            _logger.LogInformation("Adding Auto Collections configuration (merge)");

            try
            {
                using var reader = new StreamReader(Request.Body);
                var json = await reader.ReadToEndAsync();
                json = RemoveJsonComments(json);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                var configToAdd = JsonSerializer.Deserialize<PluginConfiguration>(json, options);

                if (configToAdd == null)
                {
                    return BadRequest(new { Message = "Invalid configuration file for merging." });
                }

                var currentConfig = Plugin.Instance!.Configuration;

                // Merge TitleMatchPairs
                if (configToAdd.TitleMatchPairs != null)
                {
                    if (currentConfig.TitleMatchPairs == null)
                    {
                        currentConfig.TitleMatchPairs = new List<TitleMatchPair>();
                    }
                    // Simple AddRange, consider duplicate handling if necessary in future
                    currentConfig.TitleMatchPairs.AddRange(configToAdd.TitleMatchPairs);
                    _logger.LogInformation($"Added {configToAdd.TitleMatchPairs.Count} TitleMatchPairs.");
                }

                // Merge ExpressionCollections
                if (configToAdd.ExpressionCollections != null)
                {
                    if (currentConfig.ExpressionCollections == null)
                    {
                        currentConfig.ExpressionCollections = new List<ExpressionCollection>();
                    }
                    foreach (var collection in configToAdd.ExpressionCollections)
                    {
                        // Common typo fixes & validation during merge
                        collection.Expression = collection.Expression
                            .Replace("TITEL", "TITLE")
                            .Replace("GENERE", "GENRE");
                        
                        bool isValid = collection.ParseExpression();
                        if (!isValid && collection.ParseErrors.Count > 0)
                        {
                            _logger.LogWarning($"Expression errors in merged collection '{collection.CollectionName}': {string.Join(", ", collection.ParseErrors)}");
                        }
                    }
                    currentConfig.ExpressionCollections.AddRange(configToAdd.ExpressionCollections);
                    _logger.LogInformation($"Added {configToAdd.ExpressionCollections.Count} ExpressionCollections.");
                }

                Plugin.Instance.SaveConfiguration();

                _logger.LogInformation("Configuration added (merged) successfully");
                return Ok(new { Success = true, Message = "Configuration added successfully" });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing configuration for merging");
                return BadRequest(new { Message = $"Invalid JSON format for merging: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding configuration");
                return BadRequest(new { Message = $"Error adding configuration: {ex.Message}" });
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