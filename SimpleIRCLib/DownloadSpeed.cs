namespace SimpleIRCLib
{
    public class DownloadSpeed
    {
        private readonly int _kBytesSpeed;
        public int KBytesPerSecond => _kBytesSpeed;
        public int MBytesPerSecond => _kBytesSpeed / 1024;

        public DownloadSpeed(int kBytesSpeed)
        {
            _kBytesSpeed = kBytesSpeed;
        }
    }
}
