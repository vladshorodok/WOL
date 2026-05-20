using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WakeOnLan.Core;

namespace WolApp
{
    public partial class Form1 : Form
    {
        private readonly string _configPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml");

        private AppConfig _config;
        private WolEngine _engine;

        public Form1()
        {

            InitializeComponent();

            _config = ConfigManager.Load(_configPath);
            LoadUsersIntoGrid();
            LoadAvailablePorts();
        }

        // ── Grid ─────────────────────────────────────────────────────────────

        private void LoadUsersIntoGrid()
        {
            dgvUsers.Rows.Clear();
            foreach (var u in _config.Users)
                dgvUsers.Rows.Add(u.Name, u.PhoneNumber, u.MacAddress);
        }

        private void LoadAvailablePorts()
        {
            cmbPort.Items.Clear();
            foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
                cmbPort.Items.Add(port);

            if (cmbPort.Items.Contains(_config.SerialPort.PortName))
                cmbPort.SelectedItem = _config.SerialPort.PortName;
            else if (cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0;
            else
                cmbPort.Text = "Niciun port găsit";
        }

        private void SaveGridToConfig()
        {
            _config.Users.Clear();
            foreach (DataGridViewRow row in dgvUsers.Rows)
            {
                var name = row.Cells["ColName"]?.Value?.ToString()?.Trim();
                var phone = row.Cells["PhoneNumber"]?.Value?.ToString()?.Trim();
                var mac = row.Cells["MacAddress"]?.Value?.ToString()?.Trim();
                var isPhoneCall = row.Cells["ColPhoneCall"]?.Value != null &&
                                  (bool)row.Cells["ColPhoneCall"].Value;
                var isSms = row.Cells["ColSms"]?.Value != null &&
                            (bool)row.Cells["ColSms"].Value;

                if (!string.IsNullOrEmpty(name) &&
                    !string.IsNullOrEmpty(phone) &&
                    !string.IsNullOrEmpty(mac))
                {
                    _config.Users.Add(new UserEntry
                    {
                        Name = name,
                        PhoneNumber = phone,
                        MacAddress = mac,
                        IsPhoneCall = isPhoneCall,
                        IsSms = isSms
                    });
                }
            }
            ConfigManager.Save(_config, _configPath);
        }

        // ── Start / Stop ─────────────────────────────────────────────────────

        private void btnStart_Click(object sender, EventArgs e)
        {
            // Salvăm portul ales în config
            if (cmbPort.SelectedItem != null)
                _config.SerialPort.PortName = cmbPort.SelectedItem.ToString();

            SaveGridToConfig();

            _engine = new WolEngine(_config);
            _engine.OnLog += AppendLog;
            _engine.OnWolSent += mac => AppendLog("✔ WOL trimis → " + mac);
            _engine.OnUnknownCaller += nr => AppendLog("⚠ Neautorizat: " + nr);
            _engine.Start();

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            lblStatusDot.ForeColor = System.Drawing.Color.LimeGreen;
            lblStatus.Text = "Activ";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _engine?.Stop();
            _engine?.Dispose();
            _engine = null;

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblStatusDot.ForeColor = System.Drawing.Color.OrangeRed;
            lblStatus.Text = "Oprit";
        }

        // ── Utilizatori ───────────────────────────────────────────────────────

        private void btnAdd_Click(object sender, EventArgs e)
        {
            int idx = dgvUsers.Rows.Add("Nume", "07XXXXXXXX", "00:11:22:33:44:55", false, false);
            dgvUsers.CurrentCell = dgvUsers.Rows[idx].Cells[0];
            dgvUsers.BeginEdit(true);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgvUsers.SelectedRows)
                if (!row.IsNewRow)
                    dgvUsers.Rows.Remove(row);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveGridToConfig();
            _engine?.UpdateConfig(_config);
            MessageBox.Show("Salvat!", "OK",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void AppendLog(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AppendLog), text);
                return;
            }
            lstLogs.Items.Add(text);
            if (lstLogs.Items.Count > 500)
                lstLogs.Items.RemoveAt(0);
            lstLogs.TopIndex = lstLogs.Items.Count - 1;
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _engine?.Stop();
            base.OnFormClosing(e);
        }

        private void btnRefreshPorts_Click(object sender, EventArgs e)
        {
            LoadAvailablePorts();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgvUsers.SelectedRows)
                if (!row.IsNewRow)
                    dgvUsers.Rows.Remove(row);
        }

        // ── Log ───────────────────────────────────────────────────────────────

        private void btnClearLog_Click_1(object sender, EventArgs e)
        {
            lstLogs.Items.Clear();
        }
    }
}