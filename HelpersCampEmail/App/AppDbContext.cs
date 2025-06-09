using HelpersCampEmail.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpersCampEmail.App
{
    public class AppDbContext : DbContext
    {
        public DbSet<Trainee> Applicants => Set<Trainee>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    }
}
