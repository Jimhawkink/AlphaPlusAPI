namespace AlphaPlusAPI.DTOs
{
    public class LoginRequest
    {
        public string UserID { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserData? User { get; set; }
    }

    public class UserData
    {
        public string UserID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string? EmailID { get; set; }
        public string? ContactNo { get; set; }
        public bool Active { get; set; }
        public List<UserRightsData>? Rights { get; set; }
    }

    public class UserRightsData
    {
        public string ModuleName { get; set; } = string.Empty;
        public bool CanSave { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
        public bool CanView { get; set; }
    }

    public class CreateUserRequest
    {
        public string UserID { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string? ContactNo { get; set; }
        public string? EmailID { get; set; }
        public string? SSN { get; set; }
        public string? PayrollType { get; set; }
    }
}

