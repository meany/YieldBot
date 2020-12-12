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
        public async Task<ActionResult<Stats>> Get()
        {
            return await Data.Common.GetStats(db);
        }
    }
}
