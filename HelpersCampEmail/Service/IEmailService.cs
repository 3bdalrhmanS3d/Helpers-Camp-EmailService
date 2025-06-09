using HelpersCampEmail.Models;

namespace HelpersCampEmail.Service
{
    public interface IEmailService
    {
        Task<List<EmailResult>> SendAllAsync();
        
        Task<EmailResult> SendSingleAsync(string code,string email, string fullName, bool accepted);
    }
}
