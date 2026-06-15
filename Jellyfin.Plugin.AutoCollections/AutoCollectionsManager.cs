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
        private readonly ICollectionManager _collectionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IUserDataManager? _userDataManager;
        private readonly IUserManager? _userManager;
        private readonly Timer _timer;
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
        // PERSON SEARCH HELPER METHODS
        // ================================================================
        // This section contains helper methods for searching media items
        // based on person associations (actors, directors) and actor roles.
        
        // Initialize person-to-media cache for efficient expression evaluation
        private void InitializePersonCache()
        {
            _personToMoviesCache = new Dictionary<(string, string, bool), HashSet<Guid>>();
            _personToSeriesCache = new Dictionary<(string, string, bool), HashSet<Guid>>();
            _itemPeopleCache = new Dictionary<Guid, List<(string Name, string Type)>>();
        }
        
        // Clear person-to-media cache after expression evaluation is complete
        private void ClearPersonCache()
        {
            _personToMoviesCache = null;
            _personToSeriesCache = null;
            _itemPeopleCache = null;
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

        // Helper method to find movies with a specific person type (actor or director) 
        // that match the given string (partial or exact matching)
        // This method uses Jellyfin's PersonTypes query parameter to ensure only
        // movies where the person has the specified role are returned
        private IEnumerable<Movie> GetMoviesWithPerson(string personNameToMatch, string personType, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            // First get all persons matching the name
            var persons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                Recursive = true
            }).Select(p => p as Person)
                .Where(p => p?.Name != null && p.Name.Contains(personNameToMatch, comparison))
                .ToList();
            
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
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;
                
            // First get all persons matching the name
            var persons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Person },
                Recursive = true
            }).Select(p => p as Person)
                .Where(p => p?.Name != null && p.Name.Contains(personNameToMatch, comparison))
                .ToList();
            
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

        // Helper method to find movies where actors play a specific role (e.g., "Batman")
        // Searches through all actors' roles and matches against the given role name
        private IEnumerable<Movie> GetMoviesWithRole(string roleToMatch, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            _logger.LogDebug("Searching for movies where actors play role matching '{RoleToMatch}'", roleToMatch);

            // Get all movies
            var allMovies = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true
            }).OfType<Movie>().ToList();

            var result = new HashSet<Movie>();

            foreach (var movie in allMovies)
            {
                // Get all people associated with this movie who are actors
                var actors = _libraryManager.GetPeople(movie)
                    .Where(p => p.Type == PersonType.Actor)
                    .ToList();

                // Check if any actor plays the specified role
                foreach (var actor in actors)
                {
                    if (!string.IsNullOrEmpty(actor.Role) && actor.Role.Contains(roleToMatch, comparison))
                    {
                        result.Add(movie);
                        _logger.LogDebug("  + Movie '{Title}' has actor {ActorName} playing role '{Role}'", 
                            movie.Name, actor.Name, actor.Role);
                        break; // No need to check other actors for this movie
                    }
                }
            }

            _logger.LogDebug("Found {Count} movies where actors play role matching '{RoleToMatch}'", 
                result.Count, roleToMatch);

            return result;
        }

        // Helper method to find series where actors play a specific role (e.g., "Batman")
        // Searches through all actors' roles and matches against the given role name
        private IEnumerable<Series> GetSeriesWithRole(string roleToMatch, bool caseSensitive)
        {
            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            _logger.LogDebug("Searching for series where actors play role matching '{RoleToMatch}'", roleToMatch);

            // Get all series
            var allSeries = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                IsVirtualItem = false,
                Recursive = true
            }).OfType<Series>().ToList();

            var result = new HashSet<Series>();

            foreach (var series in allSeries)
            {
                // Get all people associated with this series who are actors
                var actors = _libraryManager.GetPeople(series)
                    .Where(p => p.Type == PersonType.Actor)
                    .ToList();

                // Check if any actor plays the specified role
                foreach (var actor in actors)
                {
                    if (!string.IsNullOrEmpty(actor.Role) && actor.Role.Contains(roleToMatch, comparison))
                    {
                        result.Add(series);
                        _logger.LogDebug("  + Series '{Title}' has actor {ActorName} playing role '{Role}'", 
                            series.Name, actor.Name, actor.Role);
                        break; // No need to check other actors for this series
                    }
                }
            }

            _logger.LogDebug("Found {Count} series where actors play role matching '{RoleToMatch}'", 
                result.Count, roleToMatch);

            return result;
        }

        // Check if a movie has an actor playing a specific role
        private bool MovieHasRole(Guid movieId, string roleToMatch, bool caseSensitive)
        {
            var movie = _libraryManager.GetItemById(movieId) as Movie;
            if (movie == null)
                return false;

            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            var actors = _libraryManager.GetPeople(movie)
                .Where(p => p.Type == PersonType.Actor)
                .ToList();

            return actors.Any(actor => !string.IsNullOrEmpty(actor.Role) && actor.Role.Contains(roleToMatch, comparison));
        }

        // Check if a series has an actor playing a specific role
        private bool SeriesHasRole(Guid seriesId, string roleToMatch, bool caseSensitive)
        {
            var series = _libraryManager.GetItemById(seriesId) as Series;
            if (series == null)
                return false;

            StringComparison comparison = caseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            var actors = _libraryManager.GetPeople(series)
                .Where(p => p.Type == PersonType.Actor)
                .ToList();

            return actors.Any(actor => !string.IsNullOrEmpty(actor.Role) && actor.Role.Contains(roleToMatch, comparison));
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
                    
                case Configuration.CriteriaType.Role:
                    // Check if any actor in this movie plays the specified role
                    return MovieHasRole(movie.Id, value, caseSensitive);
                    
                // Media type criteria
                case Configuration.CriteriaType.Movie:
                    // Always true for movies
                    return true;
                    
                case Configuration.CriteriaType.Show:
                    // Always false for movies
                    return false;
                    
                case Configuration.CriteriaType.Tag:
                    return movie.Tags != null && 
                           movie.Tags.Any(t => t.Contains(value, comparison));
                           
                // Content rating and parental guidance criteria
                case Configuration.CriteriaType.ParentalRating:
                    return !string.IsNullOrEmpty(movie.OfficialRating) && 
                           movie.OfficialRating.Equals(value, comparison);
                           
                case Configuration.CriteriaType.CommunityRating:
                    return CompareNumericValue(movie.CommunityRating, value);
                    
                case Configuration.CriteriaType.CriticsRating:
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
                    return IsItemUnplayed(movie);
                    
                case Configuration.CriteriaType.Watched:
                    // Check if the movie is watched (played by at least one user)
                    return !IsItemUnplayed(movie);
                    
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
                    
                case Configuration.CriteriaType.Role:
                    // Check if any actor in this series plays the specified role
                    return SeriesHasRole(series.Id, value, caseSensitive);
                    
                case Configuration.CriteriaType.Movie:
                    // Always false for series
                    return false;
                    
                case Configuration.CriteriaType.Show:
                    // Always true for series
                    return true;
                
                case Configuration.CriteriaType.Tag:
                    return series.Tags != null && 
                           series.Tags.Any(t => t.Contains(value, comparison));
                           
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
                            return true;
                        }
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
                    return IsItemUnplayed(series);
                    
                case Configuration.CriteriaType.Watched:
                    // Check if the series is watched (played by at least one user)
                    return !IsItemUnplayed(series);
                    
                default:
                    return false;
            }
        }

        // ================================================================
        // UTILITY METHODS
        // ================================================================
        // This section contains utility and helper methods for various
        // common operations like comparisons and data processing.
        
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
        
        // Helper method to check if an item is unplayed (not watched by any user)
        private bool IsItemUnplayed(BaseItem item)
        {
            try
            {
                // If user data manager or user manager is not available, log warning and assume item is unplayed
                if (_userDataManager == null || _userManager == null)
                {
                    _logger.LogWarning("UserDataManager or UserManager not available for item {ItemName}, assuming item is unplayed", item.Name);
                    return true;
                }

                // Get all users and check if ANY of them have played this item
                var users = _userManager.Users.ToList();
                
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
                // If we can't determine the play state, assume it's unplayed (safer default)
                return true;
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

        // Placeholder methods for collection management (simplified)
        public async Task ExecuteAutoCollectionsNoProgress()
        {
            var dummyProgress = new Progress<double>();
            await ExecuteAutoCollections(dummyProgress, CancellationToken.None);
        }

        public async Task ExecuteAutoCollections(IProgress<double> progress, CancellationToken cancellationToken)
        {
            progress.Report(100);
            await Task.CompletedTask;
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
