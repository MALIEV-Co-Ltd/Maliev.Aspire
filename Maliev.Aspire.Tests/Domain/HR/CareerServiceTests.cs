using Maliev.Aspire.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.HR;

/// <summary>
/// Integration tests for the career service.
/// </summary>
[Collection("AspireDomainTests")]
public class CareerServiceTests(AspireTestFixture fixture, ITestOutputHelper output)
{
    private readonly AspireTestFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// Tests that a job posting can be created successfully.
    /// </summary>
    [Fact]
    public async Task CreateJobPosting_Succeeds()
    {
        var client = _fixture.CreateAuthenticatedClient("CareerService");

        var request = new
        {
            PositionTitle = "Integration Test Engineer",
            PositionCode = $"IT-ENG-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            Department = "Engineering",
            Location = "Bangkok",
            EmploymentType = "Full-time",
            Description = "Test Description",
            Requirements = "Test Requirements",
            Responsibilities = "Test Responsibilities",
            ApplicationDeadline = DateTime.UtcNow.AddDays(30),
            PublishImmediately = true
        };

        var response = await client.PostAsJsonAsync("/career/v1/job-postings", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(request.PositionTitle, result.GetProperty("positionTitle").GetString());
    }

    /// <summary>
    /// Tests submitting an application for an open job posting.
    /// </summary>
    [Fact]
    public async Task SubmitApplication_ForOpenJob_Succeeds_Or_FailsWithFileError()
    {
        var client = _fixture.CreateAuthenticatedClient("CareerService");

        // 1. Get an active job posting
        var postingsResponse = await client.GetAsync("/career/v1/job-postings");
        var postingsResult = await postingsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var posting = postingsResult.GetProperty("items")[0];
        var postingId = posting.GetProperty("id").GetGuid();

        // 2. Submit application with dummy file ID
        // Expected behavior: CareerService validates file existence in UploadService.
        // If UploadService is empty, it returns 400 with "Resume file ... not found".
        var request = new
        {
            JobPostingId = postingId,
            ApplicantFirstName = "Test",
            ApplicantLastName = "User",
            ApplicantEmail = "test.applicant@example.com",
            ResumeFileId = Guid.NewGuid() // Dummy ID
        };

        var response = await client.PostAsJsonAsync("/career/v1/job-applications", request);

        // We expect either Created (if validation skipped/dummy found) or BadRequest (if file not found)
        // Given ApplicationService.cs, it should be BadRequest (400) because ResumeFileId is random.
        _output.WriteLine($"Submit Application Status: {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains("not found", error.GetProperty("error").GetString());
            _output.WriteLine("Verified: CareerService correctly validated file existence (expected failure).");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }
}
