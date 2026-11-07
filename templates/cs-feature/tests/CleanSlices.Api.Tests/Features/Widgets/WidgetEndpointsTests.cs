using System.Net;
using CleanSlices.Api.Configuration;
using CleanSlices.Api.Features.Widgets;
using CleanSlices.Api.Tests.Infrastructure;

namespace CleanSlices.Api.Tests.Features.Widgets;

public class WidgetEndpointsTests(ApiFactory factory)
{
	private readonly HttpClient _client = factory.CreateClient();

	[Fact]
	public async Task Create_returns_201_and_it_can_be_read()
	{
		var response = await _client.Post("/widgets", new CreateWidget.CreateWidgetRequest("First"));

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var created = await response.Content.ReadAsAsync<CreateWidget.CreateWidgetResponse>();
		Assert.Equal("First", (await GetAsync(created.Id)).Name);
	}
#if (idempotency)

	[Fact]
	public async Task Create_retried_with_the_same_idempotency_key_creates_one_widget()
	{
		var key = Guid.NewGuid().ToString();
		var request = new CreateWidget.CreateWidgetRequest("First");

		var first = await _client.Post("/widgets", request, key);
		var retry = await _client.Post("/widgets", request, key);

		Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
		Assert.Equal(first.Headers.Location, retry.Headers.Location);
		Assert.Equal(
			(await first.Content.ReadAsAsync<CreateWidget.CreateWidgetResponse>()).Id,
			(await retry.Content.ReadAsAsync<CreateWidget.CreateWidgetResponse>()).Id);
	}
#endif

	[Fact]
	public async Task Create_with_an_empty_name_returns_400_with_the_field_error()
	{
		var response = await _client.Post("/widgets", new CreateWidget.CreateWidgetRequest(""));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		var problem = await response.Content.ReadValidationProblemAsync();
		Assert.Contains("name", problem.Errors.Keys);
	}

	[Fact]
	public async Task Get_with_an_unknown_id_returns_404()
	{
		var response = await _client.Get($"/widgets/{Guid.CreateVersion7()}");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task List_returns_pages_newest_first_with_totals()
	{
		// Its own empty database, so the totals only count what this test creates.
		await using var api = await factory.WithEmptyDatabaseAsync();
		var client = api.CreateClient();
		var oldest = await CreateAsync(client);
		var middle = await CreateAsync(client);
		var newest = await CreateAsync(client);

		var first = await GetPageAsync(client, "/widgets?pageSize=2");
		var second = await GetPageAsync(client, "/widgets?pageNumber=2&pageSize=2");

		Assert.Equal([newest, middle], first.Items.Select(item => item.Id));
		Assert.Equal((1, 2, 3, 2), (first.PageNumber, first.PageSize, first.TotalCount, first.TotalPages));
		Assert.Equal((false, true), (first.HasPreviousPage, first.HasNextPage));
		Assert.Equal(oldest, Assert.Single(second.Items).Id);
		Assert.Equal((true, false), (second.HasPreviousPage, second.HasNextPage));
	}

	[Fact]
	public async Task UpdateName_changes_the_name()
	{
		var id = await CreateAsync();

		var response = await _client.Patch($"/widgets/{id}/name", new UpdateWidgetName.UpdateWidgetNameRequest("Second"));

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal("Second", (await GetAsync(id)).Name);
	}

	[Fact]
	public async Task Delete_removes_it()
	{
		var id = await CreateAsync();

		var response = await _client.Delete($"/widgets/{id}");

		Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await _client.Get($"/widgets/{id}")).StatusCode);
	}

	private Task<Guid> CreateAsync() => CreateAsync(_client);

	private static async Task<Guid> CreateAsync(HttpClient client)
	{
		var response = await client.Post("/widgets", new CreateWidget.CreateWidgetRequest("First"));
		return (await response.Content.ReadAsAsync<CreateWidget.CreateWidgetResponse>()).Id;
	}

	private static async Task<PaginationResponse<GetWidgets.WidgetListItemResponse>> GetPageAsync(HttpClient client, string uri)
	{
		var response = await client.Get(uri);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		return await response.Content.ReadAsAsync<PaginationResponse<GetWidgets.WidgetListItemResponse>>();
	}

	private async Task<GetWidgetById.GetWidgetByIdResponse> GetAsync(Guid id) =>
		await (await _client.Get($"/widgets/{id}")).Content.ReadAsAsync<GetWidgetById.GetWidgetByIdResponse>();
}
