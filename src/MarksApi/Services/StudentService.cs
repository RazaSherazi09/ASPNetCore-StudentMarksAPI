using MarksApi.Common;
using MarksApi.Data;
using MarksApi.Data.Entities;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Students;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Services;

public class StudentService : IStudentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StudentService> _logger;

    public StudentService(AppDbContext db, ILogger<StudentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResultDto<StudentResponseDto>> GetStudentsAsync(
        ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = query.Normalize();

        var studentsQuery = _db.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            studentsQuery = studentsQuery.Where(s =>
                s.RegistrationNo.ToLower().Contains(term) ||
                s.FullName.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term));
        }

        studentsQuery = (query.Sort?.Trim().ToLowerInvariant()) switch
        {
            "name" or "fullname" or "fullname_asc" => studentsQuery.OrderBy(s => s.FullName).ThenBy(s => s.Id),
            "name_desc" or "fullname_desc" => studentsQuery.OrderByDescending(s => s.FullName).ThenBy(s => s.Id),
            "registrationno" or "registrationno_asc" => studentsQuery.OrderBy(s => s.RegistrationNo),
            "registrationno_desc" => studentsQuery.OrderByDescending(s => s.RegistrationNo),
            "email" or "email_asc" => studentsQuery.OrderBy(s => s.Email),
            "email_desc" => studentsQuery.OrderByDescending(s => s.Email),
            "createdat" or "createdat_asc" => studentsQuery.OrderBy(s => s.CreatedAtUtc),
            "createdat_desc" => studentsQuery.OrderByDescending(s => s.CreatedAtUtc),
            "id_desc" => studentsQuery.OrderByDescending(s => s.Id),
            _ => studentsQuery.OrderBy(s => s.Id)
        };

        var totalCount = await studentsQuery.CountAsync(cancellationToken);

        var items = await studentsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentResponseDto
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<StudentResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<StudentResponseDto> GetStudentByIdAsync(int id, CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentResponseDto
            {
                Id = s.Id,
                RegistrationNo = s.RegistrationNo,
                FullName = s.FullName,
                Email = s.Email,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Student not found",
                $"A student with ID {id} was not found.");
        }

        return student;
    }

    public async Task<StudentResponseDto> CreateStudentAsync(
        CreateStudentRequestDto request,
        CancellationToken cancellationToken)
    {
        var entity = new Student
        {
            RegistrationNo = request.RegistrationNo.Trim(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Students.Add(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                "Duplicate student registration attempt for RegistrationNo '{RegistrationNo}' or Email '{Email}'.",
                entity.RegistrationNo,
                entity.Email);

            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Duplicate student",
                "A student with this registration number or email already exists.");
        }

        _logger.LogInformation(
            "Created student Id={StudentId}, RegistrationNo='{RegistrationNo}'.",
            entity.Id,
            entity.RegistrationNo);

        return new StudentResponseDto
        {
            Id = entity.Id,
            RegistrationNo = entity.RegistrationNo,
            FullName = entity.FullName,
            Email = entity.Email,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task<StudentResponseDto> UpdateStudentAsync(
        int id,
        UpdateStudentRequestDto request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Student not found",
                $"A student with ID {id} was not found.");
        }

        entity.RegistrationNo = request.RegistrationNo.Trim();
        entity.FullName = request.FullName.Trim();
        entity.Email = request.Email.Trim().ToLowerInvariant();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(
                "Duplicate student update attempt for StudentId={StudentId}, RegistrationNo='{RegistrationNo}', Email='{Email}'.",
                id,
                entity.RegistrationNo,
                entity.Email);

            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Duplicate student",
                "A student with this registration number or email already exists.");
        }

        _logger.LogInformation("Updated student Id={StudentId}, RegistrationNo='{RegistrationNo}'.", entity.Id, entity.RegistrationNo);

        return new StudentResponseDto
        {
            Id = entity.Id,
            RegistrationNo = entity.RegistrationNo,
            FullName = entity.FullName,
            Email = entity.Email,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    public async Task DeleteStudentAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "Student not found",
                $"A student with ID {id} was not found.");
        }

        _db.Students.Remove(entity);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionHelper.IsForeignKeyViolation(ex, out _))
        {
            _logger.LogWarning("Rejected deletion of student Id={StudentId} because enrollments exist.", id);
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "Student has enrollments",
                "Cannot delete a student that still has course enrollments.");
        }

        _logger.LogInformation("Deleted student Id={StudentId}, RegistrationNo='{RegistrationNo}'.", id, entity.RegistrationNo);
    }
}

