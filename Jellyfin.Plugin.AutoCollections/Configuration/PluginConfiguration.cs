using MediaBrowser.Model.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.AutoCollections.Configuration
{
    // Kept for backward compatibility
    public enum TagMatchingMode
    {
        Or = 0,  // Default - match any tag (backward compatible)
        And = 1  // Match all tags
    }

    // Kept for backward compatibility
    public class TagTitlePair
    {
        public string Tag { get; set; }
        public string Title { get; set; }
        public TagMatchingMode MatchingMode { get; set; }

        // Add parameterless constructor for XML serialization
        public TagTitlePair()
        {
            Tag = string.Empty;
            Title = "Auto Collection";
            MatchingMode = TagMatchingMode.Or; // Default to OR for backward compatibility
        }

        public TagTitlePair(string tag, string title = null, TagMatchingMode matchingMode = TagMatchingMode.Or)
        {
            Tag = tag;
            Title = title ?? GetDefaultTitle(tag);
            MatchingMode = matchingMode;
        }

        private static string GetDefaultTitle(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return "Auto Collection";
                
            // If there are multiple tags, use the first one for the default title
            string firstTag = tag.Split(',')[0].Trim();
            return firstTag.Length > 0
                ? char.ToUpper(firstTag[0]) + firstTag[1..] + " Auto Collection"
                : "Auto Collection";
        }
        
        // Helper method to get individual tags as an array
        public string[] GetTagsArray()
        {
            if (string.IsNullOrEmpty(Tag))
                return new string[0];
                
            return Tag.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
        }
    }    // Match types for Auto collections
    public enum MatchType
    {
        Title = 0,   // Default - match by movie/series title
        Genre = 1,   // Match by genre
        Studio = 2,  // Match by studio
        Actor = 3,   // Match by actor
        Director = 4 // Match by director
    }

    // Class for match-based collections (previously title-based only)
    public class TitleMatchPair
    {
        public string TitleMatch { get; set; }
        public string CollectionName { get; set; }
        public bool CaseSensitive { get; set; }
        public MatchType MatchType { get; set; }

        // Add parameterless constructor for XML serialization
        public TitleMatchPair()
        {
            TitleMatch = string.Empty;
            CollectionName = "Auto Collection";
            CaseSensitive = false; // Default to case insensitive
            MatchType = MatchType.Title; // Default to title matching for backward compatibility
        }

        public TitleMatchPair(string titleMatch, string collectionName = null, bool caseSensitive = false, MatchType matchType = MatchType.Title)
        {
            TitleMatch = titleMatch;
            CollectionName = collectionName ?? GetDefaultCollectionName(titleMatch, matchType);
            CaseSensitive = caseSensitive;
            MatchType = matchType;
        }        private static string GetDefaultCollectionName(string matchString, MatchType matchType)
        {
            if (string.IsNullOrEmpty(matchString))
                return "Auto Collection";
                
            return matchType switch
            {
                MatchType.Genre => $"{matchString} Genre",
                MatchType.Studio => $"{matchString} Studio Productions",
                MatchType.Actor => $"{matchString} Acting",
                MatchType.Director => $"{matchString} Directed",
                _ => $"{matchString} Movies" // Default for Title and any future types
            };
        }
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            // Initialize with empty lists - defaults will be added by Plugin.cs only on first run
            TitleMatchPairs = new List<TitleMatchPair>();
            
            // Keep these for backward compatibility but they won't be used
            TagTitlePairs = new List<TagTitlePair>();
            Tags = new string[0];
        }

        public List<TitleMatchPair> TitleMatchPairs { get; set; }
        
        // Keep these for backward compatibility but they won't be used
        [Obsolete("Use TitleMatchPairs instead")]
        public List<TagTitlePair> TagTitlePairs { get; set; }
        
        [Obsolete("Use TitleMatchPairs instead")]
        public string[] Tags { get; set; }
    }
}
