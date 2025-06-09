namespace HelpersCampEmail.Models
{
    public class EmailLog
    {
        public int Id { get; set; }
        public int ApplicantId { get; set; }
        public Trainee Applicant { get; set; } = default!;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}