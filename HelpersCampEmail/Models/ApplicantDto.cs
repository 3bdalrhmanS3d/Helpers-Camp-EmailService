using CsvHelper.Configuration;

namespace HelpersCampEmail.Models
{
    public class ApplicantDto
    {
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class ApplicantMap : ClassMap<ApplicantDto>
    {
        public ApplicantMap()
        {
            Map(m => m.Code).Index(0);
            Map(m => m.Email).Index(1);
            Map(m => m.Status).Index(2);
            Map(m => m.FullName).Index(3);
        }
    }
}
