using System.Text.Json.Serialization;

namespace AlphaPlusAPI.DTOs
{
    /// <summary>
    /// Dashboard statistics data transfer object
    /// Contains all metrics needed for the dashboard cards
    /// </summary>
    public class DashboardStatsDto
    {
        /// <summary>
        /// Total sales amount for the period
        /// </summary>
        [JsonPropertyName("todaysSales")]
        public decimal TodaysSales { get; set; }

        /// <summary>
        /// Total cash payments
        /// </summary>
        [JsonPropertyName("cashSales")]
        public decimal CashSales { get; set; }

        /// <summary>
        /// Total M-Pesa/mobile money payments
        /// </summary>
        [JsonPropertyName("mpesaSales")]
        public decimal MpesaSales { get; set; }

        /// <summary>
        /// Total credit sales
        /// </summary>
        [JsonPropertyName("creditSales")]
        public decimal CreditSales { get; set; }

        /// <summary>
        /// Total discount amount given
        /// </summary>
        [JsonPropertyName("totalDiscount")]
        public decimal TotalDiscount { get; set; }

        /// <summary>
        /// Total returns amount
        /// </summary>
        [JsonPropertyName("totalReturns")]
        public decimal TotalReturns { get; set; }

        /// <summary>
        /// Total profit calculated from sales
        /// </summary>
        [JsonPropertyName("totalProfit")]
        public decimal TotalProfit { get; set; }

        /// <summary>
        /// Number of products in inventory
        /// </summary>
        [JsonPropertyName("productCount")]
        public int ProductCount { get; set; }

        /// <summary>
        /// Number of invoices for the period
        /// </summary>
        [JsonPropertyName("invoiceCount")]
        public int InvoiceCount { get; set; }

        /// <summary>
        /// Number of products below reorder point
        /// </summary>
        [JsonPropertyName("lowStockCount")]
        public int LowStockCount { get; set; }

        /// <summary>
        /// Net sales after deducting returns and discounts
        /// </summary>
        [JsonPropertyName("netSales")]
        public decimal NetSales { get; set; } // <-- now writable
    }
}
