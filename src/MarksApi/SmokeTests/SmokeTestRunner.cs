using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MarksApi.Data;
using MarksApi.Dtos.Auth;
using MarksApi.Dtos.Courses;
using MarksApi.Dtos.Enrollments;
using MarksApi.Dtos.Students;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.SmokeTests;

public class SmokeTestRunner
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private int _passedCount;
    private int _failedCount;

    public SmokeTestRunner(IServiceProvider services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public async Task<int> RunAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine($"Marks API Startup Smoke Test Suite | Target: {baseUrl}");
        Console.WriteLine("================================================================================");

        var teacherUsername = _configuration["SmokeTest:TeacherUsername"];
        var teacherPassword = _configuration["SmokeTest:TeacherPassword"];
        var studentUsername = _configuration["SmokeTest:StudentUsername"];
        var studentPassword = _configuration["SmokeTest:StudentPassword"];
        var otherStudentId = _configuration.GetValue<int?>("SmokeTest:OtherStudentId") ?? 2;

        if (string.IsNullOrWhiteSpace(teacherUsername) ||
            string.IsNullOrWhiteSpace(teacherPassword) ||
            string.IsNullOrWhiteSpace(studentUsername) ||
            string.IsNullOrWhiteSpace(studentPassword))
        {
            Console.Error.WriteLine("ERROR: SmokeTest credentials are missing from configuration (user-secrets or environment variables).");
            Console.Error.WriteLine("Please configure SmokeTest:TeacherUsername, SmokeTest:TeacherPassword, SmokeTest:StudentUsername, and SmokeTest:StudentPassword.");
            return 1;
        }

        // Pre-clean any leftover SMOKE- records before starting
        await CleanupSmokeDataAsync(cancellationToken);

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };

        int? createdStudentId = null;
        int? createdCourseId = null;
        int? createdEnrollmentId = null;

        try
        {
            // 1. GET /health
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/health",
                body: null,
                token: null,
                expectedStatus: HttpStatusCode.OK,
                description: "Health check open to anyone",
                cancellationToken: cancellationToken);

            // 2. POST /api/auth/login (valid teacher, valid student, 400 bad body, 401 bad credentials)
            var teacherLoginRes = await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/auth/login",
                body: new { username = teacherUsername, password = teacherPassword },
                token: null,
                expectedStatus: HttpStatusCode.OK,
                description: "Teacher login",
                cancellationToken: cancellationToken);

            var teacherAuth = await teacherLoginRes.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken: cancellationToken);
            var teacherToken = teacherAuth?.Token ?? string.Empty;

            var studentLoginRes = await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/auth/login",
                body: new { username = studentUsername, password = studentPassword },
                token: null,
                expectedStatus: HttpStatusCode.OK,
                description: "Student login",
                cancellationToken: cancellationToken);

            var studentAuth = await studentLoginRes.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken: cancellationToken);
            var studentToken = studentAuth?.Token ?? string.Empty;

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/auth/login",
                body: new { username = "", password = "" },
                token: null,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Login with empty credentials",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/auth/login",
                body: new { username = teacherUsername, password = "wrong-password-smoke-check" },
                token: null,
                expectedStatus: HttpStatusCode.Unauthorized,
                description: "Login with wrong password",
                cancellationToken: cancellationToken);

            // 3. GET /api/students (200 Teacher, 401 No token, 403 Student)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students?page=1&pageSize=10",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "List students as Teacher",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students",
                body: null,
                token: null,
                expectedStatus: HttpStatusCode.Unauthorized,
                description: "List students without token",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "List students as Student (forbidden)",
                cancellationToken: cancellationToken);

            // 4. POST /api/students (201, 400, 401, 403, 409)
            var createStudentRes = await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/students",
                body: new
                {
                    registrationNo = "SMOKE-S001",
                    fullName = "SMOKE Test Student",
                    email = "smoke.student1@example.com"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Created,
                description: "Create SMOKE student",
                cancellationToken: cancellationToken);

            var createdStudent = await createStudentRes.Content.ReadFromJsonAsync<StudentResponseDto>(cancellationToken: cancellationToken);
            createdStudentId = createdStudent?.Id;

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/students",
                body: new
                {
                    registrationNo = "SMOKE-BAD",
                    fullName = "",
                    email = "not-an-email"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Create student with invalid body",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/students",
                body: new
                {
                    registrationNo = "SMOKE-UNAUTH",
                    fullName = "Smoke Unauth",
                    email = "smoke.unauth@example.com"
                },
                token: null,
                expectedStatus: HttpStatusCode.Unauthorized,
                description: "Create student without token",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/students",
                body: new
                {
                    registrationNo = "SMOKE-FORBID",
                    fullName = "Smoke Forbid",
                    email = "smoke.forbid@example.com"
                },
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Create student as Student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/students",
                body: new
                {
                    registrationNo = "SMOKE-S001",
                    fullName = "SMOKE Duplicate Student",
                    email = "smoke.student1@example.com"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Create duplicate student (409)",
                cancellationToken: cancellationToken);

            // 5. GET /api/students/{id} (200 Teacher, 200 own Student, 401, 403 other Student, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/students/{createdStudentId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get student by ID as Teacher",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students/1",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get own student record as Student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/students/{otherStudentId}",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Student reading another student's record (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students/999999",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Get non-existent student (404)",
                cancellationToken: cancellationToken);

            // 6. PUT /api/students/{id} (200, 400, 403, 404, 409)
            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/students/{createdStudentId}",
                body: new
                {
                    registrationNo = "SMOKE-S001",
                    fullName = "SMOKE Test Student Updated",
                    email = "smoke.student1.updated@example.com"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Update SMOKE student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/students/{createdStudentId}",
                body: new
                {
                    registrationNo = "",
                    fullName = "",
                    email = "bad"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Update student with invalid body (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                "/api/students/999999",
                body: new
                {
                    registrationNo = "SMOKE-S999",
                    fullName = "SMOKE Unknown",
                    email = "smoke.unknown@example.com"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Update non-existent student (404)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/students/{createdStudentId}",
                body: new
                {
                    registrationNo = "CS-2022-001",
                    fullName = "SMOKE Conflict",
                    email = "ali.raza@example.com"
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Update student with duplicate registrationNo (409)",
                cancellationToken: cancellationToken);

            // 7. GET /api/courses (200 Teacher, 200 Student, 401 No token)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/courses?page=1&pageSize=10",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.OK,
                description: "List courses as Student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/courses",
                body: null,
                token: null,
                expectedStatus: HttpStatusCode.Unauthorized,
                description: "List courses without token (401)",
                cancellationToken: cancellationToken);

            // 8. POST /api/courses (201, 400, 403, 409)
            var createCourseRes = await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/courses",
                body: new
                {
                    code = "SMOKE101",
                    title = "SMOKE Testing Engineering",
                    creditHours = 3
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Created,
                description: "Create SMOKE course",
                cancellationToken: cancellationToken);

            var createdCourse = await createCourseRes.Content.ReadFromJsonAsync<CourseResponseDto>(cancellationToken: cancellationToken);
            createdCourseId = createdCourse?.Id;

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/courses",
                body: new
                {
                    code = "SMOKEBAD",
                    title = "Invalid Credit Hours",
                    creditHours = 6
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Create course with creditHours > 4 (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/courses",
                body: new
                {
                    code = "SMOKE102",
                    title = "Forbidden Course",
                    creditHours = 3
                },
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Create course as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/courses",
                body: new
                {
                    code = "SMOKE101",
                    title = "Duplicate Course",
                    creditHours = 3
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Create duplicate course code (409)",
                cancellationToken: cancellationToken);

            // 9. GET /api/courses/{id} (200, 401, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/courses/{createdCourseId}",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get course by ID as Student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/courses/999999",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Get non-existent course (404)",
                cancellationToken: cancellationToken);

            // 10. PUT /api/courses/{id} (200, 400, 403, 404, 409)
            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/courses/{createdCourseId}",
                body: new
                {
                    code = "SMOKE101",
                    title = "SMOKE Testing Engineering Updated",
                    creditHours = 4
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Update SMOKE course",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/courses/{createdCourseId}",
                body: new
                {
                    code = "SMOKE101",
                    title = "Invalid Credit Hours",
                    creditHours = 0
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Update course with creditHours = 0 (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                "/api/courses/999999",
                body: new
                {
                    code = "SMOKE999",
                    title = "Non-existent Course",
                    creditHours = 3
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Update non-existent course (404)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/courses/{createdCourseId}",
                body: new
                {
                    code = "CS401",
                    title = "Duplicate Code Update",
                    creditHours = 3
                },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Update course with existing code (409)",
                cancellationToken: cancellationToken);

            // 11. POST /api/courses/{courseId}/enrollments (201, 400, 403, 404, 409)
            var createEnrollmentRes = await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                $"/api/courses/{createdCourseId}/enrollments",
                body: new { studentId = createdStudentId },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Created,
                description: "Enroll SMOKE student in SMOKE course",
                cancellationToken: cancellationToken);

            var createdEnrollment = await createEnrollmentRes.Content.ReadFromJsonAsync<EnrollmentResponseDto>(cancellationToken: cancellationToken);
            createdEnrollmentId = createdEnrollment?.Id;

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                $"/api/courses/{createdCourseId}/enrollments",
                body: new { studentId = 0 },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Enroll with invalid studentId = 0 (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                $"/api/courses/{createdCourseId}/enrollments",
                body: new { studentId = createdStudentId },
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Enroll student as Student role (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                "/api/courses/999999/enrollments",
                body: new { studentId = createdStudentId },
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Enroll in non-existent course (404)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Post,
                $"/api/courses/{createdCourseId}/enrollments",
                body: new { studentId = createdStudentId },
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Double enrollment in same course (409)",
                cancellationToken: cancellationToken);

            // 12. GET /api/courses/{courseId}/enrollments (200, 403, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/courses/{createdCourseId}/enrollments",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "List enrollments for SMOKE course",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/courses/{createdCourseId}/enrollments",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "List course enrollments as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/courses/999999/enrollments",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "List enrollments for non-existent course (404)",
                cancellationToken: cancellationToken);

            // 13. PUT /api/enrollments/{id}/mark (200, 400 >100, 400 <0, 403, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/enrollments/{createdEnrollmentId}/mark",
                body: new { mark = 85 },
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Record valid mark (85) on SMOKE enrollment",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/enrollments/{createdEnrollmentId}/mark",
                body: new { mark = 101 },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Record invalid mark 101 (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/enrollments/{createdEnrollmentId}/mark",
                body: new { mark = -1 },
                token: teacherToken,
                expectedStatus: HttpStatusCode.BadRequest,
                description: "Record invalid mark -1 (400)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                $"/api/enrollments/{createdEnrollmentId}/mark",
                body: new { mark = 90 },
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Record mark as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Put,
                "/api/enrollments/999999/mark",
                body: new { mark = 75 },
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Record mark for non-existent enrollment (404)",
                cancellationToken: cancellationToken);

            // 14. GET /api/courses/{courseId}/stats (200, 403, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/courses/{createdCourseId}/stats",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get course stats as Teacher",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/courses/{createdCourseId}/stats",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Get course stats as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/courses/999999/stats",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Get stats for non-existent course (404)",
                cancellationToken: cancellationToken);

            // 15. GET /api/students/{id}/transcript (200 Teacher, 200 own Student, 403 other Student, 404)
            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/students/{createdStudentId}/transcript",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get student transcript as Teacher",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students/1/transcript",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.OK,
                description: "Get own transcript as Student",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                $"/api/students/{otherStudentId}/transcript",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Student reading another student's transcript (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Get,
                "/api/students/999999/transcript",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Get transcript for non-existent student (404)",
                cancellationToken: cancellationToken);

            // 16. DELETE /api/students/{id} while enrolled -> 409 Conflict, 403, 404
            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/students/{createdStudentId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Delete student with existing enrollment (409)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/students/{createdStudentId}",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Delete student as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                "/api/students/999999",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Delete non-existent student (404)",
                cancellationToken: cancellationToken);

            // 17. DELETE /api/courses/{id} while enrolled -> 409 Conflict, 403, 404
            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/courses/{createdCourseId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.Conflict,
                description: "Delete course with existing enrollment (409)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/courses/{createdCourseId}",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Delete course as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                "/api/courses/999999",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Delete non-existent course (404)",
                cancellationToken: cancellationToken);

            // 18. DELETE /api/enrollments/{id} -> 403, 204, 404
            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/enrollments/{createdEnrollmentId}",
                body: null,
                token: studentToken,
                expectedStatus: HttpStatusCode.Forbidden,
                description: "Delete enrollment as Student (403)",
                cancellationToken: cancellationToken);

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/enrollments/{createdEnrollmentId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NoContent,
                description: "Delete SMOKE enrollment (204)",
                cancellationToken: cancellationToken);
            createdEnrollmentId = null;

            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                "/api/enrollments/999999",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NotFound,
                description: "Delete non-existent enrollment (404)",
                cancellationToken: cancellationToken);

            // 19. DELETE /api/students/{id} -> 204 No Content (now that enrollment is deleted)
            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/students/{createdStudentId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NoContent,
                description: "Delete SMOKE student after enrollment removed (204)",
                cancellationToken: cancellationToken);
            createdStudentId = null;

            // 20. DELETE /api/courses/{id} -> 204 No Content (now that enrollment is deleted)
            await CheckEndpointAsync(
                client,
                HttpMethod.Delete,
                $"/api/courses/{createdCourseId}",
                body: null,
                token: teacherToken,
                expectedStatus: HttpStatusCode.NoContent,
                description: "Delete SMOKE course after enrollment removed (204)",
                cancellationToken: cancellationToken);
            createdCourseId = null;
        }
        finally
        {
            await CleanupSmokeDataAsync(cancellationToken);
        }

        Console.WriteLine("================================================================================");
        var total = _passedCount + _failedCount;
        Console.WriteLine($"SMOKE TEST SUMMARY: {_passedCount}/{total} checks PASSED, {_failedCount} FAILED.");
        Console.WriteLine("================================================================================");

        return _failedCount == 0 ? 0 : 1;
    }

    private async Task<HttpResponseMessage> CheckEndpointAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body,
        string? token,
        HttpStatusCode expectedStatus,
        string description,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request, cancellationToken);
        var actualStatus = response.StatusCode;
        var passed = actualStatus == expectedStatus;

        if (passed)
        {
            _passedCount++;
            Console.WriteLine($"PASS | {method.Method,-6} {path,-38} | Expected: {(int)expectedStatus} | Actual: {(int)actualStatus} | {description}");
        }
        else
        {
            _failedCount++;
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"FAIL | {method.Method,-6} {path,-38} | Expected: {(int)expectedStatus} | Actual: {(int)actualStatus} | {description} | Body: {responseBody}");
        }

        return response;
    }

    private async Task CleanupSmokeDataAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var smokeStudents = await db.Students
            .Where(s => s.RegistrationNo.StartsWith("SMOKE") || s.FullName.StartsWith("SMOKE"))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var smokeCourses = await db.Courses
            .Where(c => c.Code.StartsWith("SMOKE") || c.Title.StartsWith("SMOKE"))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (smokeStudents.Count > 0 || smokeCourses.Count > 0)
        {
            var smokeEnrollments = await db.Enrollments
                .Where(e => smokeStudents.Contains(e.StudentId) || smokeCourses.Contains(e.CourseId))
                .ToListAsync(cancellationToken);

            if (smokeEnrollments.Count > 0)
            {
                db.Enrollments.RemoveRange(smokeEnrollments);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (smokeStudents.Count > 0)
            {
                var studentsToRemove = await db.Students.Where(s => smokeStudents.Contains(s.Id)).ToListAsync(cancellationToken);
                db.Students.RemoveRange(studentsToRemove);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (smokeCourses.Count > 0)
            {
                var coursesToRemove = await db.Courses.Where(c => smokeCourses.Contains(c.Id)).ToListAsync(cancellationToken);
                db.Courses.RemoveRange(coursesToRemove);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        // Keep sequences aligned with the remaining rows so repeated smoke test runs leave the DB state identical.
        const string syncSequencesSql = """
            SELECT setval(pg_get_serial_sequence('students', 'id'), COALESCE((SELECT MAX(id) FROM students), 1), true);
            SELECT setval(pg_get_serial_sequence('courses', 'id'), COALESCE((SELECT MAX(id) FROM courses), 1), true);
            SELECT setval(pg_get_serial_sequence('enrollments', 'id'), COALESCE((SELECT MAX(id) FROM enrollments), 1), true);
            SELECT setval(pg_get_serial_sequence('users', 'id'), COALESCE((SELECT MAX(id) FROM users), 1), true);
            """;

        await db.Database.ExecuteSqlRawAsync(syncSequencesSql, cancellationToken);
    }
}

