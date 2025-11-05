using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers
{
    public class ConnectorController : Controller
    {
        private readonly AppDbContext _db;
        public async Task<IActionResult> GetTransacts(DateTime from, DateTime to)
        {
            List<Transaction> transactions = new List<Transaction>();
            transactions = await _db.Transactions.ToListAsync();
            return View();
        }
    }
}
