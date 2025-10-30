namespace AlphaPlusAPI.Models
{
    public class Product
    {
        public int PID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string? VATCommodity { get; set; }
        public string? Description { get; set; }
        public string? Barcode { get; set; }
        public string? Category { get; set; }
        public string? PurchaseUnit { get; set; }
        public string? SalesUnit { get; set; }
        public decimal? PurchaseCost { get; set; }
        public decimal? SalesCost { get; set; }
        public int? ReorderPoint { get; set; }
        public DateTime? AddedDate { get; set; }
        public decimal? MarginPer { get; set; }
        public bool? ShowPS { get; set; }
        public string? ButtonUIColor { get; set; }
        public byte[]? Photo { get; set; }
        public string? HSCode { get; set; }
        public string? BatchNo { get; set; }
        public string? SupplierName { get; set; }
        
        // This property is calculated from Temp_Stock_Company table
        // and represents the total available quantity in stock
        public decimal AvailableQty { get; set; }
    }
}