using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WakeOnLan.Core
{
    public class WolSender
    {
        public static void Send(string macAddress)
        {
            byte[] mac = ParseMac(macAddress);
            byte[] packet = BuildPacket(mac);

            using (var client = new UdpClient())
            {
                client.EnableBroadcast = true;
                var endpoint = new IPEndPoint(IPAddress.Broadcast, 9);
                client.Send(packet, packet.Length, endpoint);
            }
        }

        private static byte[] ParseMac(string mac)
        {
            string clean = mac.Replace(":", "").Replace("-", "").Trim();


            if (clean.Length != 12)
                throw new ArgumentException($"Adresă MAC invalidă: {mac}");

            return Enumerable.Range(0, 6)
                             .Select(i => Convert.ToByte(clean.Substring(i * 2, 2), 16))
                             .ToArray();
        }

        private static byte[] BuildPacket(byte[] mac)
        {
            byte[] packet = new byte[102];

            for (int i = 0; i < 6; i++)
            {
                packet[i] = 0xFF;
            }

            for (int i = 0; i < 16; i++)
            {
                Array.Copy(mac, 0, packet, 6 + i * 6, 6);
            }

            return packet;
        }
    }
}
