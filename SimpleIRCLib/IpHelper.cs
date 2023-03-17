using System;

namespace SimpleIRCLib
{
    public static class IpHelper
    {
        /// <summary>
        /// Converts a long/int64 to a ip string.
        /// </summary>
        /// <param name="address">int64 numbers representing IP address</param>
        /// <returns>string with ip</returns>
        public static string UInt64ToIPAddress(long address)
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
    }
}
