namespace HelpersCampEmail.Models
{
    public class EmailResult
    {
        public string Email { get; set; } = string.Empty;
        public bool Success { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
