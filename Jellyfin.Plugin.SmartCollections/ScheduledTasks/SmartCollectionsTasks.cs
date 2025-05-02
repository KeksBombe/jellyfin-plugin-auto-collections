using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.SmartCollections.ScheduledTasks
{
    public class ExecuteSmartCollectionsTask : IScheduledTask
    {
        private readonly ILogger<SmartCollectionsManager> _logger;
        private readonly SmartCollectionsManager _syncSmartCollectionsManager;

        public ExecuteSmartCollectionsTask(IProviderManager providerManager, ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger<SmartCollectionsManager> logger, IApplicationPaths applicationPaths)
        {
            _logger = logger;
            _syncSmartCollectionsManager = new SmartCollectionsManager(providerManager, collectionManager, libraryManager, logger, applicationPaths);
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _syncSmartCollectionsManager.ExecuteSmartCollections(progress, cancellationToken);


        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            };
        }        public string Name => "Smart Collections";
        public string Key => "SmartCollections";
        public string Description => "Enables creation of Smart Collections based on Title, Studio, or Genre";
        public string Category => "Smart Collections";
    }
}
