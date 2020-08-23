﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity;
using Volte.Commands;
using Volte.Core;
using Volte.Core.Models;
using Volte.Core.Models.EventArgs;
using Volte.Services;

namespace Gommon
{
    public static partial class Extensions
    {
        /// <summary>
        ///     Checks if the current user is the user identified in the bot's config.
        /// </summary>
        /// <param name="user">The current user</param>
        /// <returns>True, if the current user is the bot's owner; false otherwise.</returns>
        public static bool IsBotOwner(this DiscordMember user)
            => Config.Owner == user.Id;

        private static bool IsGuildOwner(this DiscordMember user)
            => user.Guild.Owner == user || IsBotOwner(user);

        public static bool IsModerator(this DiscordMember user, VolteContext ctx)
        {
            return HasRole(user, ctx.GuildData.Configuration.Moderation.ModRole) ||
                   IsAdmin(user, ctx) ||
                   IsGuildOwner(user);
        }

        public static string AsPrettyString(this DiscordMember member)
            => $"{member.Username}#{member.Discriminator}";

        private static bool HasRole(this DiscordMember user, ulong roleId)
            => user.Roles.Select(x => x.Id).Contains(roleId);

        public static bool IsAdmin(this DiscordMember user, VolteContext ctx)
        {
            return HasRole(user, ctx.GuildData.Configuration.Moderation.AdminRole) ||
                   IsGuildOwner(user);
        }

        public static DiscordRole GetHighestRole(this DiscordMember member)
        {
            var roles = member.Roles.OrderByDescending(x => x.Position);
            return roles.FirstOrDefault();
        }
        
        public static DiscordRole GetHighestRoleWithColor(this DiscordMember member)
        {
            var coloredRoles = member.Roles.Where(x => x.Color.Value != new DiscordColor(0, 0, 0).Value);
            var roles = coloredRoles.OrderByDescending(x => x.Position);
            return roles.FirstOrDefault();
        }

        public static List<Page> GetPages<T>(this IEnumerable<T> current, int entriesPerPage = 1)
        {
            var temp = current.ToList();
            var pageList = new List<Page>();

            do
            {
                pageList.Add(new Page(embed: new DiscordEmbedBuilder().WithDescription(temp.Take(entriesPerPage).Select(x => x.ToString()).Join("\n"))));
                temp.RemoveRange(0, temp.Count < entriesPerPage ? temp.Count : entriesPerPage);
            } while (!temp.IsEmpty());

            return pageList;
        }

        public static async Task SendPaginatedMessageAsync(this VolteContext ctx, List<Page> pages, string embedTitle = null)
        {
            var color = ctx.Member.GetHighestRoleWithColor()?.Color;
            var result = new List<Page>();
            var index = 0;
            foreach (var page in pages)
            {
                index++;

                var embed = page.Embed != null
                    ? new DiscordEmbedBuilder(page.Embed)
                    : new DiscordEmbedBuilder().WithDescription(page.Content);

                if (embed.Title == null && embedTitle != null)
                {
                    embed.WithTitle(embedTitle);
                }

                if (embed.Footer == null)
                {
                    embed.WithFooter($"Page {index} / {pages.Count}");
                }

                if (!embed.Color.HasValue && color.HasValue)
                {
                    embed.WithColor(color.Value);
                }
                else
                {
                    embed.WithSuccessColor();
                }

                result.Add(new Page(embed: embed));
            }

            await ctx.Interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.Member, result);
        }

        public static async Task<bool> TrySendMessageAsync(this DiscordMember user, string text = null,
            bool isTts = false, DiscordEmbed embed = null)
        {
            try
            {
                await user.SendMessageAsync(text, isTts, embed);
                return true;
            }
            catch (UnauthorizedException)
            {
                return false;
            }
        }

        public static async Task<bool> TrySendMessageAsync(this DiscordChannel channel, string text = null,
            bool isTts = false, DiscordEmbed embed = null)
        {
            try
            {
                await channel.SendMessageAsync(text, isTts, embed);
                return true;
            }
            catch (UnauthorizedException)
            {
                return false;
            }
        }

