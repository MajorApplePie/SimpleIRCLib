using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SimpleIRCLib
{
    /// <summary>
    /// Class for downloading files using the DCC protocol on a sperarate thread from the main IRC Client thread.
    /// </summary>
    public class DCCClient
    {

        #region Properties
        /// <summary>
        /// Gets or sets the maximum number of concurrent downloads. (Default: 1)
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 1;
        #endregion

        /// <summary>
        /// For firing update event using DCCEventArgs from DCCEventArgs.cs
        /// </summary>
        public event EventHandler<DCCEventArgs> OnDccEvent;
        /// <summary>
        /// For firing debug event using DCCDebugMessageArgs from DCCEventArgs.cs
        /// </summary>
        public event EventHandler<DCCDebugMessageArgs> OnDccDebugMessage;

        private BlockingCollection<Download> _downloadQueue = new BlockingCollection<Download>();
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        public DCCClient()
        {
            // start worker
            Task.Run(() => Worker());
        }

        private async void Worker()
        {
            var activeDownloadTasks = new List<Task>();
            foreach (var download in _downloadQueue.GetConsumingEnumerable()) // take next download in queue
            {
                {
                    if (activeDownloadTasks.Count >= MaxConcurrentDownloads) // wait if max concurrency 
                    {
                        var finishedTask = await Task.WhenAny(activeDownloadTasks);
                        activeDownloadTasks.Remove(finishedTask);
                    }

                    activeDownloadTasks.Add(download.Run()); // start next download
                }
            }

            await Task.WhenAll(activeDownloadTasks);
        }

        /// <summary>
        /// Add a new Download to the queue
        /// </summary>
        /// <param name="ircData">The irc command.</param>
        /// <param name="downloadDirectory">The download directory.</param>
        /// <param name="bot">The bot.</param>
        /// <param name="packageNumber">The package number.</param>
        /// <param name="client">The client.</param>
        /// <returns>The created Download.</returns>
        public Download StartDownload(string ircData, string downloadDirectory, string bot, string packageNumber, IrcClient client)
        {
            var download = new Download(ircData, downloadDirectory, bot, packageNumber, client, _tokenSource.Token);

            download.OnDccDebugMessage += (sender, args) => OnDccDebugMessage?.Invoke(sender, args);
            download.OnDccEvent += (sender, args) => OnDccEvent?.Invoke(sender, args);

            _downloadQueue.TryAdd(download, 0, _tokenSource.Token);

            return download;
        }

        /// <summary>
        /// Stops a download if one is running, checks if the donwnloader thread actually stops.
        /// </summary>
        /// <returns>true if stopped succesfully</returns>
        public bool AbortDownloader(int timeOut)
        {
            _tokenSource.Cancel();
            _downloadQueue.CompleteAdding();
            foreach (var download in _downloadQueue)
            {
                download.Run();
            }
            return true;
        }

        /// <summary>
        /// Checks if download is still running.
        /// </summary>
        /// <returns>true if still downloading</returns>
        public bool CheckIfDownloading()
        {
            return true; // TODO: 
        }

    }
}
