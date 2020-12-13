namespace GyrospeedWin {
    public class PrgFile {
        public string Path { get; set; }
        public string Name { get; set; }
        public string FileNameWithoutExtension { get; set; }
        public long Size { get; set; }
        public string TapPath { get; set; }
        public long TapDataSize { get; set; }
        public double TapDurationInSeconds { get; set; }
        public int BinNumber { get; set; }
    }
}
