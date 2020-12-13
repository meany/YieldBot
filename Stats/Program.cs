using dm.YLD.Common;
using dm.YLD.Data;
using dm.YLD.Data.Models;
using dm.YLD.Response;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Cache;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace dm.YLD.Stats
{
    class Program
    {
        private IServiceProvider services;
        private IConfigurationRoot configuration;
        private Config config;
        private AppDbContext db;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private BigInteger supply;
        private BigInteger teamAmt;
        private BigInteger xb5b9Amt;
        private BigInteger uniswapRfiAmt;
        private BigInteger uniswapEthAmt;
        private List<EsTxsResult> esTxs;
        private List<Transaction> dbTxs;

        public static void Main(string[] args)
            => new Program().MainAsync(args).GetAwaiter().GetResult();

        public async Task MainAsync(string[] args)
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Config.Stats.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("Config.Stats.Local.json", optional: true, reloadOnChange: true);

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
                log.Info("Getting Etherscan info");
                await GetInfo();
                await InsertNewTxs();

                dbTxs = db.Transactions
                    .AsNoTracking()
                    .OrderBy(x => x.TimeStamp)
                    .ToList();
                int totalTxs = (dbTxs.Count - 1) / 2;

                var circulation = supply - teamAmt - xb5b9Amt - uniswapRfiAmt - uniswapEthAmt;

                var item = new Stat
                {
                    Circulation = circulation.ToEth(),
                    Date = DateTime.UtcNow,
                    Group = Guid.NewGuid(),
                    Supply = supply.ToEth(),
                    Transactions = totalTxs
                };

                db.Add(item);

                log.Info("Saving transaction stats to database");
                db.SaveChanges();

                //

                log.Info("Building holders table");
                await BuildHolders();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task GetInfo()
        {
            try
            {
                var client = new RestClient("https://api.etherscan.io");
                await GetTxs(client);
                await Task.Delay(200);
                await GetSupply(client);
                await Task.Delay(200);
                await GetTeamAmount(client);
                await Task.Delay(200);
                await Getxb5b9Amount(client);
                await Task.Delay(200);
                await GetUniswapRFIAmount(client);
                await Task.Delay(200);
                await GetUniswapETHAmount(client);

                while (uniswapEthAmt == 0 ||
                    uniswapRfiAmt == 0 ||
                    xb5b9Amt == 0 ||
                    teamAmt == 0 ||
                    supply == 0 ||
                    esTxs == null)
                    await Task.Delay(200);

                client = null;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        private async Task GetSupply(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "stats");
            req.AddParameter("action", "tokensupply");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            supply = BigInteger.Parse(res.Data.Result);
            log.Info($"GetSupply: OK ({supply})");
        }

        private async Task GetTeamAmount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("address", "0x1E580e3Ced413ce93028B3FE5cfCe973e93E7EC8");
            req.AddParameter("tag", "latest");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            teamAmt = BigInteger.Parse(res.Data.Result);
            log.Info($"GetTeamAmount: OK ({teamAmt})");
        }

        private async Task Getxb5b9Amount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("address", "0xb5b93f7396af7e85353d9C4d900Ccbdbdac6a658");
            req.AddParameter("tag", "latest");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            xb5b9Amt = BigInteger.Parse(res.Data.Result);
            log.Info($"Get0xb5b9Amount: OK ({xb5b9Amt})");
        }

        private async Task GetUniswapRFIAmount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("address", "0x901380ab7b9e8a0ecfb14bd5219e4a7d762c656d");
            req.AddParameter("tag", "latest");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            uniswapRfiAmt = BigInteger.Parse(res.Data.Result);
            log.Info($"GetUniswapRFIAmount: OK ({uniswapRfiAmt})");
        }

        private async Task GetUniswapETHAmount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("address", "0x242d289b3eeb6842ce0fcc0d87932402299ae5b3");
            req.AddParameter("tag", "latest");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            uniswapEthAmt = BigInteger.Parse(res.Data.Result);
            log.Info($"GetUniswapETHAmount: OK ({uniswapEthAmt})");
        }

        private async Task GetTxs(RestClient client)
        {
            var lastTx = db.Transactions
                .AsNoTracking()
                .OrderByDescending(x => x.TimeStamp)
                .FirstOrDefault();

            int start = 0;
            if (lastTx != null)
                start = int.Parse(lastTx.BlockNumber) + 1;

            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokentx");
            req.AddParameter("contractaddress", "0xDcB01cc464238396E213a6fDd933E36796eAfF9f");
            req.AddParameter("startblock", start);
            req.AddParameter("endblock", "999999999");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsTxs>(req);
            if (res.Data.Result.Count == 0)
            {
                esTxs = new List<EsTxsResult>();
                log.Info($"GetTxs: {res.Data.Message} (0)");
                return;
            }

            esTxs = res.Data.Result
                .OrderBy(x => x.BlockNumber)
                .ToList();
            log.Info($"GetTxs: {res.Data.Message} ({esTxs.Count()}: {start} to {esTxs.Last().BlockNumber})");
        }

        private async Task InsertNewTxs()
        {
            if (esTxs.Count > 0)
            {
                log.Info("Inserting newest transactions");
                foreach (var tx in esTxs)
                {
                    var newTx = new Transaction
                    {
                        BlockNumber = tx.BlockNumber,
                        Hash = tx.Hash,
                        From = tx.From,
                        To = tx.To,
                        TimeStamp = tx.TimeStamp,
                        Value = tx.Value
                    };
                    db.Add(newTx);
                }
                await db.SaveChangesAsync();
            }
        }

        private async Task BuildHolders()
        {
            var holders = new List<Holder>();

            foreach (var item in dbTxs)
            {
                if (item.From == item.To)
                    continue;

                var fromHolder = holders.Where(x => x.Address == item.From).FirstOrDefault();
                if (fromHolder != null)
                {
                    var newValue = BigInteger.Parse(fromHolder.Value) - BigInteger.Parse(item.Value);
                    fromHolder.Value = newValue.ToString();
                }
                else
                {
                    holders.Add(new Holder
                    {
                        Address = item.From,
                        FirstBlockNumber = item.BlockNumber,
                        FirstTimeStamp = item.TimeStamp,
                        Value = $"-{item.Value}"
                    });
                }

                var toHolder = holders.Where(x => x.Address == item.To).FirstOrDefault();
                if (toHolder != null)
                {
                    var newValue = BigInteger.Parse(toHolder.Value) + BigInteger.Parse(item.Value);
                    toHolder.Value = newValue.ToString();
                }
                else
                {
                    holders.Add(new Holder
                    {
                        Address = item.To,
                        FirstBlockNumber = item.BlockNumber,
                        FirstTimeStamp = item.TimeStamp,
                        Value = item.Value
                    });
                }
            }

            await db.TruncateAsync<Holder>();

            // filter out:
            // - non-holders
            // - the 0x0000... acct
            // - the 0x1e58... (first acct)
            // - the 0xb5b9... (coiner req acct)
            db.AddRange(holders.Where(x => x.Value != "0" &&
                !x.Value.Contains('-') &&
                x.Address != "0x1e580e3ced413ce93028b3fe5cfce973e93e7ec8" &&
                x.Address != "0xb5b93f7396af7e85353d9c4d900ccbdbdac6a658" &&
                x.Address != "0x901380ab7b9e8a0ecfb14bd5219e4a7d762c656d" &&
                x.Address != "0x242d289b3eeb6842ce0fcc0d87932402299ae5b3"));
            await db.SaveChangesAsync();
        }
    }
}