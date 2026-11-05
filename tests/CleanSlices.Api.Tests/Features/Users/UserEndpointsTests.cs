using System.Net;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;

namespace CleanSlices.Api.Tests.Features.Users;

public class UserEndpointsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task Create_returns_201_and_the_user_can_be_read()
	{
		var request = new CreateUser.CreateUserRequest("Ana", UniqueEmail());

		var response = await _client.Post("/users", request);

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var created = await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>();
		Assert.Equal($"/users/{created.Id}", response.Headers.Location?.AbsolutePath);

		var user = await (await _client.Get($"/users/{created.Id}")).Content.ReadAsAsync<GetUserById.GetUserByIdResponse>();
		Assert.Equal(request.Name, user.Name);
		Assert.Equal(request.Email, user.Email);
	}

	[Fact]
	public async Task Create_with_invalid_fields_returns_400_with_errors_per_field()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("", "not-an-email"));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var problem = await response.Content.ReadValidationProblemAsync();
		Assert.Contains("name", problem.Errors.Keys);
		Assert.Contains("email", problem.Errors.Keys);
	}

	[Fact]
	public async Task Create_with_an_email_in_use_returns_409()
	{
		var email = UniqueEmail();
		await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", email));

		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Luis", email));

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
	}

	[Fact]
	public async Task Get_with_an_unknown_id_returns_404()
	{
		var response = await _client.Get($"/users/{Guid.CreateVersion7()}");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task UpdateName_changes_the_name()
	{
		var id = await CreateUserAsync();

		var response = await _client.Patch($"/users/{id}/name", new UpdateName.UpdateUserNameRequest("Ana María"));

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		var user = await (await _client.Get($"/users/{id}")).Content.ReadAsAsync<GetUserById.GetUserByIdResponse>();
		Assert.Equal("Ana María", user.Name);
	}

	[Fact]
	public async Task Delete_removes_the_user()
	{
		var id = await CreateUserAsync();

		var response = await _client.Delete($"/users/{id}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await _client.Get($"/users/{id}")).StatusCode);
	}

	private async Task<Guid> CreateUserAsync()
	{
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", UniqueEmail()));
		return (await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;
	}

	private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
}
