using CoinGecko.Clients;
using CoinGecko.Entities.Response.Coins;
using dm.YLD.Data;
using dm.YLD.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace dm.YLD.Prices
{
    class Program
    {
        private IServiceProvider services;
        private IConfigurationRoot configuration;
        private AppDbContext db;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private CoinFullDataById data;

        public static void Main(string[] args)
            => new Program().MainAsync(args).GetAwaiter().GetResult();

        public async Task MainAsync(string[] args)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Config.Prices.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("Config.Prices.Local.json", optional: true, reloadOnChange: true);

                configuration = builder.Build();

                services = new ServiceCollection()
                    .Configure<Config>(configuration)
                    .AddDatabase<AppDbContext>(configuration.GetConnectionString("Database"))
                    .BuildServiceProvider();
                db = services.GetService<AppDbContext>();

                if (db.Database.GetPendingMigrations().Count() > 0)
                {
                    log.Info("Migrating database");
                    db.Database.Migrate();
                }

                await Start();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task Start()
        {
            try
            {
                log.Info("Getting info");
                var stat = db.Stats
                    .AsNoTracking()
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefault();

                await GetInfo();

                // market cap
                decimal mktCapUsd = decimal.Parse(data.MarketData.MarketCap["usd"].Value.ToString());
                decimal mktCapUsdChgAmt = (data.MarketData.MarketCapChange24HInCurrency.Count == 0) ? 0 : decimal.Parse(data.MarketData.MarketCapChange24HInCurrency["usd"].ToString(), NumberStyles.Any);
                Change mktCapUsdChg = (mktCapUsdChgAmt > 0) ? Change.Up : (mktCapUsdChgAmt < 0) ? Change.Down : Change.None;
                decimal mktCapUsdChgPct = (data.MarketData.MarketCapChangePercentage24HInCurrency.Count == 0) ? 0 : decimal.Parse(data.MarketData.MarketCapChangePercentage24HInCurrency["usd"].ToString(), NumberStyles.Any);

                // volume
                int volumeUsd = (int)Math.Round(data.MarketData.TotalVolume["usd"].Value);

                // prices
                decimal priceBtc = decimal.Parse(data.MarketData.CurrentPrice["btc"].Value.ToString(), NumberStyles.Any);

                string changeBtc = "0";
                string changeEth = "0";
                string changeUsd = "0";
                string changeBtcPct = "0";
                string changeEthPct = "0";
                string changeUsdPct = "0";
                if (data.MarketData.PriceChange24HInCurrency.Count > 0 &&
                    data.MarketData.PriceChangePercentage24HInCurrency.Count > 0)
                {
                    changeBtc = data.MarketData.PriceChange24HInCurrency["btc"].ToString();
                    changeBtcPct = data.MarketData.PriceChangePercentage24HInCurrency["btc"].ToString();
                    changeEth = data.MarketData.PriceChange24HInCurrency["eth"].ToString();
                    changeEthPct = data.MarketData.PriceChangePercentage24HInCurrency["eth"].ToString();
                    changeUsd = data.MarketData.PriceChange24HInCurrency["usd"].ToString();
                    changeUsdPct = data.MarketData.PriceChangePercentage24HInCurrency["usd"].ToString();
                }

                decimal priceBtcChgAmt = decimal.Parse(changeBtc, NumberStyles.Any);
                Change priceBtcChg = (priceBtcChgAmt > 0) ? Change.Up : (priceBtcChgAmt < 0) ? Change.Down : Change.None;
                decimal priceBtcChgPct = decimal.Parse(changeBtcPct, NumberStyles.Any);

                decimal priceEth = decimal.Parse(data.MarketData.CurrentPrice["eth"].Value.ToString(), NumberStyles.Any);
                decimal priceEthChgAmt = decimal.Parse(changeEth, NumberStyles.Any);
                Change priceEthChg = (priceEthChgAmt > 0) ? Change.Up : (priceEthChgAmt < 0) ? Change.Down : Change.None;
                decimal priceEthChgPct = decimal.Parse(changeEthPct, NumberStyles.Any);

                decimal priceUsd = decimal.Parse(data.MarketData.CurrentPrice["usd"].Value.ToString(), NumberStyles.Any);
                decimal priceUsdChgAmt = decimal.Parse(changeUsd, NumberStyles.Any);
                Change priceUsdChg = (priceUsdChgAmt > 0) ? Change.Up : (priceUsdChgAmt < 0) ? Change.Down : Change.None;
                decimal priceUsdChgPct = decimal.Parse(changeUsdPct, NumberStyles.Any);

                var item = new Price
                {
                    Date = DateTime.UtcNow,
                    Group = stat.Group,
                    MarketCapUSD = int.Parse(Math.Round(mktCapUsd).ToString()),
                    MarketCapUSDChange = mktCapUsdChg,
                    MarketCapUSDChangePct = mktCapUsdChgPct,
                    PriceBTC = priceBtc,
                    PriceBTCChange = priceBtcChg,
                    PriceBTCChangePct = priceBtcChgPct,
                    PriceETH = priceEth,
                    PriceETHChange = priceEthChg,
                    PriceETHChangePct = priceEthChgPct,
                    PriceUSD = priceUsd,
                    PriceUSDChange = priceUsdChg,
                    PriceUSDChangePct = priceUsdChgPct,
                    VolumeUSD = volumeUsd,
                    Source = PriceSource.CoinGecko,
                };

                db.Add(item);

                log.Info("Saving prices to database");
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task GetInfo()
        {

            GetPrices();

            while (data == null)
                await Task.Delay(200);

        }

        private async void GetPrices()
        {
            try
            {
                var client = CoinGeckoClient.Instance;
                data = await client.CoinsClient.GetAllCoinDataWithId("yield", "false", true, true, false, false, false);

                log.Info($"GetPrices: OK");
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }
    }
}