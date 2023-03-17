using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleIRCLib
{
    public class DccDownload
    {
        /// <summary>
        /// File name of the file being downloaded
        /// </summary>
        public string FileName { get; }
        /// <summary>
        /// Pack ID of file on bot where file resides
        /// </summary>
        public int PortNumber { get; }
        /// <summary>
        /// FileSize of the file being downloaded
        /// </summary>
        public Int64 FileSize { get; }
        /// <summary>
        /// Port of server of file location
        /// </summary>
        public string Ip { get; }
        /// <summary>
        /// Progress from 0-100 (%)
        /// </summary>
        public int Progress { get; private set; }
        /// <summary>
        /// Download state.
        /// </summary>
        public DownloadState State { get; private set; }
        /// <summary>
        /// Download speed in: KB/s
        /// </summary>
        public DownloadSpeed DownloadSpeed { get; private set; }

        /// <summary>
        /// Bot name where file resides
        /// </summary>
        public string BotName { get; }
        /// <summary>
        /// Pack ID of file on bot where file resides
        /// </summary>
        public string PackNum { get; }
        /// <summary>
        /// Path to the file that is being downloaded, or has been downloaded
        /// </summary>
        public DirectoryInfo Destination { get; }

        /// <summary>
        /// To provide the latest data downloaded to outside the library
        /// </summary>
        public List<byte> Buffer { get; set; } // TODO: probably don't need this as a prop or at all

        /// <summary>
        /// Token to abort the current download.
        /// </summary>
        private CancellationToken _cancellationToken;

        public DccDownload(string dccString, DirectoryInfo destination, string botName, string pack, CancellationToken cancellationToken) : this(dccString, destination, botName, pack)
        {
            _cancellationToken = cancellationToken;
        }
        public DccDownload(string dccString, DirectoryInfo destination, string pack)
        {
            PackNum = pack;
            Destination = destination;
            State = DownloadState.Ready;
            (FileName, Ip, PortNumber, FileSize, BotName) = DccStringParser.ParseDccString(dccString);
        }

        public async Task Start()
        {
            if (_cancellationToken.IsCancellationRequested) return;

            State = DownloadState.Running;
        }
    }
}
