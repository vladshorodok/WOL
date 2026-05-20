using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace WakeOnLan.Core
{
    public class WolEngine : IDisposable
    {
        private Thread _thread;
        private bool _running;
        private AppConfig _config;
        private SerialPort _serialPort;

        public event Action<string> OnLog;
        public event Action<string> OnWolSent;
        public event Action<string> OnUnknownCaller;

        public bool IsRunning => _running;

        public WolEngine(AppConfig config)
        {
            _config = config;
        }

        public void Start()
        {
            if (_running) return;



            if (!InitSerialPort()) return;
            if (!InitModem())
            {
                Log("Modemul nu a răspuns. Verifică conexiunea.");
                return;
            }

            _running = true;
            _thread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "GsmPollThread"
            };
            _thread.Start();
            Log("Engine pornit. Utilizatori: " + _config.Users.Count);
        }



        public void Stop()
        {
            _running = false;
            _thread?.Join(3000);

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }

            Log("Engine oprit.");
        }

        public void UpdateConfig(AppConfig config)
        {
            _config = config;
            Log("Configurație reîncărcată.");

        }

        private bool InitSerialPort()
        {
            try
            {
                var porturi = SerialPort.GetPortNames();
                if (!Array.Exists(porturi, p => p == _config.SerialPort.PortName))
                {
                    Log($"Portul {_config.SerialPort.PortName} nu există.");
                    Log($"Porturi disponibile: {string.Join(", ", porturi)}");
                    return false;
                }

                _serialPort = new SerialPort(
                    _config.SerialPort.PortName,
                    _config.SerialPort.BaudRate)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    Encoding = Encoding.ASCII,
                    NewLine = "\r\n"
                };
                _serialPort.Open();
                Log($"Port {_config.SerialPort.PortName} deschis la {_config.SerialPort.BaudRate} baud.");
                return true;
            }
            catch (Exception ex)
            {
                Log("Eroare port serial: " + ex.Message);
                return false;
            }
        }

        private bool InitModem()
        {
            // Test de bază
            string r = SendAt("AT");
            if (!r.Contains("OK"))
            {
                Log("AT → fără răspuns OK");
                return false;
            }
            Log("AT → OK");

            SendAt("ATE0");              // dezactivează ecoul
            SendAt("AT+CMGF=1");         // mod text pentru SMS
            SendAt("AT+CLIP=1");         // afișează numărul apelantului
            SendAt("AT+CNMI=2,1,0,0,0"); // notificare SMS nou pe serial

            Log("Modem inițializat.");
            return true;
        }

        private void PollLoop()
        {
            while (_running)
            {
                try
                {
                    // 1. Verifică apeluri active (apel primit / în așteptare)
                    CheckIncomingCall();

                    // 2. Verifică SMS-uri necitite
                    CheckSms();
                }
                catch (Exception ex)
                {
                    Log("Eroare poll: " + ex.Message);
                }

                Thread.Sleep(_config.PollIntervalMs);
            }
        }

        private void CheckIncomingCall()
        {
            // AT+CLCC listează apelurile active
            // Răspuns dacă există apel: +CLCC: 1,1,4,0,0,"0737774955",129
            string r = SendAt("AT+CLCC");

            if (!r.Contains("+CLCC:")) return; // niciun apel activ

            // Extragem numărul dintre ghilimele
            int start = r.IndexOf('"');
            int end = r.IndexOf('"', start + 1);

            if (start < 0 || end < 0) return;

            string caller = r.Substring(start + 1, end - start - 1);
            Log($"Apel primit de la: {caller}");

            // Respingem apelul (nu răspundem, doar citim numărul)
            SendAt("ATH");

            HandleTrigger(caller);
        }

        private void CheckSms()
        {
            // Citim toate SMS-urile necitite
            string r = SendAt("AT+CMGL=\"REC UNREAD\"", 5000);

            if (!r.Contains("+CMGL:")) return; // niciun SMS nou

            // Fiecare SMS are forma:
            // +CMGL: 1,"REC UNREAD","0737774955",,"24/01/01,10:00:00+00"
            // textul mesajului
            var lines = r.Split(new[] { "\r\n", "\n" },
                                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("+CMGL:")) continue;

                // Extragem numărul (al 3-lea câmp între ghilimele)
                string header = lines[i];
                string sender = ExtractThirdQuoted(header);
                string text = (i + 1 < lines.Length) ? lines[i + 1].Trim() : "";

                Log($"SMS de la {sender}: {text}");

                // Ștergem SMS-ul după citire
                string idx = header.Split(':')[1].Split(',')[0].Trim();
                SendAt($"AT+CMGD={idx}");

                HandleTrigger(sender);
            }
        }

        public void HandleTrigger(string phoneNumber)
        {
            string nr = Normalize(phoneNumber);
            var user = _config.Users.FirstOrDefault(
                              u => Normalize(u.PhoneNumber) == nr);

            if (user == null)
            {
                Log("Număr neautorizat: " + phoneNumber);
                OnUnknownCaller?.Invoke(phoneNumber);
                return;
            }

            Log($"Autorizat: {user.Name} → WOL către {user.MacAddress}");

            try
            {
                WolSender.Send(user.MacAddress);
                Log($"✔ Magic Packet trimis către {user.MacAddress}");
            }
            catch (Exception ex)
            {
                Log($"✘ Eroare WOL: {ex.Message}");
            }

            OnWolSent?.Invoke(user.MacAddress);
        }

        private String SendAt(string command, int timeoutMs = 2000)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                return string.Empty;
            }

            try
            {
                _serialPort.DiscardInBuffer();
                _serialPort.WriteLine(command + "\r");

                var sb = new StringBuilder();
                var deadline = DateTime.Now.AddMilliseconds(timeoutMs);

                while (DateTime.Now < deadline)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        string chunk = _serialPort.ReadExisting();
                        sb.Append(chunk);

                        string current = sb.ToString();
                        if (current.Contains("OK") ||
                            current.Contains("ERROR") ||
                            current.Contains("+CMS ERROR"))
                            break;
                    }
                    Thread.Sleep(50);
                }
                return sb.ToString();
            }
            catch (TimeoutException)
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                Log("Eroare AT: " + ex.Message);
                return string.Empty;
            }
        }



        // ── Helpers ───────────────────────────────────────────────────────────


        // Extrage al 3-lea șir între ghilimele dintr-un string
        // Ex: +CMGL: 1,"REC UNREAD","0737774955",, → "0737774955"
        private static string ExtractThirdQuoted(string s)
        {
            int count = 0, start = -1;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '"') continue;
                count++;
                // Schimbăm de la 5 și 6 la 3 și 4 pentru a lua numărul de telefon
                if (count == 3) start = i + 1;
                if (count == 4) return s.Substring(start, i - start);
            }
            return string.Empty;
        }

        private static string Normalize(string nr)
        {
            if (string.IsNullOrWhiteSpace(nr)) return "";
            string s = nr.Replace(" ", "").Replace("-", "").Trim();

            if (s.StartsWith("+40")) s = "0" + s.Substring(3);  // +40737... → 0737...
            if (s.StartsWith("0040")) s = "0" + s.Substring(4);  // 0040737... → 0737...

            return s;
        }

        private void Log(string msg) =>
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}");

        public void Dispose() => Stop();
    }
}