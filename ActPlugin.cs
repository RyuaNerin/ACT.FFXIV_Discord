using System.Windows.Forms;
using Advanced_Combat_Tracker;

namespace ACT.FFXIV_Discord
{
    public class ActPlugin : IActPluginV1
    {
        private Plugin plugin;

        public void DeInitPlugin()
        {
            this.plugin?.Dispose();
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            this.plugin = new Plugin(pluginScreenSpace, pluginStatusText);
        }
    }
}
