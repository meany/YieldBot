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
        public static async Task<ViewModels.Stats> GetStats(AppDbContext db)
        {
            var vm = new ViewModels.Stats();

            var stat = await db.Stats
                .AsNoTracking()
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            vm.Stat = stat;
            var price = await db.Prices
                .AsNoTracking()
                .Where(x => x.Group == stat.Group)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            vm.Price = price;

            if (price == null)
            {
                price = await db.Prices
                    .AsNoTracking()
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                vm.Price = price;
            }

            return vm;
        }

        public static async Task<Price> GetPrices(AppDbContext db)
        {
            return await db.Prices
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
    }
}
