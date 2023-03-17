using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
        public long FileSize { get; }
        /// <summary>
        /// Port of server of file location
        /// </summary>
        public string Ip { get; }
        /// <summary>
        /// Progress from 0-100 (%)
        /// </summary>
        public long Progress { get; private set; }
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
        /// Token to abort the current download.
        /// </summary>
        private readonly CancellationToken _cancellationToken;
        private readonly IrcClient _ircClient;

        public DccDownload(string dccString, DirectoryInfo destination, string pack, CancellationToken cancellationToken, IrcClient ircClient)
        {
            _cancellationToken = cancellationToken;
            PackNum = pack;
            Destination = destination;
            _ircClient = ircClient;

            State = DownloadState.Ready;
            try
            {

                (FileName, Ip, PortNumber, FileSize, BotName) = DccStringParser.ParseDccString(dccString);
            }
            catch
            {
                ChangeState(DownloadState.Error);
            }
        }

        public async Task Start()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                ChangeState(DownloadState.Aborted); return;
            }

            var file = new FileInfo(Path.Combine(Destination.FullName, FileName));

            if (file.Exists)
            {
                ChangeState(DownloadState.Error);
            }

            if (!file.Directory.Exists)
            {
                file.Directory.Create();
            }

            var serverIp = IPAddress.Parse(Ip);

            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(serverIp, PortNumber);
            ChangeState(DownloadState.Running);

            using var fileStream = file.OpenWrite();
            using var binaryWriter = new BinaryWriter(fileStream);
            var buffer = new byte[1048576];
            int bytesRead;
            int totalBytesRead = 0;
            while( (bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None, _cancellationToken)) > 0)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    ChangeState(DownloadState.Aborted);
                    return;
                }

                binaryWriter.Write(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                Progress = totalBytesRead / FileSize / 100;
            }

            ChangeState(DownloadState.Finished);
        }

        private void ChangeState(DownloadState state)
        {
            State = state;
            switch(state)
            {
                case DownloadState.Aborted:  CleanupAbort(); break;
            };
        }

        private void CleanupAbort()
        {
            _ircClient.SendMessageToAll($"/msg ${BotName} xdcc remove ${PackNum}");
            _ircClient.SendMessageToAll($"/msg ${BotName} xdcc cancel ${PackNum}");
        }
    }
}
