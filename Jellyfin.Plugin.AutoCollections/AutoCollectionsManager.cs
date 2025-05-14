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
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;
using Jellyfin.Plugin.AutoCollections.Configuration;

namespace Jellyfin.Plugin.AutoCollections

{
    public class AutoCollectionsManager : IDisposable
    {
        private readonly ICollectionManager _collectionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly Timer _timer;
        private readonly ILogger<AutoCollectionsManager> _logger;
        private readonly string _pluginDirectory;

        public AutoCollectionsManager(IProviderManager providerManager, ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger<AutoCollectionsManager> logger, IApplicationPaths applicationPaths)
        {
            _providerManager = providerManager;
            _collectionManager = collectionManager;
            _libraryManager = libraryManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _pluginDirectory = Path.Combine(applicationPaths.DataPath, "Autocollections");
            Directory.CreateDirectory(_pluginDirectory);
        }

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
                    HasTvdbId = true,
                    Tags = [term]
                }).Select(m => m as Series);

                var byGenres = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    HasTvdbId = true,
                    Genres = [term]
                }).Select(m => m as Series);
                
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
                    HasTvdbId = true,
                    Person = personName,
                    PersonTypes = new[] { "Actor" }
                }).Select(m => m as Series);

                var byDirectors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    HasTvdbId = true,
                    Person = personName,
                    PersonTypes = new[] { "Director" }
                }).Select(m => m as Series);
                
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
                    HasTvdbId = false,
                    Tags = [term]
                }).Select(m => m as Movie);

                var byGenres = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    HasTvdbId = false,
                    Genres = [term]
                }).Select(m => m as Movie);
                
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
                    HasTvdbId = false,
                    Person = personName,
                    PersonTypes = new[] { "Actor" }
                }).Select(m => m as Movie);

                var byDirectors = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie },
                    IsVirtualItem = false,
                    Recursive = true,
                    HasTvdbId = false,
                    Person = personName,
                    PersonTypes = new[] { "Director" }
                }).Select(m => m as Movie);
                
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
            }).Select(s => s as Series);
            
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;              // Filter series based on match type
            return matchType switch
            {
                Configuration.MatchType.Title => allSeries.Where(series => 
                    series?.Name != null && series.Name.Contains(matchString, comparison)),
                
                Configuration.MatchType.Genre => allSeries.Where(series => 
                    series?.Genres != null && series.Genres.Any(genre => 
                        genre.Contains(matchString, comparison))),
                
                Configuration.MatchType.Studio => allSeries.Where(series => 
                    series?.Studios != null && series.Studios.Any(studio => 
                        studio.Contains(matchString, comparison))),
                
                Configuration.MatchType.Actor => _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = matchString,
                    PersonTypes = new[] { "Actor" }
                }).Select(s => s as Series),
                
                Configuration.MatchType.Director => _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.Series },
                    IsVirtualItem = false,
                    Recursive = true,
                    Person = matchString,
                    PersonTypes = new[] { "Director" }
                }).Select(s => s as Series),
                
                _ => allSeries.Where(series => 
                    series?.Name != null && series.Name.Contains(matchString, comparison)) // Default to title match
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

        private async Task RemoveUnwantedMediaItems(BoxSet collection, IEnumerable<BaseItem> wantedMediaItems)
        {
            // Get the set of IDs for media items we want to keep
            var wantedItemIds = wantedMediaItems.Select(item => item.Id).ToHashSet();

            // Get current items and filter for unwanted ones
            var childrenToRemove = collection.GetLinkedChildren()
                .Where(item => !wantedItemIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToArray();

            if (childrenToRemove.Length > 0)
            {
                _logger.LogInformation($"Removing {childrenToRemove.Length} items from collection {collection.Name}");
                await _collectionManager.RemoveFromCollectionAsync(collection.Id, childrenToRemove).ConfigureAwait(true);
            }
        }

        private async Task AddWantedMediaItems(BoxSet collection, IEnumerable<BaseItem> wantedMediaItems)
        {
            // Get the set of IDs for items currently in the collection
            var existingItemIds = collection.GetLinkedChildren()
                .Select(item => item.Id)
                .ToHashSet();

            // Create LinkedChild objects for items that aren't already in the collection
            var childrenToAdd = wantedMediaItems
                .Where(item => !existingItemIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToArray();

            if (childrenToAdd.Length > 0)
            {
                _logger.LogInformation($"Adding {childrenToAdd.Length} items to collection {collection.Name}");
                await _collectionManager.AddToCollectionAsync(collection.Id, childrenToAdd).ConfigureAwait(true);
            }
        }

        private BoxSet? GetBoxSetByName(string name)
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                CollapseBoxSetItems = false,
                Recursive = true,
                Tags = new[] { "Autocollection" },
                Name = name,
            }).Select(b => b as BoxSet).FirstOrDefault();
        }        public async Task ExecuteAutoCollectionsNoProgress()
        {
            _logger.LogInformation("Performing ExecuteAutoCollections");
            
            // Get title match pairs from configuration - this is the basic approach
            var titleMatchPairs = Plugin.Instance!.Configuration.TitleMatchPairs;
            _logger.LogInformation($"Starting execution of Auto collections for {titleMatchPairs.Count} title match pairs");

            foreach (var titleMatchPair in titleMatchPairs)
            {
                try
                {
                    _logger.LogInformation($"Processing Auto collection for title match: {titleMatchPair.TitleMatch}");
                    await ExecuteAutoCollectionsForTitleMatchPair(titleMatchPair);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing Auto collection for title match: {titleMatchPair.TitleMatch}");
                    // Continue with next title-match pair even if one fails
                    continue;
                }
            }
            
            // Get expression collections - this is the advanced approach
            var expressionCollections = Plugin.Instance!.Configuration.ExpressionCollections;
            _logger.LogInformation($"Starting execution of Advanced collections for {expressionCollections.Count} expression collections");

            foreach (var expressionCollection in expressionCollections)
            {
                try
                {
                    _logger.LogInformation($"Processing Advanced collection: {expressionCollection.CollectionName}");
                    await ExecuteAutoCollectionsForExpressionCollection(expressionCollection);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing Advanced collection: {expressionCollection.CollectionName}");
                    // Continue with next expression collection even if one fails
                    continue;
                }
            }

            _logger.LogInformation("Completed execution of all Auto collections");
        }

        public async Task ExecuteAutoCollections(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await ExecuteAutoCollectionsNoProgress();
        }

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

        private async Task SetPhotoForCollection(BoxSet collection, Person? specificPerson = null)
        {
            try
            {
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

                // Last fallback: Use an image from a movie/series in the collection
                var mediaItemWithImage = items
                    .Where(item => item is Movie || item is Series)
                    .FirstOrDefault(item =>
                        item.ImageInfos != null &&
                        item.ImageInfos.Any(i => i.Type == ImageType.Primary));

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

        private async Task ExecuteAutoCollectionsForTagTitlePair(TagTitlePair tagTitlePair)
        {
            _logger.LogInformation($"Performing ExecuteAutoCollections for tag: {tagTitlePair.Tag}");
            
            // Get the collection name from the tag-title pair
            var collectionName = GetCollectionName(tagTitlePair);
            
            // Get or create the collection
            var collection = GetBoxSetByName(collectionName);
            bool isNewCollection = false;
            if (collection is null)
            {
                _logger.LogInformation("{Name} not found, creating.", collectionName);
                collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = collectionName,
                    IsLocked = true
                });
                collection.Tags = new[] { "Autocollection" };
                isNewCollection = true;
            }
            collection.DisplayOrder = "Default";

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
                allMovies = GetMoviesFromLibraryWithAndMatching(tags, specificPerson).ToList();
                allSeries = GetSeriesFromLibraryWithAndMatching(tags, specificPerson).ToList();
            }
            else
            {
                // OR matching (default) - items can match any tag
                _logger.LogInformation("Using OR matching mode for tags: {Tags}", string.Join(", ", tags));
                foreach (var tag in tags)
                {
                    var movies = GetMoviesFromLibrary(tag, specificPerson).ToList();
                    var series = GetSeriesFromLibrary(tag, specificPerson).ToList();
                    
                    _logger.LogInformation($"Found {movies.Count} movies and {series.Count} series for tag: {tag}");
                    
                    allMovies.AddRange(movies);
                    allSeries.AddRange(series);
                }
                
                // Remove duplicates
                allMovies = allMovies.Distinct().ToList();
                allSeries = allSeries.Distinct().ToList();
            }
            
            _logger.LogInformation($"Processing {allMovies.Count} movies and {allSeries.Count} series total for collection: {collectionName}");
            
            var mediaItems = allMovies.Cast<BaseItem>().Concat(allSeries.Cast<BaseItem>())
                .ToList();

            await RemoveUnwantedMediaItems(collection, mediaItems);
            await AddWantedMediaItems(collection, mediaItems);
            
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
        }        private async Task ExecuteAutoCollectionsForTitleMatchPair(TitleMatchPair titleMatchPair)
        {            string matchTypeText = titleMatchPair.MatchType switch
            {
                Configuration.MatchType.Title => "title",
                Configuration.MatchType.Genre => "genre",
                Configuration.MatchType.Studio => "studio",
                Configuration.MatchType.Actor => "actor",
                Configuration.MatchType.Director => "director",
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
            var collection = GetBoxSetByName(collectionName);
            bool isNewCollection = false;
            if (collection == null)
            {
                _logger.LogInformation("{Name} not found, creating.", collectionName);
                collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = collectionName,
                    IsLocked = true
                });
                collection.Tags = new[] { "Autocollection" };
                isNewCollection = true;
            }
            collection.DisplayOrder = "Default";
              // Find all media items that match the pattern based on match type
            List<Movie> allMovies = new();
            List<Series> allSeries = new();
            
            // Apply media type filter
            switch (titleMatchPair.MediaType)
            {
                case Configuration.MediaTypeFilter.Movies:
                    // Only include movies
                    allMovies = GetMoviesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    _logger.LogInformation($"Media filter: Movies only - found {allMovies.Count} matching items");
                    break;
                    
                case Configuration.MediaTypeFilter.Series:
                    // Only include TV series
                    allSeries = GetSeriesFromLibraryByMatch(
                        titleMatchPair.TitleMatch, 
                        titleMatchPair.CaseSensitive, 
                        titleMatchPair.MatchType
                    ).ToList();
                    _logger.LogInformation($"Media filter: Series only - found {allSeries.Count} matching items");
                    break;
                    
                case Configuration.MediaTypeFilter.All:
                default:
                    // Include both movies and series (default behavior)
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
                    break;
            }
            
            _logger.LogInformation($"Found {allMovies.Count} movies and {allSeries.Count} series matching {matchTypeText} pattern '{titleMatchPair.TitleMatch}' for collection: {collectionName}");
            
            var mediaItems = allMovies.Cast<BaseItem>().Concat(allSeries.Cast<BaseItem>()).ToList();

            await RemoveUnwantedMediaItems(collection, mediaItems);
            await AddWantedMediaItems(collection, mediaItems);
            
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

        // Helper method to find movies with a specific person type (actor or director) 
        // that match the given string (partial or exact matching)
        private IEnumerable<Movie> GetMoviesWithPerson(string personNameToMatch, string personType, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            // First get all persons of the specified type (actor or director)
            var persons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                Recursive = true
            }).Select(p => p as Person)
                .Where(p => p?.Name != null && p.Name.Contains(personNameToMatch, comparison))
                .ToList();
            
            _logger.LogInformation("Found {Count} {PersonType}s matching '{NameToMatch}'", 
                persons.Count, personType, personNameToMatch);
            
            if (!persons.Any())
            {
                return Enumerable.Empty<Movie>();
            }
            
            // For each matching person, find their movies and combine results
            var result = new HashSet<Movie>();
            foreach (var person in persons)
            {
                if (person?.Name != null)
                {
                    // Use exact name matching for the person's actual name
                    var movies = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Movie },
                        IsVirtualItem = false,
                        Recursive = true,
                        Person = person.Name, // Exact name
                        PersonTypes = new[] { personType }
                    }).Select(m => m as Movie);
                    
                    foreach (var movie in movies)
                    {
                        if (movie != null)
                        {
                            result.Add(movie);
                        }
                    }
                }
            }
            
            return result;
        }
        
        // Helper method to find series with a specific person type (actor or director) 
        // that match the given string (partial or exact matching)
        private IEnumerable<Series> GetSeriesWithPerson(string personNameToMatch, string personType, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
                
            // First get all persons of the specified type (actor or director)
            var persons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                Recursive = true
            }).Select(p => p as Person)
                .Where(p => p?.Name != null && p.Name.Contains(personNameToMatch, comparison))
                .ToList();
            
            _logger.LogInformation("Found {Count} {PersonType}s matching '{NameToMatch}'", 
                persons.Count, personType, personNameToMatch);
            
            if (!persons.Any())
            {
                return Enumerable.Empty<Series>();
            }
            
            // For each matching person, find their series and combine results
            var result = new HashSet<Series>();
            foreach (var person in persons)
            {
                if (person?.Name != null)
                {
                    // Use exact name matching for the person's actual name
                    var series = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Series },
                        IsVirtualItem = false,
                        Recursive = true,
                        Person = person.Name, // Exact name
                        PersonTypes = new[] { personType }
                    }).Select(s => s as Series);
                    
                    foreach (var s in series)
                    {
                        if (s != null)
                        {
                            result.Add(s);
                        }
                    }
                }
            }
            
            return result;
        }        // Method to evaluate a criteria for a movie
        private bool EvaluateMovieCriteria(Movie movie, Configuration.CriteriaType criteriaType, string value, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
                
            switch (criteriaType)
            {
                case Configuration.CriteriaType.Title:
                    return movie.Name?.Contains(value, comparison) == true;
                    
                case Configuration.CriteriaType.Genre:
                    return movie.Genres != null && 
                           movie.Genres.Any(g => g.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Studio:
                    return movie.Studios != null && 
                           movie.Studios.Any(s => s.Contains(value, comparison));
                    
                case Configuration.CriteriaType.Actor:
                    // Find matching actors for this movie using the existing method
                    var matchingActorMovies = GetMoviesWithPerson(value, "Actor", caseSensitive);
                    return matchingActorMovies.Any(m => m.Id == movie.Id);
                    
                case Configuration.CriteriaType.Director:
                    // Find matching directors for this movie using the existing method
                    var matchingDirectorMovies = GetMoviesWithPerson(value, "Director", caseSensitive);
                    return matchingDirectorMovies.Any(m => m.Id == movie.Id);
                    
                case Configuration.CriteriaType.Movie:
                    // Always true for movies
                    return true;
                    
                case Configuration.CriteriaType.Show:
                    // Always false for movies
                    return false;
                    
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
                    // Find all series with matching actor name
                    var actorSeries = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Series },
                        IsVirtualItem = false,
                        Recursive = true,
                        Person = value,
                        PersonTypes = new[] { "Actor" }
                    }).OfType<Series>();
                    
                    return actorSeries.Any(s => s.Id == series.Id);
                    
                case Configuration.CriteriaType.Director:
                    // Find all series with matching director name
                    var directorSeries = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Series },
                        IsVirtualItem = false,
                        Recursive = true,
                        Person = value,
                        PersonTypes = new[] { "Director" }
                    }).OfType<Series>();
                    
                    return directorSeries.Any(s => s.Id == series.Id);
                    
                case Configuration.CriteriaType.Movie:
                    // Always false for series
                    return false;
                    
                case Configuration.CriteriaType.Show:
                    // Always true for series
                    return true;
                    
                default:
                    return false;
            }
        }
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
            var collection = GetBoxSetByName(collectionName);
            bool isNewCollection = false;
            
            if (collection is null)
            {
                _logger.LogInformation("{Name} not found, creating.", collectionName);
                collection = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = collectionName,
                    IsLocked = true
                });
                collection.Tags = new[] { "Autocollection" };
                isNewCollection = true;
            }
            collection.DisplayOrder = "Default";
            
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
            
            // Filter movies and series based on the expression
            var matchingMovies = new List<Movie>();
            var matchingSeries = new List<Series>();
            
            if (expressionCollection.ParsedExpression != null)
            {
                matchingMovies = allMovies
                    .Where(movie => movie != null)
                    .Where(movie => 
                    {
                        return expressionCollection.ParsedExpression.Evaluate(
                            (criteriaType, value) => EvaluateMovieCriteria(movie, criteriaType, value, expressionCollection.CaseSensitive)
                        );
                    })
                    .ToList();
                    
                matchingSeries = allSeries
                    .Where(series => series != null)
                    .Where(series => 
                    {
                        return expressionCollection.ParsedExpression.Evaluate(
                            (criteriaType, value) => EvaluateSeriesCriteria(series, criteriaType, value, expressionCollection.CaseSensitive)
                        );
                    })
                    .ToList();
            }
            
            _logger.LogInformation("Expression matched {MovieCount} movies and {SeriesCount} series", 
                matchingMovies.Count, matchingSeries.Count);
                
            // Combine movies and series
            var allMatchingItems = matchingMovies.Cast<BaseItem>()
                .Concat(matchingSeries.Cast<BaseItem>())
                .ToList();
                
            // Update the collection (add new items, remove items that no longer match)
            await RemoveUnwantedMediaItems(collection, allMatchingItems);
            await AddWantedMediaItems(collection, allMatchingItems);
            
            // Set collection image if it's a new collection
            if (isNewCollection && allMatchingItems.Count > 0)
            {
                await SetPhotoForCollection(collection);
            }
        }
    }
}
