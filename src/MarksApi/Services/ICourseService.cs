using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;

namespace MarksApi.Services;

public interface ICourseService
{
    Task<PagedResultDto<CourseResponseDto>> GetCoursesAsync(ListQueryParameters query, CancellationToken cancellationToken);
    Task<CourseResponseDto> GetCourseByIdAsync(int id, CancellationToken cancellationToken);
    Task<CourseResponseDto> CreateCourseAsync(CreateCourseRequestDto request, CancellationToken cancellationToken);
    Task<CourseResponseDto> UpdateCourseAsync(int id, UpdateCourseRequestDto request, CancellationToken cancellationToken);
    Task DeleteCourseAsync(int id, CancellationToken cancellationToken);
}

