using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleIRCLib
{
    public class Download : INotifyPropertyChanged
    {

        #region Events

        /// <summary>
        /// For firing debug event using DCCDebugMessageArgs from DCCEventArgs.cs
        /// </summary>
        public event EventHandler<DCCDebugMessageArgs> OnDccDebugMessage;

        /// <summary>
        /// For firing dcc events using DCCEventArgs.
        /// </summary>
        public event EventHandler<DCCEventArgs> OnDccEvent;


        #endregion

        #region Properties


        /// <summary>
        /// File size of the File being downloaded.
        /// </summary>
        public Int64 FileSize { get; private set; }
        /// <summary>
        /// Name of the File being downloaded.
        /// </summary>
        public string FileName { get; private set; }
        /// <summary>
        /// DCC string used for getting file location and basic file information
        /// </summary>
        public string DccString { get; private set; }
        public int Port { get; private set; }
        public string Ip { get; private set; }
        public int Progress
        {
            get => _progress;
            set
            {
                if(value != _progress)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }
        public Status Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    
        public Int64 BytesPerSecond
        {
            get => _bytesPerSecond;
            set
            {
                _bytesPerSecond = value;
                OnPropertyChanged(nameof(BytesPerSecond));
                OnPropertyChanged(nameof(KBytesPerSecond));
                OnPropertyChanged(nameof(MBytesPerSecond));
            }
        }
        public Int64 KBytesPerSecond => BytesPerSecond / 1024;
        public Int64 MBytesPerSecond => BytesPerSecond / 1024 / 1024;
        public string BotName { get; private set; }
        public string PackageNumber { get; private set; }
        public string CurrentFilePath { get; private set; }
        public string DownloadDirectory { get; private set; }

        public bool IsValid { get; private set; } = true;

        private int _progress = 0;
        private Status _status;
        private Int64 _bytesPerSecond;
        
        #endregion

        private readonly CancellationToken _cancellationToken;
        private readonly IrcClient _client;




        /// <param name="dccString">DCC string received from bot</param>
        /// <param name="downloadDirectory">The download directory.</param>
        /// <param name="bot">The bot.</param>
        /// <param name="package">The package number to download</param>
        /// <param name="client">Connected IrcClient</param>
        /// <param name="token">Cancellation token</param>
        public Download(string dccString, string downloadDirectory, string bot, string package, IrcClient client, CancellationToken token)
        {
            _cancellationToken = token;

            if (string.IsNullOrEmpty(dccString))
                throw new ArgumentNullException(nameof(dccString));
            if (string.IsNullOrEmpty(downloadDirectory))
                throw new ArgumentNullException(nameof(downloadDirectory));
            if (string.IsNullOrEmpty(bot))
                throw new ArgumentNullException(nameof(bot));
            if (string.IsNullOrEmpty(package))
                throw new ArgumentNullException(nameof(package));


            DccString = dccString;
            DownloadDirectory = downloadDirectory;
            BotName = bot;
            PackageNumber = package;
            _client = client;



            if (!dccString.Contains("SEND")) // Fail, no SEND in dcc string
            {
                Status = Status.Failed;
                IsValid = false;

                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs(
                        "DCC String does not contain SEND and/or invalid values for parsing! Ignore SEND request\n",
                        "DCC STARTER"));
            }
            else
            {
                // parse data for download
                Status = Status.Parsing;

                if (!ParseData(dccString)) // Invalid dcc string
                {
                    Status = Status.Failed;
                    IsValid = false;
                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs(
                            "Can't parse dcc string and start downloader, failed to parse data, removing from que\n",
                            "DCC STARTER"));
                    _client.SendMessageToAll("/msg " + BotName + " xdcc remove " + PackageNumber); // remove from queue
                    _client.SendMessageToAll("/msg " + BotName + " xdcc cancel"); // cancel download

                }
            }
        }


        /// <summary>
        /// Start downloading the file
        /// </summary>
        public async Task Run()
        {
            if (!IsValid) // Only continue if parsing was successful 
            {
                OnDccDebugMessage?.Invoke(this, new DCCDebugMessageArgs("Can't download. Invalid Command.", "DCC Run"));
                return;
            }

            Status = Status.Waiting;

            var directory = new DirectoryInfo(DownloadDirectory);
            if (!directory.Exists)
                directory.Create();

            var fileInfo = new FileInfo(Path.Combine(directory.FullName, FileName));

            if (fileInfo.Exists) // quit if File exists
            {
                OnDccDebugMessage?.Invoke(this, new DCCDebugMessageArgs("File already Exists, removing from queue.", "DCC Downloader"));
                _client.SendMessageToAll("/msg " + BotName + " xdcc remove " + PackageNumber); // remove from queue
                _client.SendMessageToAll("/msg " + BotName + " xdcc cancel"); // cancel download
                Status = Status.Failed;
                return;
            }

            // Abort if cancellation requested
            if (_cancellationToken.IsCancellationRequested)
            {
                Status = Status.Aborted;
                return;
            }

            try
            {
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs("File does not exist yet, start connection \n ", "DCC DOWNLOADER"));

                // start connection
                using (var tcpClient = new TcpClient(Ip, Port))
                {
                    using (var stream = tcpClient.GetStream())
                    {
                        Status = Status.Downloading;

                        long bytesRecieved = 0;
                        long previousBytesRecieved = 0;
                        long onePercent = FileSize / 100;
                        DateTime startedAt = DateTime.Now; ;

                        // create buffer
                        byte[] buffer;
                        if (FileSize > 1048576)
                        {
                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("Big file, big buffer (1 mb) \n ", "DCC DOWNLOADER"));
                            buffer = new byte[16384];
                        }
                        else if (FileSize < 1048576 && FileSize > 2048)
                        {
                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("Smaller file (< 1 mb), smaller buffer (2 kb) \n ",
                                    "DCC DOWNLOADER"));
                            buffer = new byte[2048];
                        }
                        else if (FileSize < 2048 && FileSize > 128)
                        {
                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("Small file (< 2kb mb), small buffer (128 b) \n ",
                                    "DCC DOWNLOADER"));
                            buffer = new byte[128];
                        }
                        else
                        {
                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("Tiny file (< 128 b), tiny buffer (2 b) \n ",
                                    "DCC DOWNLOADER"));
                            buffer = new byte[2];
                        }

                        // create output file
                        using (var writeStream = new FileStream(fileInfo.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                        {
                            writeStream.SetLength(FileSize);

                            while (tcpClient.Connected && bytesRecieved < FileSize)
                            {
                                // Abort if cancellation requested
                                if (_cancellationToken.IsCancellationRequested)
                                {
                                    Status = Status.Aborted;
                                    fileInfo.Delete();
                                    return;
                                }

                                DateTime now = DateTime.Now; // update speed 
                                if (now.Second != startedAt.Second)
                                {
                                    BytesPerSecond = bytesRecieved - previousBytesRecieved;
                                    previousBytesRecieved = bytesRecieved;
                                    startedAt = now;
                                }

                                var count = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken); // read stream

                                await writeStream.WriteAsync(buffer, 0, count, _cancellationToken); // write to outfile

                                bytesRecieved += count;

                                Progress = (int)(bytesRecieved / onePercent);

                                OnDccEvent?.Invoke(this, new DCCEventArgs(this));
                            }

                            if (bytesRecieved == FileSize) // finished with most of the file done
                            {
                                Status = Status.Completed;
                            }
                            else // connection failed
                            {
                                Status = Status.Failed;
                                fileInfo.Delete();
                            }
                        }

                    }
                }

            }
            catch (SocketException e)
            {
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs(
                        "Something went wrong while downloading the file! Removing from xdcc que to be sure! Error:\n" +
                        e, "DCC DOWNLOADER"));
                _client.SendMessageToAll("/msg " + BotName + " xdcc remove " + PackageNumber);
                _client.SendMessageToAll("/msg " + BotName + " xdcc cancel");
                Status = Status.Failed;
            }
            catch (Exception e)
            {
                OnDccDebugMessage?.Invoke(this,
                    new DCCDebugMessageArgs(
                        "Something went wrong while downloading the file! Removing from xdcc que to be sure! Error:\n" +
                        e, "DCC DOWNLOADER"));
                _client.SendMessageToAll("/msg " + BotName + " xdcc remove " + PackageNumber);
                _client.SendMessageToAll("/msg " + BotName + " xdcc cancel");
                Status = Status.Failed;
            }

        }


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
        /// Converts a long/int64 to a ip string.
        /// </summary>
        /// <param name="address">int64 numbers representing IP address</param>
        /// <returns>string with ip</returns>
        private string UInt64ToIPAddress(Int64 address)
        {
            string ip = string.Empty;
            for (int i = 0; i < 4; i++)
            {
                int num = (int)(address / Math.Pow(256, (3 - i)));
                address = address - (long)(num * Math.Pow(256, (3 - i)));
                if (i == 0)
                {
                    ip = num.ToString();
                }
                else
                {
                    ip = ip + "." + num.ToString();
                }
            }
            return ip;
        }

        /// <summary>
        /// Parses the received DCC string
        /// </summary>
        /// <param name="dccString">dcc string</param>
        /// <returns>returns true if parsing was succesfull, false if failed</returns>
        private bool ParseData(string dccString)
        {
            /*
           * :_bot PRIVMSG nickname :DCC SEND \"filename\" ip_networkbyteorder port filesize
           *AnimeDispenser!~desktop@Rizon-6AA4F43F.ip-37-187-118.eu PRIVMSG WeebIRCDev :DCC SEND "[LNS] Death Parade - 02 [BD 720p] [7287AE5C].mkv" 633042523 59538 258271780  
           *HelloKitty!~nyaa@ny.aa.ny.aa PRIVMSG WeebIRCDev :DCC SEND [Coalgirls]_Spirited_Away_(1280x692_Blu-ray_FLAC)_[5805EE6B].mkv 3281692293 35567 10393049211
           :[EWG]-bOnez!EWG@CRiTEN-BB8A59E9.ip-158-69-126.net PRIVMSG LittleWeeb_jtokck :DCC SEND The.Good.Doctor.S01E13.Seven.Reasons.1080p.AMZN.WEB-DL.DD+5.1.H.264-QOQ.mkv 2655354388 55000 1821620363
           *Ginpa2:DCC SEND "[HorribleSubs] Dies Irae - 05 [480p].mkv" 84036312 35016 153772128 
             */

            dccString = RemoveSpecialCharacters(dccString).Substring(1);
            OnDccDebugMessage?.Invoke(this,
                new DCCDebugMessageArgs("DCCClient: DCC STRING: " + dccString, "DCC PARSER"));


            if (!dccString.Contains(" :DCC"))
            {
                BotName = dccString.Split(':')[0];
                if (dccString.Contains("\""))
                {
                    FileName = dccString.Split('"')[1];

                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient1: FILENAME PARSED: " + FileName, "DCC PARSER"));
                    string[] thaimportantnumbers = dccString.Split('"')[2].Trim().Split(' ');
                    if (thaimportantnumbers[0].Contains(":"))
                    {
                        Ip = thaimportantnumbers[0];
                    }
                    else
                    {
                        try
                        {

                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("DCCClient1: PARSING FOLLOWING IPBYTES: " + thaimportantnumbers[0], "DCC PARSER"));
                            string ipAddress = UInt64ToIPAddress(Int64.Parse(thaimportantnumbers[0]));
                            Ip = ipAddress;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient1: IP PARSED: " + Ip, "DCC PARSER"));
                    Port = int.Parse(thaimportantnumbers[1]);
                    FileSize = Int64.Parse(thaimportantnumbers[2]);

                    return true;
                }
                else
                {
                    FileName = dccString.Split(' ')[2];


                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient2: FILENAME PARSED: " + FileName, "DCC PARSER"));

                    if (dccString.Split(' ')[3].Contains(":"))
                    {
                        Ip = dccString.Split(' ')[3];
                    }
                    else
                    {
                        try
                        {


                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("DCCClient2: PARSING FOLLOWING IPBYTES DIRECTLY: " + dccString.Split(' ')[3], "DCC PARSER"));
                            string ipAddress = UInt64ToIPAddress(Int64.Parse(dccString.Split(' ')[3]));
                            Ip = ipAddress;
                        }
                        catch
                        {

                            return false;
                        }
                    }
                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient2: IP PARSED: " + Ip, "DCC PARSER"));
                    Port = int.Parse(dccString.Split(' ')[4]);
                    FileSize = Int64.Parse(dccString.Split(' ')[5]);
                    return true;
                }
            }
            else
            {
                BotName = dccString.Split('!')[0];
                if (dccString.Contains("\""))
                {
                    FileName = dccString.Split('"')[1];

                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient3: FILENAME PARSED: " + FileName, "DCC PARSER"));
                    string[] thaimportantnumbers = dccString.Split('"')[2].Trim().Split(' ');

                    if (thaimportantnumbers[0].Contains(":"))
                    {
                        Ip = thaimportantnumbers[0];
                    }
                    else
                    {
                        try
                        {

                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("DCCClient3: PARSING FOLLOWING IPBYTES DIRECTLY: " + thaimportantnumbers[0], "DCC PARSER"));
                            string ipAddress = UInt64ToIPAddress(Int64.Parse(thaimportantnumbers[0]));
                            Ip = ipAddress;
                        }
                        catch
                        {
                            return false;
                        }
                    }


                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient3: IP PARSED: " + Ip, "DCC PARSER"));
                    Port = int.Parse(thaimportantnumbers[1]);
                    FileSize = Int64.Parse(thaimportantnumbers[2]);
                    return true;
                }
                else
                {
                    FileName = dccString.Split(' ')[5];

                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient4: FILENAME PARSED: " + FileName, "DCC PARSER"));

                    if (dccString.Split(' ')[6].Contains(":"))
                    {
                        Ip = dccString.Split(' ')[6];
                    }
                    else
                    {
                        try
                        {

                            OnDccDebugMessage?.Invoke(this,
                                new DCCDebugMessageArgs("DCCClient4: PARSING FOLLOWING IPBYTES DIRECTLY: " + dccString.Split(' ')[6], "DCC PARSER"));
                            string ipAddress = UInt64ToIPAddress(Int64.Parse(dccString.Split(' ')[6]));
                            Ip = ipAddress;
                        }
                        catch
                        {
                            return false;
                        }

                    }

                    OnDccDebugMessage?.Invoke(this,
                        new DCCDebugMessageArgs("DCCClient4: IP PARSED: " + Ip, "DCC PARSER"));
                    Port = int.Parse(dccString.Split(' ')[7]);
                    FileSize = Int64.Parse(dccString.Split(' ')[8]);
                    return true;

                }


            }

        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public enum Status
    {
        Waiting,
        Downloading,
        Failed,
        Aborted,
        Parsing,
        Completed
    }
}
