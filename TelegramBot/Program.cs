using dm.YLD.Common;
using dm.YLD.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace dm.YLD.TelegramBot
{
    public class Program
    {
        private ITelegramBotClient botClient;
        private IServiceProvider services;
        private IConfigurationRoot configuration;
        private Config config;
        private AppDbContext db;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
            => new Program().MainAsync(args).GetAwaiter().GetResult();

        public async Task MainAsync(string[] args)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Config.TelegramBot.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("Config.TelegramBot.Local.json", optional: true, reloadOnChange: true);

                configuration = builder.Build();

                services = new ServiceCollection()
                    .Configure<Config>(configuration)
                    .AddDatabase<AppDbContext>(configuration.GetConnectionString("Database"))
                    .BuildServiceProvider();
                config = services.GetService<IOptions<Config>>().Value;
                db = services.GetService<AppDbContext>();

                if (db.Database.GetPendingMigrations().Count() > 0)
                {
                    log.Info("Migrating database");
                    db.Database.Migrate();
                }

                await RunBot(args);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task RunBot(string[] args)
        {
            try
            {
                botClient = new TelegramBotClient(config.BotToken);
                log.Info($"Bot connected");

                if (args.Length > 0)
                {
                    await RunBotArgs(args, botClient);
                    return;
                }

                if (config.BotWatch)
                {
                    log.Info("BotWatch = true, waiting for messages");
                    botClient.OnMessage += BotClient_OnMessage;
                    botClient.StartReceiving();
                    await Task.Delay(-1).ConfigureAwait(false);
                }
                else
                {
                    return;

                    var item = await Data.Common.GetStats(db);

                    string text = $"" +
                        $"🤑 Price/USD: ${item.Price.PriceUSD.FormatUsd()}\n" +
                        $"🤑 Price/BTC: ₿{item.Price.PriceBTC.FormatBtc()}\n" +
                        $"🤑 Price/ETH: Ξ{item.Price.PriceETH.FormatEth()}\n" +
                        $"📈 Market Cap: ${item.Price.MarketCapUSD.FormatLarge()}\n" +
                        $"💸 Volume: ${item.Price.VolumeUSD.FormatLarge()}";

                    foreach (long chatId in config.ChatIds)
                    {
                        await botClient.SendTextMessageAsync(
                          chatId: chatId,
                          text: text
                        );

                        log.Info($"Stats sent to {chatId}");
                    }

                    if (item.IsOutOfSync())
                    {
                        await botClient.SendTextMessageAsync(
                            chatId: config.AdminId,
                            text: "Stats out of sync."
                        );

                        log.Info("Price out of sync.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task RunBotArgs(string[] args, ITelegramBotClient botClient)
        {
            return;
        }

        private void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
            log.Info($"ChatId: {e.Message.Chat.Id}, Message: {e.Message.Text}");
        }
    }
}
