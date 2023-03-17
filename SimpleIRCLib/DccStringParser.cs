using System;

namespace SimpleIRCLib
{
    public static class DccStringParser
    {
        public static (string fileName, string ip, int portNumber, int fileSize, string botName) ParseDccString(string dccString)
        {
            if (!dccString.Contains(" :DCC"))
            {
                return ParseNonDcc(dccString);
            }
            return ParseDcc(dccString);
        }
        private static (string fileName, string ip, int portNumber, int fileSize, string botName) ParseNonDcc(string dccString)
        {
            var botName = dccString.Split('!')[0];
            var infoSegments = dccString.Split(' ');
            var fileName = infoSegments[5];

            var ip = ParseIp(infoSegments[6]);

            var port = int.Parse(infoSegments[7]);
            var fileSize = int.Parse(infoSegments[8]);
            return (fileName, ip, port, fileSize, botName);

        }
        private static (string fileName, string ip, int portNumber, int fileSize, string botName) ParseDcc(string dccString)
        {
            var botName = dccString.Split(':')[0];
            var (fileName, ip, port, fileSize) = dccString.Contains('"') switch
            {
                true => ParseDCCWithQuote(dccString),
                false => ParseDCCWithoutQuote(dccString)
            };
            return (fileName, ip, port, fileSize, botName);
        }

        private static (string fileName, string ip, int port, int fileSize) ParseDCCWithQuote(string dccString)
        {
            var dccSegments = dccString.Split('"');
            var fileName = dccSegments[1];

            var fileInfo = dccSegments[2].Trim().Split(' ');

            var ip = ParseIp(fileInfo[0]);
            var port = int.Parse(dccSegments[1]);
            var fileSize = int.Parse(dccSegments[2]);

            return (fileName, ip, port, fileSize);
        }
        private static (string fileName, string ip, int port, int fileSize) ParseDCCWithoutQuote(string dccString)
        {
            var infoSegments = dccString.Split(' ');
            var fileName = infoSegments[2];
            var ip = ParseIp(infoSegments[3]);

            var port = int.Parse(infoSegments[4]);
            var fileSize = int.Parse(infoSegments[5]);
            return (fileName, ip, port, fileSize);
        }

        private static string ParseIp(string ipSegment)
        {
            if (ipSegment.Contains(':'))
            {
                return ipSegment;
            }

            if (int.TryParse(ipSegment.Trim(), out var ip))
            {
                return IpHelper.UInt64ToIPAddress(ip);
            }

            throw new Exception($"Couldn't parse IP: ${ipSegment}");
        }
    }
}
