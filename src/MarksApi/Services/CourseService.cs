using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class CourseService : ICourseService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CourseService> _logger;

    public CourseService(AppDbContext db, ILogger<CourseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResultDto<CourseResponseDto>> GetCoursesAsync(
        ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = query.Normalize();

        var coursesQuery = _db.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            coursesQuery = coursesQuery.Where(c =>
                c.Code.ToLower().Contains(term) ||
                c.Title.ToLower().Contains(term));
        }

        coursesQuery = (query.Sort?.Trim().ToLowerInvariant()) switch
        {
            "code" or "code_asc" => coursesQuery.OrderBy(c => c.Code),
            "code_desc" => coursesQuery.OrderByDescending(c => c.Code),
            "title" or "title_asc" => coursesQuery.OrderBy(c => c.Title).ThenBy(c => c.Id),
            "title_desc" => coursesQuery.OrderByDescending(c => c.Title).ThenBy(c => c.Id),
            "credithours" or "credithours_asc" => coursesQuery.OrderBy(c => c.CreditHours).ThenBy(c => c.Code),
            "credithours_desc" => coursesQuery.OrderByDescending(c => c.CreditHours).ThenBy(c => c.Code),
            "createdat" or "createdat_asc" => coursesQuery.OrderBy(c => c.CreatedAtUtc),
            "createdat_desc" => coursesQuery.OrderByDescending(c => c.CreatedAtUtc),
            "id_desc" => coursesQuery.OrderByDescending(c => c.Id),
            _ => coursesQuery.OrderBy(c => c.Id)
        };

        var totalCount = await coursesQuery.CountAsync(cancellationToken);

        var items = await coursesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<CourseResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CourseResponseDto> GetCourseByIdAsync(int id, CancellationToken cancellationToken)
    {
        var course = await _db.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Course not found",
                $"A course with ID {id} was not found.");
        }

        return course;
    }

    public async Task<CourseResponseDto> CreateCourseAsync(
        CreateCourseRequestDto request,
        CancellationToken cancellationToken)
    {
        var entity = new Course
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            CreditHours = request.CreditHours!.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Courses.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate course creation attempt for Code '{Code}'.", entity.Code);
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Duplicate course",
                $"A course with code '{entity.Code}' already exists.");
        }

        _logger.LogInformation("Created course Id={CourseId}, Code='{Code}'.", entity.Id, entity.Code);

        return new CourseResponseDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            CreditHours = entity.CreditHours,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task<CourseResponseDto> UpdateCourseAsync(
        int id,
        UpdateCourseRequestDto request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Course not found",
                $"A course with ID {id} was not found.");
        }

        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Title = request.Title.Trim();
        entity.CreditHours = request.CreditHours!.Value;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning("Duplicate course update attempt for CourseId={CourseId}, Code='{Code}'.", id, entity.Code);
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Duplicate course",
                $"A course with code '{entity.Code}' already exists.");
        }

        _logger.LogInformation("Updated course Id={CourseId}, Code='{Code}'.", entity.Id, entity.Code);

        return new CourseResponseDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            CreditHours = entity.CreditHours,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task DeleteCourseAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Course not found",
                $"A course with ID {id} was not found.");
        }

        _db.Courses.Remove(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsForeignKeyViolation(ex, out _))
        {
            _logger.LogWarning("Rejected deletion of course Id={CourseId} because enrollments exist.", id);
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Course has enrollments",
                "Cannot delete a course that still has student enrollments.");
        }

        _logger.LogInformation("Deleted course Id={CourseId}, Code='{Code}'.", id, entity.Code);
    }
}

