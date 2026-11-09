using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarksApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    credit_hours = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                    table.CheckConstraint("ck_courses_credit_hours", "credit_hours >= 1 AND credit_hours <= 4");
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    registration_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    course_id = table.Column<int>(type: "integer", nullable: false),
                    mark = table.Column<int>(type: "integer", nullable: true),
                    enrolled_at_utc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.CheckConstraint("ck_enrollments_mark", "mark IS NULL OR (mark >= 0 AND mark <= 100)");
                    table.ForeignKey(
                        name: "fk_enrollments_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    student_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('Teacher', 'Student')");
                    table.ForeignKey(
                        name: "fk_users_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "code", "created_at_utc", "credit_hours", "title" },
                values: new object[,]
                {
                    { 1, "CS401", new DateTime(2026, 1, 10, 9, 0, 0, 0, DateTimeKind.Utc), 3, "Advanced Programming" },
                    { 2, "MA201", new DateTime(2026, 1, 10, 9, 5, 0, 0, DateTimeKind.Utc), 2, "Linear Algebra and Probability" },
                    { 3, "AI302", new DateTime(2026, 1, 10, 9, 10, 0, 0, DateTimeKind.Utc), 4, "Machine Learning Fundamentals" }
                });

            migrationBuilder.InsertData(
                table: "students",
                columns: new[] { "id", "created_at_utc", "email", "full_name", "registration_no" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 15, 8, 0, 0, 0, DateTimeKind.Utc), "ali.raza@example.com", "Ali Raza", "CS-2022-001" },
                    { 2, new DateTime(2026, 1, 15, 8, 5, 0, 0, DateTimeKind.Utc), "sara.khan@example.com", "Sara Khan", "CS-2022-002" },
                    { 3, new DateTime(2026, 1, 15, 8, 10, 0, 0, DateTimeKind.Utc), "usman.tariq@example.com", "Usman Tariq", "CS-2022-003" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "password_hash", "role", "student_id", "username" },
                values: new object[] { 1, "AQAAAAIAAYagAAAAEAECAwQFBgcICQoLDA0ODxCcoFlcQtXZ9CKxNEfS6GrDruJO+IBObD12PkObrMjVkg==", "Teacher", null, "teacher1" });

            migrationBuilder.InsertData(
                table: "enrollments",
                columns: new[] { "id", "course_id", "enrolled_at_utc", "mark", "student_id" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 2, 1, 10, 0, 0, 0, DateTimeKind.Utc), 80, 1 },
                    { 2, 2, new DateTime(2026, 2, 1, 10, 5, 0, 0, DateTimeKind.Utc), 60, 1 },
                    { 3, 1, new DateTime(2026, 2, 1, 10, 10, 0, 0, DateTimeKind.Utc), 70, 2 },
                    { 4, 3, new DateTime(2026, 2, 1, 10, 15, 0, 0, DateTimeKind.Utc), 45, 2 },
                    { 5, 1, new DateTime(2026, 2, 1, 10, 20, 0, 0, DateTimeKind.Utc), null, 3 },
                    { 6, 3, new DateTime(2026, 2, 1, 10, 25, 0, 0, DateTimeKind.Utc), 88, 3 }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "password_hash", "role", "student_id", "username" },
                values: new object[,]
                {
                    { 2, "AQAAAAIAAYagAAAAEBESExQVFhcYGRobHB0eHyDz58Ko6TZl5Q6xiIl3VaegOZir2yYCsPbd0xzvnZzkaA==", "Student", 1, "student1" },
                    { 3, "AQAAAAIAAYagAAAAEBESExQVFhcYGRobHB0eHyDz58Ko6TZl5Q6xiIl3VaegOZir2yYCsPbd0xzvnZzkaA==", "Student", 2, "student2" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_courses_code",
                table: "courses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_course_id",
                table: "enrollments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id_course_id",
                table: "enrollments",
                columns: new[] { "student_id", "course_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_email",
                table: "students",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_registration_no",
                table: "students",
                column: "registration_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_student_id",
                table: "users",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('students', 'id'), COALESCE((SELECT MAX(id) FROM students), 1), true);
                SELECT setval(pg_get_serial_sequence('courses', 'id'), COALESCE((SELECT MAX(id) FROM courses), 1), true);
                SELECT setval(pg_get_serial_sequence('enrollments', 'id'), COALESCE((SELECT MAX(id) FROM enrollments), 1), true);
                SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE((SELECT MAX(id) FROM users), 1), true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "students");
        }
    }
}
