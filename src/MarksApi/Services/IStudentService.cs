using MarksApi.Dtos.Common;
using MarksApi.Dtos.Students;

namespace MarksApi.Services;

public interface IStudentService
{
    Task<PagedResultDto<StudentResponseDto>> GetStudentsAsync(ListQueryParameters query, CancellationToken cancellationToken);
    Task<StudentResponseDto> GetStudentByIdAsync(int id, CancellationToken cancellationToken);
    Task<StudentResponseDto> CreateStudentAsync(CreateStudentRequestDto request, CancellationToken cancellationToken);
    Task<StudentResponseDto> UpdateStudentAsync(int id, UpdateStudentRequestDto request, CancellationToken cancellationToken);
    Task DeleteStudentAsync(int id, CancellationToken cancellationToken);
}

