#nullable enable
using System.Collections.Generic;

namespace Jellyfin.Plugin.AutoCollections.Configuration
{
    /// <summary>
    /// One item in a preview result.
    /// </summary>
    public class PreviewItem
    {
        public string Name { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Type { get; set; } = "Item";
    }

    /// <summary>
    /// What a sync would do to one collection, without doing it.
    /// </summary>
    /// <remarks>
    /// Reported as a difference against the collection's current contents rather than a plain
    /// list of matches: a sync also removes items that stopped matching, and that is the part
    /// worth seeing before it happens.
    /// </remarks>
    public class CollectionPreview
    {
        public string CollectionName { get; set; } = string.Empty;

        /// <summary>False when the collection would be created by this sync.</summary>
        public bool CollectionExists { get; set; }

        /// <summary>How many items the collection would hold afterwards.</summary>
        public int MatchedCount { get; set; }

        /// <summary>How many items it holds right now.</summary>
        public int CurrentCount { get; set; }

        /// <summary>Items already in the collection that would stay.</summary>
        public int UnchangedCount { get; set; }

        public List<PreviewItem> Added { get; set; } = new();
        public List<PreviewItem> Removed { get; set; } = new();

        public int AddedCount { get; set; }
        public int RemovedCount { get; set; }

        /// <summary>Set when the lists were capped, so the UI can say "and N more".</summary>
        public bool AddedTruncated { get; set; }
        public bool RemovedTruncated { get; set; }

        /// <summary>Things that look like mistakes but are not errors.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Expression parse failures.</summary>
        public List<string> Errors { get; set; } = new();
    }
}
