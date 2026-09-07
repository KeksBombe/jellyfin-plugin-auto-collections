#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;
using Jellyfin.Plugin.AutoCollections.Configuration;

namespace Jellyfin.Plugin.AutoCollections
{
    // Internal enum for sort order
    internal enum SortOrder
    {
        Ascending,
        Descending
    }
    
    // ================================================================
    // CLASS DECLARATION AND DEPENDENCY INJECTION
    // ================================================================
    // This section contains the main class definition, its dependencies,
    // and initialization logic for the AutoCollectionsManager.
    public class AutoCollectionsManager : IDisposable
    {
        // Marks every collection this plugin owns so later runs recognise them again.
        private const string AutoCollectionTag = "Autocollection";

        private readonly ICollectionManager _collectionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IUserDataManager? _userDataManager;
        private readonly IUserManager? _userManager;
        private readonly Timer _timer;
        // Static so the dashboard button, the API and the scheduled task all share one gate,
        // even though each of them builds its own manager instance.
        private static readonly SemaphoreSlim _runGate = new SemaphoreSlim(1, 1);
        private readonly ILogger<AutoCollectionsManager> _logger;
        private readonly string _pluginDirectory;
        
        // Cache for person-to-media lookups during expression evaluation
        // Key: (personName, personType, caseSensitive), Value: HashSet of movie IDs
        private Dictionary<(string, string, bool), HashSet<Guid>>? _personToMoviesCache;
        // Key: (personName, personType, caseSensitive), Value: HashSet of series IDs
        private Dictionary<(string, string, bool), HashSet<Guid>>? _personToSeriesCache;
        // Cache for item's people to avoid repeated DB calls
        // Key: item ID, Value: list of (personName, personType) tuples
        private Dictionary<Guid, List<(string Name, string Type)>>? _itemPeopleCache;
        // Cache for the libraries an item belongs to
        // Key: item ID, Value: names of the libraries containing it
        private Dictionary<Guid, List<string>>? _itemLibrariesCache;

        // Constructor with IUserDataManager and IUserManager for full functionality
        public AutoCollectionsManager(IProviderManager providerManager, ICollectionManager collectionManager, ILibraryManager libraryManager, IUserDataManager userDataManager, IUserManager userManager, ILogger<AutoCollectionsManager> logger, IApplicationPaths applicationPaths)
        {
            _providerManager = providerManager;
            _collectionManager = collectionManager;
            _libraryManager = libraryManager;
            _userDataManager = userDataManager;
            _userManager = userManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _pluginDirectory = Path.Combine(applicationPaths.DataPath, "Autocollections");
            Directory.CreateDirectory(_pluginDirectory);
        }

        // Constructor without IUserDataManager/IUserManager for backward compatibility
        public AutoCollectionsManager(IProviderManager providerManager, ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger<AutoCollectionsManager> logger, IApplicationPaths applicationPaths)
        {
            _providerManager = providerManager;
            _collectionManager = collectionManager;
            _libraryManager = libraryManager;
            _userDataManager = null; // Will be null, but methods will handle this gracefully
            _userManager = null;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _pluginDirectory = Path.Combine(applicationPaths.DataPath, "Autocollections");
            Directory.CreateDirectory(_pluginDirectory);
        }

        // ================================================================
        // SERIES SEARCH METHODS
        // ================================================================
        // This section contains methods for searching and filtering TV series
        // from the Jellyfin library based on various criteria like tags, genres,
        // and person associations.
        private IEnumerable<Series> GetSeriesFromLibrary(string term, Person? specificPerson = null)
        {
            IEnumerable<Series> results = Enumerable.Empty<Series>();
            
            if (specificPerson == null)
            {
                // When no specific person is provided, search by tags and genres
                var byTags = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Tags = [term]
                }).OfType<Series>();

