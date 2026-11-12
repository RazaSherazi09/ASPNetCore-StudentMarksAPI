using MarksApi.Dtos.Reports;

namespace MarksApi.Services;

public interface IReportService
{
    Task<CourseStatsResponseDto> GetCourseStatsAsync(int courseId, CancellationToken cancellationToken);
    Task<TranscriptResponseDto> GetStudentTranscriptAsync(int studentId, CancellationToken cancellationToken);
}

