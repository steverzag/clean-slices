using CleanSlices.Api.Configuration.Endpoints;
#if (result)
using CleanSlices.Api.Extensions;
#endif
#if (cqrs)
using CleanSlices.Core.Domain;
#endif
#if (repository || cqrs)
using CleanSlices.Core.Domain.Users;
#endif
#if (cqrs && result)
using CleanSlices.Core.Shared;
#endif
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (!repository || cqrs)
using Microsoft.EntityFrameworkCore;
#endif

namespace CleanSlices.Api.Features.Users;

public class GetUserById : IEndpoint
{
	public record GetUserByIdResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt);

#if (cqrs && result)
	public sealed record GetUserByIdQuery(Guid Id) : IQuery<Result<GetUserByIdResponse>>;

	internal sealed class GetUserByIdHandler(IReadDb db) : IQueryHandler<GetUserByIdQuery, Result<GetUserByIdResponse>>
	{
		public async ValueTask<Result<GetUserByIdResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
		{
			var user = await db.Query<User>()
				.Where(u => u.Id == query.Id)
				.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
				.FirstOrDefaultAsync(cancellationToken);

			return user is null
				? Result.Failure<GetUserByIdResponse>(UserErrors.NotFound(query.Id))
				: user;
		}
	}
#elif (cqrs)
	public sealed record GetUserByIdQuery(Guid Id) : IQuery<GetUserByIdResponse>;

	internal sealed class GetUserByIdHandler(IReadDb db) : IQueryHandler<GetUserByIdQuery, GetUserByIdResponse>
	{
		public async ValueTask<GetUserByIdResponse> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
		{
			var user = await db.Query<User>()
				.Where(u => u.Id == query.Id)
				.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
				.FirstOrDefaultAsync(cancellationToken);

			return user ?? throw UserErrors.NotFound(query.Id);
		}
	}
#endif

#if (cqrs && result)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken)
	{
		var result = await sender.Send(new GetUserByIdQuery(id), cancellationToken);
		return result.ToOkOrProblem();
	}
#elif (cqrs)
	public async Task<IResult> Handler(ISender sender, Guid id, CancellationToken cancellationToken) =>
		Results.Ok(await sender.Send(new GetUserByIdQuery(id), cancellationToken));
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (result)
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users
			.AsNoTracking()
			.Where(u => u.Id == id)
			.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(user);
	}
#else
	public async Task<IResult> Handler(AppDbContext context, Guid id, CancellationToken cancellationToken)
	{
		var user = await context.Users
			.AsNoTracking()
			.Where(u => u.Id == id)
			.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
			.FirstOrDefaultAsync(cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(user);
	}
#endif

	public void Register(IEndpointRouteBuilder app)
	{
		app.MapGet("/users/{id:guid}", Handler)
			.WithTags("Users")
			.WithName(nameof(GetUserById));
	}
}
