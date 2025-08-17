using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.AutoCollections.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.AutoCollections
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly AutoCollectionsManager _syncAutoCollectionsManager;

        public Plugin(
            IServerApplicationPaths appPaths,
            IXmlSerializer xmlSerializer,
            ICollectionManager collectionManager,
            IProviderManager providerManager,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
            _syncAutoCollectionsManager = new AutoCollectionsManager(
                providerManager,
                collectionManager,
                libraryManager,
                loggerFactory.CreateLogger<AutoCollectionsManager>(),
                appPaths);

            // Initialize configuration with defaults only on first run
            InitializeConfigurationIfNeeded();
        }        private void InitializeConfigurationIfNeeded()
        {
            // First, attempt any necessary migration from old tag-based system
            AttemptMigrationFromTags();
            
            // Check if this is the first time the plugin is being loaded
            bool needsInitialization = false;
            
            // For existing installations that don't have the IsInitialized flag,
            // we need to determine if they had configurations before
            if (!Configuration.IsInitialized)
            {
                // If we have existing TitleMatchPairs or ExpressionCollections, 
                // this is likely an existing installation - mark as initialized without adding defaults
                bool hasExistingConfig = (Configuration.TitleMatchPairs != null && Configuration.TitleMatchPairs.Count > 0) ||
                                        (Configuration.ExpressionCollections != null && Configuration.ExpressionCollections.Count > 0) ||
                                        (Configuration.TagTitlePairs != null && Configuration.TagTitlePairs.Count > 0) ||
                                        (Configuration.Tags != null && Configuration.Tags.Length > 0);
                
                if (hasExistingConfig)
                {
                    // This is an existing installation, just mark as initialized
                    Configuration.IsInitialized = true;
                    SaveConfiguration();
                }
                else
                {
                    // This is a truly new installation
                    needsInitialization = true;
                }
            }
            
            // Initialize ExpressionCollections if it's null (for users upgrading from older versions)
            if (Configuration.ExpressionCollections == null)
            {
                Configuration.ExpressionCollections = new List<ExpressionCollection>();
            }
              // Only add default collections if we need initialization
            if (needsInitialization)
            {
                // Add default collections for first-time users
                Configuration.TitleMatchPairs = new List<TitleMatchPair>
                {
                    new TitleMatchPair("Marvel", "Marvel Universe"),
                    new TitleMatchPair("Star Wars", "Star Wars Collection"),
                    new TitleMatchPair("Harry Potter", "Harry Potter Series"),
                    new TitleMatchPair("Lord of the Rings", "Middle Earth"),
                    new TitleMatchPair("Pirates", "Pirates Movies"),
                    new TitleMatchPair("Fast & Furious", "Fast & Furious Saga"),
                    new TitleMatchPair("Jurassic", "Jurassic Collection"),
                };
                
                // Initialize the expression collections with some examples
                Configuration.ExpressionCollections = new List<ExpressionCollection>
                {
                    new ExpressionCollection("Marvel Action", "STUDIO \"Marvel\" AND GENRE \"Action\"", false),
                    new ExpressionCollection("Spielberg or Nolan", "DIRECTOR \"Spielberg\" OR DIRECTOR \"Nolan\"", false),
                    new ExpressionCollection("Tom Hanks Dramas", "ACTOR \"Tom Hanks\" AND GENRE \"Drama\"", false)
                };

                // For backward compatibility (empty, as we're switching from tag-based to title-based)
                #pragma warning disable CS0618
                Configuration.TagTitlePairs = new List<TagTitlePair>();
                Configuration.Tags = Array.Empty<string>();
                #pragma warning restore CS0618

                // Mark as initialized to prevent future resets
                Configuration.IsInitialized = true;

                // Save the configuration with defaults
                SaveConfiguration();
            }
        }        private void AttemptMigrationFromTags()
        {
            #pragma warning disable CS0618 // Disable obsolete warning for backward compatibility code
            // Initialize our new title match pairs list
            if (Configuration.TitleMatchPairs == null)
            {
                Configuration.TitleMatchPairs = new List<TitleMatchPair>();
            }
            
            bool migrationPerformed = false;
            
            // If we have old TagTitlePairs, convert them to TitleMatchPairs
            // This is more of a placeholder since direct conversion doesn't make sense,
            // but we're keeping the collection names for continuity
            if (Configuration.TagTitlePairs != null && Configuration.TagTitlePairs.Count > 0)
            {
                foreach (var tagPair in Configuration.TagTitlePairs)
                {
                    // Create a title match pair with the same collection name
                    // but use the tag as the title match string
                    var titleMatch = new TitleMatchPair(
                        titleMatch: tagPair.Tag,
                        collectionName: tagPair.Title,
                        caseSensitive: false);
                        
                    Configuration.TitleMatchPairs.Add(titleMatch);
                }
                migrationPerformed = true;
            }
            // If we only have old Tags, convert them too
            else if (Configuration.Tags != null && Configuration.Tags.Length > 0)
            {
                foreach (var tag in Configuration.Tags)
                {
                    var titleMatch = new TitleMatchPair(tag);
                    Configuration.TitleMatchPairs.Add(titleMatch);
                }
                migrationPerformed = true;
            }
            
            // If we performed migration, mark as initialized and save
            if (migrationPerformed)
            {
                Configuration.IsInitialized = true;
                SaveConfiguration();
            }
            #pragma warning restore CS0618
        }

        public override string Name => "Auto Collections";

        public static Plugin Instance { get; private set; }        public override string Description
            => "Enables creation of Auto Collections based on simple criteria or advanced boolean expressions with custom collection names";        
        
        private readonly Guid _id = new Guid("06ebf4a9-1326-4327-968d-8da00e1ea2eb");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Auto Collections",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
                }
            };
        }
    }
}
