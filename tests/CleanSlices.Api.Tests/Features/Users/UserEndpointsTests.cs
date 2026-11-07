using System.Net;
using CleanSlices.Api.Configuration;
using CleanSlices.Api.Features.Users;
using CleanSlices.Api.Tests.Infrastructure;
#if (validation)
using CleanSlices.Core.Domain.Users;
#endif
using CleanSlices.Infrastructure.Pagination;

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
#if (validation)

	[Fact]
	public async Task An_invalid_request_is_rejected_before_the_handler_checks_the_email()
	{
		var email = UniqueEmail();
		await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", email));

		// Without request validation the handler would answer 409 (email in use) before the domain saw the name.
		var response = await _client.Post("/users", new CreateUser.CreateUserRequest(new string('a', User.NameMaxLength + 1), email));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var problem = await response.Content.ReadValidationProblemAsync();
		Assert.True(problem.Errors.ContainsKey("name"));
	}
#endif

	[Fact]
	public async Task Create_with_an_email_in_use_ignoring_case_returns_409()
	{
		var local = Guid.NewGuid().ToString("N");
		await _client.Post("/users", new CreateUser.CreateUserRequest("Ana", $"Ana.{local}@Example.com"));

		var response = await _client.Post("/users", new CreateUser.CreateUserRequest("Luis", $"ana.{local}@example.com"));

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

	[Fact]
	public async Task List_returns_pages_newest_first_with_totals()
	{
		await using var api = await factory.WithEmptyDatabaseAsync();
		var client = api.CreateClient();
		var oldest = await CreateUserAsync(client);
		var middle = await CreateUserAsync(client);
		var newest = await CreateUserAsync(client);

		var first = await GetPageAsync(client, "/users?pageSize=2");
		var second = await GetPageAsync(client, "/users?pageNumber=2&pageSize=2");

		Assert.Equal([newest, middle], first.Items.Select(user => user.Id));
		Assert.Equal((1, 2, 3, 2), (first.PageNumber, first.PageSize, first.TotalCount, first.TotalPages));
		Assert.Equal((false, true), (first.HasPreviousPage, first.HasNextPage));
		Assert.Equal(oldest, Assert.Single(second.Items).Id);
		Assert.Equal(2, second.PageNumber);
		Assert.Equal((true, false), (second.HasPreviousPage, second.HasNextPage));
	}

	[Theory]
	[InlineData("/users", 1, PaginationParameters.DefaultPageSize)]
	[InlineData("/users?pageNumber=0&pageSize=1000", 1, PaginationParameters.MaxPageSize)]
	[InlineData("/users?pageNumber=-5&pageSize=0", 1, 1)]
	public async Task List_clamps_out_of_range_paging_values(string uri, int expectedPage, int expectedPageSize)
	{
		var page = await GetPageAsync(_client, uri);

		Assert.Equal(expectedPage, page.PageNumber);
		Assert.Equal(expectedPageSize, page.PageSize);
	}

	private Task<Guid> CreateUserAsync() => CreateUserAsync(_client);

	private static async Task<Guid> CreateUserAsync(HttpClient client)
	{
		var response = await client.Post("/users", new CreateUser.CreateUserRequest("Ana", UniqueEmail()));
		return (await response.Content.ReadAsAsync<CreateUser.CreateUserResponse>()).Id;
	}

	private static async Task<PaginationResponse<GetUsers.UserListItemResponse>> GetPageAsync(HttpClient client, string uri)
	{
		var response = await client.Get(uri);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await response.Content.ReadAsAsync<PaginationResponse<GetUsers.UserListItemResponse>>();
	}

	private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.com";
}
