using CsvHelper.Configuration.Attributes;

namespace HelpersCampEmail.Models
{
    public class Trainee
    {
        public int Id { get; set; }

        [Name("Code")]
        public string Code { get; set; } = string.Empty;
        [Name("Email")]
        public string Email { get; set; } = string.Empty;
        [Name("Result")]
        public string Status { get; set; } = string.Empty;

        [Name("Name")]
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
    }
}
