using HelpersCampEmail.Models;

namespace HelpersCampEmail.Repositories
{
    public interface IApplicantRepository
    {
        Task<IEnumerable<Trainee>> GetNotSentAsync();
        Task AddTraineesAsync(IEnumerable<Trainee> trainees);
        Task<int> CountAsync();
        Task<int> CountSentAsync();
        Task<int> CountNotSentAsync();
        Task<IEnumerable<EmailLog>> GetHistoryAsync();
        Task<IEnumerable<object>> GetFailedAttemptsAsync();
        Task<IEnumerable<object>> GetLastTriesAsync();
        Task<IEnumerable<object>> GetAllStatusAsync();
        Task<IEnumerable<object>> SearchAsync(string? keyword, string orderBy, bool descending, bool sentOnly, bool notSentOnly);
        Task DeleteAllAsync();
        Task<Trainee?> GetByIdAsync(int id);
        Task<IEnumerable<Trainee>> GetAllAsync();
        Task UpdateEmailAsync(string code, string newMail);
    }
}
