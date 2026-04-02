using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EasyPrint
{
    public static class Serialization
    {

       

        public static void SerializeJson(object t, string path)
        {
            Stream stream = null;
            BufferedStream bf = null;
            StreamWriter sw = null;
            try
            {
                if (t == null)
                    return;
                if (File.Exists(path))
                    File.Delete(path);
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };
                var str = JsonConvert.SerializeObject(t, settings);

                stream = new FileStream(path, FileMode.Create,
                  FileAccess.Write, FileShare.ReadWrite);
                bf = new BufferedStream(stream, 65336);
                sw = new StreamWriter(bf);
                sw.Write(str);

            }
            finally
            {

                sw?.Close();
                bf?.Close();
                stream?.Close();
            }
        }
        public static string DeserializeJson(string path)
        {

            StreamReader streamReader = null;
            JsonTextReader jsonReader = null;
            try
            {

                streamReader = new StreamReader(path);
                jsonReader = new JsonTextReader(streamReader);

                return jsonReader.ReadAsString();

            }
            finally
            {
                streamReader?.Close();
                jsonReader?.Close();

            }

        }

        public static T DeserializeJson<T>(string path)
        {
            StreamReader streamReader = null;
            JsonTextReader jsonReader = null;
            try
            {

                streamReader = new StreamReader(path);
                jsonReader = new JsonTextReader(streamReader);
                var sr = new JsonSerializer();
                return sr.Deserialize<T>(jsonReader);

            }
            finally
            {
                streamReader?.Close();
                jsonReader?.Close();

            }


        }
         
      
    }
}
