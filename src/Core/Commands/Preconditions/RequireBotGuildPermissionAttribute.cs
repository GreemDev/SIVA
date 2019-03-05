using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Qmmands;

namespace Volte.Core.Commands.Preconditions
{
    public class RequireBotGuildPermissionAttribute : CheckBaseAttribute
    {
        private readonly GuildPermission[] _permissions;

        public RequireBotGuildPermissionAttribute(GuildPermission[] perms) => _permissions = perms;

        public RequireBotGuildPermissionAttribute(GuildPermission perm) => _permissions = new[] {perm};

        public override Task<CheckResult> CheckAsync(
            ICommandContext context, IServiceProvider provider)
        {
            var ctx = (VolteContext) context;
            foreach (var perm in ctx.Guild.CurrentUser.GuildPermissions.ToList())
                if (_permissions.Contains(perm))
                    return Task.FromResult(CheckResult.Successful);
            return Task.FromResult(
                CheckResult.Unsuccessful("Bot is missing the required permissions to execute this command."));
        }
    }
}