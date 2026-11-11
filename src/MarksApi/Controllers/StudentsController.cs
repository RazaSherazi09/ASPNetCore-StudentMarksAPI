using System.Security.Claims;
using MarksApi.Common;
using MarksApi.Dtos.Common;
using MarksApi.Dtos.Reports;
using MarksApi.Dtos.Students;
using MarksApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IReportService _reportService;

    public StudentsController(IStudentService studentService, IReportService reportService)
    {
        _studentService = studentService;
        _reportService = reportService;
    }

    [HttpGet]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(PagedResultDto<StudentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<StudentResponseDto>>> GetStudents(
        [FromQuery] ListQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _studentService.GetStudentsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentResponseDto>> GetStudentById(
        int id,
        CancellationToken cancellationToken)
    {
        EnsureCanAccessStudentRecord(id);
        var student = await _studentService.GetStudentByIdAsync(id, cancellationToken);
        return Ok(student);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponseDto>> CreateStudent(
        [FromBody] CreateStudentRequestDto request,
        CancellationToken cancellationToken)
    {
        var created = await _studentService.CreateStudentAsync(request, cancellationToken);
        return Created($"/api/students/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudentResponseDto>> UpdateStudent(
        int id,
        [FromBody] UpdateStudentRequestDto request,
        CancellationToken cancellationToken)
    {
        var updated = await _studentService.UpdateStudentAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteStudent(
        int id,
        CancellationToken cancellationToken)
    {
        await _studentService.DeleteStudentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/transcript")]
    [Authorize(Roles = "Teacher,Student")]
    [ProducesResponseType(typeof(TranscriptResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptResponseDto>> GetTranscript(
        int id,
        CancellationToken cancellationToken)
    {
        EnsureCanAccessStudentRecord(id);
        var transcript = await _reportService.GetStudentTranscriptAsync(id, cancellationToken);
        return Ok(transcript);
    }

    private void EnsureCanAccessStudentRecord(int requestedStudentId)
    {
        if (User.IsInRole("Teacher"))
        {
            return;
        }

        var studentIdClaim = User.FindFirstValue("student_id");
        if (!int.TryParse(studentIdClaim, out var ownStudentId) || ownStudentId != requestedStudentId)
        {
            throw new ApiProblemException(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "A student may only view their own record and transcript.");
        }
    }
}

