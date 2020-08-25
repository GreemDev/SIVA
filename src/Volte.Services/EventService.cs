using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Gommon;
using Humanizer;
using Qmmands;
using Volte.Core;
using Volte.Core.Helpers;
using Volte.Core.Models;
using Volte.Core.Models.EventArgs;
using Volte.Core.Models.Guild;

namespace Volte.Services
{
    public sealed class EventService : VolteEventService
    {
        private readonly LoggingService _logger;
        private readonly DatabaseService _db;
        private readonly AntilinkService _antilink;
        private readonly BlacklistService _blacklist;
        private readonly PingChecksService _pingchecks;
        private readonly CommandService _commandService;
        private readonly CommandsService _commandsService;
        private readonly QuoteService _quoteService;
        private readonly HttpClient _http;
        private readonly DiscordShardedClient _client;

        private readonly bool _shouldStream =
            !Config.Streamer.IsNullOrWhitespace();

        private readonly bool _shouldSetGame =
            !Config.Game.IsNullOrWhitespace();

        public EventService(LoggingService loggingService,
            DatabaseService databaseService,
            AntilinkService antilinkService,
            BlacklistService blacklistService,
            PingChecksService pingChecksService,
            CommandService commandService,
            CommandsService commandsService,
            QuoteService quoteService,
            HttpClient httpClient,
            DiscordShardedClient discordShardedClient)
        {
            _logger = loggingService;
            _antilink = antilinkService;
            _db = databaseService;
            _blacklist = blacklistService;
            _pingchecks = pingChecksService;
            _commandService = commandService;
            _commandsService = commandsService;
            _quoteService = quoteService;
            _http = httpClient;
            _client = discordShardedClient;
        }
        
        public override Task DoAsync(EventArgs args)
        {
            return args switch
            {
                MessageReceivedEventArgs messageReceived => HandleMessageAsync(messageReceived),
                ReadyEventArgs ready => OnReadyAsync(_client, ready),
                _ => Task.CompletedTask
            };
        }

        public async Task HandleMessageAsync(MessageReceivedEventArgs args)
        {
            if (Config.EnabledFeatures.Blacklist)
                await _blacklist.DoAsync(args);
            if (Config.EnabledFeatures.Antilink)
                await _antilink.DoAsync(args);
            if (Config.EnabledFeatures.PingChecks)
                await _pingchecks.DoAsync(args);

            var prefixes = new List<string>
            {
                args.Data.Configuration.CommandPrefix, $"<@{args.Context.Client.CurrentUser.Id}> ",
                $"<@!{args.Context.Client.CurrentUser.Id}> "
            };

            if (CommandUtilities.HasAnyPrefix(args.Message.Content, prefixes, StringComparison.OrdinalIgnoreCase, out _,
                out var cmd))
            {
                var sw = Stopwatch.StartNew();
                var result = await _commandService.ExecuteAsync(cmd, args.Context);

                if (result is CommandNotFoundResult) return;

                sw.Stop();
                await _commandsService.OnCommandAsync(new CommandCalledEventArgs(result, args.Context, sw));

                if (args.Data.Configuration.DeleteMessageOnCommand)
                    if (!await args.Message.TryDeleteAsync())
                        _logger.Warn(LogSource.Service, $"Could not act upon the DeleteMessageOnCommand setting for {args.Context.Guild.Name} as the bot is missing the required permission, or another error occured.");
            }
            else
            {
                await _quoteService.DoAsync(args);
            }
        }

        public async Task OnReadyAsync(DiscordShardedClient shardedClient, ReadyEventArgs args)
        {
            var shard = args.Client;
            var guilds = shard.Guilds.Count;

            _logger.PrintVersion();
            _logger.Info(LogSource.Volte, "Use this URL to invite me to your guilds:");
            _logger.Info(LogSource.Volte, $"{shardedClient.GetInviteUrl()}");
            _logger.Info(LogSource.Volte, $"Logged in as {shard.CurrentUser}, shard {shard.ShardId}");
            _logger.Info(LogSource.Volte, $"Default command prefix is: \"{Config.CommandPrefix}\"");
            _logger.Info(LogSource.Volte, $"Connected to {"guild".ToQuantity(guilds)}");

            if (!_shouldStream)
            {
                if (_shouldSetGame)
                {
                    await shard.UpdateStatusAsync(new DiscordActivity(Config.Game, ActivityType.Playing));
                    _logger.Info(LogSource.Volte, $"Set {shard.CurrentUser.Username}'s game to \"{Config.Game}\".");
                }
            }
            else
            {
                await shard.UpdateStatusAsync(new DiscordActivity(Config.Game, ActivityType.Streaming) {StreamUrl = Config.FormattedStreamUrl});
                _logger.Info(LogSource.Volte,
                    $"Set {shard.CurrentUser.Username}'s activity to \"{ActivityType.Streaming}: {Config.Game}\", at Twitch user {Config.Streamer}.");
            }
            
            foreach (var guild in shard.Guilds.Values)
            {
                var ownerId = DiscordReflectionHelper.GetOwnerId(guild);
                if (ownerId != 0UL && Config.BlacklistedOwners.Contains(ownerId))
                {
                    _logger.Warn(LogSource.Volte,
                        $"Left guild \"{guild.Name}\" owned by blacklisted owner {ownerId}.");
                    await guild.LeaveAsync();
                }

                var data = _db.GetData(guild);
                foreach (var u in await guild.GetAllMembersAsync())
                {
                    var d = data.UserData.FirstOrDefault(x => x.Id == u.Id);
                    if (d is null)
                    {
                        data.UserData.Add(new GuildUserData
                        {
                            Id = u.Id
                        });
                        _db.UpdateData(data);
                    }
                }
            }

            if (Config.GuildLogging.EnsureValidConfiguration(shardedClient, out var channel))
            {
                await new DiscordEmbedBuilder()
                    .WithSuccessColor()
                    .WithDescription(
                        $"Volte {Version.FullVersion} is starting at **{DateTimeOffset.UtcNow.FormatFullTime()}, on {DateTimeOffset.UtcNow.FormatDate()}**!")
                    .SendToAsync(channel);
            }
        }
    }
}