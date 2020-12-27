using dm.YLD.Common;
using dm.YLD.Data;
using dm.YLD.Data.Models;
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
        private string cmdList;
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

                cmdList = File.ReadAllText("cmds.txt");

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
            if (cmd.Contains('@'))
                cmd = cmd.Split('@')[0];

            if (e.Message.Date.AddMinutes(1) <= DateTime.UtcNow)
            {
                log.Info($"(old: ignoring) ChatId: {e.Message.Chat.Id}, Command: {cmd}, Args: {args}");
                return;
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
                case "start":
                    return cmdList;

                case "price":
                    var price = await Data.Common.GetPrices(db);
                    return $"$ <b>{price.PriceUSD.FormatUsd()}</b>\n" +
                        $"₿ <b>{price.PriceBTC.FormatBtc()}</b>\n" +
                        $"Ξ <b>{price.PriceETH.FormatEth()}</b>";

                case "supply":
                    var supply = await Data.Common.GetStats(db);
                    return $"Supply: <b>{supply.Supply.FormatYld()}</b> $YLD\n" +
                        $"Circulation: <b>{supply.FullCirculation.FormatYld()}</b> $YLD";

                case "mcap":
                    var mcap = await Data.Common.GetPrices(db);
                    return $"Market Cap: $ <b>{mcap.MarketCapUSD.FormatLarge()}</b>\n" +
                        $"Volume (24h): $ <b>{mcap.VolumeUSD.FormatLarge()}</b>";

                case "top":
                    if (!int.TryParse(args, out int topAmt))
                        topAmt = 10;

                    var tops = await Data.Common.GetTopHolders(db, topAmt);
                    string reply = string.Empty;
                    for (int i = 0; i < tops.Count; i++)
                    {
                        var item = tops[i];
                        var value = BigInteger.Parse(item.Value);
                        var url = $"https://etherscan.io/token/{Statics.TOKEN_YLD}?a=" +
                            item.Address;
                        var shortAddr = item.Address.Substring(0, 10);
                        reply += $"<i>{i + 1}</i>. <a href='{url}'>{shortAddr}</a>: <b>{value.ToEth().FormatYld()}</b> $YLD\n";
                    }
                    return reply;

                case "rtop":
                    if (!int.TryParse(args, out int rtopAmt))
                        rtopAmt = 10;

                    var rtops = await Data.Common.GetTopGardenHolders(db, LPPair.RFI_YLD, rtopAmt);
                    string rreply = string.Empty;
                    for (int i = 0; i < rtops.Count; i++)
                    {
                        var item = rtops[i];
                        var value = BigInteger.Parse(item.Value);
                        var url = $"https://etherscan.io/token/{Statics.TOKEN_RFIYLD_GARDEN}?a=" +
                            item.Address;
                        var shortAddr = item.Address.Substring(0, 10);
                        rreply += $"<i>{i + 1}</i>. <a href='{url}'>{shortAddr}</a>: <b>{value.ToEth().FormatEth()}</b> RFI-YLD\n";
                    }
                    return rreply;

                case "etop":
                    if (!int.TryParse(args, out int etopAmt))
                        etopAmt = 10;

                    var etops = await Data.Common.GetTopGardenHolders(db, LPPair.ETH_YLD, etopAmt);
                    string ereply = string.Empty;
                    for (int i = 0; i < etops.Count; i++)
                    {
                        var item = etops[i];
                        var value = BigInteger.Parse(item.Value);
                        var url = $"https://etherscan.io/token/{Statics.TOKEN_ETHYLD_GARDEN}?a=" +
                            item.Address;
                        var shortAddr = item.Address.Substring(0, 10);
                        ereply += $"<i>{i + 1}</i>. <a href='{url}'>{shortAddr}</a>: <b>{value.ToEth().FormatEth()}</b> ETH-YLD\n";
                    }
                    return ereply;

                case "holders":
                    var holders = await Data.Common.GetTotalHolders(db);
                    return $"<b>{holders.Format()}</b> total $YLD holders";

                case "share":
                    if (decimal.TryParse(args.Replace(",", string.Empty), out decimal yldAmt))
                    {
                        if (yldAmt <= 0)
                            return "Amount must be greater than 0.";

                        var tstats = await Data.Common.GetTopHolders(db, int.MaxValue);
                        decimal ttotal = tstats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        if (yldAmt > ttotal)
                            return $"Amount must be less than the top 100 holder circulation ({ttotal.FormatYld()})";

                        decimal pct = yldAmt / ttotal * 100;
                        decimal airdrop = pct / 100 * 25000;

                        var tlast = tstats.Take(100).Last();
                        if (yldAmt <= tlast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = tstats.Count(x => x.ValueBigInt.ToEth() >= yldAmt) + 1;

                        return $"<b>{pct.FormatEth()}%</b>\n" +
                            $"<i>({yldAmt} ÷ {ttotal.FormatYld()} $YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }
                    else if (args.Contains('%') &&
                        decimal.TryParse(args.TrimEnd('%', ' ').Replace(",", string.Empty), out decimal yldPct))
                    {
                        if (yldPct <= 0 || yldPct >= 100)
                            return "Percentage must be greater than 0 and less than 100.";

                        var tstats = await Data.Common.GetTopHolders(db, int.MaxValue);
                        decimal ttotal = tstats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        decimal amt = yldPct / 100 * ttotal;
                        decimal airdrop = yldPct / 100 * 25000;
                        var tlast = tstats.Take(100).Last();
                        if (amt <= tlast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = tstats.Count(x => x.ValueBigInt.ToEth() >= amt) + 1;

                        return $"<b>{amt.FormatYld()}</b> $YLD\n" +
                            $"<i>({yldPct}% × {ttotal.FormatYld()} $YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }

                    return string.Empty;

                case "rshare":
                    if (decimal.TryParse(args.Replace(",", string.Empty), out decimal rAmt))
                    {
                        if (rAmt <= 0)
                            return "Amount must be greater than 0.";

                        var rstats = await Data.Common.GetTopGardenHolders(db, LPPair.RFI_YLD, int.MaxValue);
                        decimal rtotal = rstats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        if (rAmt > rtotal)
                            return $"Amount must be less than the top 100 RFI-YLD supply ({rtotal.FormatEth()})";

                        decimal pct = rAmt / rtotal * 100;
                        decimal airdrop = pct / 100 * 13500;

                        var rlast = rstats.Take(100).Last();
                        if (rAmt <= rlast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = rstats.Count(x => x.ValueBigInt.ToEth() >= rAmt) + 1;

                        return $"<b>{pct.FormatEth()}%</b>\n" +
                            $"<i>({rAmt} ÷ {rtotal.FormatEth()} RFI-YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }
                    else if (args.Contains('%') &&
                        decimal.TryParse(args.TrimEnd('%', ' ').Replace(",", string.Empty), out decimal rPct))
                    {
                        if (rPct <= 0 || rPct >= 100)
                            return "Percentage must be greater than 0 and less than 100.";

                        var rstats = await Data.Common.GetTopGardenHolders(db, LPPair.RFI_YLD, int.MaxValue);
                        decimal rtotal = rstats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        decimal amt = rPct / 100 * rtotal;
                        decimal airdrop = rPct / 100 * 13500;

                        var rlast = rstats.Take(100).Last();
                        if (amt <= rlast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = rstats.Count(x => x.ValueBigInt.ToEth() >= amt) + 1;

                        return $"<b>{amt.FormatEth()}</b> RFI-YLD\n" +
                            $"<i>({rPct}% × {rtotal.FormatEth()} RFI-YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }

                    return string.Empty;

                case "eshare":
                    if (decimal.TryParse(args.Replace(",", string.Empty), out decimal eAmt))
                    {
                        if (eAmt <= 0)
                            return "Amount must be greater than 0.";

                        var estats = await Data.Common.GetTopGardenHolders(db, LPPair.ETH_YLD, int.MaxValue);
                        decimal etotal = estats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        if (eAmt > etotal)
                            return $"Amount must be less than the top 100 ETH-YLD supply ({etotal.FormatEth()})";

                        decimal pct = eAmt / etotal * 100;
                        decimal airdrop = pct / 100 * 11500;

                        var elast = estats.Take(100).Last();
                        if (eAmt <= elast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = estats.Count(x => x.ValueBigInt.ToEth() >= eAmt) + 1;

                        return $"<b>{pct.FormatEth()}%</b>\n" +
                            $"<i>({eAmt} ÷ {etotal.FormatEth()} ETH-YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }
                    else if (args.Contains('%') &&
                        decimal.TryParse(args.TrimEnd('%', ' ').Replace(",", string.Empty), out decimal ePct))
                    {
                        if (ePct <= 0 || ePct >= 100)
                            return "Percentage must be greater than 0 and less than 100.";

                        var estats = await Data.Common.GetTopGardenHolders(db, LPPair.ETH_YLD, int.MaxValue);
                        decimal etotal = estats.Take(100).Select(x => x.ValueBigInt).Aggregate(BigInteger.Add).ToEth();

                        decimal amt = ePct / 100 * etotal;
                        decimal airdrop = ePct / 100 * 11500;

                        var elast = estats.Take(100).Last();
                        if (amt <= elast.ValueBigInt.ToEth())
                            airdrop = 0;

                        var position = estats.Count(x => x.ValueBigInt.ToEth() >= amt) + 1;

                        return $"<b>{amt.FormatEth()}</b> ETH-YLD\n" +
                            $"<i>({ePct}% × {etotal.FormatEth()} ETH-YLD)</i>";
                        //$"\n" +
                        //$"Airdrop: <b>{airdrop.FormatUsd()}</b> $YLD\n" +
                        //$"Position: #<b>{position}</b>";
                    }

                    return string.Empty;
            }

            return string.Empty;
        }
    }
}
