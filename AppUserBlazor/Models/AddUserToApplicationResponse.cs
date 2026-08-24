namespace AppUserBlazor.Models
{
    public class AddUserToApplicationResponse
    {
        public int ReturnCode { get; set; }
        public int UserID { get; set; }
        public int ApplicationID { get; set; }
        public int RoleID { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}