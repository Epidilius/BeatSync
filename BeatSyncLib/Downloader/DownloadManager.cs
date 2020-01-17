﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BeatSyncLib.Downloader
{
    public class DownloadManager
    {
        private BlockingCollection<IDownloadJob> _queuedJobs = new BlockingCollection<IDownloadJob>();

        private ConcurrentDictionary<string, IDownloadJob> _existingJobs = new ConcurrentDictionary<string, IDownloadJob>();
        private ConcurrentDictionary<string, IDownloadJob> _activeJobs = new ConcurrentDictionary<string, IDownloadJob>();
        private ConcurrentDictionary<string, IDownloadJob> _completedDownloads = new ConcurrentDictionary<string, IDownloadJob>();
        private ConcurrentDictionary<string, IDownloadJob> _failedDownloads = new ConcurrentDictionary<string, IDownloadJob>();
        private ConcurrentDictionary<string, IDownloadJob> _cancelledDownloads = new ConcurrentDictionary<string, IDownloadJob>();
        public IReadOnlyList<IDownloadJob> CompletedJobs
        {
            get { return _completedDownloads.Values.ToList(); }
        }
        public bool Paused { get; protected set; }
        private bool _acceptingJobs = false;
        private bool _running = false;
        private int _concurrentDownloads = 1;
        private CancellationToken _externalCancellation;
        private CancellationTokenSource _cancellationSource;
        private Task[] _tasks;

        public void Pause()
        {
            Paused = true;
            foreach (var job in _activeJobs.Values.Where(j => j.CanPause).ToArray())
            {
                job.Pause();
            }
        }

        public void Unpause()
        {
            Paused = false;
            foreach (var job in _activeJobs.Values.Where(j => j.CanPause).ToArray())
            {
                job.Unpause();
            }
        }

        public int ConcurrentDownloads
        {
            get { return _concurrentDownloads; }
            private set
            {
                if (value < 1)
                    value = 1;
                if (value == _concurrentDownloads)
                    return;
                _concurrentDownloads = value;
            }
        }

        public DownloadManager(int concurrentDownloads)
        {
            ConcurrentDownloads = concurrentDownloads;
            _acceptingJobs = true;
            _tasks = new Task[ConcurrentDownloads];
        }

        public void Start(CancellationToken cancellationToken)
        {
            _externalCancellation = cancellationToken;
            if (_running)
                return;
            _running = true;
            if (_cancellationSource == null || _cancellationSource.IsCancellationRequested)
                _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_externalCancellation);
            for (int i = 0; i < ConcurrentDownloads; i++)
            {
                int taskId = i; // Apparently using 'i' directly for HandlerStartAsync doesn't work well...
                _tasks[i] = Task.Run(() => HandlerStartAsync(_cancellationSource.Token, taskId));
            }
        }

        public void Stop()
        {
            //_queuedJobs.CompleteAdding();
            _running = false;
            _cancellationSource.Cancel();
            _cancellationSource.Dispose();
            _cancellationSource = null;
        }

        public Task StopAsync()
        {
            Stop();
            return Task.WhenAll(_tasks);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Complete()
        {
            // TODO: _running isn't set to false with this.
            _acceptingJobs = false;
            _queuedJobs.CompleteAdding();
        }

        /// <summary>
        /// Completes the DownloadManager and waits for all remaining jobs to finish.
        /// </summary>
        /// <returns></returns>
        public async Task CompleteAsync()
        {
            Complete();
            //if (cancellationToken.IsCancellationRequested)
            //{
            //    _running = false;
            //    return;
            //}
            try
            {
                if(_running)
                    await SongFeedReaders.Utilities.WaitUntil(() => _queuedJobs.Count == 0);
                //if (cancellationToken.CanBeCanceled)
                //{
                //    var cancelTask = cancellationToken.AsTask();
                //    await Task.WhenAny(Task.WhenAll(_tasks), cancelTask).ConfigureAwait(false);
                //}
                //else
                //{
                    await Task.WhenAll(_tasks).ConfigureAwait(false);
                //}
            }
            catch (Exception) { }
            _running = false;
        }

        /// <summary>
        /// Tries to post a new job to the queue. If the song was already downloaded, returns the existing DownloadJob. 
        /// Returns null if the job failed to post and the song wasn't previously downloaded.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public bool TryPostJob(IDownloadJob job, out IDownloadJob postedOrExistingJob)
        {
            if (_acceptingJobs && _existingJobs.TryAdd(job.SongHash, job) && _queuedJobs.TryAdd(job))
            {
                    job.JobFinished += Job_OnJobFinished;
                    postedOrExistingJob = job;
                    return true;
            }
            else if (_existingJobs.TryGetValue(job.SongHash, out var existingJob))
            {
                postedOrExistingJob = existingJob;
                return false;
            }
            else
            {
                postedOrExistingJob = null;
                return false;
            }
        }

        private void Job_OnJobFinished(object sender, DownloadJobFinishedEventArgs e)
        {
            IDownloadJob finishedJob = (IDownloadJob)sender;
            if(!_activeJobs.TryRemove(e.SongHash, out _))
            {
                Logger.log?.Warn($"Couldn't remove {finishedJob.ToString()} from _activeJobs, this shouldn't happen.");
            }
            switch (e.DownloadResult)
            {
                case Utilities.DownloadResultStatus.Success:
                    _completedDownloads.TryAdd(e.SongHash, (IDownloadJob)sender);
                    break;
                case Utilities.DownloadResultStatus.Canceled:
                    _cancelledDownloads.TryAdd(e.SongHash, (IDownloadJob)sender);
                    break;
                default:
                    _failedDownloads.TryAdd(e.SongHash, (IDownloadJob)sender);
                    break;
            }
        }

        private async Task HandlerStartAsync(CancellationToken cancellationToken, int taskId)
        {
            try
            {
                foreach (var job in _queuedJobs.GetConsumingEnumerable(cancellationToken))
                {
                    if(!_activeJobs.TryAdd(job.SongHash, job))
                    {
                        Logger.log?.Warn($"Couldn't add {job.ToString()} to _activeJobs, this shouldn't happen.");
                    }
                    if (Paused && job.CanPause)
                        job.Pause();
                    await job.RunAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException)
            {
                Logger.log?.Warn($"DownloadManager task {taskId} canceled.");
            }
            catch (Exception ex)
            {
                Logger.log?.Warn($"Exception in DownloadManager task {taskId}:{ex.Message}");
                Logger.log?.Debug($"Exception in DownloadManager task {taskId}:{ex.StackTrace}");
            }
        }

        public bool TryGetJob(string songHash, out IDownloadJob job)
        {
            return _existingJobs.TryGetValue(songHash, out job);
        }



    }
}
