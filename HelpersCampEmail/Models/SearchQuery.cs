namespace HelpersCampEmail.Models
{
    public class SearchQuery
    {
        public string? Keyword { get; set; }
        public string? OrderBy { get; set; } = "name"; // name | date
        public bool Descending { get; set; } = false;
    }
}