                var byGenres = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Genres = [term]
                }).OfType<Series>();
                
                results = byTags.Union(byGenres);
            }
            else
            {
                // When a specific person is provided, search by actor and director
                var personName = specificPerson.Name;
                
                var byActors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = personName,
                    PersonTypes = new[] { "Actor" }
                }).OfType<Series>();

                var byDirectors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = personName,
                    PersonTypes = new[] { "Director" }
                }).OfType<Series>();
                
                results = byActors.Union(byDirectors);
            }

            return results;
        }
        
        private IEnumerable<Series> GetSeriesFromLibraryWithAndMatching(string[] terms, Person? specificPerson = null)
        {
            if (terms.Length == 0)
                return Enumerable.Empty<Series>();
                
            // Start with all series matching the first tag
            var results = GetSeriesFromLibrary(terms[0], specificPerson).ToList();
            
            // For each additional tag, filter the results to only include series that also match that tag
            for (int i = 1; i < terms.Length && results.Any(); i++)
            {
                var matchingItems = GetSeriesFromLibrary(terms[i], specificPerson).ToList();
                results = results.Where(item => matchingItems.Any(m => m.Id == item.Id)).ToList();
            }
            
            return results;
        }

        // ================================================================
        // MOVIE SEARCH METHODS
        // ================================================================
        // This section contains methods for searching and filtering movies
        // from the Jellyfin library based on various criteria like tags, genres,
        // and person associations.
        private IEnumerable<Movie> GetMoviesFromLibrary(string term, Person? specificPerson = null)
        {
            IEnumerable<Movie> results = Enumerable.Empty<Movie>();
            
            if (specificPerson == null)
            {
                // When no specific person is provided, search by tags and genres
                var byTags = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    Tags = [term]
                }).OfType<Movie>();

                var byGenres = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    Genres = [term]
                }).OfType<Movie>();
                
                results = byTags.Union(byGenres);
            }
            else
            {
                // When a specific person is provided, search by actor and director
                var personName = specificPerson.Name;
                
                var byActors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = personName,
                    PersonTypes = new[] { "Actor" }
                }).OfType<Movie>();

                var byDirectors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = personName,
                    PersonTypes = new[] { "Director" }
                }).OfType<Movie>();
                
                results = byActors.Union(byDirectors);
            }

            return results;
        }
        
        private IEnumerable<Movie> GetMoviesFromLibraryWithAndMatching(string[] terms, Person? specificPerson = null)
        {
            if (terms.Length == 0)
                return Enumerable.Empty<Movie>();
                
            // Start with all movies matching the first tag
            var results = GetMoviesFromLibrary(terms[0], specificPerson).ToList();
            
            // For each additional tag, filter the results to only include movies that also match that tag
            for (int i = 1; i < terms.Length && results.Any(); i++)
            {
                var matchingItems = GetMoviesFromLibrary(terms[i], specificPerson).ToList();
                results = results.Where(item => matchingItems.Any(m => m.Id == item.Id)).ToList();
            }
            
            return results;
        }        
        
        // ================================================================
        // GENERIC SEARCH METHODS
        // ================================================================
        // This section contains methods that work with both movies and series,
        // providing generic search functionality based on match types and patterns.
        private IEnumerable<Movie> GetMoviesFromLibraryByMatch(string matchString, bool caseSensitive, Configuration.MatchType matchType)
        {
            // Get all non-null movies from the library
            var allMovies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true
            }).OfType<Movie>();
            
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
            
            // Filter movies based on match type
            return matchType switch
            {
                Configuration.MatchType.Title => allMovies.Where(movie => 
                    !string.IsNullOrEmpty(movie.Name) && movie.Name.Contains(matchString, comparison)),
                
                Configuration.MatchType.Genre => allMovies.Where(movie => 
                    movie.Genres != null && movie.Genres.Any(genre => 
                        !string.IsNullOrEmpty(genre) && genre.Contains(matchString, comparison))),
                
                Configuration.MatchType.Studio => allMovies.Where(movie => 
                    movie.Studios != null && movie.Studios.Any(studio => 
                        !string.IsNullOrEmpty(studio) && studio.Contains(matchString, comparison))),
                
                Configuration.MatchType.Actor => GetMoviesWithPerson(matchString, "Actor", caseSensitive),
                
                Configuration.MatchType.Director => GetMoviesWithPerson(matchString, "Director", caseSensitive),
                
                Configuration.MatchType.Tag => allMovies.Where(movie => 
                    movie.Tags != null && movie.Tags.Any(tag => 
                        !string.IsNullOrEmpty(tag) && tag.Equals(matchString, comparison))),
                
                Configuration.MatchType.Writer => GetMoviesWithPerson(matchString, "Writer", caseSensitive),
                
                _ => allMovies.Where(movie => 
                    !string.IsNullOrEmpty(movie.Name) && movie.Name.Contains(matchString, comparison))
            };
        }
          private IEnumerable<Series> GetSeriesFromLibraryByMatch(string matchString, bool caseSensitive, Configuration.MatchType matchType)
                {
                    // Get all series from the library
                    var allSeries = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Series },
                        IsVirtualItem = false,
                        Recursive = true
                    }).OfType<Series>();
                    
                    StringComparison comparison = caseSensitive 
                        ? StringComparison.Ordinal 
                        : StringComparison.OrdinalIgnoreCase;              // Filter series based on match type
                    return matchType switch
                    {
                        Configuration.MatchType.Title => allSeries.Where(series => 
                            series.Name != null && series.Name.Contains(matchString, comparison)),
                        
                        Configuration.MatchType.Genre => allSeries.Where(series => 
                            series.Genres != null && series.Genres.Any(genre => 
                                genre.Contains(matchString, comparison))),
                        
                        Configuration.MatchType.Studio => allSeries.Where(series => 
                            series.Studios != null && series.Studios.Any(studio => 
                                studio.Contains(matchString, comparison))),
                        
                        // Use GetSeriesWithPerson which properly verifies the person's role in each series
                        Configuration.MatchType.Actor => GetSeriesWithPerson(matchString, "Actor", caseSensitive),
                        
                        // Use GetSeriesWithPerson which properly verifies the person's role in each series
                        Configuration.MatchType.Director => GetSeriesWithPerson(matchString, "Director", caseSensitive),
                        
                        Configuration.MatchType.Tag => allSeries.Where(series => 
                            series.Tags != null && series.Tags.Any(tag => 
                                !string.IsNullOrEmpty(tag) && tag.Equals(matchString, comparison))),
                        
                        Configuration.MatchType.Writer => GetSeriesWithPerson(matchString, "Writer", caseSensitive),
                        
                        _ => allSeries.Where(series => 
                            series.Name != null && series.Name.Contains(matchString, comparison)) // Default to title match
                    };
                }
          // Keep these for backward compatibility
        private IEnumerable<Movie> GetMoviesFromLibraryByTitleMatch(string titleMatch, bool caseSensitive)
        {
            return GetMoviesFromLibraryByMatch(titleMatch, caseSensitive, Configuration.MatchType.Title);
        }
        
        private IEnumerable<Series> GetSeriesFromLibraryByTitleMatch(string titleMatch, bool caseSensitive)
        {
            return GetSeriesFromLibraryByMatch(titleMatch, caseSensitive, Configuration.MatchType.Title);
        }

        // ================================================================
        // COLLECTION MANAGEMENT METHODS
        // ================================================================
        // This section contains methods for managing collection contents,
        // including adding/removing items and sorting collections.
        private async Task RemoveUnwantedMediaItems(BoxSet collection, IEnumerable<BaseItem> wantedMediaItems)
        {
            // Get the set of IDs for media items we want to keep
            var wantedItemIds = wantedMediaItems.Select(item => item.Id).ToHashSet();

            // Get current items and filter for unwanted ones
            var currentChildren = collection.GetLinkedChildren().ToList();
            var childrenToRemove = currentChildren
                .Where(item => !wantedItemIds.Contains(item.Id))
                .ToList();

            if (childrenToRemove.Count > 0)
            {
                _logger.LogDebug("Removing {Count} items from collection '{CollectionName}':", 
                    childrenToRemove.Count, collection.Name);
                
                foreach (var item in childrenToRemove)
                {
                    _logger.LogDebug("  - Removing: '{Title}' (ID: {Id}) - no longer matches criteria", 
                        item.Name, item.Id);
                }
                
                await _collectionManager.RemoveFromCollectionAsync(
                    collection.Id, 
                    childrenToRemove.Select(i => i.Id).ToArray()
                ).ConfigureAwait(true);
            }
            else
            {
                _logger.LogDebug("No items to remove from collection '{CollectionName}'", collection.Name);
            }
        }

        private async Task AddWantedMediaItems(BoxSet collection, IEnumerable<BaseItem> wantedMediaItems)
        {
            // Get the set of IDs for items currently in the collection
            var existingItemIds = collection.GetLinkedChildren()
                .Select(item => item.Id)
                .ToHashSet();            

            // Create LinkedChild objects for items that aren't already in the collection
            var itemsToAdd = wantedMediaItems
                .Where(item => !existingItemIds.Contains(item.Id))
                .OrderByDescending(item => item.ProductionYear)
                .ThenByDescending(item => item.PremiereDate ?? DateTime.MinValue)
                .ToList();

            if (itemsToAdd.Count > 0)
            {
                _logger.LogDebug("Adding {Count} new items to collection '{CollectionName}':", 
                    itemsToAdd.Count, collection.Name);
                
                foreach (var item in itemsToAdd)
                {
                    var itemType = item is Movie ? "Movie" : item is Series ? "Series" : "Item";
                    var year = item.ProductionYear?.ToString() ?? "Unknown year";
                    _logger.LogDebug("  + Adding {Type}: '{Title}' ({Year}) (ID: {Id})", 
                        itemType, item.Name, year, item.Id);
                }
                
                await _collectionManager.AddToCollectionAsync(
                    collection.Id, 
                    itemsToAdd.Select(i => i.Id).ToArray()
                ).ConfigureAwait(true);
            }
            else
            {
                _logger.LogDebug("No new items to add to collection '{CollectionName}' - all matching items already present", 
                    collection.Name);
            }
        }

        private async Task SortCollectionBy(BoxSet collection, SortOrder sortOrder)
        {
            // Get the current items in the collection
            var currentItems = collection.GetLinkedChildren().ToList();

            if (currentItems.Count <= 1)
            {
                // No need to sort if there's 0 or 1 item
                return;
            }

            // Sort the items based on the sort order
            var sortedItems =
                sortOrder == SortOrder.Ascending
                    ? currentItems
                        .OrderBy(item => item.ProductionYear)
                        .ThenBy(item => item.PremiereDate ?? DateTime.MinValue)
                        .ToList()
                    : currentItems
                        .OrderByDescending(item => item.ProductionYear)
                        .ThenByDescending(item => item.PremiereDate ?? DateTime.MinValue)
                        .ToList();

            // Find the first index where items differ
            int firstDifferenceIndex = -1;
            for (int i = 0; i < currentItems.Count; i++)
            {
                if (currentItems[i].Id != sortedItems[i].Id)
                {
                    firstDifferenceIndex = i;
                    break;
                }
            }

            // If no differences found, collection is already sorted
            if (firstDifferenceIndex == -1)
            {
                _logger.LogDebug($"Collection {collection.Name} is already sorted");
                return;
            }

            // Remove items from the first difference index onwards
            var itemsToRemove = currentItems
                .Skip(firstDifferenceIndex)
                .Select(item => item.Id)
                .ToArray();

            if (itemsToRemove.Length > 0)
            {
                _logger.LogInformation(
                    $"Removing {itemsToRemove.Length} items from collection {collection.Name} for re-sorting"
                );
                await _collectionManager
                    .RemoveFromCollectionAsync(collection.Id, itemsToRemove)
                    .ConfigureAwait(true);
            }

            // Add back the sorted items from the first difference index
            var itemsToAdd = sortedItems
                .Skip(firstDifferenceIndex)
                .Select(item => item.Id)
                .ToArray();

            if (itemsToAdd.Length > 0)
            {
                _logger.LogInformation(
                    $"Adding {itemsToAdd.Length} sorted items back to collection {collection.Name}"
                );
                await _collectionManager
                    .AddToCollectionAsync(collection.Id, itemsToAdd)
                    .ConfigureAwait(true);
            }
        }

        private void ValidateCollectionContent(BoxSet collection, IEnumerable<BaseItem> expectedItems)
        {
            // Get the actual items in the collection
            var actualItems = collection.GetLinkedChildren().ToList();
            var expectedItemsList = expectedItems.ToList();
            
            // Create sets for comparison
            var actualItemIds = actualItems.Select(i => i.Id).ToHashSet();
            var expectedItemIds = expectedItemsList.Select(i => i.Id).ToHashSet();
            
            // Count statistics
            var expectedCount = expectedItemIds.Count;
            var actualCount = actualItemIds.Count;
            var matchingCount = actualItemIds.Intersect(expectedItemIds).Count();
            var missingCount = expectedItemIds.Except(actualItemIds).Count();
            var extraCount = actualItemIds.Except(expectedItemIds).Count();
            
            // Log validation summary
            _logger.LogInformation(
                "Collection '{CollectionName}' validation: Expected={Expected}, Actual={Actual}, Matching={Matching}, Missing={Missing}, Extra={Extra}",
                collection.Name, expectedCount, actualCount, matchingCount, missingCount, extraCount);
            
            // Log details if there are discrepancies
            if (missingCount > 0)
            {
                _logger.LogWarning("Collection '{CollectionName}' is missing {Count} expected items:", 
                    collection.Name, missingCount);
                
                var missingItems = expectedItemsList.Where(i => !actualItemIds.Contains(i.Id)).Take(10);
                foreach (var item in missingItems)
                {
                    var itemType = item is Movie ? "Movie" : item is Series ? "Series" : "Item";
                    var year = item.ProductionYear?.ToString() ?? "Unknown";
                    _logger.LogWarning("  - Missing {Type}: '{Title}' ({Year}) (ID: {Id})", 
                        itemType, item.Name, year, item.Id);
                }
                
                if (missingCount > 10)
                {
                    _logger.LogWarning("  ... and {Count} more missing items", missingCount - 10);
                }
            }
            
            if (extraCount > 0)
            {
                _logger.LogWarning("Collection '{CollectionName}' has {Count} unexpected items:", 
                    collection.Name, extraCount);
                
                var extraItems = actualItems.Where(i => !expectedItemIds.Contains(i.Id)).Take(10);
                foreach (var item in extraItems)
                {
                    var itemType = item is Movie ? "Movie" : item is Series ? "Series" : "Item";
                    var year = item.ProductionYear?.ToString() ?? "Unknown";
                    _logger.LogWarning("  - Extra {Type}: '{Title}' ({Year}) (ID: {Id})", 
                        itemType, item.Name, year, item.Id);
                }
                
                if (extraCount > 10)
                {
                    _logger.LogWarning("  ... and {Count} more extra items", extraCount - 10);
                }
            }
            
            // Log success if everything matches
            if (missingCount == 0 && extraCount == 0 && actualCount == expectedCount)
            {
                _logger.LogInformation("✓ Collection '{CollectionName}' content validated successfully - all {Count} items match",
                    collection.Name, actualCount);
            }
            else
            {
                _logger.LogWarning("✗ Collection '{CollectionName}' content validation failed - discrepancies found",
                    collection.Name);
            }
        }

        // ================================================================
        // COLLECTION RETRIEVAL METHODS
        // ================================================================
        // This section contains methods for retrieving existing collections
        // from the Jellyfin library.
        private BoxSet? GetBoxSetByName(string name)
        {
            var boxSets = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                CollapseBoxSetItems = false,
                Recursive = true
            }).OfType<BoxSet>().ToList();

            // Preferred: a collection this plugin owns that still carries the configured name.
            var owned = boxSets.FirstOrDefault(b =>
                b.Tags != null &&
                b.Tags.Contains(AutoCollectionTag, StringComparer.OrdinalIgnoreCase) &&
                string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));

            if (owned != null)
            {
                return owned;
            }

            // Collections created before the name lock could be renamed behind our back by a
            // metadata provider, so the display name no longer matches the configuration. The
            // folder Jellyfin created for them keeps the original name, which makes it the only
            // reliable way to recognise them again and adopt them instead of creating a duplicate.
            var byFolder = boxSets.FirstOrDefault(b =>
                !string.IsNullOrEmpty(b.Path) &&
                string.Equals(
                    Path.GetFileName(b.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    $"{name} [boxset]",
                    StringComparison.OrdinalIgnoreCase));

            if (byFolder != null)
            {
                if (!string.Equals(byFolder.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Adopting collection '{ActualName}' for configured collection '{ConfiguredName}' (matched by folder)",
                        byFolder.Name, name);
                }

                return byFolder;
            }

            // Deliberately no fall-back on the display name alone: a collection the user built by
            // hand can share a name with a configured one, and adopting it would start deleting
            // the items they picked themselves.
            var unrelated = boxSets.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
            if (unrelated != null)
            {
                _logger.LogWarning(
                    "A collection named '{CollectionName}' exists but was not created by Auto Collections, so it is left untouched",
                    name);
            }

            return null;
        }

        /// <summary>
        /// Fetches the collection for <paramref name="collectionName"/>, creating it when missing,
        /// and makes sure the plugin's ownership tag and metadata locks are actually persisted.
        /// </summary>
        private async Task<(BoxSet Collection, bool IsNew)> GetOrCreateCollectionAsync(string collectionName)
        {
            var collection = GetBoxSetByName(collectionName);
            var isNew = false;

            if (collection is null)
            {
                _logger.LogInformation("{Name} not found, creating.", collectionName);
                collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = collectionName,
                    IsLocked = true
                });
                isNew = true;
            }

            await ApplyCollectionMetadataAsync(collection, collectionName);
            return (collection, isNew);
        }

        /// <summary>
        /// Persists the plugin's ownership tag and pins the fields Jellyfin's metadata providers
        /// would otherwise take over.
        /// </summary>
        /// <remarks>
        /// Collections used to be created unlocked and the tag was only ever assigned in memory,
        /// never written back. That let the TMDB box-set provider rename a collection to whichever
        /// box set it matched (so "HBO" became "The Gathering Storm Collection"), swap its artwork,
        /// and - because the plugin could no longer find the renamed collection - create a
        /// replacement on the next run while the old one was cleaned up.
        /// </remarks>
        private async Task ApplyCollectionMetadataAsync(BoxSet collection, string collectionName)
        {
            var changed = false;

            if (!string.Equals(collection.Name, collectionName, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Restoring collection name '{ActualName}' to the configured name '{ConfiguredName}'",
                    collection.Name, collectionName);
                collection.Name = collectionName;
                changed = true;
            }

            // Blocks remote metadata refreshes entirely, which is what protects the name and artwork.
            if (!collection.IsLocked)
            {
                collection.IsLocked = true;
                changed = true;
            }

            var lockedFields = collection.LockedFields?.ToList() ?? new List<MetadataField>();
            var missingLocks = new[] { MetadataField.Name, MetadataField.Overview }
                .Where(field => !lockedFields.Contains(field))
                .ToList();

            if (missingLocks.Count > 0)
            {
                lockedFields.AddRange(missingLocks);
                collection.LockedFields = lockedFields.ToArray();
                changed = true;
            }

            var tags = collection.Tags?.ToList() ?? new List<string>();
            if (!tags.Contains(AutoCollectionTag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(AutoCollectionTag);
                collection.Tags = tags.ToArray();
                changed = true;
            }

            if (!string.Equals(collection.DisplayOrder, "Default", StringComparison.Ordinal))
            {
                collection.DisplayOrder = "Default";
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            await _libraryManager.UpdateItemAsync(
                collection,
                collection.GetParent(),
                ItemUpdateType.MetadataEdit,
                CancellationToken.None).ConfigureAwait(true);
        }

        // ================================================================
        // MAIN EXECUTION METHODS
        // ================================================================
        // This section contains the primary methods that orchestrate the
        // auto-collection process, including execution entry points and
        // progress handling.
        public async Task ExecuteAutoCollectionsNoProgress()
        {
            // Call the main method with a dummy progress reporter and non-cancellable token
            var dummyProgress = new Progress<double>();
            await ExecuteAutoCollections(dummyProgress, CancellationToken.None);
        }

        /// <summary>
        /// Runs every configured collection. Only one run happens at a time across the whole
        /// server: the dashboard button and the scheduled task both land here, and overlapping
        /// runs used to fight over the same collections.
        /// </summary>
        public async Task ExecuteAutoCollections(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!await _runGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Auto Collections is already running, skipping this request");
                progress.Report(100);
                return;
            }

            try
            {
                await ExecuteAutoCollectionsCore(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsServerIncompatibility(ex))
            {
                // The server exposes a different API surface than the one this plugin was built
                // against - almost always a Jellyfin older than 10.11.
                _logger.LogError(ex,
                    "Auto Collections could not run against this Jellyfin server. This plugin version requires Jellyfin 10.11 or newer; please update the server or install a plugin release matching it");
                throw;
            }
            finally
            {
                _runGate.Release();
            }
        }

        /// <summary>
        /// True for the failures a server built on a different Jellyfin API produces. These are
        /// fatal for the whole run, so they must not be swallowed by the per-collection handler.
        /// </summary>
        private static bool IsServerIncompatibility(Exception ex)
            => ex is MissingMethodException or MissingFieldException or TypeLoadException;

        private async Task ExecuteAutoCollectionsCore(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performing ExecuteAutoCollections");
            
            // Get title match pairs from configuration - this is the basic approach
            var titleMatchPairs = Plugin.Instance!.Configuration.TitleMatchPairs;
            // Get expression collections - this is the advanced approach
            var expressionCollections = Plugin.Instance!.Configuration.ExpressionCollections;
            
            int totalCollections = titleMatchPairs.Count + expressionCollections.Count;
            int processedCollections = 0;
            
            _logger.LogInformation($"Starting execution of Auto collections: {titleMatchPairs.Count} title match pairs + {expressionCollections.Count} expression collections = {totalCollections} total");
            
            // Report initial progress
            progress.Report(0);

            // Person lookups are shared across every collection in this run. Rebuilding them per
            // collection is the main reason large libraries took hours to process.
            InitializePersonCache();

            try
            {
                foreach (var titleMatchPair in titleMatchPairs)
                {
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                
                    try
                    {
                        _logger.LogInformation($"Processing Auto collection for title match: {titleMatchPair.TitleMatch} ({processedCollections + 1} of {totalCollections})");
                        await ExecuteAutoCollectionsForTitleMatchPair(titleMatchPair);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Auto Collections task was cancelled");
                        throw;
                    }
                    catch (Exception ex) when (IsServerIncompatibility(ex))
                    {
                        // Every collection would hit this, so stop instead of logging it once per collection
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing Auto collection for title match: {titleMatchPair.TitleMatch}");
                        // Continue with next title-match pair even if one fails
                    }
                
                    processedCollections++;
                    double progressPercentage = totalCollections > 0 ? (double)processedCollections / totalCollections * 100 : 100;
                    progress.Report(progressPercentage);
                    _logger.LogDebug($"Progress: {processedCollections} of {totalCollections} collections complete ({progressPercentage:F1}%)");
                }

                foreach (var expressionCollection in expressionCollections)
                {
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                
                    try
                    {
                        _logger.LogInformation($"Processing Advanced collection: {expressionCollection.CollectionName} ({processedCollections + 1} of {totalCollections})");
                        await ExecuteAutoCollectionsForExpressionCollection(expressionCollection);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Auto Collections task was cancelled");
                        throw;
                    }
                    catch (Exception ex) when (IsServerIncompatibility(ex))
                    {
                        // Every collection would hit this, so stop instead of logging it once per collection
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing Advanced collection: {expressionCollection.CollectionName}");
                        // Continue with next expression collection even if one fails
                    }
                
                    processedCollections++;
                    double progressPercentage = totalCollections > 0 ? (double)processedCollections / totalCollections * 100 : 100;
                    progress.Report(progressPercentage);
                    _logger.LogDebug($"Progress: {processedCollections} of {totalCollections} collections complete ({progressPercentage:F1}%)");
                }
            }
            finally
            {
                ClearPersonCache();
            }

            if (Plugin.Instance!.Configuration.DeleteOrphanedCollections)
            {
                RemoveOrphanedCollections(
                    titleMatchPairs.Select(p => p.CollectionName)
                        .Concat(expressionCollections.Select(e => e.CollectionName)));
            }

            progress.Report(100);
            _logger.LogInformation($"Completed execution of all {totalCollections} Auto collections");
        }

        /// <summary>
        /// Deletes collections this plugin created that are no longer in the configuration.
        /// </summary>
        /// <remarks>
        /// Only collections carrying the plugin's ownership tag are considered, so collections
        /// made by hand or by another plugin are never touched. Skipped when the configuration
        /// holds no collections at all, so a configuration that failed to load cannot wipe
        /// every collection the plugin has ever made.
        /// </remarks>
        private void RemoveOrphanedCollections(IEnumerable<string> configuredNames)
        {
            var wanted = new HashSet<string>(
                configuredNames.Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);

            if (wanted.Count == 0)
            {
                _logger.LogInformation("No collections are configured, skipping orphan cleanup");
                return;
            }

            var orphans = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                CollapseBoxSetItems = false,
                Recursive = true
            }).OfType<BoxSet>()
                .Where(boxSet =>
                    boxSet.Tags != null &&
                    boxSet.Tags.Contains(AutoCollectionTag, StringComparer.OrdinalIgnoreCase) &&
                    !wanted.Contains(boxSet.Name))
                .ToList();

            if (orphans.Count == 0)
            {
                _logger.LogDebug("No orphaned collections to remove");
                return;
            }

            foreach (var orphan in orphans)
            {
                try
                {
                    _logger.LogInformation(
                        "Deleting orphaned collection '{CollectionName}' - no longer in the configuration",
                        orphan.Name);

                    _libraryManager.DeleteItem(
                        orphan,
                        new DeleteOptions { DeleteFileLocation = true },
                        true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not delete orphaned collection '{CollectionName}'", orphan.Name);
                }
            }
        }

        // ================================================================
        // COLLECTION NAMING METHODS
        // ================================================================
        // This section contains methods for determining and formatting
        // collection names based on configuration settings.
        private string GetCollectionName(TagTitlePair tagTitlePair)
        {
            // If a custom title is set, use it
            if (!string.IsNullOrWhiteSpace(tagTitlePair.Title))
            {
                return tagTitlePair.Title;
            }
            
            // Otherwise use the default format based on the first tag
            string[] tags = tagTitlePair.GetTagsArray();
            if (tags.Length == 0)
                return "Auto Collection";
                
            string firstTag = tags[0];
            string capitalizedTag = firstTag.Length > 0
                ? char.ToUpper(firstTag[0]) + firstTag[1..]
                : firstTag;

            // For AND matching, use a different format to indicate the intersection
            if (tagTitlePair.MatchingMode == TagMatchingMode.And && tags.Length > 1)
            {
                return $"{capitalizedTag} + {tags.Length - 1} more tags";
            }

            return $"{capitalizedTag} Auto Collection";
        }

        // ================================================================
        // IMAGE/PHOTO SETTING METHODS
        // ================================================================
        // This section contains methods for setting collection images/photos
        // from various sources including persons and media items.
        private async Task SetPhotoForCollection(BoxSet collection, Person? specificPerson = null)
        {
            try
            {
                // Artwork the user (or an earlier run) already set is never replaced - re-picking
                // it on every sync is what made collection posters change on their own.
                if (collection.ImageInfos != null && collection.ImageInfos.Any(i => i.Type == ImageType.Primary))
                {
                    _logger.LogDebug("Collection {CollectionName} already has a primary image, keeping it",
                        collection.Name);
                    return;
                }

                // First attempt: Use the specific person if provided
                if (specificPerson != null && specificPerson.ImageInfos != null)
                {
                    var personImageInfo = specificPerson.ImageInfos
                        .FirstOrDefault(i => i.Type == ImageType.Primary);

                    if (personImageInfo != null)
                    {
                        // Set the image path directly
                        collection.SetImage(new ItemImageInfo
                        {
                            Path = personImageInfo.Path,
                            Type = ImageType.Primary
                        }, 0);

                        await _libraryManager.UpdateItemAsync(
                            collection,
                            collection.GetParent(),
                            ItemUpdateType.ImageUpdate,
                            CancellationToken.None);
                        _logger.LogInformation("Successfully set image for collection {CollectionName} from specified person {PersonName}",
                            collection.Name, specificPerson.Name);

                        return; // We're done if we used the specified person's image
                    }
                }

                // Second attempt: Try to determine the collection type and set appropriate image

                // Get the collection's items to determine its nature
                var query = new InternalItemsQuery
                {
                    Recursive = true
                };

                var items = collection.GetItems(query)
                    .Items
                    .ToList();

                _logger.LogDebug("Found {Count} items in collection {CollectionName}",
                    items.Count, collection.Name);

                // If no specific person was provided, but collection name suggests it's for a person,
                // try to find that person
                if (specificPerson == null)
                {
                    string term = collection.Name;

                    // Check if this collection might be for a person (actor or director)
                    var personQuery = new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Person },
                        Name = term
                    };

                    var person = _libraryManager.GetItemList(personQuery)
                        .FirstOrDefault(p =>
                            p.Name.Equals(term, StringComparison.OrdinalIgnoreCase) &&
                            p.ImageInfos != null &&
                            p.ImageInfos.Any(i => i.Type == ImageType.Primary)) as Person;

                    // If we found a person with an image, use their image
                    if (person != null && person.ImageInfos != null)
                    {
                        var personImageInfo = person.ImageInfos
                            .FirstOrDefault(i => i.Type == ImageType.Primary);

                        if (personImageInfo != null)
                        {
                            // Set the image path directly
                            collection.SetImage(new ItemImageInfo
                            {
                                Path = personImageInfo.Path,
                                Type = ImageType.Primary
                            }, 0);

                            await _libraryManager.UpdateItemAsync(
                                collection,
                                collection.GetParent(),
                                ItemUpdateType.ImageUpdate,
                                CancellationToken.None);
                            _logger.LogInformation("Successfully set image for collection {CollectionName} from detected person {PersonName}",
                                collection.Name, person.Name);

                            return; // We're done if we found a person image
                        }
                    }
                }

                // Last fallback: use an image from a movie/series in the collection. Ordered by id
                // so the same collection always resolves to the same poster instead of whichever
                // item the library happened to return first.
                var mediaItemWithImage = items
                    .Where(item => item is Movie || item is Series)
                    .Where(item =>
                        item.ImageInfos != null &&
                        item.ImageInfos.Any(i => i.Type == ImageType.Primary))
                    .OrderBy(item => item.Id)
                    .FirstOrDefault();

                if (mediaItemWithImage != null)
                {
                    var imageInfo = mediaItemWithImage.ImageInfos
                        .First(i => i.Type == ImageType.Primary);

                    // Set the image path directly
                    collection.SetImage(new ItemImageInfo
                    {
                        Path = imageInfo.Path,
                        Type = ImageType.Primary
                    }, 0);

                    await _libraryManager.UpdateItemAsync(
                        collection,
                        collection.GetParent(),
                        ItemUpdateType.ImageUpdate,
                        CancellationToken.None);
                    _logger.LogInformation("Successfully set image for collection {CollectionName} from {ItemName}",
                        collection.Name, mediaItemWithImage.Name);
                }
                else
                {
                    _logger.LogWarning("No items with images found in collection {CollectionName}. Items: {Items}",
                        collection.Name,
                        string.Join(", ", items.Select(i => i.Name)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting image for collection {CollectionName}",
                    collection.Name);
            }
        }

        // ================================================================
        // TAG-BASED EXECUTION METHODS
        // ================================================================
        // This section contains methods for processing tag-title pairs and
        // creating collections based on tag matching criteria.
        private async Task ExecuteAutoCollectionsForTagTitlePair(TagTitlePair tagTitlePair)
        {
            _logger.LogInformation($"Performing ExecuteAutoCollections for tag: {tagTitlePair.Tag}");
            
            // Get the collection name from the tag-title pair
            var collectionName = GetCollectionName(tagTitlePair);
            
            // Get or create the collection
            var (collection, isNewCollection) = await GetOrCreateCollectionAsync(collectionName);

            // Get all tags from the tag-title pair
            string[] tags = tagTitlePair.GetTagsArray();
            if (tags.Length == 0)
            {
                _logger.LogWarning("No tags found in tag-title pair for collection {CollectionName}", collectionName);
                return;
            }

            // Check if any tag might correspond to a person
            Person? specificPerson = null;
            foreach (var tag in tags)
            {
                var personQuery = new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Person },
                    Name = tag
                };

                specificPerson = _libraryManager.GetItemList(personQuery)
                    .FirstOrDefault(p =>
                        p.Name.Equals(tag, StringComparison.OrdinalIgnoreCase) &&
                        p.ImageInfos != null &&
                        p.ImageInfos.Any(i => i.Type == ImageType.Primary)) as Person;
                
                if (specificPerson != null)
                {
                    _logger.LogInformation("Found specific person {PersonName} matching tag {Tag}",
                        specificPerson.Name, tag);
                    break;
                }
            }

            // Collect all media items based on the matching mode
            var allMovies = new List<Movie>();
            var allSeries = new List<Series>();
            
            if (tagTitlePair.MatchingMode == TagMatchingMode.And)
            {
                // AND matching - items must match all tags
                _logger.LogInformation("Using AND matching mode for tags: {Tags}", string.Join(", ", tags));
                _logger.LogDebug("Searching for items that match ALL of: {Tags}", string.Join(", ", tags));
                
                allMovies = GetMoviesFromLibraryWithAndMatching(tags, specificPerson).ToList();
                allSeries = GetSeriesFromLibraryWithAndMatching(tags, specificPerson).ToList();
                
                _logger.LogDebug("AND matching found {MovieCount} movies and {SeriesCount} series", 
                    allMovies.Count, allSeries.Count);
            }
            else
            {
                // OR matching (default) - items can match any tag
                _logger.LogInformation("Using OR matching mode for tags: {Tags}", string.Join(", ", tags));
                _logger.LogDebug("Searching for items that match ANY of: {Tags}", string.Join(", ", tags));
                
                foreach (var tag in tags)
                {
                    _logger.LogDebug("Searching for tag: '{Tag}'", tag);
                    var movies = GetMoviesFromLibrary(tag, specificPerson).ToList();
                    var series = GetSeriesFromLibrary(tag, specificPerson).ToList();
                    
                    _logger.LogDebug("  Found {MovieCount} movies and {SeriesCount} series for tag '{Tag}'", 
                        movies.Count, series.Count, tag);
                    
                    if (movies.Count > 0)
                    {
                        foreach (var movie in movies)
                        {
                            var year = movie.ProductionYear?.ToString() ?? "Unknown year";
                            _logger.LogDebug("    + Movie: '{Title}' ({Year})", movie.Name, year);
                        }
                    }
                    
                    if (series.Count > 0)
                    {
                        foreach (var s in series)
                        {
                            var year = s.ProductionYear?.ToString() ?? "Unknown year";
                            _logger.LogDebug("    + Series: '{Title}' ({Year})", s.Name, year);
                        }
                    }
                    
                    _logger.LogInformation($"Found {movies.Count} movies and {series.Count} series for tag: {tag}");
                    
                    allMovies.AddRange(movies);
                    allSeries.AddRange(series);
                }
                
                // Remove duplicates
                var originalMovieCount = allMovies.Count;
                var originalSeriesCount = allSeries.Count;
                
                allMovies = allMovies.Distinct().ToList();
                allSeries = allSeries.Distinct().ToList();
                
                var movieDupes = originalMovieCount - allMovies.Count;
                var seriesDupes = originalSeriesCount - allSeries.Count;
                
                if (movieDupes > 0 || seriesDupes > 0)
                {
                    _logger.LogDebug("Removed {MovieDupes} duplicate movies and {SeriesDupes} duplicate series from OR matching", 
                        movieDupes, seriesDupes);
                }
            }
            
            _logger.LogInformation($"Processing {allMovies.Count} movies and {allSeries.Count} series total for collection: {collectionName}");
            
            var mediaItems = DedupeMediaItems(allMovies.Cast<BaseItem>().Concat(allSeries.Cast<BaseItem>()).ToList());

            await RemoveUnwantedMediaItems(collection, mediaItems);
            await AddWantedMediaItems(collection, mediaItems);
            await SortCollectionBy(collection, SortOrder.Descending);
            
            // Re-fetch the collection to get its updated children
            var updatedCollection = _libraryManager.GetItemById(collection.Id) as BoxSet;

            // Validate collection content
            if (updatedCollection != null)
            {
                ValidateCollectionContent(updatedCollection, mediaItems);
            }
            else
            {
                _logger.LogWarning("Could not re-fetch collection {CollectionName} for validation.", collection.Name);
            }
            
            // Only set the photo for the collection if it's newly created
            if (isNewCollection)
            {
                _logger.LogInformation("Setting image for newly created collection: {CollectionName}", collectionName);
                await SetPhotoForCollection(collection, specificPerson);
            }
            else
            {
                _logger.LogInformation("Preserving existing image for collection: {CollectionName}", collectionName);
            }
        }

        // ================================================================
        // TITLE MATCH EXECUTION METHODS
        // ================================================================
        // This section contains methods for processing title match pairs and
        // creating collections based on pattern matching criteria.
        private async Task ExecuteAutoCollectionsForTitleMatchPair(TitleMatchPair titleMatchPair)
        {            string matchTypeText = titleMatchPair.MatchType switch
            {
                Configuration.MatchType.Title => "title",
                Configuration.MatchType.Genre => "genre",
                Configuration.MatchType.Studio => "studio",
                Configuration.MatchType.Actor => "actor",
                Configuration.MatchType.Director => "director",
                Configuration.MatchType.Tag => "tag",
                Configuration.MatchType.Writer => "writer",
                _ => "title"
            };
            
            string mediaTypeText = titleMatchPair.MediaType switch
            {
                Configuration.MediaTypeFilter.Movies => "movies only",
                Configuration.MediaTypeFilter.Series => "shows only",
                Configuration.MediaTypeFilter.All => "all media",
                _ => "all media"
            };
            
            _logger.LogInformation($"Performing ExecuteAutoCollections for {matchTypeText} match: {titleMatchPair.TitleMatch} (Media filter: {mediaTypeText})");
            
            // Get the collection name from the match pair
            var collectionName = titleMatchPair.CollectionName;
            
            // Get or create the collection
            var (collection, isNewCollection) = await GetOrCreateCollectionAsync(collectionName);
              
            _logger.LogDebug("Title Match Collection '{CollectionName}' - Pattern: '{Pattern}', Match Type: {MatchType}, Case Sensitive: {CaseSensitive}", 
                collectionName, titleMatchPair.TitleMatch, titleMatchPair.MatchType, titleMatchPair.CaseSensitive);
            
            // Find all media items that match the pattern based on match type
            List<Movie> allMovies = new();
            List<Series> allSeries = new();
            
            // Apply media type filter
            switch (titleMatchPair.MediaType)
            {
                case Configuration.MediaTypeFilter.Movies:
                    // Only include movies
                    _logger.LogDebug("Media filter: Movies only");
                    allMovies = GetMoviesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    _logger.LogInformation($"Media filter: Movies only - found {allMovies.Count} matching items");
                    
                    foreach (var movie in allMovies)
                    {
                        var year = movie.ProductionYear?.ToString() ?? "Unknown year";
                        _logger.LogDebug("  + Movie: '{Title}' ({Year})", movie.Name, year);
                    }
                    break;
                    
                case Configuration.MediaTypeFilter.Series:
                    // Only include TV series
                    _logger.LogDebug("Media filter: Series only");
                    allSeries = GetSeriesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    _logger.LogInformation($"Media filter: Series only - found {allSeries.Count} matching items");
                    
                    foreach (var series in allSeries)
                    {
                        var year = series.ProductionYear?.ToString() ?? "Unknown year";
                        _logger.LogDebug("  + Series: '{Title}' ({Year})", series.Name, year);
                    }
                    break;
                    
                case Configuration.MediaTypeFilter.All:
                default:
                    // Include both movies and series (default behavior)
                    _logger.LogDebug("Media filter: All (movies and series)");
                    allMovies = GetMoviesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    
                    allSeries = GetSeriesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    _logger.LogInformation($"Media filter: All - found {allMovies.Count} movies and {allSeries.Count} series");
                    
                    foreach (var movie in allMovies)
                    {
                        var year = movie.ProductionYear?.ToString() ?? "Unknown year";
                        _logger.LogDebug("  + Movie: '{Title}' ({Year})", movie.Name, year);
                    }
                    
                    foreach (var series in allSeries)
                    {
                        var year = series.ProductionYear?.ToString() ?? "Unknown year";
                        _logger.LogDebug("  + Series: '{Title}' ({Year})", series.Name, year);
                    }
                    break;
            }
            
            _logger.LogInformation($"Found {allMovies.Count} movies and {allSeries.Count} series matching {matchTypeText} pattern '{titleMatchPair.TitleMatch}' for collection: {collectionName}");
            
            var mediaItems = DedupeMediaItems(allMovies.Cast<BaseItem>().Concat(allSeries.Cast<BaseItem>()).ToList());

            await RemoveUnwantedMediaItems(collection, mediaItems);
            await AddWantedMediaItems(collection, mediaItems);
            await SortCollectionBy(collection, SortOrder.Descending);
            
            // Re-fetch the collection to get its updated children
            var updatedCollection = _libraryManager.GetItemById(collection.Id) as BoxSet;

            // Validate collection content
            if (updatedCollection != null)
            {
                ValidateCollectionContent(updatedCollection, mediaItems);
            }
            else
            {
                _logger.LogWarning("Could not re-fetch collection {CollectionName} for validation.", collection.Name);
            }
            
            // Only set the photo for the collection if it's newly created
            if (isNewCollection && mediaItems.Count > 0)
            {
                _logger.LogInformation("Setting image for newly created collection: {CollectionName}", collectionName);
                await SetPhotoForCollection(collection, null);
            }
            else
            {
                _logger.LogInformation("Preserving existing image for collection: {CollectionName}", collectionName);
            }
        }

        // ================================================================
        // TIMER AND LIFECYCLE METHODS
        // ================================================================
        // This section contains methods for handling timer events and
        // managing the plugin's lifecycle (startup, disposal).
        private void OnTimerElapsed()
        {
            // Stop the timer until next update
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        // ================================================================
        // PERSON SEARCH HELPER METHODS
        // ================================================================
        // This section contains helper methods for searching media items
        // based on person associations (actors, directors).
        
        // Initialize person-to-media cache for efficient expression evaluation
        private void InitializePersonCache()
        {
            _personToMoviesCache = new Dictionary<(string, string, bool), HashSet<Guid>>();
            _personToSeriesCache = new Dictionary<(string, string, bool), HashSet<Guid>>();
            _itemPeopleCache = new Dictionary<Guid, List<(string Name, string Type)>>();
            _itemLibrariesCache = new Dictionary<Guid, List<string>>();
        }
        
        // Clear person-to-media cache after expression evaluation is complete
        private void ClearPersonCache()
        {
            _personToMoviesCache = null;
            _personToSeriesCache = null;
            _itemPeopleCache = null;
            _itemLibrariesCache = null;
        }
        
        // Get cached people for an item (movie or series)
        private List<(string Name, string Type)> GetCachedPeopleForItem(BaseItem item)
        {
            if (_itemPeopleCache == null)
            {
                // No cache - get directly
                var people = _libraryManager.GetPeople(item);
                return people.Select(p => (p.Name, p.Type.ToString())).ToList();
            }
            
            if (!_itemPeopleCache.TryGetValue(item.Id, out var cachedPeople))
            {
                // Cache miss - populate
                var people = _libraryManager.GetPeople(item);
                cachedPeople = people.Select(p => (p.Name, p.Type.ToString())).ToList();
                _itemPeopleCache[item.Id] = cachedPeople;
            }
            
            return cachedPeople;
        }
        
        // Check if a movie has a specific person (uses cache during expression evaluation)
        private bool MovieHasPerson(Guid movieId, string personNameToMatch, string personType, bool caseSensitive)
        {
            var cacheKey = (personNameToMatch, personType, caseSensitive);
            
            // If cache is active, check or populate it
            if (_personToMoviesCache != null)
            {
                if (!_personToMoviesCache.TryGetValue(cacheKey, out var cachedMovieIds))
                {
                    // Cache miss - populate for this person
                    _logger.LogInformation("Loading movies with {PersonType} matching '{PersonName}'...", 
                        personType, personNameToMatch);
                    var movies = GetMoviesWithPerson(personNameToMatch, personType, caseSensitive);
                    cachedMovieIds = movies.Select(m => m.Id).ToHashSet();
                    _personToMoviesCache[cacheKey] = cachedMovieIds;
                    _logger.LogInformation("Found {Count} movies with {PersonType} matching '{PersonName}'", 
                        cachedMovieIds.Count, personType, personNameToMatch);
                }
                
                return cachedMovieIds.Contains(movieId);
            }
            
            // No cache - do direct lookup (shouldn't happen during expression evaluation)
            var matchingMovies = GetMoviesWithPerson(personNameToMatch, personType, caseSensitive);
            return matchingMovies.Any(m => m.Id == movieId);
        }
        
        // Check if a series has a specific person (uses cache during expression evaluation)
        private bool SeriesHasPerson(Guid seriesId, string personNameToMatch, string personType, bool caseSensitive)
        {
            var cacheKey = (personNameToMatch, personType, caseSensitive);
            
            // If cache is active, check or populate it
            if (_personToSeriesCache != null)
            {
                if (!_personToSeriesCache.TryGetValue(cacheKey, out var cachedSeriesIds))
                {
                    // Cache miss - populate for this person
                    _logger.LogInformation("Loading series with {PersonType} matching '{PersonName}'...", 
                        personType, personNameToMatch);
                    var seriesList = GetSeriesWithPerson(personNameToMatch, personType, caseSensitive);
                    cachedSeriesIds = seriesList.Select(s => s.Id).ToHashSet();
                    _personToSeriesCache[cacheKey] = cachedSeriesIds;
                    _logger.LogInformation("Found {Count} series with {PersonType} matching '{PersonName}'", 
                        cachedSeriesIds.Count, personType, personNameToMatch);
                }
                
                return cachedSeriesIds.Contains(seriesId);
            }
            
            // No cache - do direct lookup (shouldn't happen during expression evaluation)
            var matchingSeries = GetSeriesWithPerson(personNameToMatch, personType, caseSensitive);
            return matchingSeries.Any(s => s.Id == seriesId);
        }

        /// <summary>
        /// Finds every person in the library whose name contains <paramref name="nameToMatch"/>.
        /// </summary>
        /// <remarks>
        /// The name filter is pushed into the library query. Pulling every person in the library
        /// back and filtering in memory is what made large libraries with many actor/director
        /// collections take hours: that full scan ran once per person term, per collection.
        /// </remarks>
        private List<Person> FindPersonsByName(string nameToMatch, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                Recursive = true,
                NameContains = nameToMatch
            }).OfType<Person>()
                // NameContains is case-insensitive in the query, so a case-sensitive
                // search still has to be narrowed down here.
                .Where(p => p.Name != null && p.Name.Contains(nameToMatch, comparison))
                .ToList();
        }

        // Helper method to find movies with a specific person type (actor or director) 
        // that match the given string (partial or exact matching)
        // This method uses Jellyfin's PersonTypes query parameter to ensure only
        // movies where the person has the specified role are returned
        private IEnumerable<Movie> GetMoviesWithPerson(string personNameToMatch, string personType, bool caseSensitive)
        {
            var persons = FindPersonsByName(personNameToMatch, caseSensitive);

            _logger.LogDebug("Found {Count} person(s) matching '{NameToMatch}' for {PersonType} search", 
                persons.Count, personNameToMatch, personType);
            
            if (!persons.Any())
            {
                return Enumerable.Empty<Movie>();
            }
            
            // For each matching person, find their movies where they have the correct role
            var result = new HashSet<Movie>();
            foreach (var person in persons)
            {
                if (person?.Name == null) continue;
                
                // Get all movies that include this person with the specific role type
                var moviesWithPerson = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = person.Name,
                    PersonTypes = new[] { personType }
                }).OfType<Movie>();
                
                foreach (var movie in moviesWithPerson)
                {
                    result.Add(movie);
                    _logger.LogDebug("  + Movie '{Title}' has {PersonName} as {Role}", 
                        movie.Name, person.Name, personType);
                }
            }
            
            _logger.LogDebug("Found {Count} movies where person(s) matching '{NameToMatch}' have role '{PersonType}'", 
                result.Count, personNameToMatch, personType);
            
            return result;
        }
        
        // Helper method to find series with a specific person type (actor or director) 
        // that match the given string (partial or exact matching)
        // This method uses Jellyfin's PersonTypes query parameter to ensure only
        // series where the person has the specified role are returned
        private IEnumerable<Series> GetSeriesWithPerson(string personNameToMatch, string personType, bool caseSensitive)
        {
            var persons = FindPersonsByName(personNameToMatch, caseSensitive);

            _logger.LogDebug("Found {Count} person(s) matching '{NameToMatch}' for {PersonType} search", 
                persons.Count, personNameToMatch, personType);
            
            if (!persons.Any())
            {
                return Enumerable.Empty<Series>();
            }
            
            // For each matching person, find their series where they have the correct role
            var result = new HashSet<Series>();
            foreach (var person in persons)
            {
                if (person?.Name == null) continue;
                
                // Get all series that include this person with the specific role type
                var seriesWithPerson = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = person.Name,
                    PersonTypes = new[] { personType }
                }).OfType<Series>();
                
                foreach (var series in seriesWithPerson)
                {
                    result.Add(series);
                    _logger.LogDebug("  + Series '{Title}' has {PersonName} as {Role}", 
                        series.Name, person.Name, personType);
                }
            }
            
            _logger.LogDebug("Found {Count} series where person(s) matching '{NameToMatch}' have role '{PersonType}'", 
                result.Count, personNameToMatch, personType);
            
            return result;
        }

        // ================================================================
        // CRITERIA EVALUATION METHODS
        // ================================================================
        // This section contains methods for evaluating complex criteria
        // against movies and series for advanced expression-based collections.
        // Method to evaluate a criteria for a movie
        private bool EvaluateMovieCriteria(Movie movie, Configuration.CriteriaType criteriaType, string value, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
                
            switch (criteriaType)
            {
                // Basic metadata criteria
                case Configuration.CriteriaType.Title:
                    return movie.Name?.Contains(value, comparison) == true;
                    
                case Configuration.CriteriaType.Genre:
                    return movie.Genres != null && 
                           movie.Genres.Any(g => g.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Studio:
                    return movie.Studios != null && 
                           movie.Studios.Any(s => s.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Actor:
                    // Use cached lookup for actors during expression evaluation
                    return MovieHasPerson(movie.Id, value, "Actor", caseSensitive);
                    
                case Configuration.CriteriaType.Director:
                    // Use cached lookup for directors during expression evaluation
                    return MovieHasPerson(movie.Id, value, "Director", caseSensitive);
                    
                // Media type criteria
                case Configuration.CriteriaType.Movie:
                    // Always true for movies
                    return true;
                    
                case Configuration.CriteriaType.Show:
                    // Always false for movies
                    return false;
                    
                case Configuration.CriteriaType.Tag:
                    // Whole-tag match: TAG "Best Film" must not also pull in "Best Film Editing".
                    return movie.Tags != null && 
                           movie.Tags.Any(t => t.Equals(value, comparison));
                           
                // Content rating and parental guidance criteria
                case Configuration.CriteriaType.ParentalRating:
                    return !string.IsNullOrEmpty(movie.OfficialRating) && 
                           movie.OfficialRating.Equals(value, comparison);
                             case Configuration.CriteriaType.CommunityRating:
                    return CompareNumericValue(movie.CommunityRating, value);                case Configuration.CriteriaType.CriticsRating:
                    return CompareNumericValue(movie.CriticRating, value);
                           
                // Technical and media stream criteria
                case Configuration.CriteriaType.AudioLanguage:
                    return movie.GetMediaStreams()
                           .Any(stream => 
                                stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio && 
                                !string.IsNullOrEmpty(stream.Language) && 
                                stream.Language.Contains(value, comparison));
                case Configuration.CriteriaType.Subtitle:
                    return movie.GetMediaStreams()
                           .Any(stream => 
                                stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Subtitle && 
                                !string.IsNullOrEmpty(stream.Language) && 
                                stream.Language.Contains(value, comparison));
                  case Configuration.CriteriaType.ProductionLocation:
                    return movie.ProductionLocations != null && 
                           movie.ProductionLocations.Any(l => l.Contains(value, comparison));
                           
                // Temporal and date-based criteria
                case Configuration.CriteriaType.Year:
                    if (movie.ProductionYear.HasValue)
                    {
                        return CompareNumericValue(movie.ProductionYear.Value, value);
                    }
                    return false;
                case Configuration.CriteriaType.CustomRating:
                    if (!string.IsNullOrWhiteSpace(movie.CustomRating))
                    {
                        if (value.StartsWith(">") || value.StartsWith("<") || value.StartsWith("=") || float.TryParse(value, out _))
                        {
                            if (float.TryParse(movie.CustomRating, out var actualNumeric))
                            {
                                return CompareNumericValue(actualNumeric, value);
                            }
                        }
                        return movie.CustomRating.Contains(value, comparison);
                    }
                    return false;

                case Configuration.CriteriaType.Filename:
                    return !string.IsNullOrEmpty(movie.Path) && movie.Path.Contains(value, comparison);
                    
                case Configuration.CriteriaType.ReleaseDate:
                    return CompareDateValue(movie.PremiereDate, value);
                    
                case Configuration.CriteriaType.AddedDate:
                    return CompareDateValue(movie.DateCreated, value);
                    
                case Configuration.CriteriaType.EpisodeAirDate:
                    // Movies don't have episodes, so always return false
                    return false;
                    
                case Configuration.CriteriaType.Unplayed:
                    // Check if the movie is unplayed (not watched by any user)
                    return IsItemUnplayed(movie) == true;
                    
                case Configuration.CriteriaType.Watched:
                    // Check if the movie is watched (played by at least one user)
                    return IsItemUnplayed(movie) == false;
                    
                case Configuration.CriteriaType.Library:
                    return MatchesLibrary(movie, value, comparison);

                case Configuration.CriteriaType.Runtime:
                    return MatchesRuntime(movie, value);

                case Configuration.CriteriaType.Resolution:
                    return MatchesResolution(movie, value);

                case Configuration.CriteriaType.VideoRange:
                    return MatchesVideoRange(movie, value);

                case Configuration.CriteriaType.AudioChannels:
                    return MatchesAudioChannels(movie, value);

                case Configuration.CriteriaType.AudioCodec:
                    return MatchesAudioCodec(movie, value, comparison);

                case Configuration.CriteriaType.Writer:
                    return MovieHasPerson(movie.Id, value, "Writer", caseSensitive);

                case Configuration.CriteriaType.Producer:
                    return MovieHasPerson(movie.Id, value, "Producer", caseSensitive);

                case Configuration.CriteriaType.Overview:
                    return !string.IsNullOrEmpty(movie.Overview) && movie.Overview.Contains(value, comparison);

                case Configuration.CriteriaType.Tagline:
                    return !string.IsNullOrEmpty(movie.Tagline) && movie.Tagline.Contains(value, comparison);

                default:
                    return false;
            }
        }
          // Method to evaluate a criteria for a series
        private bool EvaluateSeriesCriteria(Series series, Configuration.CriteriaType criteriaType, string value, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
                
            switch (criteriaType)
            {
                case Configuration.CriteriaType.Title:
                    return series.Name?.Contains(value, comparison) == true;
                    
                case Configuration.CriteriaType.Genre:
                    return series.Genres != null && 
                           series.Genres.Any(g => g.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Studio:
                    return series.Studios != null && 
                           series.Studios.Any(s => s.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Actor:
                    // Use cached lookup for actors during expression evaluation
                    return SeriesHasPerson(series.Id, value, "Actor", caseSensitive);
                    
                case Configuration.CriteriaType.Director:
                    // Use cached lookup for directors during expression evaluation
                    return SeriesHasPerson(series.Id, value, "Director", caseSensitive);
                    
                case Configuration.CriteriaType.Movie:
                    // Always false for series
                    return false;
                    
                case Configuration.CriteriaType.Show:
                    // Always true for series
                    return true;
                
                case Configuration.CriteriaType.Tag:
                    // Whole-tag match: TAG "Best Film" must not also pull in "Best Film Editing".
                    return series.Tags != null &&
                           series.Tags.Any(t => t.Equals(value, comparison));
                           
                case Configuration.CriteriaType.ParentalRating:
                    return !string.IsNullOrEmpty(series.OfficialRating) && 
                           series.OfficialRating.Equals(value, comparison);
                             case Configuration.CriteriaType.CommunityRating:
                    return CompareNumericValue(series.CommunityRating, value);
                      case Configuration.CriteriaType.CriticsRating:
                    return CompareNumericValue(series.CriticRating, value);
                case Configuration.CriteriaType.AudioLanguage:
                    // For series, we need to check episode media sources
                    var episodes = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        AncestorIds = new[] { series.Id },
                        IncludeItemTypes = new[] { BaseItemKind.Episode },
                        Recursive = true
                    });
                    
                    // If any episode has the specified audio language, return true
                    foreach (var episode in episodes)
                    {
                        if (episode.GetMediaStreams()
                            .Any(stream => 
                                stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio && 
                                !string.IsNullOrEmpty(stream.Language) && 
                                stream.Language.Contains(value, comparison)))
                        {
                            return true;
                        }
                    }
                    return false;
                    
                case Configuration.CriteriaType.Subtitle:
                    // For series, check episode media sources for subtitles
                    var episodesForSubs = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        AncestorIds = new[] { series.Id },
                        IncludeItemTypes = new[] { BaseItemKind.Episode },
                        Recursive = true
                    });
                    
                    // If any episode has the specified subtitle language, return true
                    foreach (var episode in episodesForSubs)
                    {
                        if (episode.GetMediaStreams()
                            .Any(stream => 
                                stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Subtitle && 
                                !string.IsNullOrEmpty(stream.Language) && 
                                stream.Language.Contains(value, comparison)))
                        {
                            return true;                        }
                    }
                    return false;
                    
                case Configuration.CriteriaType.ProductionLocation:
                    return series.ProductionLocations != null && 
                           series.ProductionLocations.Any(l => l.Contains(value, comparison));
                           
                case Configuration.CriteriaType.Year:
                    if (series.ProductionYear.HasValue)
                    {
                        return CompareNumericValue(series.ProductionYear.Value, value);
                    }
                    return false;
                case Configuration.CriteriaType.CustomRating:
                    if (!string.IsNullOrWhiteSpace(series.CustomRating))
                    {
                        if (value.StartsWith(">") || value.StartsWith("<") || value.StartsWith("=") || float.TryParse(value, out _))
                        {
                            if (float.TryParse(series.CustomRating, out var actualNumeric))
                            {
                                return CompareNumericValue(actualNumeric, value);
                            }
                        }
                        return series.CustomRating.Contains(value, comparison);
                    }
                    return false;

                case Configuration.CriteriaType.Filename:
                    var episodesForFilename = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        AncestorIds = new[] { series.Id },
                        IncludeItemTypes = new[] { BaseItemKind.Episode },
                        Recursive = true
                    });

                    foreach (var episode in episodesForFilename)
                    {
                        if (!string.IsNullOrEmpty(episode.Path) && episode.Path.Contains(value, comparison))
                        {
                            return true;
                        }
                    }
                    return false;
                    
                case Configuration.CriteriaType.ReleaseDate:
                    return CompareDateValue(series.PremiereDate, value);
                    
                case Configuration.CriteriaType.AddedDate:
                    return CompareDateValue(series.DateCreated, value);
                    
                case Configuration.CriteriaType.EpisodeAirDate:
                    return CompareDateValue(GetMostRecentEpisodeAirDate(series), value);
                    
                case Configuration.CriteriaType.Unplayed:
                    // Check if the series is unplayed (not watched by any user)
                    return IsItemUnplayed(series) == true;
                    
                case Configuration.CriteriaType.Watched:
                    // Check if the series is watched (played by at least one user)
                    return IsItemUnplayed(series) == false;
                    
                case Configuration.CriteriaType.Library:
                    return MatchesLibrary(series, value, comparison);

                case Configuration.CriteriaType.Runtime:
                    return MatchesRuntime(series, value);

                case Configuration.CriteriaType.Resolution:
                    return MatchesResolution(series, value);

                case Configuration.CriteriaType.VideoRange:
                    return MatchesVideoRange(series, value);

                case Configuration.CriteriaType.AudioChannels:
                    return MatchesAudioChannels(series, value);

                case Configuration.CriteriaType.AudioCodec:
                    return MatchesAudioCodec(series, value, comparison);

                case Configuration.CriteriaType.Writer:
                    return SeriesHasPerson(series.Id, value, "Writer", caseSensitive);

                case Configuration.CriteriaType.Producer:
                    return SeriesHasPerson(series.Id, value, "Producer", caseSensitive);

                case Configuration.CriteriaType.Overview:
                    return !string.IsNullOrEmpty(series.Overview) && series.Overview.Contains(value, comparison);

                case Configuration.CriteriaType.Tagline:
                    return !string.IsNullOrEmpty(series.Tagline) && series.Tagline.Contains(value, comparison);

                default:
                    return false;
            }
        }

        // ================================================================
        // EXPRESSION COLLECTION METHODS
        // ================================================================
        // This section contains methods for processing expression-based
        // collections using complex criteria and boolean logic.
          // Process expression collections
        private async Task ExecuteAutoCollectionsForExpressionCollection(Configuration.ExpressionCollection expressionCollection)
        {
            _logger.LogInformation("Processing expression collection: {CollectionName}", expressionCollection.CollectionName);
            
            // Always parse the expression when executing
            if (!expressionCollection.ParseExpression())
            {
                _logger.LogError("Failed to parse expression for collection {CollectionName}: {Errors}", 
                    expressionCollection.CollectionName, 
                    string.Join("; ", expressionCollection.ParseErrors));
                return;
            }
            
            // Get or create the collection
            var collectionName = expressionCollection.CollectionName;
            var (collection, isNewCollection) = await GetOrCreateCollectionAsync(collectionName);
            
            // Get all movies and series from the library
            var allMovies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true
            }).OfType<Movie>().ToList();
            
            var allSeries = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                IsVirtualItem = false,
                Recursive = true
            }).OfType<Series>().ToList();
            
            _logger.LogInformation("Found {MovieCount} movies and {SeriesCount} series to evaluate", 
                allMovies.Count, allSeries.Count);
            
            _logger.LogDebug("Expression collection '{CollectionName}' - Expression: {Expression}", 
                collectionName, expressionCollection.Expression);
            
            // Filter movies and series based on the expression
            var matchingMovies = new List<Movie>();
            var matchingSeries = new List<Series>();
            
            if (expressionCollection.ParsedExpression != null)
            {
                // Normally the cache is opened once for the whole run; only take ownership
                // of it here when this collection is being processed on its own.
                var ownsPersonCache = _personToMoviesCache == null;
                if (ownsPersonCache)
                {
                    InitializePersonCache();
                }

                try
                {
                    _logger.LogDebug("Evaluating movies against expression...");
                    
                    matchingMovies = allMovies
                        .Where(movie => movie != null)
                        .Where(movie => 
                        {
                            var matches = expressionCollection.ParsedExpression.Evaluate(
                                (criteriaType, value) => EvaluateMovieCriteria(movie, criteriaType, value, expressionCollection.CaseSensitive)
                            );
                            
                            if (matches)
                            {
                                var year = movie.ProductionYear?.ToString() ?? "Unknown year";
                                _logger.LogDebug("  ✓ Movie matched: '{Title}' ({Year}) (ID: {Id})", 
                                    movie.Name, year, movie.Id);
                            }
                            
                            return matches;
                        })
                        .ToList();
                    
                    _logger.LogDebug("Evaluating series against expression...");
                        
                    matchingSeries = allSeries
                        .Where(series => series != null)
                        .Where(series => 
                        {
                            var matches = expressionCollection.ParsedExpression.Evaluate(
                                (criteriaType, value) => EvaluateSeriesCriteria(series, criteriaType, value, expressionCollection.CaseSensitive)
                            );
                            
                            if (matches)
                            {
                                var year = series.ProductionYear?.ToString() ?? "Unknown year";
                                _logger.LogDebug("  ✓ Series matched: '{Title}' ({Year}) (ID: {Id})", 
                                    series.Name, year, series.Id);
                            }
                            
                            return matches;
                        })
                        .ToList();
                }
                finally
                {
                    if (ownsPersonCache)
                    {
                        ClearPersonCache();
                    }
                }
            }
            
            _logger.LogInformation("Expression matched {MovieCount} movies and {SeriesCount} series", 
                matchingMovies.Count, matchingSeries.Count);
                
            // Combine movies and series
            var allMatchingItems = DedupeMediaItems(matchingMovies.Cast<BaseItem>().Concat(matchingSeries.Cast<BaseItem>()).ToList());         

            // Update the collection (add new items, remove items that no longer match)
            await RemoveUnwantedMediaItems(collection, allMatchingItems);
            await AddWantedMediaItems(collection, allMatchingItems);
            await SortCollectionBy(collection, SortOrder.Descending);

            // Re-fetch the collection to get its updated children
            var updatedCollection = _libraryManager.GetItemById(collection.Id) as BoxSet;

            // Validate collection content
            if (updatedCollection != null)
            {
                ValidateCollectionContent(updatedCollection, allMatchingItems);
            }
            else
            {
                _logger.LogWarning("Could not re-fetch expression collection {CollectionName} for validation.", collection.Name);
            }
            
            // Set collection image if it's a new collection
            if (isNewCollection && allMatchingItems.Count > 0)
            {
                await SetPhotoForCollection(collection);
            }
        }
        
        // ================================================================
        // UTILITY METHODS
        // ================================================================
        // This section contains utility and helper methods for various
        // common operations like deduplication, comparisons, and data processing.
        private List<BaseItem> DedupeMediaItems(List<BaseItem> mediaItems)
        {
            _logger.LogDebug("Starting deduplication process for {Count} media items", mediaItems.Count);
            
            var withoutDateOrTitle = mediaItems
                .Where(i => !i.PremiereDate.HasValue || string.IsNullOrWhiteSpace(i.Name))
                .ToList();
                
            if (withoutDateOrTitle.Count > 0)
            {
                _logger.LogDebug("Found {Count} items without date or title - keeping all:", 
                    withoutDateOrTitle.Count);
                foreach (var item in withoutDateOrTitle)
                {
                    var reason = string.IsNullOrWhiteSpace(item.Name) ? "missing title" : "missing premiere date";
                    _logger.LogDebug("  - '{Title}' (ID: {Id}) - kept ({Reason})", 
                        item.Name ?? "Unknown", item.Id, reason);
                }
            }
            
            var itemsWithData = mediaItems
                .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.PremiereDate.HasValue)
                .ToList();
                
            var grouped = itemsWithData
                .GroupBy(i => new { Title = i.Name!.Trim().ToLowerInvariant(), Date = i.PremiereDate!.Value })
                .ToList();
                
            var uniqueItems = new List<BaseItem>();
            var duplicatesRemoved = 0;
            
            foreach (var group in grouped)
            {
                var items = group.ToList();
                var kept = items.First();
                uniqueItems.Add(kept);
                
                if (items.Count > 1)
                {
                    duplicatesRemoved += items.Count - 1;
                    var itemType = kept is Movie ? "Movie" : kept is Series ? "Series" : "Item";
                    _logger.LogDebug("Duplicate {Type} found - '{Title}' ({Date}):", 
                        itemType, kept.Name, kept.PremiereDate!.Value.ToShortDateString());
                    _logger.LogDebug("  ✓ Keeping: ID {Id} from '{Path}'", 
                        kept.Id, kept.Path ?? "Unknown path");
                    
                    foreach (var duplicate in items.Skip(1))
                    {
                        _logger.LogDebug("  ✗ Removing duplicate: ID {Id} from '{Path}'", 
                            duplicate.Id, duplicate.Path ?? "Unknown path");
                    }
                }
            }
            
            var result = uniqueItems.Concat(withoutDateOrTitle).ToList();
            
            _logger.LogDebug("Deduplication complete: {Original} items → {Final} items ({Removed} duplicates removed)", 
                mediaItems.Count, result.Count, duplicatesRemoved);
            
            return result;
        }
        
          // Helper method to handle numeric comparisons for ratings
        private bool CompareNumericValue(float? actualValue, string targetValueString)
        {
            if (!actualValue.HasValue)
                return false;
                
            // Remove any surrounding whitespace
            targetValueString = targetValueString.Trim();
                
            try
            {
                // Check for comparison operators
                if (targetValueString.StartsWith(">="))
                {
                    if (float.TryParse(targetValueString.Substring(2), out float targetValue))
                        return actualValue >= targetValue;
                }
                else if (targetValueString.StartsWith("<="))
                {
                    if (float.TryParse(targetValueString.Substring(2), out float targetValue))
                        return actualValue <= targetValue;
                }
                else if (targetValueString.StartsWith(">"))
                {
                    if (float.TryParse(targetValueString.Substring(1), out float targetValue))
                        return actualValue > targetValue;
                }
                else if (targetValueString.StartsWith("<"))
                {
                    if (float.TryParse(targetValueString.Substring(1), out float targetValue))
                        return actualValue < targetValue;
                }
                else if (targetValueString.StartsWith("="))
                {
                    if (float.TryParse(targetValueString.Substring(1), out float targetValue))
                        return Math.Abs(actualValue.Value - targetValue) < 0.1f;
                }
                else if (float.TryParse(targetValueString, out float targetValue))
                {
                    // Default to exact match if no comparison operator
                    return Math.Abs(actualValue.Value - targetValue) < 0.1f;
                }
            }
            catch (FormatException)
            {
                _logger.LogWarning($"Failed to parse '{targetValueString}' as a numeric value for comparison");
            }
            
            return false;
        }
        
        // Helper method to handle date comparisons with day-based expressions
        private bool CompareDateValue(DateTime? actualDate, string targetValueString)
        {
            if (!actualDate.HasValue)
                return false;
                
            // Remove any surrounding whitespace
            targetValueString = targetValueString.Trim();
                
            try
            {
                // Parse the number of days
                string numberPart;
                string operatorPart;
                
                if (targetValueString.StartsWith(">="))
                {
                    operatorPart = ">=";
                    numberPart = targetValueString.Substring(2);
                }
                else if (targetValueString.StartsWith("<="))
                {
                    operatorPart = "<=";
                    numberPart = targetValueString.Substring(2);
                }
                else if (targetValueString.StartsWith(">"))
                {
                    operatorPart = ">";
                    numberPart = targetValueString.Substring(1);
                }
                else if (targetValueString.StartsWith("<"))
                {
                    operatorPart = "<";
                    numberPart = targetValueString.Substring(1);
                }
                else if (targetValueString.StartsWith("="))
                {
                    operatorPart = "=";
                    numberPart = targetValueString.Substring(1);
                }
                else
                {
                    // Default to > if no operator specified
                    operatorPart = ">";
                    numberPart = targetValueString;
                }
                
                if (!int.TryParse(numberPart, out int targetDays))
                    return false;
                    
                // Calculate the difference in days
                // Use the same date handling as Jellyfin for consistency
                var now = DateTime.Now; // Use local time instead of UTC for consistency with Jellyfin
                var daysDifference = (now - actualDate.Value).TotalDays;
                
                // Perform comparison
                switch (operatorPart)
                {
                    case ">=":
                        return daysDifference >= targetDays;
                    case "<=":
                        return daysDifference <= targetDays;
                    case ">":
                        return daysDifference > targetDays;
                    case "<":
                        return daysDifference < targetDays;
                    case "=":
                        return Math.Abs(daysDifference - targetDays) < 1.0; // Within 1 day
                    default:
                        return false;
                }
            }
            catch (FormatException)
            {
                _logger.LogWarning($"Failed to parse '{targetValueString}' as a day value for date comparison");
            }
            
            return false;
        }
        
        // ================================================================
        // LIBRARY, TECHNICAL AND TEXT CRITERIA
        // ================================================================
        // Shared evaluation helpers. These behave identically for movies and
        // series, so both criteria switches delegate here.

        /// <summary>
        /// Names of the libraries (media folders) an item belongs to.
        /// </summary>
        private List<string> GetLibraryNames(BaseItem item)
        {
            if (_itemLibrariesCache != null && _itemLibrariesCache.TryGetValue(item.Id, out var cached))
            {
                return cached;
            }

            List<string> names;
            try
            {
                names = _libraryManager.GetCollectionFolders(item)
                    .Select(folder => folder.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine the library for item {ItemName}", item.Name);
                names = new List<string>();
            }

            if (_itemLibrariesCache != null)
            {
                _itemLibrariesCache[item.Id] = names;
            }

            return names;
        }

        private bool MatchesLibrary(BaseItem item, string value, StringComparison comparison)
        {
            return GetLibraryNames(item).Any(name => name.Equals(value, comparison));
        }

        /// <summary>
        /// Compares the item's runtime, in whole minutes, against a value such as "&lt;45" or "&gt;=100".
        /// </summary>
        private bool MatchesRuntime(BaseItem item, string value)
        {
            if (!item.RunTimeTicks.HasValue || item.RunTimeTicks.Value <= 0)
            {
                return false;
            }

            var minutes = (float)item.RunTimeTicks.Value / TimeSpan.TicksPerMinute;
            return CompareNumericValue(minutes, value);
        }

        private MediaBrowser.Model.Entities.MediaStream? GetPrimaryVideoStream(BaseItem item)
        {
            return item.GetMediaStreams()
                .Where(stream => stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Video)
                .OrderByDescending(stream => stream.Width ?? 0)
                .FirstOrDefault();
        }

        /// <summary>
        /// Matches a resolution label (4K, 1080p, 720p, SD) against the item's video stream.
        /// </summary>
        /// <remarks>
        /// Buckets are based on width with a height fallback, because anamorphic and
        /// non-16:9 sources do not line up with the nominal "1080p"-style heights.
        /// </remarks>
        private bool MatchesResolution(BaseItem item, string value)
        {
            var video = GetPrimaryVideoStream(item);
            if (video == null)
            {
                return false;
            }

            var width = video.Width ?? 0;
            var height = video.Height ?? 0;
            if (width == 0 && height == 0)
            {
                return false;
            }

            string bucket;
            if (width >= 3800 || height >= 2000)
            {
                bucket = "4K";
            }
            else if (width >= 2500 || height >= 1400)
            {
                bucket = "1440P";
            }
            else if (width >= 1800 || height >= 1000)
            {
                bucket = "1080P";
            }
            else if (width >= 1200 || height >= 700)
            {
                bucket = "720P";
            }
            else
            {
                bucket = "SD";
            }

            var wanted = value.Trim().ToUpperInvariant() switch
            {
                "4K" or "2160P" or "UHD" => "4K",
                "1440P" or "2K" or "QHD" => "1440P",
                "1080P" or "FHD" or "FULLHD" => "1080P",
                "720P" or "HD" => "720P",
                "SD" or "480P" or "576P" or "DVD" => "SD",
                _ => value.Trim().ToUpperInvariant()
            };

            return bucket == wanted;
        }

        /// <summary>
        /// Matches a dynamic range label (SDR, HDR, HDR10, HLG, DoVi) against the item's video stream.
        /// </summary>
        private bool MatchesVideoRange(BaseItem item, string value)
        {
            var video = GetPrimaryVideoStream(item);
            if (video == null)
            {
                return false;
            }

            var rangeType = video.VideoRangeType.ToString().ToUpperInvariant();
            var wanted = value.Trim().ToUpperInvariant();

            return wanted switch
            {
                // "HDR" is the umbrella term users reach for, so it covers every
                // high-dynamic-range flavour rather than only the HDR10 profile.
                "HDR" => rangeType != "SDR" && rangeType != "UNKNOWN",
                "SDR" => rangeType == "SDR",
                "DOVI" or "DV" or "DOLBYVISION" => rangeType.StartsWith("DOVI", StringComparison.Ordinal),
                _ => rangeType == wanted
            };
        }

        /// <summary>
        /// Compares the highest audio channel count against a value such as "&gt;=6", "5.1" or "stereo".
        /// </summary>
        private bool MatchesAudioChannels(BaseItem item, string value)
        {
            var channels = item.GetMediaStreams()
                .Where(stream => stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio)
                .Select(stream => stream.Channels ?? 0)
                .DefaultIfEmpty(0)
                .Max();

            if (channels == 0)
            {
                return false;
            }

            var wanted = value.Trim().ToUpperInvariant() switch
            {
                "MONO" => "1",
                "STEREO" or "2.0" => "2",
                "5.1" => "6",
                "7.1" => "8",
                _ => value.Trim()
            };

            return CompareNumericValue(channels, wanted);
        }

        /// <summary>
        /// Matches an audio codec or track description, so both "dts" and "atmos" work.
        /// </summary>
        /// <remarks>
        /// Atmos and similar are not codecs of their own; they only show up in the
        /// stream's profile or display title, so both are searched.
        /// </remarks>
        private bool MatchesAudioCodec(BaseItem item, string value, StringComparison comparison)
        {
            return item.GetMediaStreams()
                .Where(stream => stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio)
                .Any(stream =>
                    (!string.IsNullOrEmpty(stream.Codec) && stream.Codec.Contains(value, comparison)) ||
                    (!string.IsNullOrEmpty(stream.Profile) && stream.Profile.Contains(value, comparison)) ||
                    (!string.IsNullOrEmpty(stream.DisplayTitle) && stream.DisplayTitle.Contains(value, comparison)));
        }

        // Helper method to check if an item is unplayed (not watched by any user).
        // Returns null when the play state cannot be determined - callers must then treat
        // both UNPLAYED and WATCHED as "no match" rather than guessing, otherwise an
        // unreadable play state silently sweeps the whole library into the collection.
        private bool? IsItemUnplayed(BaseItem item)
        {
            try
            {
                // If user data manager or user manager is not available, the play state is unknown
                if (_userDataManager == null || _userManager == null)
                {
                    _logger.LogWarning("UserDataManager or UserManager not available for item {ItemName}, play state is unknown", item.Name);
                    return null;
                }

                // Get all users and check if ANY of them have played this item.
                // Resolved through JellyfinCompat because 10.11.9 replaced IUserManager.Users with GetUsers().
                var users = JellyfinCompat.GetUsers(_userManager);

                if (users.Count == 0)
                {
                    _logger.LogDebug("No users found, assuming item {ItemName} is unplayed", item.Name);
                    return true;
                }

                // Check each user's play state for this item
                foreach (var user in users)
                {
                    var userData = _userDataManager.GetUserData(user, item);
                    if (userData != null && userData.Played)
                    {
                        // At least one user has played this item, so it's not "unplayed"
                        _logger.LogDebug("Item {ItemName} has been played by user {UserName}", item.Name, user.Username);
                        return false;
                    }
                }
                
                // No user has played this item
                _logger.LogDebug("Item {ItemName} is unplayed by all users", item.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking play state for item {ItemName}", item.Name);
                // Unknown play state - let the caller drop the item instead of assuming
                return null;
            }
        }

        // Helper method to get the most recent episode air date for a series
        private DateTime? GetMostRecentEpisodeAirDate(Series series)
        {
            try
            {
                // Get all episodes for this series
                var episodes = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Episode },
                    IsVirtualItem = false,
                    Recursive = true,
                    ParentId = series.Id
                });

                DateTime? mostRecentDate = null;
                
                foreach (var episode in episodes)
                {
                    var episodeAirDate = episode.PremiereDate;
                    if (episodeAirDate.HasValue)
                    {
                        if (!mostRecentDate.HasValue || episodeAirDate.Value > mostRecentDate.Value)
                        {
                            mostRecentDate = episodeAirDate.Value;
                        }
                    }
                }

                return mostRecentDate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting most recent episode air date for series {SeriesName}", series.Name);
                return null;
            }
        }
    }
}