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
        private BigInteger uniswapRfiAmt;
        private BigInteger gardenRfiSupply;
        //private BigInteger firstRfiAmt;
        private BigInteger uniswapEthAmt;
        private BigInteger gardenEthSupply;
        //private BigInteger firstEthAmt;
        private List<EsTxsResult> esTxs;
        private List<Transaction> dbTxs;
        //private List<EsTxsResult> esLpTxs;
        //private List<LPTransaction> dbLpTxs;
        private List<EsTxsResult> esGardenTxs;
        private List<GardenTransaction> dbGardenTxs;

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
                //await InsertNewLpTxs();
                await InsertNewGardenTxs();

                dbTxs = db.Transactions
                    .AsNoTracking()
                    .OrderBy(x => x.TimeStamp)
                    .ToList();
                int totalTxs = (dbTxs.Count - 1) / 2;

                var fullCirculation = supply - teamAmt;
                var holderCirculation = fullCirculation - uniswapRfiAmt - uniswapEthAmt;
                //var rfiSupply = gardenRfiSupply - firstRfiAmt;
                //var ethSupply = gardenEthSupply - firstEthAmt;

                var item = new Stat
                {
                    FullCirculation = fullCirculation.ToEth(),
                    HolderCirculation = holderCirculation.ToEth(),
                    Date = DateTime.UtcNow,
                    Group = Guid.NewGuid(),
                    Supply = supply.ToEth(),
                    UniswapRFIAmount = uniswapRfiAmt.ToEth(),
                    UniswapETHAmount = uniswapEthAmt.ToEth(),
                    GardenRFISupply = gardenRfiSupply.ToEth(),
                    GardenETHSupply = gardenEthSupply.ToEth(),
                    Transactions = totalTxs
                };

                db.Add(item);

                log.Info("Saving transaction stats to database");
                db.SaveChanges();

                //

                log.Info("Building holders table");
                await BuildHolders();

                //log.Info("Building LP holders table");
                //dbLpTxs = db.LPTransactions
                //    .AsNoTracking()
                //    .OrderBy(x => x.TimeStamp)
                //    .ToList();
                //await BuildLPHolders();

                log.Info("Building garden holders table");
                dbGardenTxs = db.GardenTransactions
                    .AsNoTracking()
                    .OrderBy(x => x.TimeStamp)
                    .ToList();
                await BuildGardenHolders();

                log.Info("Complete");
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
                await Task.Delay(400);
                await GetGardenTxs(client);
                await Task.Delay(400);
                await GetSupply(client);
                await Task.Delay(400);
                await GetTeamAmount(client);
                await Task.Delay(400);
                await GetUniswapRFIAmount(client);
                await Task.Delay(400);
                await GetGardenRFISupply(client);
                await Task.Delay(400);
                await GetUniswapETHAmount(client);
                await Task.Delay(400);
                await GetGardenETHSupply(client);

                while (gardenEthSupply == 0 ||
                    uniswapEthAmt == 0 ||
                    gardenRfiSupply == 0 ||
                    uniswapRfiAmt == 0 ||
                    teamAmt == 0 ||
                    supply == 0 ||
                    esGardenTxs == null ||
                    esTxs == null)
                    await Task.Delay(250);

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
            req.AddParameter("contractaddress", Statics.TOKEN_YLD);
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            supply = BigInteger.Parse(res.Data.Result);
            log.Info($"GetSupply: OK ({supply})");
        }

        private async Task GetGardenRFISupply(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", Statics.TOKEN_UNISWAP_RFI);
            req.AddParameter("address", Statics.TOKEN_RFIYLD_GARDEN);
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            gardenRfiSupply = BigInteger.Parse(res.Data.Result);
            log.Info($"GetGardenRFISupply: OK ({gardenRfiSupply})");
        }

        private async Task GetGardenETHSupply(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", Statics.TOKEN_UNISWAP_ETH);
            req.AddParameter("address", Statics.TOKEN_ETHYLD_GARDEN);
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            gardenEthSupply = BigInteger.Parse(res.Data.Result);
            log.Info($"GetGardenETHSupply: OK ({gardenEthSupply})");
        }

        private async Task GetTeamAmount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", Statics.TOKEN_YLD);
            req.AddParameter("address", Statics.ADDRESS_FIRST);
            req.AddParameter("tag", "latest");
            req.AddParameter("apikey", config.EtherscanToken);

            var res = await client.ExecuteAsync<EsToken>(req);
            teamAmt = BigInteger.Parse(res.Data.Result);
            log.Info($"GetTeamAmount: OK ({teamAmt})");
        }

        private async Task GetUniswapRFIAmount(RestClient client)
        {
            var req = new RestRequest("api", Method.GET);
            req.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req.AddParameter("module", "account");
            req.AddParameter("action", "tokenbalance");
            req.AddParameter("contractaddress", Statics.TOKEN_YLD);
            req.AddParameter("address", Statics.TOKEN_UNISWAP_RFI);
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
            req.AddParameter("contractaddress", Statics.TOKEN_YLD);
            req.AddParameter("address", Statics.TOKEN_UNISWAP_ETH);
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
            req.AddParameter("contractaddress", Statics.TOKEN_YLD);
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

        //private async Task GetLpTxs(RestClient client)
        //{
        //    var lastTx = db.LPTransactions
        //        .AsNoTracking()
        //        .OrderByDescending(x => x.TimeStamp)
        //        .FirstOrDefault();

        //    int start = 0;
        //    if (lastTx != null)
        //        start = int.Parse(lastTx.BlockNumber) + 1;

        //    var req1 = new RestRequest("api", Method.GET);
        //    req1.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        //    req1.AddParameter("module", "account");
        //    req1.AddParameter("action", "tokentx");
        //    req1.AddParameter("contractaddress", Statics.TOKEN_UNISWAP_RFI);
        //    req1.AddParameter("startblock", start);
        //    req1.AddParameter("endblock", "999999999");
        //    req1.AddParameter("apikey", config.EtherscanToken);

        //    var res1 = await client.ExecuteAsync<EsTxs>(req1);
            
        //    var req2 = new RestRequest("api", Method.GET);
        //    req2.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        //    req2.AddParameter("module", "account");
        //    req2.AddParameter("action", "tokentx");
        //    req2.AddParameter("contractaddress", Statics.TOKEN_UNISWAP_ETH);
        //    req2.AddParameter("startblock", start);
        //    req2.AddParameter("endblock", "999999999");
        //    req2.AddParameter("apikey", config.EtherscanToken);

        //    var res2 = await client.ExecuteAsync<EsTxs>(req2);

        //    var res = res1.Data.Result.Concat(res2.Data.Result).ToList();

        //    if (res.Count == 0)
        //    {
        //        esLpTxs = new List<EsTxsResult>();
        //        log.Info($"GetLpTxs: 0");
        //        return;
        //    }

        //    esLpTxs = res.OrderBy(x => x.BlockNumber).ToList();
        //    log.Info($"GetLpTxs: {esLpTxs.Count()}: {start} to {esLpTxs.Last().BlockNumber}");
        //}

        private async Task GetGardenTxs(RestClient client)
        {
            var lastTx = db.GardenTransactions
                .AsNoTracking()
                .OrderByDescending(x => x.TimeStamp)
                .FirstOrDefault();

            int start = 0;
            if (lastTx != null)
                start = int.Parse(lastTx.BlockNumber) + 1;

            var req1 = new RestRequest("api", Method.GET);
            req1.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req1.AddParameter("module", "account");
            req1.AddParameter("action", "tokentx");
            req1.AddParameter("address", Statics.TOKEN_RFIYLD_GARDEN);
            req1.AddParameter("startblock", start);
            req1.AddParameter("endblock", "999999999");
            req1.AddParameter("apikey", config.EtherscanToken);

            var res1 = await client.ExecuteAsync<EsTxs>(req1);

            var req2 = new RestRequest("api", Method.GET);
            req2.AddParameter("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            req2.AddParameter("module", "account");
            req2.AddParameter("action", "tokentx");
            req2.AddParameter("address", Statics.TOKEN_ETHYLD_GARDEN);
            req2.AddParameter("startblock", start);
            req2.AddParameter("endblock", "999999999");
            req2.AddParameter("apikey", config.EtherscanToken);

            var res2 = await client.ExecuteAsync<EsTxs>(req2);

            var res = res1.Data.Result.Concat(res2.Data.Result).ToList();

            if (res.Count == 0)
            {
                esGardenTxs = new List<EsTxsResult>();
                log.Info($"GetGardenTxs: 0");
                return;
            }

            esGardenTxs = res.OrderBy(x => x.BlockNumber).ToList();
            log.Info($"GetGardenTxs: {esGardenTxs.Count()}: {start} to {esGardenTxs.Last().BlockNumber}");
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

        //private async Task InsertNewLpTxs()
        //{
        //    if (esLpTxs.Count > 0)
        //    {
        //        log.Info("Inserting newest LP transactions");
        //        foreach (var tx in esLpTxs)
        //        {
        //            if (tx.From == tx.To)
        //                continue;

        //            var pair = Utils.GetLPPair(tx.ContractAddress);
        //            var newTx = new LPTransaction
        //            {
        //                BlockNumber = tx.BlockNumber,
        //                Hash = tx.Hash,
        //                From = tx.From,
        //                To = tx.To,
        //                Pair = pair,
        //                TimeStamp = tx.TimeStamp,
        //                Value = tx.Value
        //            };
        //            db.Add(newTx);
        //        }
        //        await db.SaveChangesAsync();
        //    }
        //}

        private async Task InsertNewGardenTxs()
        {
            if (esGardenTxs.Count > 0)
            {
                log.Info("Inserting newest garden transactions");
                foreach (var tx in esGardenTxs)
                {
                    if (tx.From == tx.To)
                        continue;

                    var pair = Utils.GetLPPair(tx.ContractAddress);
                    var newTx = new GardenTransaction
                    {
                        BlockNumber = tx.BlockNumber,
                        Hash = tx.Hash,
                        From = tx.From,
                        To = tx.To,
                        Pair = pair,
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

            db.AddRange(holders.Where(x => x.Value != "0" &&
                !x.Value.Contains('-') &&
                x.Address != Statics.ADDRESS_FIRST &&
                x.Address != Statics.ADDRESS_COINER &&
                x.Address != Statics.ADDRESS_FIRST_RFI &&
                x.Address != Statics.TOKEN_UNISWAP_RFI &&
                x.Address != Statics.TOKEN_UNISWAP_ETH &&
                x.Address != Statics.TOKEN_ETHYLD_GARDEN &&
                x.Address != Statics.TOKEN_RFIYLD_GARDEN));
            await db.SaveChangesAsync();
        }

        //private async Task BuildLPHolders()
        //{
        //    var holders = new List<LPHolder>();

        //    foreach (var item in dbLpTxs)
        //    {
        //        if (item.From == item.To)
        //            continue;

        //        var fromHolder = holders.Where(x => x.Address == item.From && x.Pair == item.Pair).FirstOrDefault();
        //        if (fromHolder != null)
        //        {
        //            var newValue = BigInteger.Parse(fromHolder.Value) - BigInteger.Parse(item.Value);
        //            fromHolder.Value = newValue.ToString();
        //        }
        //        else
        //        {
        //            holders.Add(new LPHolder
        //            {
        //                Address = item.From,
        //                Pair = item.Pair,
        //                FirstBlockNumber = item.BlockNumber,
        //                FirstTimeStamp = item.TimeStamp,
        //                Value = $"-{item.Value}"
        //            });
        //        }

        //        var toHolder = holders.Where(x => x.Address == item.To && x.Pair == item.Pair).FirstOrDefault();
        //        if (toHolder != null)
        //        {
        //            var newValue = BigInteger.Parse(toHolder.Value) + BigInteger.Parse(item.Value);
        //            toHolder.Value = newValue.ToString();
        //        }
        //        else
        //        {
        //            holders.Add(new LPHolder
        //            {
        //                Address = item.To,
        //                Pair = item.Pair,
        //                FirstBlockNumber = item.BlockNumber,
        //                FirstTimeStamp = item.TimeStamp,
        //                Value = item.Value
        //            });
        //        }
        //    }

        //    await db.TruncateAsync<LPHolder>();

        //    db.AddRange(holders.Where(x => x.Value != "0" &&
        //        !x.Value.Contains('-') &&
        //        x.Address != Statics.ADDRESS_FIRST &&
        //        x.Address != Statics.ADDRESS_COINER &&
        //        x.Address != Statics.ADDRESS_FIRST_RFI &&
        //        x.Address != Statics.TOKEN_UNISWAP_RFI &&
        //        x.Address != Statics.TOKEN_UNISWAP_ETH &&
        //        x.Address != Statics.TOKEN_ETHYLD_GARDEN &&
        //        x.Address != Statics.TOKEN_RFIYLD_GARDEN));
        //    await db.SaveChangesAsync();
        //}

        private async Task BuildGardenHolders()
        {
            var holders = new List<GardenHolder>();

            foreach (var item in dbGardenTxs)
            {
                if (item.From == item.To)
                    continue;

                var fromHolder = holders.Where(x => x.Address == item.From && x.Pair == item.Pair).FirstOrDefault();
                if (fromHolder != null)
                {
                    var newValue = BigInteger.Parse(fromHolder.Value) - BigInteger.Parse(item.Value);
                    fromHolder.Value = newValue.ToString();
                }
                else
                {
                    holders.Add(new GardenHolder
                    {
                        Address = item.From,
                        Pair = item.Pair,
                        FirstBlockNumber = item.BlockNumber,
                        FirstTimeStamp = item.TimeStamp,
                        Value = item.Value
                    });
                }

                var toHolder = holders.Where(x => x.Address == item.To && x.Pair == item.Pair).FirstOrDefault();
                if (toHolder != null)
                {
                    var newValue = BigInteger.Parse(toHolder.Value) + BigInteger.Parse(item.Value);
                    toHolder.Value = newValue.ToString();
                }
                else
                {
                    holders.Add(new GardenHolder
                    {
                        Address = item.To,
                        Pair = item.Pair,
                        FirstBlockNumber = item.BlockNumber,
                        FirstTimeStamp = item.TimeStamp,
                        Value = $"-{item.Value}"
                    });
                }
            }

            await db.TruncateAsync<GardenHolder>();

            db.AddRange(holders.Where(x => x.Value != "0" &&
                !x.Value.Contains('-') &&
                x.Address != Statics.ADDRESS_FIRST &&
                x.Address != Statics.ADDRESS_COINER &&
                x.Address != Statics.ADDRESS_FIRST_RFI &&
                x.Address != Statics.TOKEN_UNISWAP_RFI &&
                x.Address != Statics.TOKEN_UNISWAP_ETH &&
                x.Address != Statics.TOKEN_ETHYLD_GARDEN &&
                x.Address != Statics.TOKEN_RFIYLD_GARDEN));
            await db.SaveChangesAsync();
        }
    }
}