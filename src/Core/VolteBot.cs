using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Gommon;
using Microsoft.Extensions.DependencyInjection;

namespace Volte.Core
{
    public class VolteBot
    {
        public static Task StartAsync()
            => new VolteBot().LoginAsync();

        private static ServiceProvider BuildServiceProvider(int shardCount)
            => new ServiceCollection()
                .AddAllServices(shardCount)
                .BuildServiceProvider();

        private VolteBot() { }

        private async Task LoginAsync()
        {
            Console.Title = "Volte";
            Console.CursorVisible = false;

            if (!Directory.Exists("data"))
            {
                await Console.Out.WriteLineAsync("The \"data\" directory didn't exist, so I created it for you.");
                Directory.CreateDirectory("data");
            }

            if (!Config.CreateIfNotExists())
            {
                await Console.Out.WriteLineAsync("Please fill in the config.json located in \"data/config.json\"; restart me when you've done so.");
                return;
            }
            if (Config.Token.IsNullOrEmpty() || Config.Token.EqualsIgnoreCase("token here")) return;

            var rest = new DiscordRestClient();
            await rest.LoginAsync(TokenType.Bot, Config.Token);
            var shardCount = await rest.GetRecommendedShardCountAsync();
            await rest.LogoutAsync().ContinueWith(_ => rest.Dispose());

            var provider = BuildServiceProvider(shardCount);

            var client = provider.GetRequiredService<DiscordShardedClient>();
            var cts = provider.GetRequiredService<CancellationTokenSource>();
            var handler = provider.GetRequiredService<VolteHandler>();

            await client.LoginAsync(TokenType.Bot, Config.Token);
            await client.StartAsync().ContinueWith(_ => client.SetStatusAsync(UserStatus.Online));

            await handler.InitializeAsync(provider);

            try
            {
                await Task.Delay(-1, cts.Token);
            }
            catch (TaskCanceledException)
            {
                //this exception always occurs when CancellationTokenSource#Cancel() is called; so we put the shutdown logic inside the catch block
                await ShutdownAsync(client, cts, provider);
            }
        }

        private async Task ShutdownAsync(DiscordShardedClient client, CancellationTokenSource cts,
            ServiceProvider provider)
        {
            await client.SetStatusAsync(UserStatus.Invisible);
            await client.LogoutAsync();
            await client.StopAsync();
            Dispose(cts, provider, client);
            Environment.Exit(0);
        }

        private void Dispose(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }
}