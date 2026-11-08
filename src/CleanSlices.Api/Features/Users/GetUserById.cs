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
#if (hybrid_cache)
using CleanSlices.Infrastructure.EventHandlers.Users;
#endif
#if (!repository && !cqrs)
using CleanSlices.Infrastructure.Persistence;
#endif
#if (hybrid_cache)
using Microsoft.Extensions.Caching.Hybrid;
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

#elif (cqrs)
	public sealed record GetUserByIdQuery(Guid Id) : IQuery<GetUserByIdResponse>;

#endif
#if (cqrs && result && hybrid_cache)
	internal sealed class GetUserByIdHandler(IReadDb db, HybridCache cache) : IQueryHandler<GetUserByIdQuery, Result<GetUserByIdResponse>>
	{
		public async ValueTask<Result<GetUserByIdResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
		{
			var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
				UserCache.Key(query.Id),
				async token => await db.Query<User>()
					.Where(u => u.Id == query.Id)
					.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
					.FirstOrDefaultAsync(token),
				cancellationToken: cancellationToken);

			return user is null
				? Result.Failure<GetUserByIdResponse>(UserErrors.NotFound(query.Id))
				: user;
		}
	}
#elif (cqrs && result)
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
#elif (cqrs && hybrid_cache)
	internal sealed class GetUserByIdHandler(IReadDb db, HybridCache cache) : IQueryHandler<GetUserByIdQuery, GetUserByIdResponse>
	{
		public async ValueTask<GetUserByIdResponse> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
		{
			var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
				UserCache.Key(query.Id),
				async token => await db.Query<User>()
					.Where(u => u.Id == query.Id)
					.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
					.FirstOrDefaultAsync(token),
				cancellationToken: cancellationToken);

			return user ?? throw UserErrors.NotFound(query.Id);
		}
	}
#elif (cqrs)
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
#elif (repository && result && hybrid_cache)
	public async Task<IResult> Handler(IUserRepository users, HybridCache cache, Guid id, CancellationToken cancellationToken)
	{
		var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
			UserCache.Key(id),
			async token => await users.GetByIdAsync(id, token) is { } found
				? new GetUserByIdResponse(found.Id, found.Name, found.Email, found.CreatedAt)
				: null,
			cancellationToken: cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(user);
	}
#elif (repository && result)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (repository && hybrid_cache)
	public async Task<IResult> Handler(IUserRepository users, HybridCache cache, Guid id, CancellationToken cancellationToken)
	{
		var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
			UserCache.Key(id),
			async token => await users.GetByIdAsync(id, token) is { } found
				? new GetUserByIdResponse(found.Id, found.Name, found.Email, found.CreatedAt)
				: null,
			cancellationToken: cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(user);
	}
#elif (repository)
	public async Task<IResult> Handler(IUserRepository users, Guid id, CancellationToken cancellationToken)
	{
		var user = await users.GetByIdAsync(id, cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
			: Results.Ok(new GetUserByIdResponse(user.Id, user.Name, user.Email, user.CreatedAt));
	}
#elif (result && hybrid_cache)
	public async Task<IResult> Handler(AppDbContext context, HybridCache cache, Guid id, CancellationToken cancellationToken)
	{
		var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
			UserCache.Key(id),
			async token => await context.Users
				.AsNoTracking()
				.Where(u => u.Id == id)
				.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
				.FirstOrDefaultAsync(token),
			cancellationToken: cancellationToken);

		return user is null
			? UserErrors.NotFound(id).ToProblem()
			: Results.Ok(user);
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
#elif (hybrid_cache)
	public async Task<IResult> Handler(AppDbContext context, HybridCache cache, Guid id, CancellationToken cancellationToken)
	{
		var user = await cache.GetOrCreateAsync<GetUserByIdResponse?>(
			UserCache.Key(id),
			async token => await context.Users
				.AsNoTracking()
				.Where(u => u.Id == id)
				.Select(u => new GetUserByIdResponse(u.Id, u.Name, u.Email, u.CreatedAt))
				.FirstOrDefaultAsync(token),
			cancellationToken: cancellationToken);

		return user is null
			? throw UserErrors.NotFound(id)
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
