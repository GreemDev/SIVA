﻿using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using SIVA.Core.Files.Readers;
using SIVA.Helpers;

namespace SIVA.Core.Discord.Modules.Owner
{
    public class CreateConfigCommand : SIVACommand
    {
        [Command("CreateConfig")]
        public async Task CreateConfig(ulong serverId = 0)
        {
            if (!Utils.IsBotOwner(Context.User))
            {
                await Context.Message.AddReactionAsync(new Emoji("❌"));
                return;
            }

            if (serverId == 0) serverId = Context.Guild.Id;

            var tG = DiscordLogin.Client.GetGuild(serverId);

            ServerConfig.Get(tG);
            ServerConfig.Save();

        }
    }
}