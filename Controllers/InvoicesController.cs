using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using AlphaPlusAPI.Services;
using Dapper;

namespace AlphaPlusAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger<InvoicesController> _logger;
        private readonly string _connectionString;

        public InvoicesController(
            DatabaseService databaseService, 
            ILogger<InvoicesController> logger,
            IConfiguration configuration)
        {
            _databaseService = databaseService;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("Connection string not found");
        }

        // ✅ CRITICAL: GET MAX INVOICE ID - REQUIRED FOR FRONTEND
        [HttpGet("max-id")]
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

        // ✅ GET NEXT INVOICE NUMBER
        [HttpGet("next-number")]
        public async Task<IActionResult> GetNextInvoiceNumber()
        {
            try
            {
                _logger.LogInformation("=== GetNextInvoiceNumber called ===");

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = "SELECT ISNULL(MAX(Inv_ID), 0) AS MaxId FROM InvoiceInfo";
                var maxId = await connection.ExecuteScalarAsync<int>(query);
                
                var nextId = maxId + 1;
                var nextInvoiceNo = $"RCT-{nextId:D5}";
                
                _logger.LogInformation($"Next Invoice: {nextInvoiceNo} (ID: {nextId})");

                return Ok(new
                {
                    success = true,
                    message = "Next invoice number generated",
                    data = new
                    {
                        invoiceId = nextId,
                        invoiceNo = nextInvoiceNo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating next invoice number");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        // GET: api/Invoices
        [HttpGet]
        public async Task<IActionResult> GetInvoices([FromQuery] string? fromDate = null, [FromQuery] string? toDate = null)
        {
            try
            {
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
                    WHERE (@FromDate IS NULL OR InvoiceDate >= @FromDate)
                      AND (@ToDate IS NULL OR InvoiceDate <= @ToDate)
                    ORDER BY InvoiceDate DESC";

                var parameters = new[]
                {
                    new SqlParameter("@FromDate", string.IsNullOrEmpty(fromDate) ? DBNull.Value : (object)fromDate),
                    new SqlParameter("@ToDate", string.IsNullOrEmpty(toDate) ? DBNull.Value : (object)toDate)
                };

                var invoices = await _databaseService.ExecuteQueryAsync(query, parameters);

                return Ok(new
                {
                    success = true,
                    message = "Invoices retrieved successfully",
                    data = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error retrieving invoices: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        // GET: api/Invoices/today
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayInvoices()
        {
            try
            {
                var today = DateTime.Today.ToString("yyyy-MM-dd");
                
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
                        ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID AND UPPER(LTRIM(RTRIM(PaymentMode))) = 'CASH'), 0) as TotalCash,
                        ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID AND UPPER(LTRIM(RTRIM(PaymentMode))) IN ('MPESA', 'M-PESA', 'MOBILE MONEY', 'MOBILE PAYMENT')), 0) as TotalMPesa,
                        ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID AND UPPER(LTRIM(RTRIM(PaymentMode))) IN ('CREDIT', 'CREDIT CUSTOMER', 'CREDIT CARD')), 0) as TotalCredit
                    FROM InvoiceInfo i
                    WHERE CAST(i.InvoiceDate AS DATE) = @Today
                    ORDER BY i.InvoiceDate DESC";

                var parameters = new[]
                {
                    new SqlParameter("@Today", today)
                };

                var invoices = await _databaseService.ExecuteQueryAsync(query, parameters);

                return Ok(new
                {
                    success = true,
                    message = "Today's invoices retrieved successfully",
                    data = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving today's invoices");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error retrieving today's invoices: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        // GET: api/Invoices/unpaid
        [HttpGet("unpaid")]
        public async Task<IActionResult> GetUnpaidInvoices()
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
                        ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID), 0) as PaidAmount,
                        i.GrandTotal - ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID), 0) as Outstanding,
                        CASE 
                            WHEN ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID), 0) = 0 THEN 'Not Paid'
                            WHEN ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID), 0) < i.GrandTotal THEN 'Partially Paid'
                            ELSE 'Paid'
                        END as PaymentStatus,
                        STUFF((SELECT ', ' + PaymentMode FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID FOR XML PATH('')), 1, 2, '') as PaymentModes
                    FROM InvoiceInfo i
                    WHERE i.GrandTotal > ISNULL((SELECT SUM(Amount) FROM Invoice_Payment WHERE InvoiceID = i.Inv_ID), 0)
                    ORDER BY i.InvoiceDate DESC";

                var invoices = await _databaseService.ExecuteQueryAsync(query, null);

                return Ok(new
                {
                    success = true,
                    message = "Unpaid invoices retrieved successfully",
                    data = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unpaid invoices");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        // GET: api/Invoices/{id}
        [HttpGet("{id}")]
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
                        i.TaxType
                    FROM InvoiceInfo i
                    WHERE i.Inv_ID = @InvoiceId";

                var parameters = new[]
                {
                    new SqlParameter("@InvoiceId", id)
                };

                var invoiceResult = await _databaseService.ExecuteQueryAsync(query, parameters);

                if (invoiceResult.Rows.Count == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Invoice not found",
                        data = (object?)null
                    });
                }

                // Get invoice products
                var productsQuery = @"
                    SELECT 
                        ip.Inv_Prod_ID,
                        ip.ProductID,
                        ip.Barcode,
                        ip.Qty,
                        ip.SalesRate as Rate,
                        ip.TotalAmount,
                        ip.Margin,
                        ip.PurchaseRate,
                        ip.DiscountPer,
                        ip.Discount,
                        ip.VATPer,
                        ip.VAT
                    FROM Invoice_Product ip
                    WHERE ip.InvoiceID = @InvoiceId";

                var productsResult = await _databaseService.ExecuteQueryAsync(productsQuery, parameters);

                // Get invoice payments
                var paymentsQuery = @"
                    SELECT 
                        PaymentID,
                        PaymentMode,
                        Amount,
                        PaymentDate
                    FROM Invoice_Payment
                    WHERE InvoiceID = @InvoiceId";

                var paymentsResult = await _databaseService.ExecuteQueryAsync(paymentsQuery, parameters);

                return Ok(new
                {
                    success = true,
                    message = "Invoice retrieved successfully",
                    data = new
                    {
                        invoice = invoiceResult.Rows[0],
                        products = productsResult,
                        payments = paymentsResult
                    }
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

        // GET: api/Invoices/recent
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentInvoices([FromQuery] int count = 10)
        {
            try
            {
                var query = @"
                    SELECT TOP (@Count)
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
                    ORDER BY InvoiceDate DESC, Inv_ID DESC";

                var parameters = new[]
                {
                    new SqlParameter("@Count", count)
                };

                var invoices = await _databaseService.ExecuteQueryAsync(query, parameters);

                return Ok(new
                {
                    success = true,
                    message = $"Recent {count} invoices retrieved successfully",
                    data = invoices
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent invoices");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error retrieving recent invoices: {ex.Message}",
                    data = (object?)null
                });
            }
        }

        // GET: api/Invoices/health
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "Healthy",
                message = "Invoices API is running",
                timestamp = DateTime.Now,
                version = "1.0.0"
            });
        }
    }
}