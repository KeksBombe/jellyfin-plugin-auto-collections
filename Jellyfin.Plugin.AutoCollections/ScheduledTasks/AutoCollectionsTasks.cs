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

namespace Jellyfin.Plugin.AutoCollections.ScheduledTasks
{
    public class ExecuteAutoCollectionsTask : IScheduledTask
    {
        private readonly ILogger<AutoCollectionsManager> _logger;
        private readonly AutoCollectionsManager _syncAutoCollectionsManager;

        public ExecuteAutoCollectionsTask(IProviderManager providerManager, ICollectionManager collectionManager, ILibraryManager libraryManager, ILogger<AutoCollectionsManager> logger, IApplicationPaths applicationPaths)
        {
            _logger = logger;
            _syncAutoCollectionsManager = new AutoCollectionsManager(providerManager, collectionManager, libraryManager, logger, applicationPaths);
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _syncAutoCollectionsManager.ExecuteAutoCollections(progress, cancellationToken);


        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            };
        }        public string Name => "Auto Collections";
        public string Key => "AutoCollections";
        public string Description => "Enables creation of Auto Collections based on simple criteria or advanced boolean expressions";
        public string Category => "Auto Collections";
    }
}
