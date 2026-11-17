namespace AppUserBlazor.Models
{
    public class ApplicationLookup
    {
        public int ApplicationID { get; set; }

        public string ApplicationCode { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}