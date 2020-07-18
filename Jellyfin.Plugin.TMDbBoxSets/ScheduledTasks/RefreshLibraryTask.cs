using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.ScheduledTasks
{
    public class RefreshLibraryTask : IScheduledTask
    {
        private readonly ILogger<RefreshLibraryTask> _logger;
        private readonly TMDbBoxSetManager _tmDbBoxSetManager;

        public RefreshLibraryTask(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger<RefreshLibraryTask> logger, ILogger<TMDbBoxSetManager> boxset_logger)
        {
            _logger = logger;
            _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, boxset_logger);
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting TMDbBoxSets refresh library task");
            _tmDbBoxSetManager.ScanLibrary(progress);
            _logger.LogInformation("TMDbBoxSets refresh library task finished");
            return Task.CompletedTask;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            return new[] {
                new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(24).Ticks}
            };
        }

        public string Name => "Scan library for new box sets";
        public string Key => "TMDbBoxSetsRefreshLibraryTask";
        public string Description => "Scans all libraries for movies and adds them to box sets if the conditions are met";
        public string Category => "TMDb";
    }
}
