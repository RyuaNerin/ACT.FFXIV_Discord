using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Advanced_Combat_Tracker;

using Timer = System.Threading.Timer;

namespace ACT.FFXIV_Discord
{
    public class Plugin : IActPluginV1
    {
        private const int UpdatePeriod = 5000;

        private Label m_copyRight;
        private Label m_pluginStatusText;
        private Timer m_newTimer;
        
        public void DeInitPlugin()
        {
            if (this.m_newTimer != null)
            {
                this.m_newTimer.Change(Timeout.Infinite, Timeout.Infinite);
                this.m_newTimer.Dispose();
                this.m_newTimer = null;
            }

            DiscordRpc.ClearPresence();
            
            ActGlobals.oFormActMain.OnCombatStart -= this.OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd   -= this.OFormActMain_OnCombatEnd;

            this.m_pluginStatusText.Text = "Deinited.";
            this.m_pluginStatusText = null;
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            if (this.m_copyRight == null)
            {
                this.m_copyRight = new Label();
                this.m_copyRight.Text = "CopyRight (C) By RyuaNerin";
                this.m_copyRight.TextAlign = ContentAlignment.MiddleCenter;
                this.m_copyRight.Font = new Font(pluginScreenSpace.Font.FontFamily, 16, FontStyle.Bold);
                this.m_copyRight.Dock = DockStyle.Fill;
                this.m_copyRight.Cursor = Cursors.Hand;
                this.m_copyRight.MouseDoubleClick +=
                    (s, e) => Process.Start(
                        new ProcessStartInfo {
                            FileName = "https://github.com/RyuaNerin/ACT.FFXIV_Discord",
                            UseShellExecute = true
                            }
                        );
            }

            pluginScreenSpace.Controls.Add(this.m_copyRight);
            pluginScreenSpace.Text = "FFXIV_Discord";

            this.m_pluginStatusText = pluginStatusText;
            this.m_pluginStatusText.Text = $"Inited. (v{System.Reflection.Assembly.GetAssembly(typeof(Plugin)).GetName().Version.ToString()})";
            
            ActGlobals.oFormActMain.OnCombatStart += this.OFormActMain_OnCombatStart;
            ActGlobals.oFormActMain.OnCombatEnd   += this.OFormActMain_OnCombatEnd;

            this.m_newTimer = new Timer(this.UpdateDiscordPresence, null, UpdatePeriod, UpdatePeriod);

            var handlers = new DiscordRpc.EventHandlers
            {
                readyCallback = HandleReadyCallback,
                errorCallback = HandleErrorCallback,
                disconnectedCallback = HandleDisconnectedCallback
            };
            DiscordRpc.Initialize("455179024310861827", ref handlers, true, null);
        }

        private void OFormActMain_OnCombatStart(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            this.m_newTimer.Change(Timeout.Infinite, Timeout.Infinite);
            UpdateDiscordPresence(true);
        }
        private void OFormActMain_OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            this.m_newTimer.Change(UpdatePeriod, UpdatePeriod);
            UpdateDiscordPresence(true);
        }
        
        private bool GetPlayerJob(out string name, out int job)
        {
            name = null;
            job = 0;
            
            var plugin = ActGlobals.oFormActMain.ActPlugins
                    ?.Where(e => e.pluginFile.Name.IndexOf("FFXIV_ACT_Plugin", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    ?.Where(e => e.lblPluginStatus.Text.IndexOf("FFXIV Plugin Started.", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    ?.FirstOrDefault()
                    ?.pluginObj;
            if (plugin == null)
                return false;

            var memory = plugin.GetType()
                .GetField("_Memory", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(plugin);
            if (memory == null)
                return false;

            var config = memory.GetType()
                .GetField("_config", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(memory);
            if (memory == null)
                return false;

            var scanCombatants = config.GetType()
                .GetField("ScanCombatants", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(config);
            if (scanCombatants == null)
                return false;

            var list = scanCombatants.GetType()
                .GetField("_Combatants", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(scanCombatants);
            if (list == null)
                return false;

            foreach (dynamic item in (IEnumerable)list)
            {
                if (item == null) continue;

                job = (byte)item.Job;
                name = item.Name.ToString();

                return true;
            }

            return false;
        }
        
        private void UpdateDiscordPresence(object state)
        {
            this.UpdateDiscordPresence(false);
        }

        private string m_lastName;
        private int m_lastJob;
        private string m_lastZone;
        private long m_zoneChanged;

        private int m_updating = 0;
        private void UpdateDiscordPresence(bool force)
        {
            if (force)
            {
                while (Interlocked.Exchange(ref this.m_updating, 1) == 0)
                    Thread.Sleep(100);
            }
            else
            {
                if (Interlocked.Exchange(ref this.m_updating, 1) != 0)
                    return;
            }

            try
            {
                if (GetPlayerJob(out string curName, out int curJob))
                {
                    this.m_lastJob = curJob;

                    if (!string.IsNullOrWhiteSpace(curName))
                        this.m_lastName = curName;
                }

                if (this.m_lastName == null)
                    return;
                
                var curZone = ActGlobals.oFormActMain.CurrentZone;

                var presence = new DiscordRpc.RichPresence();
                presence.largeImageKey = "ffxiv";
                
                if (1 <= this.m_lastJob && this.m_lastJob <= 35)
                    presence.smallImageKey = this.m_lastJob.ToString();

                presence.details = TruncateString(this.m_lastName);

                if (!string.IsNullOrWhiteSpace(curZone))
                {
                    if (this.m_lastZone != curZone)
                    {
                        this.m_zoneChanged = DateTimeOffset.Now.ToUnixTimeSeconds();
                        this.m_lastZone = curZone;
                    }

                    presence.state = TruncateString(ActGlobals.oFormActMain.CurrentZone);
                    presence.startTimestamp = this.m_zoneChanged;
                }

                DiscordRpc.UpdatePresence(presence);
            }
            finally
            {
                Interlocked.Exchange(ref this.m_updating, 0);
            }
        }
        private static void HandleReadyCallback(ref DiscordRpc.DiscordUser connectedUser) { }
        private static void HandleErrorCallback(int errorCode, string message) { }
        private static void HandleDisconnectedCallback(int errorCode, string message) { }

        private static string TruncateString(string s)
        {
            if (Encoding.Unicode.GetByteCount(s) < 128)
                return s;

            do
            {
                s = s.Substring(0, s.Length - 1);
            } while (Encoding.Unicode.GetByteCount(s) < 128 - 3);

            return s + "...";
        }
    }
}
