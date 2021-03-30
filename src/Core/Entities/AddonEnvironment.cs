using System;
using Discord.WebSocket;
using Gommon;
using Qmmands;
using Volte.Commands;
using Volte.Services;

namespace Volte.Core.Entities
{
    public class AddonEnvironment
    {
        public IServiceProvider Services { get; }
        public DiscordShardedClient Client { get; }
        public CommandService Commands { get; }
        public DatabaseService Database { get; }

        public AddonEnvironment(IServiceProvider provider)
        {
            //Commands.GetTypeParser<TimeSpan>().ParseAsync()
            Services = provider;
            Client = provider.Get<DiscordShardedClient>();
            Commands = provider.Get<CommandService>();
            Database = provider.Get<DatabaseService>();
        }

        public bool IsCommand(SocketUserMessage message, ulong guildId) 
            => CommandUtilities.HasAnyPrefix(message.Content, 
                new[] { Database.GetData(guildId).Configuration.CommandPrefix, $"<@{Client.CurrentUser.Id}> ", $"<@!{Client.CurrentUser.Id}> " }, 
                StringComparison.OrdinalIgnoreCase, out _, out _);

    }
}