using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dm.YLD.Data;
using dm.YLD.Data.Models;
using dm.YLD.Data.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dm.YLD.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class DataController : ControllerBase
    {
        private AppDbContext db;

        public DataController(AppDbContext db)
        {
            this.db = db;
        }

        // GET data
        [HttpGet]
        public async Task<ActionResult<AllInfo>> GetAllInfo()
        {
            return await Data.Common.GetAllInfo(db);
        }

        // GET data/supply
        [HttpGet]
        [Route("supply")]
        public async Task<ActionResult<decimal>> GetSupply()
        {
            var item = await Data.Common.GetStats(db);
            return item.Supply;
        }
    }
}
