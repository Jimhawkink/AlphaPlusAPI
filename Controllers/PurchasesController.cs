using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AlphaPlusAPI.Services;
using AlphaPlusAPI.Models;
using AlphaPlusAPI.DTOs;

namespace AlphaPlusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PurchasesController : ControllerBase
    {
        private readonly SyncService _syncService;

        public PurchasesController(SyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Purchase>>>> GetPurchases(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var result = await _syncService.GetPurchasesAsync(fromDate, toDate);
            return Ok(result);
        }

        [HttpGet("recent")]
        public async Task<ActionResult<ApiResponse<List<Purchase>>>> GetRecentPurchases()
        {
            var fromDate = DateTime.Today.AddDays(-30);
            var result = await _syncService.GetPurchasesAsync(fromDate, DateTime.Today);
            return Ok(result);
        }
    }
}

