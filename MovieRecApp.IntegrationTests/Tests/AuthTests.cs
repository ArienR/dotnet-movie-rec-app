using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using MovieRecApp.IntegrationTests.TestHelpers;
using MovieRecApp.Shared.Models;
using Xunit.Abstractions;

namespace MovieRecApp.IntegrationTests.Tests;

[Collection("IntegrationTests")]
public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public AuthTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        }).CreateClient();

        _output = output;
    }


    [Fact]
    public async Task Register_NewUser_ReturnsCreated()
    {
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            UserName = "testuser",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
    
    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsAreValid()
    {
        // Arrange: Register user
        var registerPayload = new RegisterRequest
        {
            UserName = "loginuser",
            Email = "login@example.com",
            Password = "P4$$word",
            ConfirmPassword = "P4$$word"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Act: Login with correct credentials
        var loginPayload = new LoginRequest
        {
            EmailOrUsername = "loginuser",
            Password = "P4$$word"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("token", out var token));
        _output.WriteLine($"JWT Token: {token}");
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
    {
        var loginPayload = new LoginRequest
        {
            EmailOrUsername = "nonexistent",
            Password = "WrongPassword123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenFieldsAreMissing()
    {
        var loginPayload = new LoginRequest()
        {
            EmailOrUsername = "",
            Password = ""
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var registerPayload = new RegisterRequest
        {
            UserName = "duplicateuser",
            Email = "user@example.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!"
        };

        // First registration (should succeed)
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        // Second registration with same username
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }
    
    [Fact]
    public async Task GetUserProfile_AuthenticatedUser_ReturnsCorrectData()
    {
        var registerPayload = new RegisterRequest
        {
            UserName = "profileuser",
            Email = "profile@example.com",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        var loginPayload = new LoginRequest
        {
            EmailOrUsername = "profileuser",
            Password = "Test123!"
        };

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var content = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = content.GetProperty("token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/profileuser");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        var profileContent = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("profileuser", profileContent);
    }

    [Fact]
    public async Task GetUserProfile_UnknownUser_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/users/unknownuser12345");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_MismatchedPasswords_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Email = "mismatch@example.com",
            UserName = "mismatchuser",
            Password = "Test123!",
            ConfirmPassword = "WrongPassword"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmail_ReturnsToken()
    {
        var registerPayload = new RegisterRequest
        {
            UserName = "emailuser",
            Email = "email@example.com",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        var loginPayload = new LoginRequest
        {
            EmailOrUsername = "email@example.com",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("token", out _));
    }
    
    [Fact]
    public async Task Login_WithUsername_ReturnsToken()
    {
        var registerPayload = new RegisterRequest
        {
            UserName = "emailuser",
            Email = "email@example.com",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        var loginPayload = new LoginRequest
        {
            EmailOrUsername = "emailuser",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(content.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Email = "notanemail",
            UserName = "bademailuser",
            Password = "Test123!",
            ConfirmPassword = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}