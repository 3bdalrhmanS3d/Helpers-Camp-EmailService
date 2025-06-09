using HelpersCampEmail.App;
using HelpersCampEmail.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HelpersCampEmail.Service
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtp;
        private readonly string _campName;
        private readonly string _templatesRoot;
        private readonly AppDbContext _context;

        public EmailService(
            IConfiguration config,
            IOptions<SmtpSettings> smtpOpts,
            IWebHostEnvironment env,
            AppDbContext context)
        {
            _smtp = smtpOpts.Value;
            _campName = config["Camp"]!;
            _templatesRoot = Path.Combine(env.ContentRootPath, "Templates");
            _context = context;
        }

        // ✅ إرسال لكل من لم يُرسل له بنجاح
        public async Task<List<EmailResult>> SendAllAsync()
        {
            var trainees = await _context.Applicants
                .Where(t => t.Status == "Accept" && !t.EmailLogs.Any(e => e.Success))
                .ToListAsync();

            return await SendBatchAsync(trainees);
        }

        public async Task<EmailResult> SendSingleAsync(string code, string email, string fullName, bool accepted)
        {
            var tempTrainee = new Trainee
            {
                Email = email,
                Code = code,
                FullName = fullName,
                Status = accepted ? "Accept" : "Reject"
            };

            return await SendSingleInternalAsync(tempTrainee);
        }

        private async Task<List<EmailResult>> SendBatchAsync(List<Trainee> trainees)
        {
            var results = new List<EmailResult>();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.SmtpServer, _smtp.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtp.Email, _smtp.Password);

            foreach (var trainee in trainees)
            {
                var result = await SendSingleInternalAsync(client, trainee);
                results.Add(result);
                await Task.Delay(100);
            }

            await client.DisconnectAsync(true);
            return results;
        }

        private async Task<EmailResult> SendSingleInternalAsync(Trainee trainee)
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.SmtpServer, _smtp.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtp.Email, _smtp.Password);

            var result = await SendSingleInternalAsync(client, trainee);
            await client.DisconnectAsync(true);
            return result;
        }

        private async Task<EmailResult> SendSingleInternalAsync(SmtpClient client, Trainee trainee)
        {
            var result = new EmailResult
            {
                Email = trainee.Email,
                TimestampUtc = DateTime.UtcNow
            };

            try
            {
                var message = BuildMessage(trainee);
                await client.SendAsync(message);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            // ✅ تسجل المحاولة لو المتدرب محفوظ في القاعدة
            var existing = await _context.Applicants.FirstOrDefaultAsync(t => t.Email == trainee.Email);
            if (existing != null)
            {
                _context.EmailLogs.Add(new EmailLog
                {
                    ApplicantId = existing.Id,
                    SentAt = result.TimestampUtc,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage
                });
                await _context.SaveChangesAsync();
            }

            return result;
        }

        private MimeMessage BuildMessage(Trainee t)
        {
            var templatePath = Path.Combine(_templatesRoot, "ExamInvitationTemplate.html");
            var html = File.ReadAllText(templatePath)
                .Replace("{{Name}}", t.FullName.Split(' ')[0])
                .Replace("{{Code}}", t.Code)
                .Replace("{{HeaderURL}}", "https://drive.google.com/uc?export=view&id=1t2of4TFhRtKEoS9Ji5GM1Sknekdqhm4w")
                .Replace("{{ExamLink}}", "https://link.to/your/exam"); // ضع الرابط الحقيقي هنا

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Helpers Assuit Community", _smtp.Email));
            msg.To.Add(MailboxAddress.Parse(t.Email));
            msg.Subject = "🎓 Final Exam Invitation | Helpers Summer Camp";
            msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            return msg;
        }

    }
}
