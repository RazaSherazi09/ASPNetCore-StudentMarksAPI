using MarksApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public class AppDbContext : DbContext
{
    // Pre-computed ASP.NET Core Identity PasswordHasher<User> V3 hashes for development seed users:
    public const string SeedTeacherPasswordHash = "AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxCcoFlcQtXZ9CKxNEfS6GrDruJO+IBObD12PkObrMjVkg==";
    public const string SeedStudentPasswordHash = "AQAAAAIAAYagAAAAEBESExQVFhcYGRobHB0eHyDz58Ko6TZl5Q6xiIl3VaegOZir2yYCsPbd0xzvnZzkaA==";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. students table
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();

            entity.Property(s => s.RegistrationNo)
                .IsRequired()
                .HasMaxLength(20);
            entity.HasIndex(s => s.RegistrationNo)
                .IsUnique();

            entity.Property(s => s.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Email)
                .IsRequired()
                .HasMaxLength(150);
            entity.HasIndex(s => s.Email)
                .IsUnique();

            entity.Property(s => s.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            entity.HasData(
                new Student
                {
                    Id = 1,
                    RegistrationNo = "CS-2022-001",
                    FullName = "Ali Raza",
                    Email = "ali.raza@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc)
                },
                new Student
                {
                    Id = 2,
                    RegistrationNo = "CS-2022-002",
                    FullName = "Sara Khan",
                    Email = "sara.khan@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 15, 8, 5, 0, DateTimeKind.Utc)
                },
                new Student
                {
                    Id = 3,
                    RegistrationNo = "CS-2022-003",
                    FullName = "Usman Tariq",
                    Email = "usman.tariq@example.com",
                    CreatedAtUtc = new DateTime(2026, 1, 15, 8, 10, 0, DateTimeKind.Utc)
                }
            );
        });

        // 2. courses table
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses", t =>
            {
                t.HasCheckConstraint("ck_courses_credit_hours", "credit_hours >= 1 AND credit_hours <= 4");
            });
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedOnAdd();

            entity.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(10);
            entity.HasIndex(c => c.Code)
                .IsUnique();

            entity.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(c => c.CreditHours)
                .IsRequired();

            entity.Property(c => c.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            entity.HasData(
                new Course
                {
                    Id = 1,
                    Code = "CS401",
                    Title = "Advanced Programming",
                    CreditHours = 3,
                    CreatedAtUtc = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc)
                },
                new Course
                {
                    Id = 2,
                    Code = "MA201",
                    Title = "Linear Algebra and Probability",
                    CreditHours = 2,
                    CreatedAtUtc = new DateTime(2026, 1, 10, 9, 5, 0, DateTimeKind.Utc)
                },
                new Course
                {
                    Id = 3,
                    Code = "AI302",
                    Title = "Machine Learning Fundamentals",
                    CreditHours = 4,
                    CreatedAtUtc = new DateTime(2026, 1, 10, 9, 10, 0, DateTimeKind.Utc)
                }
            );
        });

        // 3. enrollments table
        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments", t =>
            {
                t.HasCheckConstraint("ck_enrollments_mark", "mark IS NULL OR (mark >= 0 AND mark <= 100)");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.StudentId).IsRequired();
            entity.Property(e => e.CourseId).IsRequired();
            entity.Property(e => e.Mark).IsRequired(false);

            entity.Property(e => e.EnrolledAtUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            // Optimistic concurrency via PostgreSQL system column xmin
            entity.Property(e => e.Version)
                .IsRowVersion();

            entity.HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();

            entity.HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new Enrollment
                {
                    Id = 1,
                    StudentId = 1,
                    CourseId = 1,
                    Mark = 80,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc)
                },
                new Enrollment
                {
                    Id = 2,
                    StudentId = 1,
                    CourseId = 2,
                    Mark = 60,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 5, 0, DateTimeKind.Utc)
                },
                new Enrollment
                {
                    Id = 3,
                    StudentId = 2,
                    CourseId = 1,
                    Mark = 70,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 10, 0, DateTimeKind.Utc)
                },
                new Enrollment
                {
                    Id = 4,
                    StudentId = 2,
                    CourseId = 3,
                    Mark = 45,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 15, 0, DateTimeKind.Utc)
                },
                new Enrollment
                {
                    Id = 5,
                    StudentId = 3,
                    CourseId = 1,
                    Mark = null,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 20, 0, DateTimeKind.Utc)
                },
                new Enrollment
                {
                    Id = 6,
                    StudentId = 3,
                    CourseId = 3,
                    Mark = 88,
                    EnrolledAtUtc = new DateTime(2026, 2, 1, 10, 25, 0, DateTimeKind.Utc)
                }
            );
        });

        // 4. users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_role", "role IN ('Teacher', 'Student')");
            });
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasColumnType("text");

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(u => u.StudentId)
                .IsRequired(false);

            entity.HasOne(u => u.Student)
                .WithMany()
                .HasForeignKey(u => u.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(
                new User
                {
                    Id = 1,
                    Username = "teacher1",
                    PasswordHash = SeedTeacherPasswordHash,
                    Role = "Teacher",
                    StudentId = null
                },
                new User
                {
                    Id = 2,
                    Username = "student1",
                    PasswordHash = SeedStudentPasswordHash,
                    Role = "Student",
                    StudentId = 1
                },
                new User
                {
                    Id = 3,
                    Username = "student2",
                    PasswordHash = SeedStudentPasswordHash,
                    Role = "Student",
                    StudentId = 2
                }
            );
        });
    }
}
