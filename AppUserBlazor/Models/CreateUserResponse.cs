namespace AppUserBlazor.Models
{
    public class CreateUserResponse
    {
        public int ReturnCode { get; set; }
        public int? UserID { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
    }
}