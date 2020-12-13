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
using System.Numerics;
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

            string cmdAndArgs = e.Message.Text.Substring(1).Trim();
            string cmd = cmdAndArgs;
            string args = string.Empty;
            if (cmdAndArgs.Contains(' '))
            {
                int firstSpace = cmdAndArgs.IndexOf(' ');
                cmd = cmdAndArgs.Substring(0, firstSpace);
                args = cmdAndArgs.Substring(firstSpace + 1).Trim();
            }

            log.Info($"ChatId: {e.Message.Chat.Id}, Command: {cmd}, Args: {args}");

            // TODO: add request/rate limit

            string reply = await GetCmdReply(cmd, args);
            if (string.IsNullOrEmpty(reply))
                return;

            await botClient.SendTextMessageAsync(
              chatId: e.Message.Chat,
              text: reply,
              parseMode: ParseMode.Html,
              disableNotification: true,
              disableWebPagePreview: true
            );
        }

        private async Task<string> GetCmdReply(string cmd, string args)
        {
            switch (cmd)
            {
                case "price":
                    var price = await Data.Common.GetPrices(db);
                    return $"$ <b>{price.PriceUSD.FormatUsd()}</b>\n" +
                        $"₿ <b>{price.PriceBTC.FormatBtc()}</b>\n" +
                        $"Ξ <b>{price.PriceETH.FormatEth()}</b>";

                case "supply":
                    var supply = await Data.Common.GetStatsAndPrices(db);
                    return $"Supply: <b>{supply.Stat.Supply.FormatYld()}</b> $YLD\n" +
                        $"Circulation: <b>{supply.Stat.Circulation.FormatYld()}</b> $YLD";

                case "mcap":
                    var mcap = await Data.Common.GetPrices(db);
                    return $"Market Cap: $ <b>{mcap.MarketCapUSD.FormatLarge()}</b>\n" +
                        $"Volume (24h): $ <b>{mcap.VolumeUSD.FormatLarge()}</b>";

                case "top":
                    if (!int.TryParse(args, out int topAmt))
                        topAmt = 10;

                    var tops = await Data.Common.GetTop(db, topAmt);
                    string reply = string.Empty;
                    for (int i = 0; i < topAmt; i++)
                    {
                        var item = tops[i];
                        var value = BigInteger.Parse(item.Value);
                        var url = $"https://etherscan.io/token/0xdcb01cc464238396e213a6fdd933e36796eaff9f?a=" +
                            item.Address;
                        var shortAddr = item.Address.Substring(0, 10);
                        reply += $"<i>{i + 1}</i>. <a href='{url}'>{shortAddr}</a>: <b>{value.ToEth().FormatYld()}</b>\n";
                    }
                    return reply;

                case "holders":
                    var holders = await Data.Common.GetHolders(db);
                    return $"<b>{holders.Format()}</b> total holders";

                case "share":
                case "tshare":
                    if (decimal.TryParse(args.Replace(",", string.Empty), out decimal yldAmt))
                    {
                        if (yldAmt <= 0)
                            return "Amount must be greater than 0.";

                        var tstats = await Data.Common.GetStats(db);

                        if (yldAmt > tstats.Circulation)
                            return $"Amount must be less than the total circulation ({tstats.Circulation.FormatYld()})";
                        
                        var pct = yldAmt / tstats.Circulation * 100;
                        return $"<b>{pct.FormatEth()}%</b>\n" +
                            $"<i>({yldAmt} ÷ {tstats.Circulation.FormatYld()})</i>";
                    }
                    else if (args.Contains('%') &&
                        decimal.TryParse(args.TrimEnd('%', ' ').Replace(",", string.Empty), out decimal yldPct))
                    {
                        if (yldPct <= 0 || yldPct >= 100)
                            return "Percentage must be greater than 0 and less than 100.";

                        var tstats = await Data.Common.GetStats(db);
                        var amt = yldPct / 100 * tstats.Circulation;
                        return $"<b>{amt.FormatYld()}</b>\n" +
                            $"<i>({yldPct}% × {tstats.Circulation.FormatYld()})</i>";
                    }

                    return string.Empty;

                case "rshare":
                    return new NotImplementedException().Message;
                case "eshare":
                    return new NotImplementedException().Message;
            }

            return string.Empty;
        }
    }
}
