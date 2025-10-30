namespace AlphaPlusAPI.Models
{
    public class InvoiceInfo
    {
        public int Inv_ID { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? OpenID { get; set; }
        public string? CurrencyCode { get; set; }
        public decimal? ExchangeRate { get; set; }
        public decimal? DiscPer { get; set; }
        public decimal? DiscAmt { get; set; }
        public decimal? GrandTotal { get; set; }
        public string? TaxType { get; set; }
        public int? Member_ID { get; set; }
        public string? SalesmanID { get; set; }
        public decimal? Cash { get; set; }
        public decimal? Change { get; set; }
        public int? CustID { get; set; }
        public int? LoyaltyMemberID { get; set; }
        public string? BillNote { get; set; }
        public decimal? LP { get; set; }
        public string? SalesmanName { get; set; }
        public string? CustomerName { get; set; }
        public bool? IsMerged { get; set; }
    }

    public class InvoiceProduct
    {
        public int IPo_ID { get; set; }
        public string? InvoiceID { get; set; }
        public int? ProductID { get; set; }
        public string? Barcode { get; set; }
        public DateTime? MfgDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? SalesRate { get; set; }
        public decimal? DiscountPer { get; set; }
        public decimal? Discount { get; set; }
        public decimal? VATPer { get; set; }
        public decimal? VAT { get; set; }
        public decimal? Qty { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? PurchaseRate { get; set; }
        public decimal? Margin { get; set; }
        public string? TillID { get; set; }
    }

    public class InvoicePayment
    {
        public int IP_ID { get; set; }
        public string? InvoiceID { get; set; }
        public string? PaymentMode { get; set; }
        public decimal? Amount { get; set; }
    }
}

