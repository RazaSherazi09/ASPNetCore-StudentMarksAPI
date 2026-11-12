using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Enrollments;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(AppDbContext db, ILogger<EnrollmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResultDto<EnrollmentResponseDto>> GetCourseEnrollmentsAsync(
        int courseId,
        ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var courseExists = await _db.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);

        if (!courseExists)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Course not found",
                $"A course with ID {courseId} was not found.");
        }

        var (page, pageSize) = query.Normalize();

        var enrollmentsQuery = _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            enrollmentsQuery = enrollmentsQuery.Where(e =>
                e.Student.FullName.ToLower().Contains(term) ||
                e.Student.RegistrationNo.ToLower().Contains(term) ||
                e.Student.Email.ToLower().Contains(term));
        }

        enrollmentsQuery = (query.Sort?.Trim().ToLowerInvariant()) switch
        {
            "studentname" or "name" or "name_asc" => enrollmentsQuery.OrderBy(e => e.Student.FullName).ThenBy(e => e.Id),
            "studentname_desc" or "name_desc" => enrollmentsQuery.OrderByDescending(e => e.Student.FullName).ThenBy(e => e.Id),
            "registrationno" or "registrationno_asc" => enrollmentsQuery.OrderBy(e => e.Student.RegistrationNo),
            "registrationno_desc" => enrollmentsQuery.OrderByDescending(e => e.Student.RegistrationNo),
            "mark" or "mark_asc" => enrollmentsQuery.OrderBy(e => e.Mark).ThenBy(e => e.Id),
            "mark_desc" => enrollmentsQuery.OrderByDescending(e => e.Mark).ThenBy(e => e.Id),
            "enrolledat" or "enrolledat_asc" => enrollmentsQuery.OrderBy(e => e.EnrolledAtUtc),
            "enrolledat_desc" => enrollmentsQuery.OrderByDescending(e => e.EnrolledAtUtc),
            "id_desc" => enrollmentsQuery.OrderByDescending(e => e.Id),
            _ => enrollmentsQuery.OrderBy(e => e.Id)
        };

        var totalCount = await enrollmentsQuery.CountAsync(cancellationToken);

        var items = await enrollmentsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnrollmentResponseDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                RegistrationNo = e.Student.RegistrationNo,
                StudentName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<EnrollmentResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<EnrollmentResponseDto> EnrollStudentAsync(
        int courseId,
        CreateEnrollmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var studentId = request.StudentId!.Value;

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

        var entity = new Enrollment
        {
            CourseId = courseId,
            StudentId = studentId,
            Mark = null,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _db.Enrollments.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                "Duplicate enrollment attempt for StudentId={StudentId} in CourseId={CourseId}.",
                studentId,
                courseId);

            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Duplicate enrollment",
                "This student is already enrolled in the specified course.");
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsForeignKeyViolation(ex, out _))
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Resource not found",
                "The specified student or course was not found.");
        }

        _logger.LogInformation(
            "Enrolled StudentId={StudentId} in CourseId={CourseId} (EnrollmentId={EnrollmentId}).",
            studentId,
            courseId,
            entity.Id);

        return new EnrollmentResponseDto
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            RegistrationNo = student.RegistrationNo,
            StudentName = student.FullName,
            CourseId = entity.CourseId,
            CourseCode = course.Code,
            Mark = entity.Mark,
            EnrolledAtUtc = entity.EnrolledAtUtc
        };
    }

    public async Task<EnrollmentResponseDto> UpdateMarkAsync(
        int enrollmentId,
        UpdateMarkRequestDto request,
        CancellationToken cancellationToken)
    {
        var mark = request.Mark!.Value;
        if (mark < 0 || mark > 100)
        {
            throw new ApiProblemException(
                StatusCodes.Status400BadRequest,
                "Invalid mark",
                "Mark must be a whole number from 0 to 100.");
        }

        var entity = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Enrollment not found",
                $"An enrollment with ID {enrollmentId} was not found.");
        }

        var previousMark = entity.Mark;
        entity.Mark = mark;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Optimistic concurrency conflict while updating mark for EnrollmentId={EnrollmentId}.",
                enrollmentId);

            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The mark for this enrollment was modified by another request. Please refresh and try again.");
        }

        _logger.LogInformation(
            "Updated mark for EnrollmentId={EnrollmentId} (StudentId={StudentId}, CourseId={CourseId}) from {PreviousMark} to {NewMark}.",
            entity.Id,
            entity.StudentId,
            entity.CourseId,
            previousMark?.ToString() ?? "null",
            entity.Mark);

        return new EnrollmentResponseDto
        {
            Id = entity.Id,
            StudentId = entity.StudentId,
            CourseId = entity.CourseId,
            Mark = entity.Mark,
            EnrolledAtUtc = entity.EnrolledAtUtc
        };
    }

    public async Task DeleteEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken)
    {
        var entity = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);

        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Enrollment not found",
                $"An enrollment with ID {enrollmentId} was not found.");
        }

        _db.Enrollments.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted EnrollmentId={EnrollmentId} (StudentId={StudentId}, CourseId={CourseId}).",
            enrollmentId,
            entity.StudentId,
            entity.CourseId);
    }
}

