using CsvHelper.Configuration;
using HelpersCampEmail.Models;

namespace HelpersCampEmail.Maps
{
    public sealed class TraineeMap : ClassMap<Trainee>
    {
        public TraineeMap()
        {
            Map(m => m.Code).Name("Code", "الكود");
            Map(m => m.Email).Name("Email", "البريد الإلكتروني");
            Map(m => m.Status).Name("Result", "النتيجة", "الحالة");
            Map(m => m.FullName).Name("Name", "الاسم");
            // Ignore CreatedAt in CSV
            Map(m => m.CreatedAt).Ignore();
        }
    }
}
