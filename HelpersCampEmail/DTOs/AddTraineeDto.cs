namespace HelpersCampEmail.DTOs
{
    public class AddTraineeDto
    {
        public string Code { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Status { get; set; } = "Accept"; // خيار افتراضي
    }
}