        public static string GetInviteUrl(this DiscordShardedClient client, bool withAdmin = true)
            => withAdmin
                ? $"https://discordapp.com/oauth2/authorize?client_id={client.CurrentUser.Id}&scope=bot&permissions=8"
                : $"https://discordapp.com/oauth2/authorize?client_id={client.CurrentUser.Id}&scope=bot&permissions=402992246";

        public static DiscordGuild GetPrimaryGuild(this DiscordShardedClient client)
            => client.GetGuild(405806471578648588);

        public static DiscordEmbedBuilder AddField(this DiscordEmbedBuilder builder, string name, object value,
            bool inline = false)
            => builder.AddField(name, value.ToString(), inline);
        
        // https://discord.com/developers/docs/topics/gateway#sharding-sharding-formula
        /// <summary>
        /// Gets a shard id from a guild id and total shard count.
        /// </summary>
        /// <param name="guildId">The guild id the shard is on.</param>
        /// <param name="shardCount">The total amount of shards.</param>
        /// <returns>The shard id.</returns>
        public static int GetShardId(ulong guildId, int shardCount)
            => (int)(guildId >> 22) % shardCount;

        public static DiscordClient GetShardFor(this DiscordShardedClient client, DiscordGuild guild)
            => client.ShardClients[GetShardId(guild.Id, client.ShardClients.Count)];

        public static DiscordGuild GetGuild(this DiscordShardedClient client, ulong guildId)
            => client.ShardClients[GetShardId(guildId, client.ShardClients.Count)].Guilds[guildId]; // TODO test

        public static void RegisterVolteEventHandlers(this DiscordShardedClient client, IServiceProvider provider)
        {
            var welcome = provider.Get<WelcomeService>();
            var guild = provider.Get<GuildService>();
            var evt = provider.Get<EventService>();
            var autorole = provider.Get<AutoroleService>();
            var logger = provider.Get<LoggingService>();
            client.ClientErrored += (args) =>
            {
                logger.Error(LogSource.Discord, args.Exception.Message, args.Exception.InnerException);
                return Task.CompletedTask;
            };
            client.DebugLogger.LogMessageReceived += async (_, args) => await logger.DoAsync(new LogEventArgs(args));
            client.GuildCreated += async args => await guild.DoAsync(args);
            client.GuildDeleted += async args => await guild.DoAsync(args);
            client.GuildMemberAdded += async args =>
            {
                if (Config.EnabledFeatures.Welcome) await welcome.JoinAsync(args);
                if (Config.EnabledFeatures.Autorole) await autorole.DoAsync(args);
            };
            client.GuildMemberRemoved += async args =>
            {
                if (Config.EnabledFeatures.Welcome) await welcome.LeaveAsync(args);
            };

            client.Ready += async args => await evt.OnShardReadyAsync(client, args);
            client.MessageCreated += async args =>
            {
                if (args.Message.ShouldHandle())
                {
                    if (args.Channel is DiscordDmChannel)
                        await args.Channel.SendMessageAsync("I do not support commands via DM.");
                    else
                        _ = Task.Run(async () => await evt.HandleMessageAsync(new MessageReceivedEventArgs(args.Message, provider)));
                }
            };
        }

        public static Task<DiscordMessage> SendToAsync(this DiscordEmbedBuilder e, DiscordChannel c) =>
            c.SendMessageAsync(string.Empty, false, e.Build());

        public static Task<DiscordMessage> SendToAsync(this DiscordEmbed e, DiscordChannel c) =>
            c.SendMessageAsync(string.Empty, false, e);

        // ReSharper disable twice UnusedMethodReturnValue.Global
        public static async Task<DiscordMessage> SendToAsync(this DiscordEmbedBuilder e, DiscordMember u) =>
            await u.SendMessageAsync(string.Empty, false, e.Build());

        public static async Task<DiscordMessage> SendToAsync(this DiscordEmbed e, DiscordMember u) =>
            await u.SendMessageAsync(string.Empty, false, e);

