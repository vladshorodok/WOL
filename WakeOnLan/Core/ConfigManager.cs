using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WakeOnLan.Core
{
    public static class ConfigManager
    {
        private static readonly XmlSerializer _ser =
            new XmlSerializer(typeof(AppConfig));

        public static AppConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                var def = new AppConfig();
                Save(def, path);
                return def;
            }
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return (AppConfig)_ser.Deserialize(fs);
            }
        }

        public static void Save(AppConfig config, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                _ser.Serialize(fs, config);
            }
        }
    }
}
