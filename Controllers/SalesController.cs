using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System;
using System.Threading.Tasks;
using AlphaPlusAPI.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Data;
using AlphaPlusAPI.Services;
using Microsoft.Extensions.Configuration;

namespace AlphaPlusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly string? _connectionString;
        private readonly ILogger<SalesController> _logger;
        private readonly DatabaseService _db;

        public SalesController(IConfiguration configuration, ILogger<SalesController> logger, DatabaseService databaseService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException("DefaultConnection string is missing");
            _logger = logger;
            _db = databaseService;
        }

        [HttpGet("max-id")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMaxInvoiceId()
        {
            try
            {
                _logger.LogInformation("=== GetMaxInvoiceId called ===");

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = "SELECT ISNULL(MAX(Inv_ID), 0) AS MaxId FROM InvoiceInfo";
                
                var maxId = await connection.ExecuteScalarAsync<int>(query);
                
                _logger.LogInformation($"Max Invoice ID: {maxId}");

                return Ok(new
                {
                    success = true,
                    message = "Max invoice ID retrieved successfully",
                    data = maxId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving max invoice ID");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    data = (int?)null
                });
            }
        }

        [HttpGet("next-number")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNextInvoiceNumber()
        {
            try
            {
                _logger.LogInformation("=== GetNextInvoiceNumber called ===");

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                await using var transaction = connection.BeginTransaction();

                const string query = @"
                    SELECT MAX(CAST(REPLACE(InvoiceNo, 'RCT-', '') AS INT)) 
                    FROM InvoiceInfo 
                    WHERE InvoiceNo LIKE 'RCT-%' 
                    AND ISNUMERIC(REPLACE(InvoiceNo, 'RCT-', '')) = 1";
                
                var result = await connection.ExecuteScalarAsync<object>(query, transaction: transaction);

                int maxInvoiceNo = 0;
                if (result != null && result != DBNull.Value)
                {
                    maxInvoiceNo = Convert.ToInt32(result);
                }
                
                int nextNumber = maxInvoiceNo + 1;
                string nextInvoiceNo = $"RCT-{nextNumber}";
                
                const string nextIdQuery = "SELECT ISNULL(MAX(Inv_ID), 0) + 1 FROM InvoiceInfo";
                int nextInvId = await connection.ExecuteScalarAsync<int>(nextIdQuery, transaction: transaction);

                await transaction.CommitAsync();

                _logger.LogInformation($"✅ Next Invoice Generated: ID={nextInvId}, No={nextInvoiceNo}");

                return Ok(new
                {
                    success = true,
                    message = "Next invoice number retrieved successfully",
                    data = new
                    {
                        invoiceId = nextInvId,
                        invoiceNo = nextInvoiceNo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving next invoice number");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok(new { 
                success = true, 
                message = "SalesController is working!",
                timestamp = DateTime.Now
            });
        }

        [HttpPost("save")]
        [AllowAnonymous]
        public async Task<IActionResult> SaveSale([FromBody] SaveSaleRequest request)
        {
            _logger.LogInformation("🟢 === SAVE SALE ENDPOINT CALLED ===");
            _logger.LogInformation($"📦 Received InvId: {request.InvId}");
            _logger.LogInformation($"📦 Request - InvoiceNo: {request.InvoiceNo}, Products: {request.Products?.Count}, Payments: {request.Payments?.Count}");
            _logger.LogInformation($"💰 Grand Total: {request.GrandTotal}, Customer: {request.CustomerName}, Salesman: {request.SalesmanName}");

            if (request.InvId <= 0)
            {
                _logger.LogError($"❌ INVALID InvId received: {request.InvId}");
                return BadRequest(new 
                { 
                    success = false, 
                    message = $"Invalid Invoice ID: {request.InvId}. Must be greater than 0." 
                });
            }

            if (request.Products == null || request.Products.Count == 0)
            {
                _logger.LogWarning("⚠️ No products in request");
                return BadRequest(new { success = false, message = "No products in sale" });
            }

            if (string.IsNullOrWhiteSpace(request.InvoiceNo) || !request.InvoiceNo.StartsWith("RCT-"))
            {
                _logger.LogError($"❌ Invalid InvoiceNo format: {request.InvoiceNo}");
                return BadRequest(new { success = false, message = $"Invalid invoice number format: {request.InvoiceNo}" });
            }

            await using var connection = new SqlConnection(_connectionString);

            try
            {
                await connection.OpenAsync();
                _logger.LogInformation("✅ Database connection opened");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to database");
                return BadRequest(new { success = false, message = "Database connection failed: " + ex.Message });
            }

            await using var transaction = connection.BeginTransaction();

            try
            {
                _logger.LogInformation("🚀 Starting sale transaction...");

                int invoiceIdToUse = request.InvId;
                string invoiceNo = request.InvoiceNo;

                _logger.LogInformation($"✅ Using from request: InvId={invoiceIdToUse}, InvoiceNo={invoiceNo}");

                const string checkIdQuery = "SELECT COUNT(*) FROM InvoiceInfo WHERE Inv_ID = @InvId";
                int existingCount = await connection.ExecuteScalarAsync<int>(checkIdQuery, new { InvId = invoiceIdToUse }, transaction);
                
                if (existingCount > 0)
                {
                    _logger.LogError($"❌ InvId {invoiceIdToUse} already exists in database!");
                    throw new Exception($"Invoice ID {invoiceIdToUse} already exists. Please generate a new invoice number.");
                }

                const string checkNoQuery = "SELECT COUNT(*) FROM InvoiceInfo WHERE InvoiceNo = @InvoiceNo";
                int existingNoCount = await connection.ExecuteScalarAsync<int>(checkNoQuery, new { InvoiceNo = invoiceNo }, transaction);
                
                if (existingNoCount > 0)
                {
                    _logger.LogError($"❌ InvoiceNo {invoiceNo} already exists in database!");
                    throw new Exception($"Invoice number {invoiceNo} already exists. Please generate a new invoice number.");
                }

                var parsedDate = DateTime.TryParse(request.InvoiceDate, out var invoiceDate)
                    ? invoiceDate
                    : DateTime.Now;

                _logger.LogInformation($"📅 Using invoice date: {parsedDate:yyyy-MM-dd HH:mm:ss}");

                _logger.LogInformation("💾 Inserting into InvoiceInfo with IDENTITY_INSERT...");
                
                const string invoiceQuery = @"
    INSERT INTO InvoiceInfo (
        InvoiceNo, InvoiceDate, OpenID, CurrencyCode, ExchangeRate,
        GrandTotal, Cash, Change, TaxType, Member_ID, DiscPer, DiscAmt,
        SalesmanID, CustID, LoyaltyMemberID, LP, BillNote, SalesmanName, CustomerName
    )
    OUTPUT INSERTED.Inv_ID
    VALUES (
        @InvoiceNo, @InvoiceDate, 0, 'KES', 1,
        @GrandTotal, @AmountTendered, @ChangeAmount, 'Inclusive', '', 0, @TotalDiscount,
        '', 0, 0, 0, '', @SalesmanName, @CustomerName
    );";

                var invoiceParams = new
                {
                    InvoiceNo = invoiceNo,
                    InvoiceDate = parsedDate,
                    request.GrandTotal,
                    request.AmountTendered,
                    request.ChangeAmount,
                    request.TotalDiscount,
                    request.SalesmanName,
                    request.CustomerName
                };

                // This will return the auto-generated Inv_ID
                int nextInvId = await connection.ExecuteScalarAsync<int>(invoiceQuery, invoiceParams, transaction);
                _logger.LogInformation($"✅ InvoiceInfo inserted - Generated Inv_ID: {nextInvId}");

                _logger.LogInformation($"📦 Inserting {request.Products.Count} products into Invoice_Product...");
                const string productQuery = @"
                    INSERT INTO Invoice_Product (
                        InvoiceID, ProductID, Barcode, SalesRate, PurchaseRate,
                        DiscountPer, Discount, VATPer, VAT, Qty, TotalAmount, Margin
                    )
                    VALUES (
                        @InvoiceID, @ProductId, @Barcode, @SalesRate, @PurchaseRate,
                        @DiscountPer, @Discount, @VatPer, @Vat, @Quantity, @TotalAmount, @Margin
                    )";

                foreach (var product in request.Products)
                {
                    var productParams = new
                    {
                        InvoiceID = nextInvId,
                        product.ProductId,
                        Barcode = string.IsNullOrWhiteSpace(product.Barcode) ? (object)DBNull.Value : product.Barcode.Trim(),
                        product.SalesRate,
                        product.PurchaseRate,
                        product.DiscountPer,
                        product.Discount,
                        product.VatPer,
                        product.Vat,
                        product.Quantity,
                        product.TotalAmount,
                        product.Margin
                    };

                    await connection.ExecuteAsync(productQuery, productParams, transaction);
                    _logger.LogInformation($"✅ Product inserted - ID: {product.ProductId}, Qty: {product.Quantity}, Total: {product.TotalAmount}");
                }
                _logger.LogInformation("🎉 All products inserted successfully");

                _logger.LogInformation("📉 Deducting stock from Temp_Stock_Company...");
                await DeductStock(connection, transaction, request.Products);
                _logger.LogInformation("📊 Stock deduction completed successfully");

                _logger.LogInformation($"💳 Inserting {request.Payments?.Count ?? 0} payments into Invoice_Payment...");
                
                if (request.Payments != null && request.Payments.Count > 0)
                {
                    const string paymentQuery = @"
                        INSERT INTO Invoice_Payment (
                            InvoiceID, PaymentMode, Amount, PaymentDate
                        )
                        VALUES (
                            @InvoiceID, @PaymentMode, @Amount, @PaymentDate
                        )";

                    foreach (var payment in request.Payments)
                    {
                        var paymentParams = new
                        {
                            InvoiceID = nextInvId,
                            PaymentMode = string.IsNullOrWhiteSpace(payment.PaymentMode) ? "Cash" : payment.PaymentMode.Trim(),
                            payment.Amount,
                            PaymentDate = DateTime.Now
                        };

                        await connection.ExecuteAsync(paymentQuery, paymentParams, transaction);
                        _logger.LogInformation($"✅ Payment inserted - Mode: {payment.PaymentMode}, Amount: {payment.Amount}");
                    }
                    _logger.LogInformation("💰 All payments inserted successfully");
                }
                else
                {
                    _logger.LogInformation("ℹ️ No payments to insert (Order mode)");
                }

                await transaction.CommitAsync();
                _logger.LogInformation("🎊 TRANSACTION COMMITTED SUCCESSFULLY!");
                _logger.LogInformation($"🏁 SALE COMPLETED - Inv_ID: {nextInvId}, InvoiceNo: {invoiceNo}, Total: {request.GrandTotal}");

                return Ok(new
                {
                    success = true,
                    message = "Sale saved successfully!",
                    data = new
                    {
                        invoiceId = nextInvId,
                        invoiceNo = invoiceNo,
                        grandTotal = request.GrandTotal,
                        productsCount = request.Products.Count,
                        paymentsCount = request.Payments?.Count ?? 0,
                        timestamp = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("🔄 Transaction rolled back due to error");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "❌ Failed to rollback transaction");
                }

                _logger.LogError(ex, "💥 ERROR SAVING SALE");
                
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to save sale: {ex.Message}",
                    errorDetails = ex.ToString()
                });
            }
        }

        // --- Remaining methods (GenerateInvoiceNo, DeductStock, TestSaveSale, TestInvoiceAndStock, GetInvoices, GetInvoiceById, SearchInvoices, GetDailyStats, InvoiceInfo class) ---
        private async Task<string> GenerateInvoiceNo(SqlConnection connection, SqlTransaction transaction)
        {
            try
            {
                _logger.LogInformation("🔢 Generating invoice number...");
                
                const string query = @"
                    SELECT MAX(CAST(REPLACE(InvoiceNo, 'RCT-', '') AS INT)) 
                    FROM InvoiceInfo 
                    WHERE InvoiceNo LIKE 'RCT-%' 
                    AND ISNUMERIC(REPLACE(InvoiceNo, 'RCT-', '')) = 1";
                
                var result = await connection.ExecuteScalarAsync<object>(query, transaction: transaction);
                _logger.LogInformation($"📊 Query result: {result}");

                int maxInvoiceNo = 0;
                if (result != null && result != DBNull.Value)
                {
                    maxInvoiceNo = Convert.ToInt32(result);
                }
                
                _logger.LogInformation($"📊 Max invoice number found: {maxInvoiceNo}");
                
                int nextNumber = maxInvoiceNo + 1;
                string newInvoiceNo = $"RCT-{nextNumber}";
                
                _logger.LogInformation($"🎯 Next invoice number: {newInvoiceNo}");
                return newInvoiceNo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating invoice number, using fallback RCT-1");
                return "RCT-1";
            }
        }

        private async Task DeductStock(SqlConnection connection, SqlTransaction transaction, List<SaleProductRequest> products)
        {
            try
            {
                _logger.LogInformation("📉 Starting stock deduction...");

                const string checkStockQuery = @"
                    SELECT Qty 
                    FROM Temp_Stock_Company 
                    WHERE ProductID = @ProductId 
                    AND Barcode = @Barcode";

                const string stockUpdateQuery = @"
                    UPDATE Temp_Stock_Company 
                    SET Qty = Qty - @Quantity 
                    WHERE ProductID = @ProductId 
                    AND Barcode = @Barcode";

                foreach (var product in products)
                {
                    var barcode = string.IsNullOrWhiteSpace(product.Barcode) ? "" : product.Barcode.Trim();

                    var currentStock = await connection.ExecuteScalarAsync<double?>(
                        checkStockQuery,
                        new { product.ProductId, Barcode = barcode },
                        transaction
                    );

                    if (!currentStock.HasValue)
                    {
                        _logger.LogError($"❌ No stock record found for ProductID: {product.ProductId}, Barcode: {barcode}");
                        throw new Exception($"Product '{product.ProductId}' not found in stock. Cannot complete sale.");
                    }

                    if (currentStock.Value < product.Quantity)
                    {
                        _logger.LogError($"❌ Insufficient stock - ProductID: {product.ProductId}, Available: {currentStock.Value}, Needed: {product.Quantity}");
                        throw new Exception($"Insufficient stock for product '{product.ProductId}'. Available: {currentStock.Value}, Needed: {product.Quantity}");
                    }

                    int stockRows = await connection.ExecuteAsync(
                        stockUpdateQuery,
                        new { product.Quantity, product.ProductId, Barcode = barcode },
                        transaction
                    );

                    if (stockRows > 0)
                    {
                        _logger.LogInformation($"✅ Stock deducted - ProductID: {product.ProductId}, Barcode: {barcode}, Qty: -{product.Quantity}");
                    }
                    else
                    {
                        throw new Exception($"Failed to update stock for ProductID: {product.ProductId}");
                    }
                }

                _logger.LogInformation("📊 Stock deduction completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during stock deduction");
                throw;
            }
        }

        [HttpPost("test-save")]
        [AllowAnonymous]
        public async Task<IActionResult> TestSaveSale()
        {
            var testRequest = new SaveSaleRequest
            {
                InvId = 0,
                InvoiceNo = "",
                InvoiceDate = DateTime.Now.ToString("yyyy-MM-dd"),
                CustomerName = "Test Customer",
                SalesmanName = "Test Salesman",
                UserId = "1",
                GrandTotal = 150.0,
                TotalDiscount = 0.0,
                AmountTendered = 150.0,
                ChangeAmount = 0.0,
                Products = new List<SaleProductRequest>
                {
                    new SaleProductRequest
                    {
                        ProductId = 1,
                        ProductCode = "TEST001",
                        Barcode = "123456789",
                        Quantity = 2,
                        SalesRate = 75.0,
                        PurchaseRate = 50.0,
                        DiscountPer = 0.0,
                        Discount = 0.0,
                        VatPer = 0.0,
                        Vat = 0.0,
                        TotalAmount = 150.0,
                        Margin = 25.0,
                        MfgDate = "",
                        ExpiryDate = ""
                    }
                },
                Payments = new List<SalePaymentRequest>
                {
                    new SalePaymentRequest("Cash", 150.0)
                }
            };

            return await SaveSale(testRequest);
        }

        [HttpPost("test-invoice-and-stock")]
        [AllowAnonymous]
        public async Task<IActionResult> TestInvoiceAndStock()
        {
            _logger.LogInformation("🧪 Testing invoice generation and stock deduction...");
            
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            await using var transaction = connection.BeginTransaction();
            
            try
            {
                var invoiceNo = await GenerateInvoiceNo(connection, transaction);
                _logger.LogInformation($"✅ Invoice generation test: {invoiceNo}");
                
                const string stockTestQuery = "SELECT COUNT(*) FROM Temp_Stock_Company WHERE ProductID = 1";
                var stockCount = await connection.ExecuteScalarAsync<int>(stockTestQuery, transaction: transaction);
                _logger.LogInformation($"✅ Stock check test: {stockCount} records found for ProductID 1");
                
                await transaction.CommitAsync();
                
                return Ok(new { 
                    success = true, 
                    invoiceNo = invoiceNo,
                    stockRecords = stockCount,
                    message = "Test completed successfully" 
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvoices([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var whereClause = "";
                var parameters = new List<SqlParameter>();

                if (fromDate.HasValue && toDate.HasValue)
                {
                    whereClause = "WHERE CAST(InvoiceDate AS DATE) BETWEEN @FromDate AND @ToDate";
                    parameters.Add(new SqlParameter("@FromDate", fromDate.Value.Date));
                    parameters.Add(new SqlParameter("@ToDate", toDate.Value.Date));
                }

                var query = $@"SELECT TOP 1000 * FROM InvoiceInfo 
                             {whereClause}
                             ORDER BY Inv_ID DESC";

                var result = await _db.ExecuteQueryAsync(query, parameters.ToArray());
                var invoices = new List<InvoiceInfo>();

                foreach (DataRow row in result.Rows)
                {
                    invoices.Add(MapToInvoiceInfo(row));
                }

                return Ok(new
                {
                    success = true,
                    message = "Invoices retrieved successfully",
                    data = invoices,
                    totalCount = invoices.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices");
                return BadRequest(new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    data = new List<InvoiceInfo>()
                });
            }
        }

        private InvoiceInfo MapToInvoiceInfo(DataRow row)
        {
            return new InvoiceInfo
            {
                Inv_ID = row["Inv_ID"] != DBNull.Value ? Convert.ToInt32(row["Inv_ID"]) : 0,
                InvoiceNo = row["InvoiceNo"]?.ToString() ?? "",
                InvoiceDate = row["InvoiceDate"] != DBNull.Value ? Convert.ToDateTime(row["InvoiceDate"]) : DateTime.MinValue,
                CustomerName = row["CustomerName"]?.ToString() ?? "",
                SalesmanName = row["SalesmanName"]?.ToString() ?? "",
                GrandTotal = row["GrandTotal"] != DBNull.Value ? Convert.ToDouble(row["GrandTotal"]) : 0.0,
                TotalDiscount = row["DiscAmt"] != DBNull.Value ? Convert.ToDouble(row["DiscAmt"]) : 0.0,
                Cash = row["Cash"] != DBNull.Value ? Convert.ToDouble(row["Cash"]) : 0.0,
                Change = row["Change"] != DBNull.Value ? Convert.ToDouble(row["Change"]) : 0.0
            };
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvoiceById(int id)
        {
            try
            {
                var query = @"
                    SELECT 
                        i.Inv_ID,
                        i.InvoiceNo,
                        i.InvoiceDate,
                        i.CustomerName,
                        i.SalesmanName,
                        i.GrandTotal,
                        i.Cash,
                        i.Change,
                        i.DiscAmt,
                        i.DiscPer,
                        i.TaxType,
                        i.CurrencyCode,
                        i.ExchangeRate,
                        i.OpenID
                    FROM InvoiceInfo i
                    WHERE i.Inv_ID = @InvoiceId";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@InvoiceId", id)
                };

                var invoiceResult = await _db.ExecuteQueryAsync(query, parameters);

                if (invoiceResult.Rows.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Invoice not found",
                        data = (object?)null
                    });
                }

                var productsQuery = @"
                    SELECT 
                        ip.Inv_Prod_ID,
                        ip.ProductID,
                        p.ProductName,
                        p.ProductCode,
                        ip.Qty,
                        ip.Rate,
                        ip.TotalAmount,
                        ip.Margin,
                        ip.PurchaseRate
                    FROM Invoice_Product ip
                    INNER JOIN Product p ON ip.ProductID = p.PID
                    WHERE ip.InvoiceID = @InvoiceId";

                var productsResult = await _db.ExecuteQueryAsync(productsQuery, parameters);

                var paymentsQuery = @"
                    SELECT 
                        PaymentID,
                        PaymentMode,
                        Amount,
                        Reference,
                        PaymentDate
                    FROM Invoice_Payment
                    WHERE InvoiceID = @InvoiceId";

                var paymentsResult = await _db.ExecuteQueryAsync(paymentsQuery, parameters);

                var invoiceData = new
                {
                    invoice = invoiceResult.Rows[0],
                    products = productsResult,
                    payments = paymentsResult
                };

                return Ok(new
                {
                    success = true,
                    message = "Invoice retrieved successfully",
                    data = invoiceData
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice {InvoiceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error retrieving invoice: {ex.Message}",
                    data = (object?)null
                });
            }
        }
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchInvoices([FromQuery] string queryTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(queryTerm))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Search term is required",
                        data = (object?)null
                    });
                }

                var query = @"
                    SELECT 
                        Inv_ID,
                        InvoiceNo,
                        InvoiceDate,
                        CustomerName,
                        SalesmanName,
                        GrandTotal,
                        Cash,
                        Change,
                        DiscAmt
                    FROM InvoiceInfo
                    WHERE InvoiceNo LIKE @QueryTerm
                       OR CustomerName LIKE @QueryTerm
                       OR SalesmanName LIKE @QueryTerm
                    ORDER BY InvoiceDate DESC";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@QueryTerm", $"%{queryTerm}%")
                };

                var invoices = await _db.ExecuteQueryAsync(query, parameters);

                return Ok(new
                {
                    success = true,
                    message = "Invoices search completed successfully",
                    data = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching invoices");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error searching invoices: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        [HttpGet("stats/daily")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDailyStats([FromQuery] string? date = null)
        {
            try
            {
                var targetDate = string.IsNullOrEmpty(date) ? DateTime.Today.ToString("yyyy-MM-dd") : date;

                var query = @"
                    SELECT 
                        COUNT(*) as TotalInvoices,
                        ISNULL(SUM(GrandTotal), 0) as TotalSales,
                        ISNULL(SUM(DiscAmt), 0) as TotalDiscount,
                        ISNULL(AVG(GrandTotal), 0) as AverageSale,
                        MIN(GrandTotal) as MinSale,
                        MAX(GrandTotal) as MaxSale
                    FROM InvoiceInfo
                    WHERE CAST(InvoiceDate AS DATE) = @Date";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@Date", targetDate)
                };

                var stats = await _db.ExecuteQueryAsync(query, parameters);

                if (stats.Rows.Count > 0)
                {
                    var row = stats.Rows[0];
                    var result = new
                    {
                        TotalInvoices = Convert.ToInt32(row["TotalInvoices"]),
                        TotalSales = Convert.ToDecimal(row["TotalSales"]),
                        TotalDiscount = Convert.ToDecimal(row["TotalDiscount"]),
                        AverageSale = Convert.ToDecimal(row["AverageSale"]),
                        MinSale = Convert.ToDecimal(row["MinSale"]),
                        MaxSale = Convert.ToDecimal(row["MaxSale"])
                    };

                    return Ok(new
                    {
                        success = true,
                        message = "Daily stats retrieved successfully",
                        data = result
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No data found for the specified date",
                        data = new
                        {
                            TotalInvoices = 0,
                            TotalSales = 0,
                            TotalDiscount = 0,
                            AverageSale = 0,
                            MinSale = 0,
                            MaxSale = 0
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily stats");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error retrieving daily stats: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        public class InvoiceInfo
        {
            public int Inv_ID { get; set; }
            public string InvoiceNo { get; set; } = string.Empty;
            public DateTime InvoiceDate { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string SalesmanName { get; set; } = string.Empty;
            public double GrandTotal { get; set; }
            public double TotalDiscount { get; set; }
            public double Cash { get; set; }
            public double Change { get; set; }
        }
    }
}
