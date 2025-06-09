namespace HelpersCampEmail.Models
{
    public class FailedApplicantResult
    {
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public string? ErrorMessage { get; set; }

    }
}
