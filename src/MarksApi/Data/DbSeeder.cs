using MarksApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public static class DbSeeder
{
    public static async Task EnsureSeededAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync(cancellationToken);

        var teacherUsername = configuration["SmokeTest:TeacherUsername"] ?? "teacher1";
        var teacherPassword = configuration["SmokeTest:TeacherPassword"];
        var studentUsername = configuration["SmokeTest:StudentUsername"] ?? "student1";
        var studentPassword = configuration["SmokeTest:StudentPassword"];

        if (!string.IsNullOrWhiteSpace(teacherPassword))
        {
            var teacher = await db.Users.FirstOrDefaultAsync(u => u.Username == teacherUsername, cancellationToken);
            if (teacher is null)
            {
                teacher = new User
                {
                    Username = teacherUsername,
                    Role = "Teacher",
                    StudentId = null
                };
                teacher.PasswordHash = passwordHasher.HashPassword(teacher, teacherPassword);
                db.Users.Add(teacher);
            }
            else if (passwordHasher.VerifyHashedPassword(teacher, teacher.PasswordHash, teacherPassword) == PasswordVerificationResult.Failed)
            {
                teacher.PasswordHash = passwordHasher.HashPassword(teacher, teacherPassword);
            }
        }

        if (!string.IsNullOrWhiteSpace(studentPassword))
        {
            var student1 = await db.Users.FirstOrDefaultAsync(u => u.Username == studentUsername, cancellationToken);
            if (student1 is null)
            {
                student1 = new User
                {
                    Username = studentUsername,
                    Role = "Student",
                    StudentId = 1
                };
                student1.PasswordHash = passwordHasher.HashPassword(student1, studentPassword);
                db.Users.Add(student1);
            }
            else if (passwordHasher.VerifyHashedPassword(student1, student1.PasswordHash, studentPassword) == PasswordVerificationResult.Failed)
            {
                student1.PasswordHash = passwordHasher.HashPassword(student1, studentPassword);
            }

            var student2 = await db.Users.FirstOrDefaultAsync(u => u.Username == "student2", cancellationToken);
            if (student2 is null)
            {
                student2 = new User
                {
                    Username = "student2",
                    Role = "Student",
                    StudentId = 2
                };
                student2.PasswordHash = passwordHasher.HashPassword(student2, studentPassword);
                db.Users.Add(student2);
            }
            else if (passwordHasher.VerifyHashedPassword(student2, student2.PasswordHash, studentPassword) == PasswordVerificationResult.Failed)
            {
                student2.PasswordHash = passwordHasher.HashPassword(student2, studentPassword);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Synchronize PostgreSQL identity sequences with seeded rows so new inserts never collide with seeded IDs.
        const string syncSequencesSql = """
            SELECT setval(pg_get_serial_sequence('students', 'id'), COALESCE((SELECT MAX(id) FROM students), 1), true);
            SELECT setval(pg_get_serial_sequence('courses', 'id'), COALESCE((SELECT MAX(id) FROM courses), 1), true);
            SELECT setval(pg_get_serial_sequence('enrollments', 'id'), COALESCE((SELECT MAX(id) FROM enrollments), 1), true);
            SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE((SELECT MAX(id) FROM users), 1), true);
            """;

        await db.Database.ExecuteSqlRawAsync(syncSequencesSql, cancellationToken);
        logger.LogInformation("Database migration and seed verification completed.");
    }
}

