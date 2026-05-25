namespace EasyPrint
{
    [Serializable]
    public class AppSettings
    {

        public string Ip               { get; set; } = "Any";
        public int    Port             { get; set; } = 8765;
        public bool   AutoStart        { get; set; } = true;
        public bool   StartWithWindows { get; set; } = false;

        public long MaxPackageLength   { get; set; } = 1022886006;

        public long ReceiveBufferSize  { get; set; } = 409600;
        public bool  UseSSL            { get; set; } = false;
        public int SSLPort { get; set; } = 8766;
        public string SSLCertPath      { get; set; } = "mkcert-192.168.2.138.pfx";
        public string SSLPassword      { get; set; } = "123456";
 
    }
}
