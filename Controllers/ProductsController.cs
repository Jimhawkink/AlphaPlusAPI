using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AlphaPlusAPI.Services;
using AlphaPlusAPI.Models;
using AlphaPlusAPI.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AlphaPlusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly SyncService _syncService;

        public ProductsController(SyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<Product>>>> GetProducts(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 100,
            [FromQuery] string? search = null)
        {
            var result = await _syncService.GetProductsAsync(page, pageSize, search);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse<List<Product>>>> SearchProducts([FromQuery] string query)
        {
            var result = await _syncService.GetProductsAsync(1, 50, query);
            return Ok(result);
        }

        // ====================================================================
        // ✅ NEW ENDPOINT: Get Categories (Fixes hardcoded categories issue)
        // ====================================================================
        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
        {
            try
            {
                // Assuming SyncService has a method to fetch categories
                var categoryObjects = await _syncService.GetCategoriesAsync();

// Convert List<Category> → List<string>
var categories = categoryObjects
    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
    .Select(c => c.Name.Trim())
    .Distinct()
    .OrderBy(name => name)
    .ToList();

return Ok(new ApiResponse<List<string>>
{
    Success = true,
    Message = "Categories retrieved successfully",
    Data = categories
});

            }
            catch (Exception ex)
            {
                // Log the exception
                return StatusCode(500, new ApiResponse<List<string>>
                {
                    Success = false,
                    Message = $"Error retrieving categories: {ex.Message}",
                    Data = null
                });
            }
        }
    }
}
