using MarksApi.Dtos.Common;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Enrollments;
using MarksApi.Dtos.Reports;
using MarksApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IReportService _reportService;

    public CoursesController(
        ICourseService courseService,
        IEnrollmentService enrollmentService,
        IReportService reportService)
    {
        _courseService = courseService;
        _enrollmentService = enrollmentService;
        _reportService = reportService;
    }

    [HttpGet]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(PagedResultDto<CourseResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResultDto<CourseResponseDto>>> GetCourses(
        [FromQuery] ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _courseService.GetCoursesAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseResponseDto>> GetCourseById(
        int id,
        CancellationToken cancellationToken)
    {
        var course = await _courseService.GetCourseByIdAsync(id, cancellationToken);
        return Ok(course);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseResponseDto>> CreateCourse(
        [FromBody] CreateCourseRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await _courseService.CreateCourseAsync(request, cancellationToken);
        return Created($"/api/courses/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseResponseDto>> UpdateCourse(
        int id,
        [FromBody] UpdateCourseRequestDto request,
        CancellationToken cancellationToken)
    {
        var updated = await _courseService.UpdateCourseAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCourse(
        int id,
        CancellationToken cancellationToken)
    {
        await _courseService.DeleteCourseAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{courseId:int}/enrollments")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentResponseDto>> EnrollStudent(
        int courseId,
        [FromBody] CreateEnrollmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await _enrollmentService.EnrollStudentAsync(courseId, request, cancellationToken);
        return Created($"/api/courses/{courseId}/enrollments", created);
    }

    [HttpGet("{courseId:int}/enrollments")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(PagedResultDto<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResultDto<EnrollmentResponseDto>>> GetCourseEnrollments(
        int courseId,
        [FromQuery] ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _enrollmentService.GetCourseEnrollmentsAsync(courseId, query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{courseId:int}/stats")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(CourseStatsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseStatsResponseDto>> GetCourseStats(
        int courseId,
        CancellationToken cancellationToken)
    {
        var stats = await _reportService.GetCourseStatsAsync(courseId, cancellationToken);
        return Ok(stats);
    }
}

