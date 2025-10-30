using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using AlphaPlusAPI.DTOs;
using AlphaPlusAPI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
#nullable disable warnings

namespace AlphaPlusAPI.Services
{
    public class SyncService
    {
        private readonly DatabaseService _db;
        private readonly ILogger<SyncService> _logger;

        public SyncService(DatabaseService db, ILogger<SyncService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ============================================
        // NEW METHOD: Dashboard Statistics using Stored Procedure
        // ============================================
        /// <summary>
        /// Get dashboard statistics using stored procedure for a specific date
        /// </summary>
        public async Task<ApiResponse<DashboardSummary>> GetDashboardSummaryAsync(DateTime? date = null)
        {
            try
            {
                var targetDate = (date ?? DateTime.Today).Date;
                
                using (var conn = _db.GetConnection())
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("sp_GetDashboardSummary", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Date", targetDate);
                        
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var summary = new DashboardSummary
                                {
                                    TotalSales = reader.GetDecimal(0),
                                    CashSales = reader.GetDecimal(1),
                                    MpesaSales = reader.GetDecimal(2),
                                    CreditSales = reader.GetDecimal(3),
                                    TotalTransactions = reader.GetInt32(4),
                                    TotalProfit = reader.GetDecimal(5),
                                    NetSales = reader.GetDecimal(6),
                                    TotalProducts = reader.GetInt32(7),
                                    LowStockCount = reader.GetInt32(8)
                                };

                                return new ApiResponse<DashboardSummary>
                                {
                                    Success = true,
                                    Message = "Dashboard summary retrieved successfully using stored procedure",
                                    Data = summary
                                };
                            }
                        }
                    }
                }

