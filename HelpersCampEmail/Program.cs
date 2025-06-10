
using HelpersCampEmail.App;
using HelpersCampEmail.Service;
using HelpersCampEmail.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using HelpersCampEmail.Repositories;


namespace HelpersCampEmail
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Add services to the container.
            builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
            builder.Services.Configure<CsvSettings>(builder.Configuration.GetSection("Csv"));
            builder.Services.AddScoped<IApplicantRepository, ApplicantRepository>();

            builder.Services.AddTransient<IEmailService, EmailService>();
            builder.Services.AddControllers();

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseCors("AllowAll");
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
