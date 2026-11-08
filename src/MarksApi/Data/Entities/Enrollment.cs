namespace MarksApi.Data.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int? Mark { get; set; }
    public DateTime EnrolledAtUtc { get; set; }

    /// <summary>
    /// Mapped to PostgreSQL system column xmin for optimistic concurrency on mark updates.
    /// </summary>
    public uint Version { get; set; }

    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

