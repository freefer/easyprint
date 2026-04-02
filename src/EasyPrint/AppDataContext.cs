using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EasyPrint
{


    public static class AppDataContext
    {
        /// <summary>Chromium 下载缓存目录（传给 BrowserFetcher.CacheDir）</summary>
        public static readonly string PuppeteerCacheDir = Path.Combine(Application.StartupPath, "puppeteer_cache");

        /// <summary>
        /// Chromium 可执行文件完整路径（由 DownloadBrowserAsync 在找到/下载后赋值）。
        /// 这才是 LaunchOptions.ExecutablePath 应该使用的值。
        /// </summary>
        public static string? PuppeteerExecutablePath { get; set; }

        public static string AssemblyDirPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static bool IsLogoff = false;

        private const string ConfigName = "cfg.json";
        private static string _appdata;
        public static string Appdata
        {
            get
            {
                if (string.IsNullOrEmpty(_appdata))
                {
                    _appdata = Path.Combine(AssemblyDirPath, "data");
                }
                return _appdata;
            }
        }
        private static AppSettings _appConfig;
        public static AppSettings LoadConfig(string path = "")
        {
            if (path == "")
            {
                path = Path.Combine(Appdata, ConfigName);
            }

            try
            {
                if (_appConfig != null) return _appConfig;

                if (!File.Exists(path))
                {
                    _appConfig = GetConfig();
                    SaveTo(_appConfig, path);
                }
                else
                {

                    _appConfig = Serialization.DeserializeJson<AppSettings>(path);

                }

            }
            catch (Exception e)
            {
                //if (File.Exists(path))
                //{
                //    File.Delete(path);
                //}
                // _appConfig = GetConfig();
                throw new FileLoadException("文件内容格式不正确");


            }
            return _appConfig;
        }

        public static AppSettings GetConfig()
        {
            if (_appConfig != null) return _appConfig;
            _appConfig = new AppSettings();

            return _appConfig;
        }
        public static void Save(AppSettings config)
        {
            SaveTo(config);
        }

        public static void SaveTo(AppSettings config, string path = "")
        {
            try
            {
                if (!Directory.Exists(Appdata))
                {
                    Directory.CreateDirectory(Appdata);
                }
                string cfgPath = "";
                if (path == "")
                {

                    cfgPath = Path.Combine(Appdata, ConfigName);
                }
                else
                {
                    cfgPath = path;
                }

                Serialization.SerializeJson(config, cfgPath);
            }
            catch (Exception e)
            {
                throw new Exception("application save config fail.");

            }
        }

        public static void Logoff()
        {
            var path = Path.Combine(Appdata, ConfigName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            IsLogoff = true;

        }

    }
}
