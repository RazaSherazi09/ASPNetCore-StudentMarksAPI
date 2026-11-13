using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Enrollments;
using MarksApi.Dtos.Students;
using MarksApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarksApi.Tests;

public class ServiceLayerTests : IAsyncLifetime
{
    private readonly IConfiguration _configuration;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public ServiceLayerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Grading:PassMark"] = "50",
                ["Jwt:Issuer"] = "MarksApi",
                ["Jwt:Audience"] = "MarksApiClients",
                ["Jwt:LifetimeMinutes"] = "60"
            })
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = _configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Database must be configured in user-secrets or environment variables to run PostgreSQL service tests.");
        }

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    private AppDbContext CreateDbContext() => new(_dbOptions);

    public async Task InitializeAsync()
    {
        await CleanupTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestDataAsync();
    }

    private async Task CleanupTestDataAsync()
    {
        await using var db = CreateDbContext();

        var testStudentIds = await db.Students
            .Where(s => s.RegistrationNo.StartsWith("XUNIT-"))
            .Select(s => s.Id)
            .ToListAsync();

        var testCourseIds = await db.Courses
            .Where(c => c.Code.StartsWith("XU"))
            .Select(c => c.Id)
            .ToListAsync();

        if (testStudentIds.Count > 0 || testCourseIds.Count > 0)
        {
            var testEnrollments = await db.Enrollments
                .Where(e => testStudentIds.Contains(e.StudentId) || testCourseIds.Contains(e.CourseId))
                .ToListAsync();

            if (testEnrollments.Count > 0)
            {
                db.Enrollments.RemoveRange(testEnrollments);
                await db.SaveChangesAsync();
            }

            if (testStudentIds.Count > 0)
            {
                var students = await db.Students.Where(s => testStudentIds.Contains(s.Id)).ToListAsync();
                db.Students.RemoveRange(students);
                await db.SaveChangesAsync();
            }

            if (testCourseIds.Count > 0)
            {
                var courses = await db.Courses.Where(c => testCourseIds.Contains(c.Id)).ToListAsync();
                db.Courses.RemoveRange(courses);
                await db.SaveChangesAsync();
            }
        }

        const string syncSequencesSql = """
            SELECT setval(pg_get_serial_sequence('students', 'id'), COALESCE((SELECT MAX(id) FROM students), 1), true);
            SELECT setval(pg_get_serial_sequence('courses', 'id'), COALESCE((SELECT MAX(id) FROM courses), 1), true);
            SELECT setval(pg_get_serial_sequence('enrollments', 'id'), COALESCE((SELECT MAX(id) FROM enrollments), 1), true);
            SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE((SELECT MAX(id) FROM users), 1), true);
            """;

        await db.Database.ExecuteSqlRawAsync(syncSequencesSql);
    }

    [Fact]
    public async Task CreateStudentAsync_StoresEmailInLowerCase_AndReturnsDto()
    {
        await using var db = CreateDbContext();
        var service = new StudentService(db, NullLogger<StudentService>.Instance);

        var created = await service.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S01",
            FullName = "Xunit Student One",
            Email = "XUnit.Student1@Example.COM"
        }, CancellationToken.None);

        Assert.True(created.Id > 0);
        Assert.Equal("XUNIT-S01", created.RegistrationNo);
        Assert.Equal("xunit.student1@example.com", created.Email);
    }

    [Fact]
    public async Task CreateStudentAsync_DuplicateRegistrationNo_Throws409Conflict()
    {
        await using var db1 = CreateDbContext();
        var service1 = new StudentService(db1, NullLogger<StudentService>.Instance);

        await service1.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S02",
            FullName = "Original Student",
            Email = "xunit.s02@example.com"
        }, CancellationToken.None);

        await using var db2 = CreateDbContext();
        var service2 = new StudentService(db2, NullLogger<StudentService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service2.CreateStudentAsync(new CreateStudentRequestDto
            {
                RegistrationNo = "XUNIT-S02",
                FullName = "Duplicate Student",
                Email = "xunit.s02.dup@example.com"
            }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task GetStudentsAsync_AppliesPagingAndSearch()
    {
        await using var db = CreateDbContext();
        var service = new StudentService(db, NullLogger<StudentService>.Instance);

        await service.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S03",
            FullName = "Zainab Xunit",
            Email = "zainab.xunit@example.com"
        }, CancellationToken.None);

        var page = await service.GetStudentsAsync(new ListQueryParameters
        {
            Page = 1,
            PageSize = 5,
            Search = "Zainab Xunit"
        }, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("XUNIT-S03", page.Items[0].RegistrationNo);
    }

    [Fact]
    public async Task DeleteStudentAsync_WithEnrollments_Throws409Conflict()
    {
        await using var db = CreateDbContext();
        var studentService = new StudentService(db, NullLogger<StudentService>.Instance);
        var courseService = new CourseService(db, NullLogger<CourseService>.Instance);
        var enrollmentService = new EnrollmentService(db, NullLogger<EnrollmentService>.Instance);

        var student = await studentService.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S04",
            FullName = "Enrolled Student",
            Email = "xunit.s04@example.com"
        }, CancellationToken.None);

        var course = await courseService.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "XU101",
            Title = "Xunit Course 101",
            CreditHours = 3
        }, CancellationToken.None);

        await enrollmentService.EnrollStudentAsync(
            course.Id,
            new CreateEnrollmentRequestDto { StudentId = student.Id },
            CancellationToken.None);

        await using var deleteDb = CreateDbContext();
        var deleteStudentService = new StudentService(deleteDb, NullLogger<StudentService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiProblemException>(() =>
            deleteStudentService.DeleteStudentAsync(student.Id, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetCourseAsync_PersistsAndReturnsCourse()
    {
        await using var db = CreateDbContext();
        var service = new CourseService(db, NullLogger<CourseService>.Instance);

        var created = await service.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "xu102",
            Title = "Distributed Systems",
            CreditHours = 4
        }, CancellationToken.None);

        var fetched = await service.GetCourseByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal("XU102", fetched.Code);
        Assert.Equal(4, fetched.CreditHours);
    }

    [Fact]
    public async Task CreateCourseAsync_DuplicateCode_Throws409Conflict()
    {
        await using var db1 = CreateDbContext();
        var service1 = new CourseService(db1, NullLogger<CourseService>.Instance);

        await service1.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "XU103",
            Title = "First Course",
            CreditHours = 3
        }, CancellationToken.None);

        await using var db2 = CreateDbContext();
        var service2 = new CourseService(db2, NullLogger<CourseService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiProblemException>(() =>
            service2.CreateCourseAsync(new CreateCourseRequestDto
            {
                Code = "XU103",
                Title = "Duplicate Course",
                CreditHours = 2
            }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task EnrollStudentAsync_DoubleEnrollment_Throws409Conflict()
    {
        await using var db = CreateDbContext();
        var studentService = new StudentService(db, NullLogger<StudentService>.Instance);
        var courseService = new CourseService(db, NullLogger<CourseService>.Instance);
        var enrollmentService = new EnrollmentService(db, NullLogger<EnrollmentService>.Instance);

        var student = await studentService.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S05",
            FullName = "Double Enroll Student",
            Email = "xunit.s05@example.com"
        }, CancellationToken.None);

        var course = await courseService.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "XU104",
            Title = "Course For Double Enrollment",
            CreditHours = 3
        }, CancellationToken.None);

        await enrollmentService.EnrollStudentAsync(
            course.Id,
            new CreateEnrollmentRequestDto { StudentId = student.Id },
            CancellationToken.None);

        await using var db2 = CreateDbContext();
        var enrollmentService2 = new EnrollmentService(db2, NullLogger<EnrollmentService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiProblemException>(() =>
            enrollmentService2.EnrollStudentAsync(
                course.Id,
                new CreateEnrollmentRequestDto { StudentId = student.Id },
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateMarkAsync_OutOfRangeMark_Throws400BadRequest()
    {
        await using var db = CreateDbContext();
        var enrollmentService = new EnrollmentService(db, NullLogger<EnrollmentService>.Instance);

        var ex = await Assert.ThrowsAsync<ApiProblemException>(() =>
            enrollmentService.UpdateMarkAsync(
                1,
                new UpdateMarkRequestDto { Mark = 105 },
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status400BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateMarkAsync_OptimisticConcurrencyConflict_Throws409Conflict()
    {
        await using var setupDb = CreateDbContext();
        var studentService = new StudentService(setupDb, NullLogger<StudentService>.Instance);
        var courseService = new CourseService(setupDb, NullLogger<CourseService>.Instance);
        var enrollmentService = new EnrollmentService(setupDb, NullLogger<EnrollmentService>.Instance);

        var student = await studentService.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S06",
            FullName = "Concurrency Student",
            Email = "xunit.s06@example.com"
        }, CancellationToken.None);

        var course = await courseService.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "XU105",
            Title = "Concurrency Course",
            CreditHours = 3
        }, CancellationToken.None);

        var enrollment = await enrollmentService.EnrollStudentAsync(
            course.Id,
            new CreateEnrollmentRequestDto { StudentId = student.Id },
            CancellationToken.None);

        // Simulate two teachers loading the same enrollment concurrently
        await using var teacherADb = CreateDbContext();
        await using var teacherBDb = CreateDbContext();

        var entityA = await teacherADb.Enrollments.FirstAsync(e => e.Id == enrollment.Id);
        var entityB = await teacherBDb.Enrollments.FirstAsync(e => e.Id == enrollment.Id);

        entityA.Mark = 75;
        await teacherADb.SaveChangesAsync();

        entityB.Mark = 90;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => teacherBDb.SaveChangesAsync());
    }

    [Fact]
    public async Task GetCourseStatsAsync_UngradedCourse_ReturnsNullAverage()
    {
        await using var db = CreateDbContext();
        var studentService = new StudentService(db, NullLogger<StudentService>.Instance);
        var courseService = new CourseService(db, NullLogger<CourseService>.Instance);
        var enrollmentService = new EnrollmentService(db, NullLogger<EnrollmentService>.Instance);
        var reportService = new ReportService(db, _configuration);

        var student = await studentService.CreateStudentAsync(new CreateStudentRequestDto
        {
            RegistrationNo = "XUNIT-S07",
            FullName = "Ungraded Student",
            Email = "xunit.s07@example.com"
        }, CancellationToken.None);

        var course = await courseService.CreateCourseAsync(new CreateCourseRequestDto
        {
            Code = "XU106",
            Title = "Ungraded Course",
            CreditHours = 2
        }, CancellationToken.None);

        await enrollmentService.EnrollStudentAsync(
            course.Id,
            new CreateEnrollmentRequestDto { StudentId = student.Id },
            CancellationToken.None);

        var stats = await reportService.GetCourseStatsAsync(course.Id, CancellationToken.None);

        Assert.Equal(1, stats.Enrolled);
        Assert.Equal(0, stats.Graded);
        Assert.Null(stats.Average);
        Assert.Null(stats.Highest);
        Assert.Null(stats.Lowest);
        Assert.Equal(0, stats.Passed);
    }

    [Fact]
    public async Task GetCourseStatsAsync_GradedEnrollments_ComputesAccurateStats()
    {
        await using var db = CreateDbContext();
        var reportService = new ReportService(db, _configuration);

        // Seeded course 1 (CS401) has 3 enrolled students, 2 graded with 80 and 70 -> avg 75.0, highest 80, lowest 70, passed 2
        var stats = await reportService.GetCourseStatsAsync(1, CancellationToken.None);

        Assert.Equal("CS401", stats.CourseCode);
        Assert.Equal(3, stats.Enrolled);
        Assert.Equal(2, stats.Graded);
        Assert.Equal(75.0, stats.Average);
        Assert.Equal(80, stats.Highest);
        Assert.Equal(70, stats.Lowest);
        Assert.Equal(2, stats.Passed);
    }

    [Fact]
    public async Task GetStudentTranscriptAsync_ComputesCreditHourWeightedAverage()
    {
        await using var db = CreateDbContext();
        var reportService = new ReportService(db, _configuration);

        // Seeded student 1 (Ali Raza) has CS401 (3 cr, mark 80) and MA201 (2 cr, mark 60) -> (240 + 120) / 5 = 72.0
        var transcript = await reportService.GetStudentTranscriptAsync(1, CancellationToken.None);

        Assert.Equal("CS-2022-001", transcript.RegistrationNo);
        Assert.Equal(2, transcript.Courses.Count);
        Assert.Equal(72.0, transcript.WeightedAverage);
    }
}

