namespace AlphaPlusAPI.Models
{
    public class DashboardSummary
    {
        public decimal TotalSales { get; set; }
        public decimal CashSales { get; set; }
        public decimal MpesaSales { get; set; }
        public decimal CreditSales { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal NetSales { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
    }
}
