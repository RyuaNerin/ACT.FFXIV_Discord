using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using DiscordRPC;
using Timer = System.Threading.Timer;

namespace ACT.FFXIV_Discord
{
    public class Plugin : IDisposable
    {
        private const int UpdatePeriod = 5000;

        private readonly Timer timerDetectPlugin;

        private readonly DiscordRpcClient client;

        private readonly Label labelCopyRight;
        private readonly Label labelPluginStatusText; // Dispose 하지 않도록 주의

        private readonly RichPresence richPresence = new RichPresence {Assets = new Assets() };

        private object pluginLock = new object();
        private FFXIVPluginWrapper plugin;

        public Plugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            this.timerDetectPlugin = new Timer(this.DetectGamePlugin, null, UpdatePeriod, Timeout.Infinite);

            this.labelCopyRight = new Label
            {
                Cursor = Cursors.Hand,
                Dock = DockStyle.Fill,
                Font = new Font(pluginScreenSpace.Font.FontFamily, 16, FontStyle.Bold),
                Text = "CopyRight (C) By RyuaNerin",
                TextAlign = ContentAlignment.MiddleCenter,
            };

            this.labelCopyRight.MouseDoubleClick += this.CopyRight_MouseDoubleClick;

            pluginScreenSpace.Controls.Add(this.labelCopyRight);
            pluginScreenSpace.Text = "FFXIV_Discord";

            this.labelPluginStatusText = pluginStatusText;
            this.labelPluginStatusText.Text = $"Inited. (v{Assembly.GetAssembly(typeof(Plugin)).GetName().Version})";

            this.client = new DiscordRpcClient("455179024310861827");
            this.client.Initialize();
        }

        ~Plugin()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool disposed = false;
        private void Dispose(bool disposing)
        {
            if (this.disposed) return;
            this.disposed = true;

            if (disposing)
            {
                this.timerDetectPlugin.Change(Timeout.Infinite, Timeout.Infinite);
                this.timerDetectPlugin.Dispose();

                this.client.ClearPresence();
                this.client.Invoke();
                this.client.Dispose();

                this.labelCopyRight.Dispose();
            }

            this.ReconnectPlugin(null);

            this.labelPluginStatusText.Text = "Deinited.";
        }

        private void CopyRight_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "https://github.com/RyuaNerin/ACT.FFXIV_Discord",
                        UseShellExecute = true
                    }
                )?.Dispose();
            }
            catch
            {
            }
        }

        private void DetectGamePlugin(object state)
        {
            var plugin = ActGlobals.oFormActMain.ActPlugins
                    .Where(e => e.pluginFile.Name.IndexOf("FFXIV_ACT_Plugin", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    .Where(e => e.lblPluginStatus.Text.IndexOf("FFXIV Plugin Started.", StringComparison.CurrentCultureIgnoreCase) >= 0)
                    .FirstOrDefault()
                    ?.pluginObj;

            if (plugin != null)
            {
                this.ReconnectPlugin(plugin);
            }

            this.timerDetectPlugin.Change(UpdatePeriod, Timeout.Infinite);
        }

        private void ReconnectPlugin(object plugin)
        {
            lock (this.pluginLock)
            {
                if (this.plugin?.Raw == plugin)
                    return;

                if (this.plugin != null)
                {
                    this.plugin.Dispose();
                }

                if (plugin != null)
                {
                    this.plugin = new FFXIVPluginWrapper(plugin);
                    this.plugin.DataSubscription.RegisterEvents();
                    this.plugin.DataSubscription.PrimaryPlayerChanged += this.DataSubscription_PrimaryPlayerChanged;
                    this.plugin.DataSubscription.PlayerStatsChanged   += this.DataSubscription_PlayerStatsChanged;
                    this.plugin.DataSubscription.ZoneChanged          += this.DataSubscription_ZoneChanged;

                    this.SetFirstPresence();
                }
            }
        }

        private void SetFirstPresence()
        {
            var player = this.plugin.DataRepository.GetPlayer();
            var playerId = this.plugin.DataRepository.GetCurrentPlayerID();
            var playerCombatant = this.plugin.DataRepository.GetCombatantList().FirstOrDefault(e => e.ID == playerId);

            this.richPresence.Details = playerCombatant?.Name;
            this.richPresence.Assets.LargeImageKey = player.JobID.ToString();
            this.richPresence.State = TruncateString(ActGlobals.oFormActMain.CurrentZone);
            this.richPresence.Timestamps = Timestamps.Now;

            this.UpdatePresence();
        }

        private void DataSubscription_PrimaryPlayerChanged()
        {
            lock (this.pluginLock)
            {
                try
                {
                    var playerId = this.plugin.DataRepository.GetCurrentPlayerID();
                    var playerCombatant = this.plugin.DataRepository.GetCombatantList().First(e => e.ID == playerId);

                    this.richPresence.Details = playerCombatant.Name;
                }
                catch
                {
                    this.richPresence.Details = null;
                }
                finally
                {
                    this.UpdatePresence();
                }
            }
        }

        private void DataSubscription_PlayerStatsChanged(object playerStats)
        {
            try
            {
                var playerStat = new FFXIVPluginWrapper.IPlayer(playerStats);

                this.richPresence.Assets.LargeImageKey = playerStat.JobID.ToString();

                this.UpdatePresence();
            }
            catch
            {
            }
        }

        private void DataSubscription_ZoneChanged(uint ZoneID, string ZoneName)
        {
            this.richPresence.State = TruncateString(ZoneName);
            this.richPresence.Timestamps = Timestamps.Now;

            this.UpdatePresence();
        }

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

        private void UpdatePresence()
        {
            try
            {
                this.client.SetPresence(this.richPresence);
                this.labelCopyRight.Invoke(new Action(() => this.client.Invoke()));
            }
            catch
            {
            }
        }
    }
}
