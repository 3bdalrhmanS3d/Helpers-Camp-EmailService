using HelpersCampEmail.App;
using HelpersCampEmail.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using System.Text.RegularExpressions;

namespace HelpersCampEmail.Service
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtp;
        private readonly string _campName;
        private readonly string _templatesRoot;
        private readonly AppDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration config,
            IOptions<SmtpSettings> smtpOpts,
            IWebHostEnvironment env,
            AppDbContext context,
            ILogger<EmailService> logger)
        {
            _smtp = smtpOpts.Value;
            _campName = config["Camp"]!;
            _templatesRoot = Path.Combine(env.ContentRootPath, "Templates");
            _context = context;
            _logger = logger;
        }

        public async Task<List<EmailResult>> SendAllAsync()
        {
            var trainees = await _context.Applicants
                .Where(t => t.Status == "Accept" && !t.EmailLogs.Any(e => e.Success))
                .ToListAsync();

            return await SendBatchAsync(trainees);
        }

        public async Task<EmailResult> SendSingleAsync(string code, string email, string fullName, bool accepted)
        {
            var temp = new Trainee
            {
                Code = code,
                Email = email,
                FullName = fullName,
                Status = accepted ? "Accept" : "Reject"
            };

            return (await SendBatchAsync(new List<Trainee> { temp }))[0];
        }

        private async Task<List<EmailResult>> SendBatchAsync(List<Trainee> trainees)
        {
            var results = new List<EmailResult>();
            using var client = new SmtpClient();

            // Configure client for better reliability
            client.Timeout = 60000; // 60 seconds timeout
            client.ServerCertificateValidationCallback = (s, c, h, e) => true; // Accept all certificates

            // Additional MailKit settings for better reliability
            client.CheckCertificateRevocation = false;

            try
            {
                await ConnectAndAuthenticateAsync(client);
            }
            catch (Exception authEx)
            {
                _logger.LogError(authEx, "Initial SMTP authentication failed");
                var failed = trainees.Select(t => new EmailResult
                {
                    Email = t.Email,
                    TimestampUtc = DateTime.UtcNow,
                    Success = false,
                    ErrorMessage = $"SMTP auth failed: {authEx.Message}"
                }).ToList();

                return failed;
            }

            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 5;

            foreach (var t in trainees)
            {
                // If too many consecutive failures, wait longer and reconnect
                if (consecutiveFailures >= maxConsecutiveFailures)
                {
                    _logger.LogWarning($"Too many consecutive failures ({consecutiveFailures}), waiting 30 seconds and reconnecting");
                    await Task.Delay(30000);

                    await SafeDisconnectAsync(client);
                    try
                    {
                        await ConnectAndAuthenticateAsync(client);
                        consecutiveFailures = 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to reconnect after consecutive failures");
                        results.Add(new EmailResult
                        {
                            Email = t.Email,
                            TimestampUtc = DateTime.UtcNow,
                            Success = false,
                            ErrorMessage = $"Reconnection failed: {ex.Message}"
                        });
                        continue;
                    }
                }

                var result = await SendSingleInternalAsync(client, t);
                results.Add(result);

                if (!result.Success)
                {
                    consecutiveFailures++;
                    _logger.LogWarning($"Failed to send to {t.Email}: {result.ErrorMessage}");
                }
                else
                {
                    consecutiveFailures = 0;
                    _logger.LogInformation($"Successfully sent email to {t.Email}");
                }

                // Dynamic delay based on success/failure
                int delay = result.Success ? 5000 : 10000; // 5s for success, 10s for failure
                await Task.Delay(delay);
            }

            await SafeDisconnectAsync(client);
            return results;
        }

        private async Task<EmailResult> SendSingleInternalAsync(SmtpClient client, Trainee t)
        {
            var result = new EmailResult
            {
                Email = t.Email,
                TimestampUtc = DateTime.UtcNow
            };

            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Validate email format first
                    if (!IsValidEmail(t.Email))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Invalid email format";
                        break;
                    }

                    // Ensure connected before sending
                    if (!client.IsConnected || !client.IsAuthenticated)
                    {
                        await ConnectAndAuthenticateAsync(client);
                    }

                    var msg = BuildMessage(t);

                    // Validate message before sending
                    if (!ValidateMessage(msg))
                    {
                        result.Success = false;
                        result.ErrorMessage = "Message validation failed";
                        break;
                    }

                    await client.SendAsync(msg);
                    result.Success = true;
                    _logger.LogInformation($"Email sent successfully to {t.Email} on attempt {attempt}");
                    break;
                }
                catch (SmtpCommandException cmdEx)
                {
                    _logger.LogWarning($"SMTP Command Error on attempt {attempt} for {t.Email}: {cmdEx.Message} (Status: {cmdEx.StatusCode})");

                    // Handle specific Yahoo SMTP errors
                    if (cmdEx.StatusCode == SmtpStatusCode.InsufficientStorage ||
                        cmdEx.Message.Contains("6.6.0") ||
                        cmdEx.Message.Contains("delivery"))
                    {
                        if (attempt < maxRetries)
                        {
                            // Wait longer for delivery issues
                            await Task.Delay(15000 * attempt);

                            // Try reconnecting
                            await SafeDisconnectAsync(client);
                            await Task.Delay(5000);

                            try
                            {
                                await ConnectAndAuthenticateAsync(client);
                            }
                            catch (Exception reconnectEx)
                            {
                                _logger.LogError(reconnectEx, $"Failed to reconnect on attempt {attempt}");
                                result.Success = false;
                                result.ErrorMessage = $"Reconnection failed: {reconnectEx.Message}";
                                break;
                            }
                        }
                        else
                        {
                            result.Success = false;
                            result.ErrorMessage = $"SMTP delivery error after {maxRetries} attempts: {cmdEx.Message}";
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"SMTP Command Error: {cmdEx.Message} (Status: {cmdEx.StatusCode})";
                        break;
                    }
                }
                catch (SmtpProtocolException protocolEx)
                {
                    _logger.LogWarning($"SMTP Protocol Error on attempt {attempt} for {t.Email}: {protocolEx.Message}");

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(10000 * attempt);
                        await SafeDisconnectAsync(client);

                        try
                        {
                            await ConnectAndAuthenticateAsync(client);
                        }
                        catch (Exception reconnectEx)
                        {
                            result.Success = false;
                            result.ErrorMessage = $"Protocol error and reconnection failed: {reconnectEx.Message}";
                            break;
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"SMTP Protocol Error after {maxRetries} attempts: {protocolEx.Message}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unexpected error on attempt {attempt} for {t.Email}");
                    result.Success = false;
                    result.ErrorMessage = $"Unexpected error: {ex.Message}";
                    break;
                }
            }

            // Log the attempt if applicant exists
            var existing = await _context.Applicants
                .FirstOrDefaultAsync(a => a.Email == t.Email);

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

        private async Task ConnectAndAuthenticateAsync(SmtpClient client)
        {
            // Gracefully disconnect any prior session
            if (client.IsConnected)
                await SafeDisconnectAsync(client);

            // Configure timeouts
            client.Timeout = 60000; // 60 seconds
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Try different SMTP servers and configurations
            var smtpConfigs = new[]
            {
                // Yahoo SMTP servers
                new { Server = "smtp.mail.yahoo.com", Port = 587, Options = SecureSocketOptions.StartTls },
                new { Server = "smtp.mail.yahoo.com", Port = 465, Options = SecureSocketOptions.SslOnConnect },
                new { Server = "plus.smtp.mail.yahoo.com", Port = 465, Options = SecureSocketOptions.SslOnConnect },
                new { Server = "plus.smtp.mail.yahoo.com", Port = 587, Options = SecureSocketOptions.StartTls },
                
                // Alternative configurations
                new { Server = "smtp.mail.yahoo.com", Port = 25, Options = SecureSocketOptions.StartTlsWhenAvailable },
                new { Server = "smtp.mail.yahoo.com", Port = 2525, Options = SecureSocketOptions.StartTlsWhenAvailable }
            };

            Exception lastException = null;
            int totalAttempts = 0;

            foreach (var config in smtpConfigs)
            {
                for (int retry = 0; retry < 3; retry++)
                {
                    totalAttempts++;
                    try
                    {
                        _logger.LogInformation($"Attempt {totalAttempts}: Connecting to {config.Server}:{config.Port} with {config.Options}");

                        // Add delay between attempts
                        if (retry > 0)
                        {
                            await Task.Delay(5000 * retry);
                        }

                        // Try to connect with timeout
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await client.ConnectAsync(config.Server, config.Port, config.Options, cts.Token);

                        if (!client.IsConnected)
                        {
                            throw new Exception("Connection established but client reports not connected");
                        }

                        _logger.LogInformation($"Connected successfully to {config.Server}:{config.Port}");

                        // Remove problematic authentication mechanisms
                        client.AuthenticationMechanisms.Remove("XOAUTH2");
                        client.AuthenticationMechanisms.Remove("NTLM");
                        client.AuthenticationMechanisms.Remove("GSSAPI");

                        _logger.LogInformation($"Available auth mechanisms: {string.Join(", ", client.AuthenticationMechanisms)}");

                        // Authenticate with timeout
                        using var authCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await client.AuthenticateAsync(_smtp.Email, _smtp.Password, authCts.Token);

                        _logger.LogInformation($"Authentication successful for {_smtp.Email}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        _logger.LogWarning($"Attempt {totalAttempts} failed for {config.Server}:{config.Port} - {ex.Message}");

                        if (client.IsConnected)
                        {
                            await SafeDisconnectAsync(client);
                        }

                        // Don't retry on authentication failures
                        if (ex.Message.Contains("authentication") || ex.Message.Contains("username") || ex.Message.Contains("password"))
                        {
                            break;
                        }
                    }
                }
            }

            var errorMessage = $"Failed to connect after {totalAttempts} attempts. Last error: {lastException?.Message}";
            _logger.LogError(errorMessage);
            throw new Exception(errorMessage, lastException);
        }

        private async Task SafeDisconnectAsync(SmtpClient client)
        {
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during disconnect");
            }
        }

        private MimeMessage BuildMessage(Trainee t)
        {
            var templatePath = Path.Combine(_templatesRoot, "ExamInvitationTemplate.html");
            var html = File.ReadAllText(templatePath)
                .Replace("{{Name}}", t.FullName.Split(' ')[0])
                .Replace("{{Code}}", t.Code)
                .Replace("{{HeaderURL}}", "https://drive.google.com/uc?export=view&id=1t2of4TFhRtKEoS9Ji5GM1Sknekdqhm4w")
                .Replace("{{ExamLink}}", "https://forms.gle/jqDMZ83C73UYhZJv6");

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("Helpers Assuit Community", _smtp.Email));
            msg.To.Add(MailboxAddress.Parse(t.Email));
            msg.Subject = "🎓 Final Exam Invitation | Helpers Summer Camp";

            // Set message priority to normal
            msg.Priority = MessagePriority.Normal;

            // Add message ID to prevent duplication issues
            msg.MessageId = MimeUtils.GenerateMessageId();

            msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();
            return msg;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateMessage(MimeMessage message)
        {
            if (message == null) return false;
            if (!message.From.Any()) return false;
            if (!message.To.Any()) return false;
            if (string.IsNullOrWhiteSpace(message.Subject)) return false;
            if (message.Body == null) return false;

            return true;
        }
    }
}