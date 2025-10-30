using System;
using System.Collections.Generic;

namespace AlphaPlusAPI.DTOs
{
    public class SaveSaleRequest
    {
        public int InvId { get; set; }
        public string InvoiceNo { get; set; } = "";
        public string InvoiceDate { get; set; } = "";
        public string UserId { get; set; } = "";
        public string SalesmanName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public double GrandTotal { get; set; }
        public double TotalDiscount { get; set; }
        public double AmountTendered { get; set; }
        public double ChangeAmount { get; set; }
        public List<SaleProductRequest> Products { get; set; } = new();
        public List<SalePaymentRequest> Payments { get; set; } = new();
    }

    public class SaleProductRequest
    {
        public int ProductId { get; set; }
        public string ProductCode { get; set; } = "";  // Added this field
        public string Barcode { get; set; } = "";
        public double Quantity { get; set; }
        public double SalesRate { get; set; }
        public double PurchaseRate { get; set; }
        public double DiscountPer { get; set; }
        public double Discount { get; set; }
        public double VatPer { get; set; }
        public double Vat { get; set; }
        public double TotalAmount { get; set; }
        public double Margin { get; set; }
        public string MfgDate { get; set; } = "";      // Added this field
        public string ExpiryDate { get; set; } = "";   // Added this field
    }

    public class SalePaymentRequest
    {
        public string PaymentMode { get; set; } = "";
        public double Amount { get; set; }

        public SalePaymentRequest() { }
        
        public SalePaymentRequest(string paymentMode, double amount)
        {
            PaymentMode = paymentMode;
            Amount = amount;
        }
    }
}