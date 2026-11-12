using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Dtos.Reports;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ReportService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    private int GetConfiguredPassMark()
    {
        var passMark = _configuration.GetValue<int?>("Grading:PassMark");
        if (!passMark.HasValue)
        {
            throw new InvalidOperationException("Grading:PassMark must be configured in application settings.");
        }

        return passMark.Value;
    }

    public async Task<CourseStatsResponseDto> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new { c.Id, c.Code })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Course not found",
                $"A course with ID {courseId} was not found.");
        }

        var passMark = GetConfiguredPassMark();

        var marks = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.Mark)
            .ToListAsync(cancellationToken);

        var enrolledCount = marks.Count;
        var gradedMarks = marks.Where(m => m.HasValue).Select(m => m!.Value).ToList();
        var gradedCount = gradedMarks.Count;

        if (gradedCount == 0)
        {
            return new CourseStatsResponseDto
            {
                CourseId = course.Id,
                CourseCode = course.Code,
                Enrolled = enrolledCount,
                Graded = 0,
                Average = null,
                Highest = null,
                Lowest = null,
                Passed = 0
            };
        }

        return new CourseStatsResponseDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            Enrolled = enrolledCount,
            Graded = gradedCount,
            Average = Math.Round(gradedMarks.Average(), 2),
            Highest = gradedMarks.Max(),
            Lowest = gradedMarks.Min(),
            Passed = gradedMarks.Count(m => m >= passMark)
        };
    }

    public async Task<TranscriptResponseDto> GetStudentTranscriptAsync(int studentId, CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.RegistrationNo, s.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Student not found",
                $"A student with ID {studentId} was not found.");
        }

        var passMark = GetConfiguredPassMark();

        var courses = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderBy(e => e.Course.Code)
            .Select(e => new TranscriptCourseItemDto
            {
                Code = e.Course.Code,
                Title = e.Course.Title,
                CreditHours = e.Course.CreditHours,
                Mark = e.Mark,
                Passed = e.Mark.HasValue && e.Mark.Value >= passMark
            })
            .ToListAsync(cancellationToken);

        var gradedCourses = courses.Where(c => c.Mark.HasValue).ToList();
        double? weightedAverage = null;

        if (gradedCourses.Count > 0)
        {
            var totalWeightedMarks = gradedCourses.Sum(c => (double)c.Mark!.Value * c.CreditHours);
            var totalCreditHours = gradedCourses.Sum(c => c.CreditHours);
            if (totalCreditHours > 0)
            {
                weightedAverage = Math.Round(totalWeightedMarks / totalCreditHours, 2);
            }
        }

        return new TranscriptResponseDto
        {
            StudentId = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Courses = courses,
            WeightedAverage = weightedAverage
        };
    }
}

