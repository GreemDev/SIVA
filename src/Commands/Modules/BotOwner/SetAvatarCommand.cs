using System;
using System.Threading.Tasks;
using Discord;
using Gommon;
using Qmmands;
using Volte.Commands.Results;

namespace Volte.Commands.Modules
{
    public sealed partial class BotOwnerModule
    {
        [Command("SetAvatar")]
        [Description("Sets the bot's avatar to the image at the given URL.")]
        [Remarks("setavatar {String}")]
        public async Task<ActionResult> SetAvatarAsync(string url)
        {
            if (url.IsNullOrWhitespace() || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return BadRequest("That URL is malformed or empty.");
            }

            using var sr = await Http.GetAsync(url);

            if (!sr.IsImage())
            {
                return BadRequest(
                    "Provided URL does not lead to an image. Note that I cannot follow redirects; so provide *direct* image URLs please!");
            }

            await using var img = (await sr.Content.ReadAsByteArrayAsync()).ToStream();
            await Context.Client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(img));
            return Ok("Done!");
        }
    }
}