                return new ApiResponse<DashboardSummary>
                {
                    Success = false,
                    Message = "No data returned from stored procedure",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in GetDashboardSummaryAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                return new ApiResponse<DashboardSummary>
                {
                    Success = false,
                    Message = $"Error retrieving dashboard summary: {ex.Message}",
                    Data = null
                };
            }
        }
public async Task<List<Category>> GetCategoriesAsync()
{
    var categories = new List<Category>();

    using (var connection = _db.GetConnection())
    {
        var query = "SELECT DISTINCT CategoryName FROM Products WHERE CategoryName IS NOT NULL AND CategoryName <> '' ORDER BY CategoryName";

        await connection.OpenAsync();
        using (var command = new SqlCommand(query, connection))
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    Name = reader["CategoryName"].ToString()
                });
            }
        }
    }

    return categories;
}


        // ============================================
        // EXISTING METHOD: Dashboard Statistics
        // ============================================
        /// <summary>
        /// Get dashboard statistics for a specific date range
        /// Matches VB.NET implementation exactly
        /// </summary>
        public async Task<ApiResponse<DashboardStatsDto>> GetDashboardStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Default to today if no date provided
                var dateFrom = (fromDate ?? DateTime.Today).Date;
                var dateTo = (toDate ?? DateTime.Today.AddDays(1)).Date;

                var stats = new DashboardStatsDto();

                // 1. Get Total Sales
                var totalSalesQuery = "SELECT ISNULL(SUM(GrandTotal), 0) FROM InvoiceInfo WHERE InvoiceDate >= @d1 AND InvoiceDate < @d2";
                var totalSalesResult = await _db.ExecuteScalarAsync(totalSalesQuery, 
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.TodaysSales = Convert.ToDecimal(totalSalesResult ?? 0);

                // 2. Get Payment Method Totals (Cash, MPesa, Credit)
                // CRITICAL FIX: Separate queries for each payment method with proper trimming
                
                // Get CASH payments
                var cashQuery = @"
                    SELECT ISNULL(SUM(p.Amount), 0) 
                    FROM Invoice_Payment p
                    INNER JOIN InvoiceInfo i ON p.InvoiceID = i.Inv_ID
                    WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2 
                    AND LTRIM(RTRIM(UPPER(p.PaymentMode))) = 'CASH'";
                
                var cashResult = await _db.ExecuteScalarAsync(cashQuery,
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.CashSales = Convert.ToDecimal(cashResult ?? 0);

                // Get MPESA payments
                var mpesaQuery = @"
                    SELECT ISNULL(SUM(p.Amount), 0) 
                    FROM Invoice_Payment p
                    INNER JOIN InvoiceInfo i ON p.InvoiceID = i.Inv_ID
                    WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2 
                    AND LTRIM(RTRIM(UPPER(p.PaymentMode))) = 'MPESA'";
                
                var mpesaResult = await _db.ExecuteScalarAsync(mpesaQuery,
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.MpesaSales = Convert.ToDecimal(mpesaResult ?? 0);

                // Get CREDIT payments (both 'CREDIT CUSTOMER' and 'CREDIT')
                var creditQuery = @"
                    SELECT ISNULL(SUM(p.Amount), 0) 
                    FROM Invoice_Payment p
                    INNER JOIN InvoiceInfo i ON p.InvoiceID = i.Inv_ID
                    WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2 
                    AND (LTRIM(RTRIM(UPPER(p.PaymentMode))) = 'CREDIT CUSTOMER' 
                         OR LTRIM(RTRIM(UPPER(p.PaymentMode))) = 'CREDIT')";
                
                var creditResult = await _db.ExecuteScalarAsync(creditQuery,
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.CreditSales = Convert.ToDecimal(creditResult ?? 0);

                // 3. Get Invoice Count
                var invoiceCountQuery = "SELECT COUNT(*) FROM InvoiceInfo WHERE InvoiceDate >= @d1 AND InvoiceDate < @d2";
                var invoiceCountResult = await _db.ExecuteScalarAsync(invoiceCountQuery,
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.InvoiceCount = Convert.ToInt32(invoiceCountResult ?? 0);

                // 4. Get Total Discounts
                var discountQuery = "SELECT ISNULL(SUM(DiscAmt), 0) FROM InvoiceInfo WHERE InvoiceDate >= @d1 AND InvoiceDate < @d2";
                var discountResult = await _db.ExecuteScalarAsync(discountQuery,
                    new SqlParameter[] {
                        new SqlParameter("@d1", dateFrom),
                        new SqlParameter("@d2", dateTo)
                    });
                stats.TotalDiscount = Convert.ToDecimal(discountResult ?? 0);

                // 5. Get Total Returns (if SalesReturn table exists)
                try
                {
                    var returnsQuery = "SELECT ISNULL(SUM(GrandTotal), 0) FROM SalesReturn WHERE Date >= @d1 AND Date < @d2";
                    var returnsResult = await _db.ExecuteScalarAsync(returnsQuery,
                        new SqlParameter[] {
                            new SqlParameter("@d1", dateFrom),
                            new SqlParameter("@d2", dateTo)
                        });
                    stats.TotalReturns = Convert.ToDecimal(returnsResult ?? 0);
                }
                catch
                {
                    stats.TotalReturns = 0;
                }

                // 6. Calculate Total Profit - Try multiple approaches like VB.NET
                try
                {
                    // First try: Using Temp_Stock_Company (matches VB.NET exactly)
                    var profitQuery = @"
                        SELECT ISNULL(SUM(ip.TotalAmount - (ip.Qty * ts.PurchaseRate)), 0)
                        FROM InvoiceInfo i 
                        INNER JOIN Invoice_Product ip ON i.Inv_ID = ip.InvoiceID 
                        INNER JOIN Temp_Stock_Company ts ON ip.ProductID = ts.ProductID 
                        WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2";
                    
                    var profitResult = await _db.ExecuteScalarAsync(profitQuery,
                        new SqlParameter[] {
                            new SqlParameter("@d1", dateFrom),
                            new SqlParameter("@d2", dateTo)
                        });
                    stats.TotalProfit = Convert.ToDecimal(profitResult ?? 0);
                }
                catch
                {
                    try
                    {
                        // Fallback: Try with PurchaseRate in Invoice_Product
                        var profitQuery = @"
                            SELECT ISNULL(SUM(ip.TotalAmount - (ip.Qty * ISNULL(ip.PurchaseRate, 0))), 0)
                            FROM InvoiceInfo i 
                            INNER JOIN Invoice_Product ip ON i.Inv_ID = ip.InvoiceID 
                            WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2";
                        
                        var profitResult = await _db.ExecuteScalarAsync(profitQuery,
                            new SqlParameter[] {
                                new SqlParameter("@d1", dateFrom),
                                new SqlParameter("@d2", dateTo)
                            });
                        stats.TotalProfit = Convert.ToDecimal(profitResult ?? 0);
                    }
                    catch
                    {
                        try
                        {
                            // Last resort: Use Margin field
                            var profitQuery = @"
                                SELECT ISNULL(SUM(ISNULL(ip.Margin, 0) * ip.Qty), 0)
                                FROM InvoiceInfo i 
                                INNER JOIN Invoice_Product ip ON i.Inv_ID = ip.InvoiceID 
                                WHERE i.InvoiceDate >= @d1 AND i.InvoiceDate < @d2";
                            
                            var profitResult = await _db.ExecuteScalarAsync(profitQuery,
                                new SqlParameter[] {
                                    new SqlParameter("@d1", dateFrom),
                                    new SqlParameter("@d2", dateTo)
                                });
                            stats.TotalProfit = Convert.ToDecimal(profitResult ?? 0);
                        }
                        catch
                        {
                            stats.TotalProfit = 0;
                        }
                    }
                }

                // 7. Get Product Count
                var productCountQuery = "SELECT COUNT(*) FROM Product";
                var productCountResult = await _db.ExecuteScalarAsync(productCountQuery);
                stats.ProductCount = Convert.ToInt32(productCountResult ?? 0);

                // 8. Get Low Stock Count
                try
                {
                    var lowStockQuery = @"
                        SELECT COUNT(*) 
                        FROM Product p 
                        INNER JOIN Temp_Stock_Company ts ON p.PID = ts.ProductID 
                        WHERE p.ReorderPoint > 0 AND ts.Qty <= p.ReorderPoint";
                    
                    var lowStockResult = await _db.ExecuteScalarAsync(lowStockQuery);
                    stats.LowStockCount = Convert.ToInt32(lowStockResult ?? 0);
                }
                catch
                {
                    try
                    {
                        // Fallback if Temp_Stock_Company doesn't have Qty column
                        var lowStockQuery = @"
                            SELECT COUNT(*) 
                            FROM Product 
                            WHERE ReorderPoint > 0 AND Stock <= ReorderPoint";
                        
                        var lowStockResult = await _db.ExecuteScalarAsync(lowStockQuery);
                        stats.LowStockCount = Convert.ToInt32(lowStockResult ?? 0);
                    }
                    catch
                    {
                        stats.LowStockCount = 0;
                    }
                }

                // Debug logging to verify values
                System.Diagnostics.Debug.WriteLine($"========== DASHBOARD STATS DEBUG ==========");
                System.Diagnostics.Debug.WriteLine($"Date Range: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}");
                System.Diagnostics.Debug.WriteLine($"Total Sales: {stats.TodaysSales:N2}");
                System.Diagnostics.Debug.WriteLine($"Cash Sales: {stats.CashSales:N2}");
                System.Diagnostics.Debug.WriteLine($"MPesa Sales: {stats.MpesaSales:N2}");
                System.Diagnostics.Debug.WriteLine($"Credit Sales: {stats.CreditSales:N2}");
                System.Diagnostics.Debug.WriteLine($"Total Profit: {stats.TotalProfit:N2}");
                System.Diagnostics.Debug.WriteLine($"Total Discount: {stats.TotalDiscount:N2}");
                System.Diagnostics.Debug.WriteLine($"Total Returns: {stats.TotalReturns:N2}");
                System.Diagnostics.Debug.WriteLine($"Invoice Count: {stats.InvoiceCount}");
                System.Diagnostics.Debug.WriteLine($"Product Count: {stats.ProductCount}");
                System.Diagnostics.Debug.WriteLine($"Low Stock Count: {stats.LowStockCount}");
                
                // Verification check
                var totalPayments = stats.CashSales + stats.MpesaSales + stats.CreditSales;
                System.Diagnostics.Debug.WriteLine($"Total Payments: {totalPayments:N2}");
                if (Math.Abs(totalPayments - stats.TodaysSales) > 0.01m)
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING: Payment breakdown ({totalPayments:N2}) doesn't match total sales ({stats.TodaysSales:N2})");
                }
                System.Diagnostics.Debug.WriteLine($"==========================================");

                return new ApiResponse<DashboardStatsDto>
                {
                    Success = true,
                    Message = "Dashboard statistics retrieved successfully",
                    Data = stats
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in GetDashboardStatsAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                
                return new ApiResponse<DashboardStatsDto>
                {
                    Success = false,
                    Message = $"Error retrieving dashboard statistics: {ex.Message}",
                    Data = null
                };
            }
        }

        // ============================================
        // PRODUCT CRUD OPERATIONS
        // ============================================

        public async Task<ApiResponse<List<Product>>> GetProductsAsync(int page = 1, int pageSize = 100, string? search = null)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                var whereClause = string.IsNullOrEmpty(search) 
                    ? "" 
                    : @"WHERE p.ProductName LIKE @Search 
                        OR p.ProductCode LIKE @Search 
                        OR p.Barcode LIKE @Search 
                        OR p.Category LIKE @Search";

                // FIXED: Order by PID (not TEXT columns) to avoid SQL Server error
                var query = $@"
                    SELECT 
                        p.PID,
                        p.ProductCode,
                        p.ProductName,
                        p.Alias,
                        p.VATCommodity,
                        p.Description,
                        p.Barcode,
                        p.Category,
                        p.PurchaseUnit,
                        p.SalesUnit,
                        ISNULL(MAX(s.PurchaseRate), p.PurchaseCost) AS PurchaseCost,
                        ISNULL(MAX(s.SalesRate), p.SalesCost) AS SalesCost,
                        p.ReorderPoint,
                        p.AddedDate,
                        p.MarginPer,
                        p.ShowPS,
                        p.ButtonUIColor,
                        p.photo,
                        p.hscode,
                        p.batchNo,
                        NULL AS SupplierName,
                        ISNULL(SUM(s.Qty), 0) AS AvailableQty
                    FROM Product p
                    LEFT JOIN Temp_Stock_Company s ON p.PID = s.ProductID
                    {whereClause}
                    GROUP BY 
                        p.PID, p.ProductCode, p.ProductName, p.Alias, p.VATCommodity,
                        p.Description, p.Barcode, p.Category, p.PurchaseUnit, p.SalesUnit,
                        p.PurchaseCost, p.SalesCost, p.ReorderPoint, p.AddedDate, p.MarginPer,
                        p.ShowPS, p.ButtonUIColor, p.photo, p.hscode, p.batchNo
                    ORDER BY p.PID DESC
                    OFFSET @Offset ROWS 
                    FETCH NEXT @PageSize ROWS ONLY";

                var countQuery = $@"
                    SELECT COUNT(DISTINCT p.PID) 
                    FROM Product p
                    LEFT JOIN Temp_Stock_Company s ON p.PID = s.ProductID
                    {whereClause}";

                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@Offset", offset),
                    new SqlParameter("@PageSize", pageSize)
                };

                if (!string.IsNullOrEmpty(search))
                {
                    parameters.Add(new SqlParameter("@Search", $"%{search}%"));
                }

                var result = await _db.ExecuteQueryAsync(query, parameters.ToArray());
                
                var countParameters = string.IsNullOrEmpty(search) 
                    ? null 
                    : new SqlParameter[] { new SqlParameter("@Search", $"%{search}%") };
                    
                var totalCount = Convert.ToInt32(await _db.ExecuteScalarAsync(countQuery, countParameters) ?? 0);

                var products = new List<Product>();
                foreach (DataRow row in result.Rows)
                {
                    products.Add(MapToProduct(row));
                }

                return new ApiResponse<List<Product>>
                {
                    Success = true,
                    Message = "Products retrieved successfully",
                    Data = products,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                return new ApiResponse<List<Product>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = new List<Product>()
                };
            }
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            try
            {
                var query = @"
                    SELECT 
                        p.PID,
                        p.ProductCode,
                        p.ProductName,
                        p.Alias,
                        p.VATCommodity,
                        p.Description,
                        p.Barcode,
                        p.Category,
                        p.PurchaseUnit,
                        p.SalesUnit,
                        p.PurchaseCost,
                        p.SalesCost,
                        p.ReorderPoint,
                        p.AddedDate,
                        p.MarginPer,
                        p.ShowPS,
                        p.ButtonUIColor,
                        p.photo,
                        p.hscode,
                        p.batchNo,
                        NULL AS SupplierName,
                        ISNULL(SUM(s.Qty), 0) AS AvailableQty
                    FROM Product p
                    LEFT JOIN Temp_Stock_Company s ON p.PID = s.ProductID
                    WHERE p.PID = @PID
                    GROUP BY 
                        p.PID, p.ProductCode, p.ProductName, p.Alias, p.VATCommodity,
                        p.Description, p.Barcode, p.Category, p.PurchaseUnit, p.SalesUnit,
                        p.PurchaseCost, p.SalesCost, p.ReorderPoint, p.AddedDate, p.MarginPer,
                        p.ShowPS, p.ButtonUIColor, p.photo, p.hscode, p.batchNo";

                var result = await _db.ExecuteQueryAsync(query, new SqlParameter[] { new SqlParameter("@PID", id) });

                if (result.Rows.Count > 0)
                {
                    return MapToProduct(result.Rows[0]);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", id);
                return null;
            }
        }

        public async Task<ApiResponse<Product>> CreateProductAsync(Product product)
        {
            try
            {
                var query = @"
                    INSERT INTO Product (
                        ProductCode, ProductName, Alias, VATCommodity, Description, 
                        Barcode, Category, PurchaseUnit, SalesUnit, PurchaseCost, 
                        SalesCost, ReorderPoint, MarginPer, ShowPS, ButtonUIColor, 
                        photo, hscode, batchNo, AddedDate
                    ) VALUES (
                        @ProductCode, @ProductName, @Alias, @VATCommodity, @Description,
                        @Barcode, @Category, @PurchaseUnit, @SalesUnit, @PurchaseCost,
                        @SalesCost, @ReorderPoint, @MarginPer, @ShowPS, @ButtonUIColor,
                        @Photo, @HSCode, @BatchNo, @AddedDate
                    );
                    SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@ProductCode", (object)product.ProductCode ?? DBNull.Value),
                    new SqlParameter("@ProductName", (object)product.ProductName ?? DBNull.Value),
                    new SqlParameter("@Alias", (object)product.Alias ?? DBNull.Value),
                    new SqlParameter("@VATCommodity", (object)product.VATCommodity ?? DBNull.Value),
                    new SqlParameter("@Description", (object)product.Description ?? DBNull.Value),
                    new SqlParameter("@Barcode", (object)product.Barcode ?? DBNull.Value),
                    new SqlParameter("@Category", (object)product.Category ?? DBNull.Value),
                    new SqlParameter("@PurchaseUnit", (object)product.PurchaseUnit ?? DBNull.Value),
                    new SqlParameter("@SalesUnit", (object)product.SalesUnit ?? DBNull.Value),
                    new SqlParameter("@PurchaseCost", (object)product.PurchaseCost ?? DBNull.Value),
                    new SqlParameter("@SalesCost", (object)product.SalesCost ?? DBNull.Value),
                    new SqlParameter("@ReorderPoint", (object)product.ReorderPoint ?? DBNull.Value),
                    new SqlParameter("@MarginPer", (object)product.MarginPer ?? DBNull.Value),
                    new SqlParameter("@ShowPS", (object)product.ShowPS ?? DBNull.Value),
                    new SqlParameter("@ButtonUIColor", (object)product.ButtonUIColor ?? DBNull.Value),
                    new SqlParameter("@Photo", (object)product.Photo ?? DBNull.Value),
                    new SqlParameter("@HSCode", (object)product.HSCode ?? DBNull.Value),
                    new SqlParameter("@BatchNo", (object)product.BatchNo ?? DBNull.Value),
                    new SqlParameter("@AddedDate", DateTime.Now)
                };

                var productId = Convert.ToInt32(await _db.ExecuteScalarAsync(query, parameters) ?? 0);
                var createdProduct = await GetProductByIdAsync(productId);

                return new ApiResponse<Product>
                {
                    Success = true,
                    Message = "Product created successfully",
                    Data = createdProduct
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return new ApiResponse<Product>
                {
                    Success = false,
                    Message = $"Failed to create product: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<Product>> UpdateProductAsync(int id, Product product)
        {
            try
            {
                // Check if product exists
                var existingProduct = await GetProductByIdAsync(id);
                if (existingProduct == null)
                {
                    return new ApiResponse<Product>
                    {
                        Success = false,
                        Message = "Product not found"
                    };
                }

                var query = @"
                    UPDATE Product SET
                        ProductCode = @ProductCode,
                        ProductName = @ProductName,
                        Alias = @Alias,
                        VATCommodity = @VATCommodity,
                        Description = @Description,
                        Barcode = @Barcode,
                        Category = @Category,
                        PurchaseUnit = @PurchaseUnit,
                        SalesUnit = @SalesUnit,
                        PurchaseCost = @PurchaseCost,
                        SalesCost = @SalesCost,
                        ReorderPoint = @ReorderPoint,
                        MarginPer = @MarginPer,
                        ShowPS = @ShowPS,
                        ButtonUIColor = @ButtonUIColor,
                        photo = @Photo,
                        hscode = @HSCode,
                        batchNo = @BatchNo
                    WHERE PID = @PID";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@PID", id),
                    new SqlParameter("@ProductCode", (object)product.ProductCode ?? DBNull.Value),
                    new SqlParameter("@ProductName", (object)product.ProductName ?? DBNull.Value),
                    new SqlParameter("@Alias", (object)product.Alias ?? DBNull.Value),
                    new SqlParameter("@VATCommodity", (object)product.VATCommodity ?? DBNull.Value),
                    new SqlParameter("@Description", (object)product.Description ?? DBNull.Value),
                    new SqlParameter("@Barcode", (object)product.Barcode ?? DBNull.Value),
                    new SqlParameter("@Category", (object)product.Category ?? DBNull.Value),
                    new SqlParameter("@PurchaseUnit", (object)product.PurchaseUnit ?? DBNull.Value),
                    new SqlParameter("@SalesUnit", (object)product.SalesUnit ?? DBNull.Value),
                    new SqlParameter("@PurchaseCost", (object)product.PurchaseCost ?? DBNull.Value),
                    new SqlParameter("@SalesCost", (object)product.SalesCost ?? DBNull.Value),
                    new SqlParameter("@ReorderPoint", (object)product.ReorderPoint ?? DBNull.Value),
                    new SqlParameter("@MarginPer", (object)product.MarginPer ?? DBNull.Value),
                    new SqlParameter("@ShowPS", (object)product.ShowPS ?? DBNull.Value),
                    new SqlParameter("@ButtonUIColor", (object)product.ButtonUIColor ?? DBNull.Value),
                    new SqlParameter("@Photo", (object)product.Photo ?? DBNull.Value),
                    new SqlParameter("@HSCode", (object)product.HSCode ?? DBNull.Value),
                    new SqlParameter("@BatchNo", (object)product.BatchNo ?? DBNull.Value)
                };

                await _db.ExecuteNonQueryAsync(query, parameters);
                var updatedProduct = await GetProductByIdAsync(id);

                return new ApiResponse<Product>
                {
                    Success = true,
                    Message = "Product updated successfully",
                    Data = updatedProduct
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                return new ApiResponse<Product>
                {
                    Success = false,
                    Message = $"Failed to update product: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<object>> DeleteProductAsync(int id)
        {
            try
            {
                // Check if product exists
                var existingProduct = await GetProductByIdAsync(id);
                if (existingProduct == null)
                {
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found"
                    };
                }

                var query = "DELETE FROM Product WHERE PID = @PID";
                await _db.ExecuteNonQueryAsync(query, new SqlParameter[] { new SqlParameter("@PID", id) });

                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Product deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = $"Failed to delete product: {ex.Message}"
                };
            }
        }

        // ============================================
        // INVOICE OPERATIONS - FIXED METHOD
        // ============================================

        public async Task<ApiResponse<List<InvoiceInfo>>> GetInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation($"GetInvoicesAsync called - From: {fromDate}, To: {toDate}");

                var whereClause = "";
                var parameters = new List<SqlParameter>();

                if (fromDate.HasValue && toDate.HasValue)
                {
                    // ✅ FIXED: Use proper date comparison
                    whereClause = "WHERE CAST(InvoiceDate AS DATE) BETWEEN @FromDate AND @ToDate";
                    parameters.Add(new SqlParameter("@FromDate", fromDate.Value.Date));
                    parameters.Add(new SqlParameter("@ToDate", toDate.Value.Date));
                }

                var query = $@"SELECT TOP 1000 * FROM InvoiceInfo 
                             {whereClause}
                             ORDER BY Inv_ID DESC";

                _logger.LogInformation($"Executing query: {query}");

                var result = await _db.ExecuteQueryAsync(query, parameters.ToArray());
                var invoices = new List<InvoiceInfo>();

                foreach (DataRow row in result.Rows)
                {
                    invoices.Add(MapToInvoiceInfo(row));
                }

                _logger.LogInformation($"Retrieved {invoices.Count} invoices");

                return new ApiResponse<List<InvoiceInfo>>
                {
                    Success = true,
                    Message = "Invoices retrieved successfully",
                    Data = invoices,
                    TotalCount = invoices.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetInvoicesAsync");
                return new ApiResponse<List<InvoiceInfo>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = new List<InvoiceInfo>()
                };
            }
        }

        // ============================================
        // PURCHASE OPERATIONS
        // ============================================

        public async Task<ApiResponse<List<Purchase>>> GetPurchasesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var whereClause = "";
                var parameters = new List<SqlParameter>();

                if (fromDate.HasValue && toDate.HasValue)
                {
                    whereClause = "WHERE Date BETWEEN @FromDate AND @ToDate";
                    parameters.Add(new SqlParameter("@FromDate", fromDate.Value));
                    parameters.Add(new SqlParameter("@ToDate", toDate.Value));
                }

                var query = $@"SELECT TOP 1000 * FROM Purchase 
                             {whereClause}
                             ORDER BY ST_ID DESC";

                var result = await _db.ExecuteQueryAsync(query, parameters.Count > 0 ? parameters.ToArray() : null);
                var purchases = new List<Purchase>();

                foreach (DataRow row in result.Rows)
                {
                    purchases.Add(MapToPurchase(row));
                }

                return new ApiResponse<List<Purchase>>
                {
                    Success = true,
                    Message = "Purchases retrieved successfully",
                    Data = purchases,
                    TotalCount = purchases.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchases");
                return new ApiResponse<List<Purchase>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = new List<Purchase>()
                };
            }
        }

        // ============================================
        // CUSTOMER OPERATIONS
        // ============================================

        public async Task<ApiResponse<List<Customer>>> GetCustomersAsync(int page = 1, int pageSize = 100, string? search = null)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                var whereClause = string.IsNullOrEmpty(search) 
                    ? "" 
                    : @"WHERE CustomerName LIKE @Search 
                        OR ContactNo LIKE @Search 
                        OR EmailID LIKE @Search";

                var query = $@"
                    SELECT 
                        Cust_ID,
                        CustomerName,
                        ContactNo,
                        EmailID,
                        Address,
                        City,
                        State,
                        Country,
                        AddedDate
                    FROM Customer
                    {whereClause}
                    ORDER BY Cust_ID DESC
                    OFFSET @Offset ROWS 
                    FETCH NEXT @PageSize ROWS ONLY";

                var countQuery = $@"
                    SELECT COUNT(*) 
                    FROM Customer
                    {whereClause}";

                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@Offset", offset),
                    new SqlParameter("@PageSize", pageSize)
                };

                if (!string.IsNullOrEmpty(search))
                {
                    parameters.Add(new SqlParameter("@Search", $"%{search}%"));
                }

                var result = await _db.ExecuteQueryAsync(query, parameters.ToArray());
                
                var countParameters = string.IsNullOrEmpty(search) 
                    ? null 
                    : new SqlParameter[] { new SqlParameter("@Search", $"%{search}%") };
                    
                var totalCount = Convert.ToInt32(await _db.ExecuteScalarAsync(countQuery, countParameters) ?? 0);

                var customers = new List<Customer>();
                foreach (DataRow row in result.Rows)
                {
                    customers.Add(MapToCustomer(row));
                }

                return new ApiResponse<List<Customer>>
                {
                    Success = true,
                    Message = "Customers retrieved successfully",
                    Data = customers,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers");
                return new ApiResponse<List<Customer>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = new List<Customer>()
                };
            }
        }

        public async Task<Customer?> GetCustomerByIdAsync(int id)
        {
            try
            {
                var query = @"
                    SELECT 
                        Cust_ID,
                        CustomerName,
                        ContactNo,
                        EmailID,
                        Address,
                        City,
                        State,
                        Country,
                        AddedDate
                    FROM Customer
                    WHERE Cust_ID = @CustID";

                var result = await _db.ExecuteQueryAsync(query, new SqlParameter[] { new SqlParameter("@CustID", id) });

                if (result.Rows.Count > 0)
                {
                    return MapToCustomer(result.Rows[0]);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer {CustomerId}", id);
                return null;
            }
        }

        // ============================================
        // SUPPLIER OPERATIONS
        // ============================================

        public async Task<ApiResponse<List<Supplier>>> GetSuppliersAsync(int page = 1, int pageSize = 100, string? search = null)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                var whereClause = string.IsNullOrEmpty(search) 
                    ? "" 
                    : @"WHERE SupplierName LIKE @Search 
                        OR ContactNo LIKE @Search 
                        OR EmailID LIKE @Search";

                var query = $@"
                    SELECT 
                        SupplierID,
                        SupplierName,
                        ContactNo,
                        EmailID,
                        Address,
                        City,
                        State,
                        Country,
                        AddedDate
                    FROM Supplier
                    {whereClause}
                    ORDER BY SupplierID DESC
                    OFFSET @Offset ROWS 
                    FETCH NEXT @PageSize ROWS ONLY";

                var countQuery = $@"
                    SELECT COUNT(*) 
                    FROM Supplier
                    {whereClause}";

                var parameters = new List<SqlParameter>
                {
                    new SqlParameter("@Offset", offset),
                    new SqlParameter("@PageSize", pageSize)
                };

                if (!string.IsNullOrEmpty(search))
                {
                    parameters.Add(new SqlParameter("@Search", $"%{search}%"));
                }

                var result = await _db.ExecuteQueryAsync(query, parameters.ToArray());
                
                var countParameters = string.IsNullOrEmpty(search) 
                    ? null 
                    : new SqlParameter[] { new SqlParameter("@Search", $"%{search}%") };
                    
                var totalCount = Convert.ToInt32(await _db.ExecuteScalarAsync(countQuery, countParameters) ?? 0);

                var suppliers = new List<Supplier>();
                foreach (DataRow row in result.Rows)
                {
                    suppliers.Add(MapToSupplier(row));
                }

                return new ApiResponse<List<Supplier>>
                {
                    Success = true,
                    Message = "Suppliers retrieved successfully",
                    Data = suppliers,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suppliers");
                return new ApiResponse<List<Supplier>>
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Data = new List<Supplier>()
                };
            }
        }

        // ============================================
        // MAPPER METHODS
        // ============================================

        private Product MapToProduct(DataRow row)
        {
            return new Product
            {
                PID = Convert.ToInt32(row["PID"]),
                ProductCode = row["ProductCode"]?.ToString() ?? "",
                ProductName = row["ProductName"]?.ToString() ?? "",
                Alias = row["Alias"]?.ToString() ?? "",
                VATCommodity = row["VATCommodity"]?.ToString() ?? "",
                Description = row["Description"]?.ToString() ?? "",
                Barcode = row["Barcode"]?.ToString() ?? "",
                Category = row["Category"]?.ToString() ?? "",
                PurchaseUnit = row["PurchaseUnit"]?.ToString() ?? "",
                SalesUnit = row["SalesUnit"]?.ToString() ?? "",
                PurchaseCost = row["PurchaseCost"] != DBNull.Value ? Convert.ToDecimal(row["PurchaseCost"]) : 0,
                SalesCost = row["SalesCost"] != DBNull.Value ? Convert.ToDecimal(row["SalesCost"]) : 0,
                ReorderPoint = row["ReorderPoint"] != DBNull.Value ? Convert.ToInt32(row["ReorderPoint"]) : 0,
                AddedDate = row["AddedDate"] != DBNull.Value ? Convert.ToDateTime(row["AddedDate"]) : DateTime.MinValue,
                MarginPer = row["MarginPer"] != DBNull.Value ? Convert.ToDecimal(row["MarginPer"]) : 0,
                ShowPS = row["ShowPS"] != DBNull.Value ? Convert.ToBoolean(row["ShowPS"]) : false,
                ButtonUIColor = row["ButtonUIColor"]?.ToString() ?? "",
                Photo = row["photo"] != DBNull.Value ? (byte[])row["photo"] : null,
                HSCode = row["hscode"]?.ToString() ?? "",
                BatchNo = row["batchNo"]?.ToString() ?? "",
                SupplierName = row["SupplierName"]?.ToString() ?? "",
                AvailableQty = row["AvailableQty"] != DBNull.Value ? Convert.ToDecimal(row["AvailableQty"]) : 0
            };
        }

        private InvoiceInfo MapToInvoiceInfo(DataRow row)
        {
            return new InvoiceInfo
            {
                Inv_ID = Convert.ToInt32(row["Inv_ID"]),
                InvoiceNo = row["InvoiceNo"]?.ToString() ?? "",
                InvoiceDate = row["InvoiceDate"] != DBNull.Value ? Convert.ToDateTime(row["InvoiceDate"]) : DateTime.MinValue,
                OpenID = row["OpenID"]?.ToString() ?? "",
                CurrencyCode = row["CurrencyCode"]?.ToString() ?? "",
                ExchangeRate = row["ExchangeRate"] != DBNull.Value ? Convert.ToDecimal(row["ExchangeRate"]) : 0,
                DiscPer = row["DiscPer"] != DBNull.Value ? Convert.ToDecimal(row["DiscPer"]) : 0,
                DiscAmt = row["DiscAmt"] != DBNull.Value ? Convert.ToDecimal(row["DiscAmt"]) : 0,
                GrandTotal = row["GrandTotal"] != DBNull.Value ? Convert.ToDecimal(row["GrandTotal"]) : 0,
                TaxType = row["TaxType"]?.ToString() ?? "",
                SalesmanName = row["SalesmanName"]?.ToString() ?? "",
                CustomerName = row["CustomerName"]?.ToString() ?? ""
            };
        }

        private Purchase MapToPurchase(DataRow row)
        {
            return new Purchase
            {
                ST_ID = Convert.ToInt32(row["ST_ID"]),
                InvoiceNo = row["InvoiceNo"]?.ToString() ?? "",
                Date = row["Date"] != DBNull.Value ? Convert.ToDateTime(row["Date"]) : DateTime.MinValue,
                PurchaseType = row["PurchaseType"]?.ToString() ?? "",
                SubTotal = row["SubTotal"] != DBNull.Value ? Convert.ToDecimal(row["SubTotal"]) : 0,
                GrandTotal = row["GrandTotal"] != DBNull.Value ? Convert.ToDecimal(row["GrandTotal"]) : 0,
                SupplierName = row["SupplierName"]?.ToString() ?? "",
                ProductName = row["ProductName"]?.ToString() ?? "",
                Status = row["Status"]?.ToString() ?? ""
            };
        }

        private Customer MapToCustomer(DataRow row)
        {
            return new Customer
            {
                Cust_ID = Convert.ToInt32(row["Cust_ID"]),
                CustomerName = row["CustomerName"]?.ToString() ?? "",
                ContactNo = row["ContactNo"]?.ToString() ?? "",
                EmailID = row["EmailID"]?.ToString() ?? "",
                Address = row["Address"]?.ToString() ?? "",
                City = row["City"]?.ToString() ?? "",
                State = row["State"]?.ToString() ?? "",
                Country = row["Country"]?.ToString() ?? "",
                AddedDate = row["AddedDate"] != DBNull.Value ? Convert.ToDateTime(row["AddedDate"]) : DateTime.MinValue
            };
        }

        private Supplier MapToSupplier(DataRow row)
        {
            return new Supplier
            {
                SupplierID = Convert.ToInt32(row["SupplierID"]),
                SupplierName = row["SupplierName"]?.ToString() ?? "",
                ContactNo = row["ContactNo"]?.ToString() ?? "",
                EmailID = row["EmailID"]?.ToString() ?? "",
                Address = row["Address"]?.ToString() ?? "",
                City = row["City"]?.ToString() ?? "",
                State = row["State"]?.ToString() ?? "",
                Country = row["Country"]?.ToString() ?? "",
                AddedDate = row["AddedDate"] != DBNull.Value ? Convert.ToDateTime(row["AddedDate"]) : DateTime.MinValue
            };
        }
    }

    // ============================================
    // MISSING MODEL CLASSES
    // ============================================

    public class Customer
    {
        public int Cust_ID { get; set; }
        public string CustomerName { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string EmailID { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime AddedDate { get; set; }
    }

    public class Supplier
    {
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string EmailID { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime AddedDate { get; set; }
    }
}