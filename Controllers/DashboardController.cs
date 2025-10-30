using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using AlphaPlusAPI.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AlphaPlusAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IConfiguration configuration, ILogger<DashboardController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' is missing.");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get dashboard statistics for a specific date or date range
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var dateFrom = (fromDate ?? DateTime.Today).Date;
                var dateTo = (toDate ?? dateFrom.AddDays(1)).Date;

                _logger.LogInformation("========== DASHBOARD STATS REQUEST ==========");
                _logger.LogInformation($"Date Range: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}");

                var stats = new DashboardStatsDto();

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    _logger.LogInformation("✓ Database connection established");

                    // 1. Get Total Sales, Invoice Count, and Discount
                    _logger.LogInformation("--- Fetching Sales Summary ---");
                    var salesQuery = @"
                        SELECT 
                            ISNULL(SUM(GrandTotal), 0) AS TotalSales,
                            COUNT(*) AS InvoiceCount,
                            ISNULL(SUM(DiscAmt), 0) AS TotalDiscount
                        FROM InvoiceInfo
                        WHERE InvoiceDate >= @dateFrom AND InvoiceDate < @dateTo
                    ";

                    var salesResult = await connection.QueryFirstOrDefaultAsync(salesQuery, new { dateFrom, dateTo });

                    // Use Convert to safely handle numeric types returned by DB
                    stats.TodaysSales = Convert.ToDecimal(salesResult?.TotalSales ?? 0);
                    stats.InvoiceCount = Convert.ToInt32(salesResult?.InvoiceCount ?? 0);
                    stats.TotalDiscount = Convert.ToDecimal(salesResult?.TotalDiscount ?? 0);

                    _logger.LogInformation($"✓ Total Sales: KES {stats.TodaysSales:N2}");
                    _logger.LogInformation($"✓ Invoice Count: {stats.InvoiceCount}");
                    _logger.LogInformation($"✓ Total Discount: KES {stats.TotalDiscount:N2}");

                    // 2. Get Payment Breakdown
                    _logger.LogInformation("--- Fetching Payment Breakdown ---");
                    var paymentQuery = @"
                        SELECT 
                            LOWER(LTRIM(RTRIM(p.PaymentMode))) AS CleanMode,
                            ISNULL(SUM(p.Amount), 0) AS TotalAmount,
                            COUNT(*) as RecordCount
                        FROM InvoiceInfo i
                        INNER JOIN Invoice_Payment p ON i.Inv_ID = p.InvoiceID
                        WHERE i.InvoiceDate >= @dateFrom AND i.InvoiceDate < @dateTo
                        GROUP BY LOWER(LTRIM(RTRIM(p.PaymentMode)))
                    ";

                    var payments = await connection.QueryAsync(paymentQuery, new { dateFrom, dateTo });

                    decimal totalCash = 0;
                    decimal totalMpesa = 0;
                    decimal totalCredit = 0;

                    var paymentModeMap = new Dictionary<string, Action<decimal>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "cash", amount => totalCash += amount },
                        { "mpesa", amount => totalMpesa += amount },
                        { "m-pesa", amount => totalMpesa += amount },
                        { "m pesa", amount => totalMpesa += amount },
                        { "credit", amount => totalCredit += amount },
                        { "credit customer", amount => totalCredit += amount }
                    };

                    foreach (var payment in payments)
                    {
                        var cleanMode = (payment.CleanMode ?? string.Empty).ToString();
                        var amount = Convert.ToDecimal(payment.TotalAmount ?? 0);
                        var count = Convert.ToInt32(payment.RecordCount ?? 0);

                        _logger.LogInformation($"  Payment Mode: '{cleanMode}' = KES {amount:N2} ({count} records)");

                        // Explicitly type the out var to avoid CS8197 inference error
                        if (paymentModeMap.TryGetValue(cleanMode, out Action<decimal>? assignAction) && assignAction != null)
                        {
                            assignAction(amount);
                            _logger.LogInformation($"    → Assigned to {(cleanMode.Contains("mpesa", StringComparison.OrdinalIgnoreCase) ? "MPESA" : cleanMode.Contains("credit", StringComparison.OrdinalIgnoreCase) ? "CREDIT" : "CASH")}");
                        }
                        else
                        {
                            totalCash += amount;
                            _logger.LogWarning($"    → UNKNOWN: '{cleanMode}' - Adding to CASH");
                        }
                    }

                    stats.CashSales = totalCash;
                    stats.MpesaSales = totalMpesa;
                    stats.CreditSales = totalCredit;

                    _logger.LogInformation("--- Payment Totals ---");
                    _logger.LogInformation($"  Cash Sales:   KES {stats.CashSales:N2}");
                    _logger.LogInformation($"  MPesa Sales:  KES {stats.MpesaSales:N2}");
                    _logger.LogInformation($"  Credit Sales: KES {stats.CreditSales:N2}");

                    var totalPayments = stats.CashSales + stats.MpesaSales + stats.CreditSales;
                    _logger.LogInformation($"  Total Payments: KES {totalPayments:N2}");

                    if (Math.Abs(totalPayments - stats.TodaysSales) > 0.01m)
                    {
                        _logger.LogWarning($"  ⚠️ Mismatch! Expected: {stats.TodaysSales:N2}, Got: {totalPayments:N2}");
                    }
                    else
                    {
                        _logger.LogInformation($"  ✓ Payments match sales");
                    }

                    // 3. Get Returns
                    _logger.LogInformation("--- Fetching Returns ---");
                    try
                    {
                        var returnsQuery = @"
                            SELECT ISNULL(SUM(GrandTotal), 0) AS TotalReturns
                            FROM SalesReturn
                            WHERE Date >= @dateFrom AND Date < @dateTo
                        ";

                        var returnsResult = await connection.QueryFirstOrDefaultAsync(returnsQuery, new { dateFrom, dateTo });

                        stats.TotalReturns = Convert.ToDecimal(returnsResult?.TotalReturns ?? 0);
                        _logger.LogInformation($"✓ Total Returns: KES {stats.TotalReturns:N2}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"SalesReturn table not accessible: {ex.Message}");
                        stats.TotalReturns = 0;
                    }

                    // 4. Calculate Profit
                    _logger.LogInformation("--- Calculating Profit ---");
                    try
                    {
                        var profitQuery = @"
                            SELECT ISNULL(SUM(
                                ip.TotalAmount - (ip.Qty * ISNULL(ts.PurchaseRate, 0))
                            ), 0) AS TotalProfit
                            FROM InvoiceInfo i
                            INNER JOIN Invoice_Product ip ON i.Inv_ID = ip.InvoiceID
                            LEFT JOIN (
                                SELECT ProductID, AVG(PurchaseRate) AS PurchaseRate
                                FROM Temp_Stock_Company
                                WHERE PurchaseRate IS NOT NULL AND PurchaseRate > 0
                                GROUP BY ProductID
                            ) ts ON ip.ProductID = ts.ProductID
                            WHERE i.InvoiceDate >= @dateFrom AND i.InvoiceDate < @dateTo
                        ";

                        var profitResult = await connection.QueryFirstOrDefaultAsync(profitQuery, new { dateFrom, dateTo });

                        stats.TotalProfit = Convert.ToDecimal(profitResult?.TotalProfit ?? 0);
                        _logger.LogInformation($"✓ Total Profit: KES {stats.TotalProfit:N2}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Profit calculation failed: {ex.Message}");
                        // Fallback: estimate 15% profit
                        stats.TotalProfit = stats.TodaysSales * 0.15m;
                        _logger.LogInformation($"✓ Estimated Profit (15%): KES {stats.TotalProfit:N2}");
                    }

                    // 5. Get Product Count
                    _logger.LogInformation("--- Fetching Product Count ---");
                    var productCountQuery = "SELECT COUNT(*) AS ProductCount FROM Product";
                    var productResult = await connection.QueryFirstOrDefaultAsync(productCountQuery);
                    stats.ProductCount = Convert.ToInt32(productResult?.ProductCount ?? 0);
                    _logger.LogInformation($"✓ Total Products: {stats.ProductCount}");

                    // 6. Get Low Stock Count
                    _logger.LogInformation("--- Fetching Low Stock Count ---");
                    try
                    {
                        var lowStockQuery = @"
                            SELECT COUNT(DISTINCT p.PID) AS LowStockCount
                            FROM Product p
                            LEFT JOIN Temp_Stock_Company ts ON p.PID = ts.ProductID
                            WHERE p.ReorderPoint > 0
                            AND (
                                SELECT ISNULL(SUM(Qty), 0)
                                FROM Temp_Stock_Company
                                WHERE ProductID = p.PID
                            ) <= p.ReorderPoint
                        ";

                        var lowStockResult = await connection.QueryFirstOrDefaultAsync(lowStockQuery);
                        stats.LowStockCount = Convert.ToInt32(lowStockResult?.LowStockCount ?? 0);
                        _logger.LogInformation($"✓ Low Stock Items: {stats.LowStockCount}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Low stock query failed: {ex.Message}");
                        stats.LowStockCount = 0;
                    }

                    // NOTE: NetSales is a read-only computed property on the DTO; do not set it here.
                    // It's computed as: TodaysSales - TotalReturns - TotalDiscount
                }

                // Final Summary
                _logger.LogInformation("========== FINAL DASHBOARD STATS ==========");
                _logger.LogInformation($"Total Sales:    KES {stats.TodaysSales:N2}");
                _logger.LogInformation($"Cash Sales:     KES {stats.CashSales:N2}");
                _logger.LogInformation($"MPesa Sales:    KES {stats.MpesaSales:N2}");
                _logger.LogInformation($"Credit Sales:   KES {stats.CreditSales:N2}");
                _logger.LogInformation($"Total Profit:   KES {stats.TotalProfit:N2}");
                _logger.LogInformation($"Total Discount: KES {stats.TotalDiscount:N2}");
                _logger.LogInformation($"Total Returns:  KES {stats.TotalReturns:N2}");
                _logger.LogInformation($"Net Sales:      KES {stats.NetSales:N2}");
                _logger.LogInformation($"Invoices:       {stats.InvoiceCount}");
                _logger.LogInformation($"Products:       {stats.ProductCount}");
                _logger.LogInformation($"Low Stock:      {stats.LowStockCount}");
                _logger.LogInformation("==========================================");

                return Ok(new
                {
                    success = true,
                    message = "Dashboard statistics retrieved successfully",
                    data = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics");
                _logger.LogError($"Exception: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to retrieve dashboard statistics: {ex.Message}",
                    data = (DashboardStatsDto)null
                });
            }
        }

        /// <summary>
        /// Get today's dashboard statistics (shortcut endpoint)
        /// </summary>
        [HttpGet("stats/today")]
        public async Task<IActionResult> GetTodayDashboardStats()
        {
            return await GetDashboardStats(DateTime.Today, null);
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [AllowAnonymous]
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "OK",
                message = "Dashboard API is running",
                timestamp = DateTime.Now,
                version = "2.0.0"
            });
        }

        /// <summary>
        /// Get simplified real-time summary for today's dashboard (totals only)
        /// </summary>
        [HttpGet("today-summary")]
        public async Task<IActionResult> GetTodaySummary()
        {
            try
            {
                _logger.LogInformation("========== TODAY SUMMARY REQUEST ==========");
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                _logger.LogInformation("✓ Database connection established");

                var query = @"
                    SELECT 
                        ISNULL(SUM(i.GrandTotal), 0) AS TotalSales,
                        ISNULL(SUM(CASE 
                            WHEN LOWER(p.PaymentMode) LIKE '%cash%' THEN p.Amount 
                            ELSE 0 END), 0) AS CashSales,
                        ISNULL(SUM(CASE 
                            WHEN LOWER(p.PaymentMode) LIKE '%mpesa%' 
                              OR LOWER(p.PaymentMode) LIKE '%m-pesa%' 
                              OR LOWER(p.PaymentMode) LIKE '%m pesa%' 
                              OR LOWER(p.PaymentMode) LIKE '%mobile money%' 
                            THEN p.Amount ELSE 0 END), 0) AS MPesaSales,
                        ISNULL(SUM(CASE 
                            WHEN LOWER(p.PaymentMode) LIKE '%credit%' 
                              OR LOWER(p.PaymentMode) LIKE '%credit customer%' 
                            THEN p.Amount ELSE 0 END), 0) AS CreditSales,
                        ISNULL(SUM((ip.TotalAmount - (ip.Qty * ISNULL(ts.PurchaseRate, 0)))), 0) AS TotalProfit,
                        COUNT(DISTINCT i.Inv_ID) AS Transactions,
                        ISNULL(SUM(i.DiscAmt), 0) AS TotalDiscount,
                        ISNULL((SELECT SUM(GrandTotal) FROM SalesReturn WHERE CAST(Date AS DATE) = CAST(GETDATE() AS DATE)), 0) AS TotalReturns
                    FROM InvoiceInfo i
                    LEFT JOIN Invoice_Payment p ON i.Inv_ID = p.InvoiceID
                    LEFT JOIN Invoice_Product ip ON i.Inv_ID = ip.InvoiceID
                    LEFT JOIN Temp_Stock_Company ts ON ip.ProductID = ts.ProductID
                    WHERE CAST(i.InvoiceDate AS DATE) = CAST(GETDATE() AS DATE);
                ";

                var result = await connection.QueryFirstOrDefaultAsync(query);

                if (result == null)
                {
                    _logger.LogWarning("No data found for today.");
                    return Ok(new { success = false, message = "No data found for today." });
                }

                var data = new
                {
                    totalSales = Convert.ToDecimal(result.TotalSales ?? 0),
                    cashSales = Convert.ToDecimal(result.CashSales ?? 0),
                    mpesaSales = Convert.ToDecimal(result.MPesaSales ?? 0),
                    creditSales = Convert.ToDecimal(result.CreditSales ?? 0),
                    totalProfit = Convert.ToDecimal(result.TotalProfit ?? 0),
                    transactions = Convert.ToInt32(result.Transactions ?? 0),
                    totalDiscount = Convert.ToDecimal(result.TotalDiscount ?? 0),
                    totalReturns = Convert.ToDecimal(result.TotalReturns ?? 0),
                    netSales = Convert.ToDecimal(result.TotalSales ?? 0) - Convert.ToDecimal(result.TotalDiscount ?? 0) - Convert.ToDecimal(result.TotalReturns ?? 0)
                };

                _logger.LogInformation("========== TODAY SUMMARY ==========");
                _logger.LogInformation($"Total Sales:    KES {data.totalSales:N2}");
                _logger.LogInformation($"Cash Sales:     KES {data.cashSales:N2}");
                _logger.LogInformation($"MPesa Sales:    KES {data.mpesaSales:N2}");
                _logger.LogInformation($"Credit Sales:   KES {data.creditSales:N2}");
                _logger.LogInformation($"Total Profit:   KES {data.totalProfit:N2}");
                _logger.LogInformation($"Total Discount: KES {data.totalDiscount:N2}");
                _logger.LogInformation($"Total Returns:  KES {data.totalReturns:N2}");
                _logger.LogInformation($"Net Sales:      KES {data.netSales:N2}");
                _logger.LogInformation($"Transactions:   {data.transactions}");
                _logger.LogInformation("==================================");

                return Ok(new
                {
                    success = true,
                    message = "Today's summary retrieved successfully",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating today-summary");
                return BadRequest(new { success = false, message = $"Failed to retrieve today's summary: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get full dashboard summary using stored procedure (optimized)
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = (date ?? DateTime.Now).Date;
                _logger.LogInformation("========== DASHBOARD SUMMARY REQUEST ==========");
                _logger.LogInformation($"Target Date: {targetDate:yyyy-MM-dd}");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                _logger.LogInformation("✓ Database connection established");

                // Call stored procedure
                var result = await connection.QueryFirstOrDefaultAsync(@"
                    EXEC sp_GetDashboardSummary @Date",
                    new { Date = targetDate }
                );

                if (result == null)
                {
                    _logger.LogWarning($"No data found for date: {targetDate:yyyy-MM-dd}");
                    return Ok(new { success = false, message = $"No data found for date {targetDate:yyyy-MM-dd}." });
                }

                var data = new
                {
                    totalSales = Convert.ToDecimal(result.TotalSales ?? 0),
                    cashSales = Convert.ToDecimal(result.CashSales ?? 0),
                    mpesaSales = Convert.ToDecimal(result.MpesaSales ?? 0),
                    creditSales = Convert.ToDecimal(result.CreditSales ?? 0),
                    totalTransactions = Convert.ToInt32(result.TotalTransactions ?? 0),
                    totalProfit = Convert.ToDecimal(result.TotalProfit ?? 0),
                    netSales = Convert.ToDecimal(result.NetSales ?? 0),
                    totalProducts = Convert.ToInt32(result.TotalProducts ?? 0),
                    lowStockCount = Convert.ToInt32(result.LowStockCount ?? 0)
                };

                _logger.LogInformation("========== DASHBOARD SUMMARY ==========");
                _logger.LogInformation($"Total Sales:      KES {data.totalSales:N2}");
                _logger.LogInformation($"Cash Sales:       KES {data.cashSales:N2}");
                _logger.LogInformation($"MPesa Sales:      KES {data.mpesaSales:N2}");
                _logger.LogInformation($"Credit Sales:     KES {data.creditSales:N2}");
                _logger.LogInformation($"Total Profit:     KES {data.totalProfit:N2}");
                _logger.LogInformation($"Net Sales:        KES {data.netSales:N2}");
                _logger.LogInformation($"Transactions:     {data.totalTransactions}");
                _logger.LogInformation($"Total Products:   {data.totalProducts}");
                _logger.LogInformation($"Low Stock Count:  {data.lowStockCount}");
                _logger.LogInformation("======================================");

                return Ok(new
                {
                    success = true,
                    message = "Dashboard summary retrieved successfully.",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing sp_GetDashboardSummary");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to retrieve dashboard summary: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get sales trends for charts (daily sales for the last 30 days)
        /// </summary>
        [HttpGet("sales-trends")]
        public async Task<IActionResult> GetSalesTrends([FromQuery] int days = 30)
        {
            try
            {
                _logger.LogInformation("========== SALES TRENDS REQUEST ==========");
                _logger.LogInformation($"Days: {days}");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        CAST(InvoiceDate AS DATE) AS SaleDate,
                        ISNULL(SUM(GrandTotal), 0) AS DailySales,
                        COUNT(*) AS DailyTransactions,
                        ISNULL(SUM(DiscAmt), 0) AS DailyDiscount
                    FROM InvoiceInfo
                    WHERE InvoiceDate >= DATEADD(DAY, -@days, GETDATE())
                    GROUP BY CAST(InvoiceDate AS DATE)
                    ORDER BY SaleDate ASC";

                var trends = await connection.QueryAsync(query, new { days });

                var data = new List<object>();
                foreach (var trend in trends)
                {
                    data.Add(new
                    {
                        date = Convert.ToDateTime(trend.SaleDate).ToString("yyyy-MM-dd"),
                        sales = Convert.ToDecimal(trend.DailySales ?? 0),
                        transactions = Convert.ToInt32(trend.DailyTransactions ?? 0),
                        discount = Convert.ToDecimal(trend.DailyDiscount ?? 0)
                    });
                }

                _logger.LogInformation($"✓ Retrieved {data.Count} days of sales trends");

                return Ok(new
                {
                    success = true,
                    message = "Sales trends retrieved successfully",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sales trends");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to retrieve sales trends: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get top selling products
        /// </summary>
        [HttpGet("top-products")]
        public async Task<IActionResult> GetTopProducts([FromQuery] int limit = 10, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var dateFrom = (fromDate ?? DateTime.Today.AddDays(-30)).Date;
                var dateTo = (toDate ?? DateTime.Today).Date;

                _logger.LogInformation("========== TOP PRODUCTS REQUEST ==========");
                _logger.LogInformation($"Date Range: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}, Limit: {limit}");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT TOP (@limit)
                        p.PID AS ProductId,
                        p.ProductName,
                        p.ProductCode,
                        p.Category,
                        ISNULL(SUM(ip.Qty), 0) AS TotalQuantity,
                        ISNULL(SUM(ip.TotalAmount), 0) AS TotalSales,
                        COUNT(DISTINCT ip.InvoiceID) AS TimesSold
                    FROM Product p
                    INNER JOIN Invoice_Product ip ON p.PID = ip.ProductID
                    INNER JOIN InvoiceInfo i ON ip.InvoiceID = i.Inv_ID
                    WHERE i.InvoiceDate >= @dateFrom AND i.InvoiceDate <= @dateTo
                    GROUP BY p.PID, p.ProductName, p.ProductCode, p.Category
                    ORDER BY TotalSales DESC";

                var products = await connection.QueryAsync(query, new { limit, dateFrom, dateTo });

                var data = new List<object>();
                foreach (var product in products)
                {
                    data.Add(new
                    {
                        productId = Convert.ToInt32(product.ProductId),
                        productName = product.ProductName?.ToString() ?? "Unknown",
                        productCode = product.ProductCode?.ToString() ?? "N/A",
                        category = product.Category?.ToString() ?? "Uncategorized",
                        totalQuantity = Convert.ToDecimal(product.TotalQuantity ?? 0),
                        totalSales = Convert.ToDecimal(product.TotalSales ?? 0),
                        timesSold = Convert.ToInt32(product.TimesSold ?? 0)
                    });
                }

                _logger.LogInformation($"✓ Retrieved {data.Count} top products");

                return Ok(new
                {
                    success = true,
                    message = "Top products retrieved successfully",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top products");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to retrieve top products: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get low stock alerts
        /// </summary>
        [HttpGet("low-stock-alerts")]
        public async Task<IActionResult> GetLowStockAlerts()
        {
            try
            {
                _logger.LogInformation("========== LOW STOCK ALERTS REQUEST ==========");

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        p.PID AS ProductId,
                        p.ProductName,
                        p.ProductCode,
                        p.Category,
                        p.ReorderPoint,
                        ISNULL(SUM(ts.Qty), 0) AS CurrentStock,
                        CASE 
                            WHEN ISNULL(SUM(ts.Qty), 0) <= 0 THEN 'Out of Stock'
                            WHEN ISNULL(SUM(ts.Qty), 0) <= p.ReorderPoint THEN 'Low Stock'
                            ELSE 'In Stock'
                        END AS StockStatus
                    FROM Product p
                    LEFT JOIN Temp_Stock_Company ts ON p.PID = ts.ProductID
                    WHERE p.ReorderPoint > 0
                    GROUP BY p.PID, p.ProductName, p.ProductCode, p.Category, p.ReorderPoint
                    HAVING ISNULL(SUM(ts.Qty), 0) <= p.ReorderPoint
                    ORDER BY CurrentStock ASC";

                var alerts = await connection.QueryAsync(query);

                var data = new List<object>();
                foreach (var alert in alerts)
                {
                    data.Add(new
                    {
                        productId = Convert.ToInt32(alert.ProductId),
                        productName = alert.ProductName?.ToString() ?? "Unknown",
                        productCode = alert.ProductCode?.ToString() ?? "N/A",
                        category = alert.Category?.ToString() ?? "Uncategorized",
                        reorderPoint = Convert.ToInt32(alert.ReorderPoint ?? 0),
                        currentStock = Convert.ToDecimal(alert.CurrentStock ?? 0),
                        stockStatus = alert.StockStatus?.ToString() ?? "Unknown"
                    });
                }

                _logger.LogInformation($"✓ Retrieved {data.Count} low stock alerts");

                return Ok(new
                {
                    success = true,
                    message = "Low stock alerts retrieved successfully",
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock alerts");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to retrieve low stock alerts: {ex.Message}"
                });
            }
        }
    }
}