using MarksApi.Dtos.Common;
using MarksApi.Dtos.Enrollments;

namespace MarksApi.Services;

public interface IEnrollmentService
{
    Task<PagedResultDto<EnrollmentResponseDto>> GetCourseEnrollmentsAsync(
        int courseId,
        ListQueryParameters query,
        CancellationToken cancellationToken);

    Task<EnrollmentResponseDto> EnrollStudentAsync(
        int courseId,
        CreateEnrollmentRequestDto request,
        CancellationToken cancellationToken);

    Task<EnrollmentResponseDto> UpdateMarkAsync(
        int enrollmentId,
        UpdateMarkRequestDto request,
        CancellationToken cancellationToken);

    Task DeleteEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken);
}

