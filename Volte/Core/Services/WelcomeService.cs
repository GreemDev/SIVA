﻿using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Volte.Core.Discord;
using Volte.Core.Files.Readers;

namespace Volte.Core.Services {
    internal class WelcomeService {
        private DatabaseService Db = VolteBot.ServiceProvider.GetRequiredService<DatabaseService>();
        internal async Task Join(SocketGuildUser user) {
            var config = Db.GetConfig(user.Guild);
            if (string.IsNullOrEmpty(config.WelcomeMessage)) return; //we don't want to send an empty join message
            var welcomeMessage = config.WelcomeMessage
                .Replace("{ServerName}", user.Guild.Name)
                .Replace("{UserName}", user.Username)
                .Replace("{OwnerMention}", user.Guild.Owner.Mention)
                .Replace("{UserTag}", user.Discriminator);

            if (user.Guild.TextChannels.Any(c => c.Id == config.WelcomeChannel)) //if the channel even exists
            {
                var embed = new EmbedBuilder()
                    .WithColor(new Color(config.WelcomeColourR, config.WelcomeColourG, config.WelcomeColourB))
                    .WithDescription(welcomeMessage)
                    .WithThumbnailUrl(user.Guild.IconUrl)
                    .WithCurrentTimestamp();

                await VolteBot.Client.GetGuild(user.Guild.Id).GetTextChannel(config.WelcomeChannel)
                    .SendMessageAsync("", false, embed.Build());
            }
        }

        internal async Task Leave(SocketGuildUser user) {
            var config = Db.GetConfig(user.Guild);
            if (string.IsNullOrEmpty(config.LeavingMessage)) return; //we don't want to send an empty leaving message
            var leavingMessage = config.LeavingMessage
                .Replace("{ServerName}", user.Guild.Name)
                .Replace("{UserName}", user.Username)
                .Replace("{OwnerMention}", user.Guild.Owner.Mention)
                .Replace("{UserTag}", user.Discriminator);

            if (user.Guild.TextChannels.Any(c => c.Id == config.WelcomeChannel)) //if the channel even exists
            {
                var embed = new EmbedBuilder()
                    .WithColor(new Color(config.WelcomeColourR, config.WelcomeColourG, config.WelcomeColourB))
                    .WithDescription(leavingMessage)
                    .WithThumbnailUrl(user.Guild.IconUrl)
                    .WithCurrentTimestamp();

                await VolteBot.Client.GetGuild(user.Guild.Id).GetTextChannel(config.WelcomeChannel)
                    .SendMessageAsync("", false, embed.Build());
            }
        }
    }
}