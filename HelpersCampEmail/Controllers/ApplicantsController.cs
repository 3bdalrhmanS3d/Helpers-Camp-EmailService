using CsvHelper.Configuration;
using CsvHelper;
using HelpersCampEmail.App;
using HelpersCampEmail.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace HelpersCampEmail.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicantsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApplicantsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv([FromForm] UploadCsvRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("CSV file is required.");

            using var stream = request.File.OpenReadStream();
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            var applicantsFromCsv = csv.GetRecords<Trainee>().ToList();

            var accepted = applicantsFromCsv
                .Where(a => a.Status.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int addedCount = 0;

            foreach (var app in accepted)
            {
                var exists = _context.Applicants.Any(a => a.Email == app.Email);
                if (!exists)
                {
                    app.CreatedAt = DateTime.UtcNow;
                    _context.Applicants.Add(app);
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = $"{addedCount} applicant(s) added to database.",
                TotalFromCsv = applicantsFromCsv.Count,
                AcceptedFromCsv = accepted.Count
            });
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<ApplicantStatisticsDto>> GetStatistics()
        {
            var total = await _context.Applicants.CountAsync();
            var sent = await _context.EmailLogs
                .Where(e => e.Success)
                .Select(e => e.ApplicantId)
                .Distinct()
                .CountAsync();
            var notSent = await _context.Applicants
                .CountAsync(a => !a.EmailLogs.Any(e => e.Success));

            return Ok(new ApplicantStatisticsDto
            {
                TotalApplicants = total,
                SentCount = sent,
                NotSentCount = notSent
            });
        }

        // ✅ إحضار اللي موصلهمش إيميل
        [HttpGet("not-sent")]
        public async Task<ActionResult> GetNotSent()
        {
            var data = await _context.Applicants
                .Where(a => !a.EmailLogs.Any(e => e.Success))
                .Select(a => new { a.FullName, a.Email })
                .ToListAsync();

            return Ok(data);
        }


        // ✅ محاولات فاشلة لكل متقدم
        [HttpGet("failed-attempts")]
        public async Task<ActionResult> GetFailedAttempts()
        {
            var result = await _context.EmailLogs
                .Where(e => !e.Success)
                .GroupBy(e => e.Applicant.Email)
                .Select(g => new { Email = g.Key, FailCount = g.Count() })
                .ToListAsync();

            return Ok(result);
        }

        // ✅ آخر محاولة إرسال لكل شخص
        [HttpGet("last-tries")]
        public async Task<ActionResult> GetLastTries()
        {
            var result = await _context.EmailLogs
                .GroupBy(e => e.Applicant.Email)
                .Select(g => new
                {
                    Email = g.Key,
                    LastTry = g.Max(e => e.SentAt),
                    Success = g.OrderByDescending(e => e.SentAt).First().Success
                })
                .ToListAsync();

            return Ok(result);
        }

        // ✅ الكل مع حالة الإرسال
        [HttpGet("all-status")]
        public async Task<ActionResult> GetAllWithStatus()
        {
            var result = await _context.Applicants
                .Select(a => new
                {
                    a.FullName,
                    a.Email,
                    Sent = a.EmailLogs.Any(l => l.Success),
                    LastTry = a.EmailLogs.OrderByDescending(l => l.SentAt).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<ActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string orderBy = "name",
        [FromQuery] bool descending = false,
        [FromQuery] bool sentOnly = false,
        [FromQuery] bool notSentOnly = false)
        {
            var query = _context.Applicants.AsQueryable();

            // 🔍 فلترة بالكلمة المفتاحية (اسم، إيميل، كود)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(a =>
                    a.FullName.ToLower().Contains(keyword) ||
                    a.Email.ToLower().Contains(keyword) ||
                    a.Code.ToLower().Contains(keyword));
            }

            // ✅ فلترة حسب حالة الإرسال
            if (sentOnly)
                query = query.Where(a => a.EmailLogs.Any(e => e.Success));

            if (notSentOnly)
                query = query.Where(a => !a.EmailLogs.Any(e => e.Success));

            // 🧾 ترتيب
            query = (orderBy.ToLower(), descending) switch
            {
                ("date", true) => query.OrderByDescending(a => a.CreatedAt),
                ("date", false) => query.OrderBy(a => a.CreatedAt),
                (_, true) => query.OrderByDescending(a => a.FullName),
                _ => query.OrderBy(a => a.FullName),
            };

            // 📋 استرجاع البيانات
            var results = await query.Select(a => new
            {
                a.Code,
                a.FullName,
                a.Email,
                a.Status,
                Sent = a.EmailLogs.Any(e => e.Success),
                LastSentAt = a.EmailLogs
                    .Where(e => e.Success)
                    .OrderByDescending(e => e.SentAt)
                    .Select(e => e.SentAt)
                    .FirstOrDefault()
            }).ToListAsync();

            return Ok(results);
        }

        [HttpDelete("DeleteAll")]
        public async Task<IActionResult> DeleteAll()
        {
            var applicants = await _context.Applicants.ToListAsync();
            var emailLogs = await _context.EmailLogs.ToListAsync();
            _context.Applicants.RemoveRange(applicants);
            _context.EmailLogs.RemoveRange(emailLogs);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "All applicants and email logs deleted successfully." });
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetHistory()
        {
            var history = await _context.EmailLogs
                .Include(e => e.Applicant)
                .Select(e => new
                {
                    e.Applicant.FullName,
                    e.Applicant.Email,
                    e.SentAt,
                    e.Success,
                    e.ErrorMessage
                })
                .OrderByDescending(e => e.SentAt)
                .ToListAsync();
            return Ok(history);

        }

        [HttpPost("AddTrainee")]
        public async Task<IActionResult> AddTrainee([FromBody] AddTraineeDto dto)
        {

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            bool exists = await _context.Applicants
                .AnyAsync(a => a.Email == dto.Email || a.Code == dto.Code);

            if (exists)
                return Conflict(new { Message = "Intern with the same mail or code pre-exists." });

            var trainee = new Trainee
            {
                Code = dto.Code,
                Email = dto.Email,
                FullName = dto.FullName,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            _context.Applicants.Add(trainee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetTraineeById),
                new { id = trainee.Id },
                new
                {
                    Message = "The trainee has been added successfully.",
                    Trainee = new
                    {
                        trainee.Id,
                        trainee.Code,
                        trainee.Email,
                        trainee.FullName,
                        trainee.Status,
                        trainee.CreatedAt
                    }
                }
            );
        }


        [HttpGet("{id:int}", Name = nameof(GetTraineeById))]
        public async Task<IActionResult> GetTraineeById(int id)
        {
            var t = await _context.Applicants.FindAsync(id);
            if (t == null) return NotFound();
            return Ok(t);
        }
        

        [HttpGet("GetTrainees")]
        public async Task<ActionResult<IEnumerable<Trainee>>> GetTrainees()
        {
            var trainees = await _context.Applicants
                .Select(t => new
                {
                    t.Id,
                    t.Code,
                    t.FullName,
                    t.Email,
                    t.Status,
                    t.CreatedAt
                })
                .ToListAsync();
            return Ok( trainees );
        }

        [HttpPut("edit-email")]
        public async Task<IActionResult> EditTraineeEmail([FromBody] UpdateTraineeEmailDto dto)
        {
            // dto.Code and dto.NewMail come from the JSON body.
            var trainee = await _context.Applicants.SingleOrDefaultAsync(t => t.Code == dto.Code);
            if (trainee == null) return NotFound();

            trainee.Email = dto.NewMail;
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Email updated successfully.", Trainee = trainee });
        }

    }
}
