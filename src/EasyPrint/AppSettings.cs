namespace EasyPrint
{
    [Serializable]
    public class AppSettings
    {

        public string Ip{ get; set; } = "Any";
        public int    Port       { get; set; } = 201212;
        public bool   AutoStart  { get; set; } = true;

        public long MaxPackageLength { get; set; } = 1022886006;

        public long ReceiveBufferSize { get; set; } = 409600;
    }
}
