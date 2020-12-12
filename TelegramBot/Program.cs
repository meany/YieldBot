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
using Telegram.Bot.Types.ReplyMarkups;

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
                    log.Info("BotWatch = false, sending ad-hoc message");
                    return;                   
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task RunBotArgs(string[] args, ITelegramBotClient botClient)
        {
            log.Info("Running with args");
            return;
        }

        private async void BotClient_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text == null || !e.Message.Text.StartsWith('/'))
                return;

            string cmd = e.Message.Text.Substring(1);
            log.Info($"ChatId: {e.Message.Chat.Id}, Command: {cmd}");

            string reply = await GetCmdReply(cmd);
            if (string.IsNullOrEmpty(reply))
                return;

            await botClient.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: reply,
              parseMode: ParseMode.Html,
              disableNotification: true
            );
        }

        private async Task<string> GetCmdReply(string cmd)
        {
            string reply = string.Empty;

            switch (cmd)
            {
                case "price":
                    var price = await Data.Common.GetPrices(db);
                    reply = $"$ <b>{price.PriceUSD.FormatUsd()}</b>\n" +
                        $"₿ <b>{price.PriceBTC.FormatBtc()}</b>\n" +
                        $"Ξ <b>{price.PriceETH.FormatEth()}</b>";
                    break;
                case "supply":
                    var supply = await Data.Common.GetStats(db);
                    reply = $"Supply: <b>{supply.Stat.Supply.FormatYld()}</b> $YLD\n" +
                        $"Circulation: <b>{supply.Stat.Circulation.FormatYld()}</b> $YLD";
                    break;
                case "mcap":
                    var mcap = await Data.Common.GetPrices(db);
                    reply = $"Market Cap: $ <b>{mcap.MarketCapUSD.FormatLarge()}</b>\n" +
                        $"Volume (24h): $ <b>{mcap.VolumeUSD.FormatLarge()}</b>";
                    break;
            }

            return reply;
        }
    }
}
