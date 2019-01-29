﻿using System.Threading.Tasks;
using Discord.Commands;
using Volte.Core.Files.Readers;
using Volte.Helpers;

namespace Volte.Core.Modules.General {
    public partial class GeneralModule : VolteModule {
        [Command("Prefix")]
        public async Task Prefix() {
            await Reply(Context.Channel, CreateEmbed(Context,
                    $"The prefix for this server is `{Db.GetConfig(Context.Guild).CommandPrefix}`."
                )
            );
        }
    }
}