using System.Net;
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
	public async Task List_includes_a_created_one()
	{
		var id = await CreateAsync();

		var response = await _client.Get("/widgets");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var items = await response.Content.ReadAsAsync<List<GetWidgets.WidgetListItemResponse>>();
		Assert.Contains(items, item => item.Id == id);
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

	private async Task<Guid> CreateAsync()
	{
		var response = await _client.Post("/widgets", new CreateWidget.CreateWidgetRequest("First"));
		return (await response.Content.ReadAsAsync<CreateWidget.CreateWidgetResponse>()).Id;
	}

	private async Task<GetWidgetById.GetWidgetByIdResponse> GetAsync(Guid id) =>
		await (await _client.Get($"/widgets/{id}")).Content.ReadAsAsync<GetWidgetById.GetWidgetByIdResponse>();
}
