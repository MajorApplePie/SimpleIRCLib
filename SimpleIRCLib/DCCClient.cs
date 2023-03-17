using System;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleIRCLib
{
    /// <summary>
    /// Class for downloading files using the DCC protocol on a sperarate thread from the main IRC Client thread.
    /// </summary>
    public class DCCClient
    {

        /// <summary>
        /// For firing update event using DCCEventArgs from DCCEventArgs.cs
        /// </summary>
        public event EventHandler<DCCEventArgs> OnDccEvent;
        /// <summary>
        /// For firing debug event using DCCDebugMessageArgs from DCCEventArgs.cs
        /// </summary>
        public event EventHandler<DCCDebugMessageArgs> OnDccDebugMessage;

        public int MaxConcurrentDownloads { get; }

        /// <summary>
        /// List of all Downloads, running and finished.
        /// </summary>
        public IReadOnlyList<DccDownload> Downloads { get => _downloads.ToList(); }
        private readonly ConcurrentBag<DccDownload> _downloads = new ConcurrentBag<DccDownload>();
        private readonly ConcurrentQueue<DccDownload> _activeDownloads = new ConcurrentQueue<DccDownload>();


        public DCCClient(int maxConcurrentDownloads = 3)
        {
            MaxConcurrentDownloads = maxConcurrentDownloads;
        }

        public void AddDownload(DccDownload download)
        {
            _downloads.Add(download);
            _activeDownloads.Enqueue(download);
        }

        private Task StartDownloadTask()
        {
            while(_activeDownloads.TryDequeue(out var download))
            {

            }
        }

        /// <summary>
        /// Starts a downloader by parsing the received message from the irc server on information
        /// </summary>
        /// <param name="dccString">message from irc server</param>
        /// <param name="downloaddir">download directory</param>
        /// <param name="bot">bot where the file came from</param>
        /// <param name="pack">pack on bot where the file came from</param>
        /// <param name="client">irc client used the moment it received the dcc message, used for sending abort messages when download fails unexpectedly</param>
        public void StartDownloader(string dccString, string downloaddir, string bot, string pack, IrcClient client)
        {
            if ((dccString ?? downloaddir ?? bot ?? pack) != null && dccString.Contains("SEND") && !IsDownloading)
            {
                NewDccString = dccString;
                _curDownloadDir = downloaddir;
                BotName = bot;
                PackNum = pack;
                _ircClient = client;

                //parsing the data for downloader thread

                UpdateStatus("PARSING");
                bool isParsed = ParseData(dccString);

                //try to set the necesary information for the downloader
                if (isParsed)
                {
                    _shouldAbort = false;
                    //start the downloader thread
                    _downloader = new Thread(new ThreadStart(this.Downloader));
                    _downloader.IsBackground = true;
                    _downloader.Start();
                }
                else
                {
                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs(
                            "Can't parse dcc string and start downloader, failed to parse data, removing from que\n", "DCC STARTER"));
                    _ircClient.SendMessageToAll("/msg " + BotName + " xdcc remove " + PackNum);
                    _ircClient.SendMessageToAll("/msg " + BotName + " xdcc cancel");
                }
            }
            else
            {

                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("DCC String does not contain SEND and/or invalid values for parsing! Ignore SEND request\n", "DCC STARTER"));

            }
        }
        public string CurrentFilePath { get; set; }


        /// <summary>
        /// Removes special characters from  a string (used for filenames)
        /// </summary>
        /// <param name="str">string to parse</param>
        /// <returns>string wihtout special chars</returns>
        private string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (c > 31 && c < 219)
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reverses IP from little endian to big endian or vice versa depending on what succeeds.
        /// </summary>
        /// <param name="ip">ip string</param>
        /// <returns>reversed ip string</returns>
        private string ReverseIp(string ip)
        {
            string[] parts = ip.Trim().Split('.');
            if (parts.Length >= 3)
            {
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("DCCClient: converting ip: " + ip, "DCC IP PARSER"));
                string NewIP = parts[3] + "." + parts[2] + "." + parts[1] + "." + parts[0];
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("DCCClient: to: " + NewIP, "DCC IP PARSER"));

                return NewIP;
            }
            else
            {
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("DCCClient: converting ip: " + ip, "DCC IP PARSER"));
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("DCCClient: amount of parts: " + parts.Length, "DCC IP PARSER"));
                return "0.0.0.0";
            }
        }
    }
}
