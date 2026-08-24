namespace AppUserBlazor.Models
{
    public class AddUserToApplicationRequest
    {
        public int ApplicationID { get; set; }
        public int RoleID { get; set; }
        public int CreatedBy { get; set; }
    }
}