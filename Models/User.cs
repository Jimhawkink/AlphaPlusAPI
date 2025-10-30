namespace AlphaPlusAPI.Models
{
    public class User
    {
        public string UserID { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ContactNo { get; set; }
        public string? SSN { get; set; }
        public string? EmailID { get; set; }
        public DateTime? JoiningDate { get; set; }
        public bool Active { get; set; }
        public string? PayrollType { get; set; }
        public string? VoidPassword { get; set; }
    }

    public class UserRights
    {
        public int ID { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public bool UR_Save { get; set; }
        public bool UR_Update { get; set; }
        public bool UR_Delete { get; set; }
        public bool UR_View { get; set; }
        public string UserID { get; set; } = string.Empty;
    }
}

