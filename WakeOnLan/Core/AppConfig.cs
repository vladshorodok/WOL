using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace WakeOnLan.Core
{
    [XmlRoot("WakeOnLanConfig")]
    public class AppConfig
    {
        public SerialPortConfig SerialPort { get; set; } = new SerialPortConfig();
        public int PollIntervalMs { get; set; } = 2000;

        [XmlArrayItem("User")]
        public List<UserEntry> Users { get; set; } = new List<UserEntry>();
    }

    public class SerialPortConfig
    {
        public string PortName { get; set; } = "COM3";
        public int BaudRate { get; set; } = 115200;
    }

    public class UserEntry
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string MacAddress { get; set; }
    }
}
