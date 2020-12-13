using dm.YLD.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace dm.YLD.Data
{
    public static class Common
    {
        public static async Task<ViewModels.Stats> GetStatsAndPrices(AppDbContext db)
        {
            var vm = new ViewModels.Stats();
            vm.Stat = await GetStats(db);
            vm.Price = await GetPrices(db, vm.Stat.Group);

            if (vm.Price == null)
            {
                vm.Price = await db.Prices
                    .AsNoTracking()
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
            }

            return vm;
        }

        public static async Task<Price> GetPrices(AppDbContext db, Guid group = new Guid())
        {
            return await db.Prices
                .AsNoTracking()
                .Where(x => group != new Guid() && x.Group == group)
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public static async Task<Stat> GetStats(AppDbContext db)
        {
            return await db.Stats
                .AsNoTracking()
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        public static async Task<List<Holder>> GetTop(AppDbContext db, int takeAmt)
        {
            var items = await db.Holders
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false);

            return items.OrderByDescending(x => BigInteger.Parse(x.Value))
                .Take(takeAmt)
                .ToList();
        }

        public static async Task<int> GetHolders(AppDbContext db)
        {
            return await db.Holders
                .AsNoTracking()
                .CountAsync()
                .ConfigureAwait(false);
        }
    }
}
