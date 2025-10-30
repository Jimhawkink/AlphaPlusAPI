namespace AlphaPlusAPI.DTOs
{
    public class SyncRequest
    {
        public DateTime? LastSyncTime { get; set; }
        public string? DeviceID { get; set; }
    }

    public class SyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime SyncTime { get; set; }
        public SyncData? Data { get; set; }
    }

    public class SyncData
    {
        public List<object>? Products { get; set; }
        public List<object>? Invoices { get; set; }
        public List<object>? Purchases { get; set; }
        public List<object>? Users { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public int? TotalCount { get; set; }
    }
}

