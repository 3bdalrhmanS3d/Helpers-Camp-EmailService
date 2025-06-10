using HelpersCampEmail.App;
using HelpersCampEmail.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpersCampEmail.Repositories
{
    public class ApplicantRepository : IApplicantRepository
    {
        private readonly AppDbContext _context;
        public ApplicantRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Trainee>> GetNotSentAsync() => await _context.Applicants
            .Where(a => !a.EmailLogs.Any(e => e.Success))
            .ToListAsync();

        public async Task AddTraineesAsync(IEnumerable<Trainee> trainees)
        {
            _context.Applicants.AddRange(trainees);
            await _context.SaveChangesAsync();
        }

        public Task<int> CountAsync() => _context.Applicants.CountAsync();
        public Task<int> CountSentAsync() => _context.EmailLogs
            .Where(e => e.Success)
            .Select(e => e.ApplicantId)
            .Distinct().CountAsync();
        public Task<int> CountNotSentAsync() => _context.Applicants
            .CountAsync(a => !a.EmailLogs.Any(e => e.Success));

        public async Task<IEnumerable<EmailLog>> GetHistoryAsync() => await _context.EmailLogs
            .Include(e => e.Applicant)
            .OrderByDescending(e => e.SentAt)
            .ToListAsync();

        public async Task<IEnumerable<object>> GetFailedAttemptsAsync() => await _context.EmailLogs
            .Where(e => !e.Success)
            .GroupBy(e => e.Applicant.Email)
            .Select(g => new { Email = g.Key, FailCount = g.Count() })
            .ToListAsync();

        public async Task<IEnumerable<object>> GetLastTriesAsync() => await _context.EmailLogs
            .GroupBy(e => e.Applicant.Email)
            .Select(g => new
            {
                Email = g.Key,
                LastTry = g.Max(e => e.SentAt),
                Success = g.OrderByDescending(e => e.SentAt).First().Success
            }).ToListAsync();

        public async Task<IEnumerable<object>> GetAllStatusAsync() => await _context.Applicants
            .Select(a => new
            {
                a.FullName,
                a.Email,
                Sent = a.EmailLogs.Any(l => l.Success),
                LastTry = a.EmailLogs.OrderByDescending(l => l.SentAt).FirstOrDefault()
            }).ToListAsync();

        public async Task<IEnumerable<object>> SearchAsync(string? keyword, string orderBy, bool descending, bool sentOnly, bool notSentOnly)
        {
            var query = _context.Applicants.AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim().ToLower();
                query = query.Where(a => a.FullName.ToLower().Contains(k)
                    || a.Email.ToLower().Contains(k)
                    || a.Code.ToLower().Contains(k));
            }
            if (sentOnly) query = query.Where(a => a.EmailLogs.Any(e => e.Success));
            if (notSentOnly) query = query.Where(a => !a.EmailLogs.Any(e => e.Success));
            query = (orderBy.ToLower(), descending) switch
            {
                ("date", true) => query.OrderByDescending(a => a.CreatedAt),
                ("date", false) => query.OrderBy(a => a.CreatedAt),
                (_, true) => query.OrderByDescending(a => a.FullName),
                _ => query.OrderBy(a => a.FullName)
            };
            return await query.Select(a => new
            {
                a.Code,
                a.FullName,
                a.Email,
                a.Status,
                Sent = a.EmailLogs.Any(e => e.Success),
                LastSentAt = a.EmailLogs.Where(e => e.Success)
                    .OrderByDescending(e => e.SentAt)
                    .Select(e => e.SentAt)
                    .FirstOrDefault()
            }).ToListAsync();
        }

        public async Task DeleteAllAsync()
        {
            _context.Applicants.RemoveRange(_context.Applicants);
            _context.EmailLogs.RemoveRange(_context.EmailLogs);
            await _context.SaveChangesAsync();
        }

        public Task<Trainee?> GetByIdAsync(int id) => _context.Applicants.FindAsync(id).AsTask();

        public async Task<IEnumerable<Trainee>> GetAllAsync() => await _context.Applicants.ToListAsync();

        public async Task UpdateEmailAsync(string code, string newMail)
        {
            var t = await _context.Applicants.SingleOrDefaultAsync(a => a.Code == code);
            if (t != null)
            {
                t.Email = newMail;
                await _context.SaveChangesAsync();
            }
        }
    }
}
