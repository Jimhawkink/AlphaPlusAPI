namespace AlphaPlusAPI.Models
{
    public class Purchase
    {
        public int ST_ID { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime? Date { get; set; }
        public string? PurchaseType { get; set; }
        public int? Supplier_ID { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? DiscountPer { get; set; }
        public decimal? Discount { get; set; }
        public decimal? PreviousDue { get; set; }
        public decimal? FreightCharges { get; set; }
        public decimal? OtherCharges { get; set; }
        public decimal? Total { get; set; }
        public decimal? RoundOff { get; set; }
        public decimal? GrandTotal { get; set; }
        public decimal? TotalPayment { get; set; }
        public decimal? PaymentDue { get; set; }
        public string? Remarks { get; set; }
        public decimal? VATPer { get; set; }
        public decimal? VAT { get; set; }
        public string? ReferenceNo1 { get; set; }
        public string? ReferenceNo2 { get; set; }
        public string? SupplierInvoiceNo { get; set; }
        public DateTime? SupplierInvoiceDate { get; set; }
        public string? TaxType { get; set; }
        public string? SupplierName { get; set; }
        public string? ProductName { get; set; }
        public string? Status { get; set; }
    }

    public class PurchaseJoin
    {
        public int SP_ID { get; set; }
        public int? PurchaseID { get; set; }
        public int? ProductID { get; set; }
        public decimal? Qty { get; set; }
        public decimal? Price { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? StorageType { get; set; }
        public string? Warehouse_Store { get; set; }
        public decimal? SalesCost { get; set; }
        public string? Barcode { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ManufacturingDate { get; set; }
        public string? RackNo { get; set; }
        public string? ProductName { get; set; }
        public string? SupplierName { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime? Date { get; set; }
    }
}

