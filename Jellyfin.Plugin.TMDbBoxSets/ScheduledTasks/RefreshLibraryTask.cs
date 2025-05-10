using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbBoxSets.ScheduledTasks;

/// <summary>
/// Class representing a task to refresh library box sets.
/// </summary>
public class RefreshLibraryTask : IScheduledTask, IDisposable
{
    private readonly ILogger<RefreshLibraryTask> _logger;
    private readonly TMDbBoxSetManager _tmDbBoxSetManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshLibraryTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RefreshLibraryTask}"/> interface.</param>
    /// <param name="boxsetLogger">Instance of the <see cref="ILogger{TMDbBoxSetManager}"/> interface.</param>
    public RefreshLibraryTask(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ILogger<RefreshLibraryTask> logger,
        ILogger<TMDbBoxSetManager> boxsetLogger)
    {
        _logger = logger;
        _tmDbBoxSetManager = new TMDbBoxSetManager(libraryManager, collectionManager, boxsetLogger);
    }

    /// <inheritdoc/>
    public string Name => "Scan library for new box sets";

    /// <inheritdoc/>
    public string Key => "TMDbBoxSetsRefreshLibraryTask";

    /// <inheritdoc/>
    public string Description => "Scans all libraries for movies and adds them to box sets if the conditions are met";

    /// <inheritdoc/>
    public string Category => "TMDb";

    /// <inheritdoc/>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting TMDbBoxSets refresh library task");
        await _tmDbBoxSetManager.ScanLibrary(progress).ConfigureAwait(false);
        _logger.LogInformation("TMDbBoxSets refresh library task finished");
    }

    /// <inheritdoc/>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run this task every 24 hours
        return [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            _tmDbBoxSetManager.Dispose();
        }
    }
}
