using CsvHelper;
using CsvHelper.Configuration;
using HelpersCampEmail.DTOs;
using HelpersCampEmail.Models;
using HelpersCampEmail.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using HelpersCampEmail.Maps;
namespace HelpersCampEmail.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApplicantsController : ControllerBase
    {
        private readonly IApplicantRepository _repo;

        public ApplicantsController(IApplicantRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv([FromForm] UploadCsvRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("CSV file is required.");

            using var reader = new StreamReader(request.File.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });
            csv.Context.RegisterClassMap<TraineeMap>();

            var records = csv.GetRecords<Trainee>().ToList();
            var accepted = records
                .Where(t => t.Status.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                .ToList();

            await _repo.AddTraineesAsync(accepted);

            return Ok(new
            {
                Message = $"{accepted.Count} applicant(s) added to database.",
                TotalFromCsv = records.Count,
                AcceptedFromCsv = accepted.Count
            });
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<ApplicantStatisticsDto>> GetStatistics()
        {
            var total = await _repo.CountAsync();
            var sent = await _repo.CountSentAsync();
            var notSent = await _repo.CountNotSentAsync();

            return Ok(new ApplicantStatisticsDto
            {
                TotalApplicants = total,
                SentCount = sent,
                NotSentCount = notSent
            });
        }

        [HttpGet("not-sent")]
        public async Task<ActionResult<IEnumerable<NotSentExportDto>>> GetNotSent()
        {
            var list = await _repo.GetNotSentAsync();
            var dto = list.Select(a => new NotSentExportDto { Code = a.Code, Name = a.FullName, Email = a.Email, State = a.Status });
            return Ok(dto);
        }

        [HttpGet("failed-attempts")]
        public async Task<ActionResult> GetFailedAttempts() => Ok(await _repo.GetFailedAttemptsAsync());

        [HttpGet("last-tries")]
        public async Task<ActionResult> GetLastTries() => Ok(await _repo.GetLastTriesAsync());

        [HttpGet("all-status")]
        public async Task<ActionResult> GetAllStatus() => Ok(await _repo.GetAllStatusAsync());

        [HttpGet("search")]
        public async Task<ActionResult> Search([FromQuery] string? keyword, [FromQuery] string orderBy = "name",
            [FromQuery] bool descending = false, [FromQuery] bool sentOnly = false, [FromQuery] bool notSentOnly = false)
        {
            var results = await _repo.SearchAsync(keyword, orderBy, descending, sentOnly, notSentOnly);
            return Ok(results);
        }

        [HttpDelete("DeleteAll")]
        public async Task<IActionResult> DeleteAll()
        {
            await _repo.DeleteAllAsync();
            return Ok(new { Message = "All applicants and email logs deleted successfully." });
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetHistory() => Ok(await _repo.GetHistoryAsync());

        [HttpPost("AddTrainee")]
        public async Task<IActionResult> AddTrainee([FromBody] AddTraineeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            bool exists = (await _repo.SearchAsync(dto.Email, "", false, false, false))
                .Cast<dynamic>()
                .Any(a => a.Email == dto.Email || a.Code == dto.Code);

            if (exists)
                return Conflict(new { Message = "Trainee with the same email or code already exists." });

            var trainee = new Trainee
            {
                Code = dto.Code,
                Email = dto.Email,
                FullName = dto.FullName,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddTraineesAsync(new[] { trainee });
            return CreatedAtAction(nameof(GetTraineeById), new { id = trainee.Id }, trainee);
        }

        [HttpGet("{id:int}", Name = nameof(GetTraineeById))]
        public async Task<IActionResult> GetTraineeById(int id)
        {
            var t = await _repo.GetByIdAsync(id);
            if (t == null) return NotFound();
            return Ok(t);
        }

        [HttpGet("GetTrainees")]
        public async Task<ActionResult<IEnumerable<Trainee>>> GetTrainees() => Ok(await _repo.GetAllAsync());

        [HttpPut("edit-email")]
        public async Task<IActionResult> EditTraineeEmail([FromBody] UpdateTraineeEmailDto dto)
        {
            await _repo.UpdateEmailAsync(dto.Code, dto.NewMail);
            return Ok(new { Message = "Email updated successfully." });
        }
    }
}