        public static async Task<bool> TryDeleteAsync(this DiscordMessage message, string reason = null)
        {
            try
            {
                await message.DeleteAsync(reason);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DiscordEmbedBuilder WithColor(this DiscordEmbedBuilder e, uint color) => e.WithColor(new DiscordColor((int) color));

        public static DiscordEmbedBuilder WithColor(this DiscordEmbedBuilder e, long color) => e.WithColor(new DiscordColor((int) color));

        public static DiscordEmbedBuilder WithSuccessColor(this DiscordEmbedBuilder e) => e.WithColor(Config.SuccessColor);

        public static DiscordEmbedBuilder WithErrorColor(this DiscordEmbedBuilder e) => e.WithColor(Config.ErrorColor);

        public static DiscordEmbedBuilder WithCurrentTimestamp(this DiscordEmbedBuilder e) => e.WithTimestamp(DateTimeOffset.Now);

        public static DiscordEmbedBuilder WithAuthor(this DiscordEmbedBuilder builder, DiscordUser user) =>
            builder.WithAuthor($"{user.Username}#{user.Discriminator}", iconUrl: user.AvatarUrl);

        public static DiscordEmoji ToEmoji(this string str) => DiscordEmoji.FromUnicode(str);

        public static bool ShouldHandle(this DiscordMessage message)
        {
            return !message.Author.IsBot;
        }

        public static string GetEffectiveUsername(this DiscordMember user) =>
            user.Nickname ?? user.Username;

        public static bool HasAttachments(this DiscordMessage message)
            => !message.Attachments.IsEmpty();

        public static bool HasColor(this DiscordRole role)
            => !(role.Color.Value is 0);
        
        public static Permissions GetGuildPermissions(this DiscordMember member)
        {
            // future note: might be able to simplify @everyone role checks to just check any role ... but i'm not sure
            // xoxo, ~uwx
            //
            // you should use a single tilde
            // ~emzi
            
            // user > role > everyone
            // allow > deny > undefined
            // =>
            // user allow > user deny > role allow > role deny > everyone allow > everyone deny
            // thanks to meew0

            var guild = member.Guild;

            if (guild.Owner == member)
                return Permissions.All;

            Permissions perms;

            // assign @everyone permissions
            var everyoneRole = guild.EveryoneRole;
            perms = everyoneRole.Permissions;

            // roles that member is in
            var mbRoles = member.Roles.Where(xr => xr.Id != everyoneRole.Id);

            // assign permissions from member's roles (in order)
            perms |= mbRoles.Aggregate(Permissions.None, (c, role) => c | role.Permissions);

            // Adminstrator grants all permissions and cannot be overridden
            if ((perms & Permissions.Administrator) == Permissions.Administrator)
                return Permissions.All;

            return perms;
        }

        public static int GetGuildCount(this DiscordShardedClient client)
            => client.ShardClients.Sum(e => e.Value.Guilds.Count);

        public static int GetChannelCount(this DiscordShardedClient client)
            => client.ShardClients.Sum(e => e.Value.Guilds.Sum(e1 => e1.Value.Channels.Count));

        // Dirty workaround for a limitation in D#+
        public static async Task<DiscordUser> UpdateCurrentUserAsync(this DiscordShardedClient client, string username = null, Optional<Stream> avatar = default)
            => await client.ShardClients.First().Value.UpdateCurrentUserAsync(username, avatar);

        public static int GetMeanLatency(this DiscordShardedClient client)
            => (int) Math.Round((double) client.ShardClients.Sum(e => e.Value.Ping) / client.ShardClients.Count);

        public static IEnumerable<DiscordChannel> GetCategoryChannels(this DiscordGuild guild)
            => guild.Channels.Where(e => e.Value.IsCategory).Select(e => e.Value);
        
        public static IEnumerable<DiscordChannel> GetVoiceChannels(this DiscordGuild guild)
            => guild.Channels.Where(e => e.Value.Type == ChannelType.Voice).Select(e => e.Value);
        
        public static IEnumerable<DiscordChannel> GetTextChannels(this DiscordGuild guild)
            => guild.Channels.Where(e => e.Value.Type != ChannelType.Voice && !e.Value.IsCategory).Select(e => e.Value);
        
        public static DiscordChannel FindFirstChannel(this DiscordShardedClient client, ulong id)
        {
            foreach (var (_, shard) in client.ShardClients)
            foreach (var (_, guild) in shard.Guilds)
            {
                if (guild.Channels.TryGetValue(id, out var channel))
                {
                    return channel;
                }
            }

            return null;
        }
    }
}