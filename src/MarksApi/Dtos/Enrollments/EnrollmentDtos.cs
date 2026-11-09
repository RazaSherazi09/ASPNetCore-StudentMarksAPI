using System.ComponentModel.DataAnnotations;

namespace MarksApi.Dtos.Enrollments;

public class CreateEnrollmentRequestDto
{
    [Required(ErrorMessage = "Student ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Student ID must be a positive integer.")]
    public int? StudentId { get; set; }
}

public class UpdateMarkRequestDto
{
    [Required(ErrorMessage = "Mark is required.")]
    [Range(0, 100, ErrorMessage = "Mark must be a whole number from 0 to 100.")]
    public int? Mark { get; set; }
}

public class EnrollmentResponseDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string? RegistrationNo { get; set; }
    public string? StudentName { get; set; }
    public int CourseId { get; set; }
    public string? CourseCode { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }
}

