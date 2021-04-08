using System.Diagnostics;
using System.Threading.Tasks;
using Gommon;
using Qmmands;
using Volte.Commands;

namespace Volte.Commands.Modules
{
    public sealed partial class UtilityModule
    {
        [Command("Uptime")]
        [Description("Shows the bot's uptime in a human-friendly fashion.")]
        public Task<ActionResult> UptimeAsync()
            => Ok($"I've been online for **{Process.GetCurrentProcess().CalculateUptime()}**!");
    }
}