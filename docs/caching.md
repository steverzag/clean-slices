# Caching

Endpoints cache through **`HybridCache`** (`Microsoft.Extensions.Caching.Hybrid`): a copy in the memory of each instance, and a second level behind it when one is configured. One call reads the cached value or creates it, and only one request creates it when many ask at once.

Nothing is cached by default. Caching is a decision per endpoint: add it where reads are frequent and a slightly old answer is acceptable.

<!--#if (fusioncache_redis)-->
**In this solution:** [FusionCache](https://github.com/ZiggyCreatures/FusionCache) runs `HybridCache`, with Redis as its second level and as its **backplane**: when one instance changes or removes a value, the others drop their copy in memory.
<!--#elseif (fusioncache)-->
**In this solution:** [FusionCache](https://github.com/ZiggyCreatures/FusionCache) runs `HybridCache`, in memory. With the Redis option it also gets Redis as its second level and backplane.
<!--#else-->
**In this solution:** Microsoft's `HybridCache`, with Redis as its second level (`IDistributedCache`), shared by every instance.
<!--#endif-->

## Caching a read

```csharp
public async Task<IResult> Handler(AppDbContext context, HybridCache cache, Guid id, CancellationToken cancellationToken)
{
	var product = await cache.GetOrCreateAsync<GetProductByIdResponse?>(
		$"product:{id}",
		async token => await context.Products
			.AsNoTracking()
			.Where(p => p.Id == id)
			.Select(p => new GetProductByIdResponse(p.Id, p.Name, p.CreatedAt))
			.FirstOrDefaultAsync(token),
		cancellationToken: cancellationToken);

	return product is null ? throw ProductErrors.NotFound(id) : Results.Ok(product);
}
```

- **Cache responses, not entities**: the cached value is serialized, and an entity carries change tracking and navigation properties.
- **Keys name what they hold**: `product:{id}`. Keys for data that depends on the user must contain the user ID.
- Entries expire after the default expiration unless you pass `HybridCacheEntryOptions` with another one.
- **Tags** remove groups of entries at once: cache a list with `tags: ["products"]`, then `RemoveByTagAsync("products")` when any product changes.

## Removing a value when the data changes

Remove cached copies from a **domain event handler**, and do it **after the commit** with `IAfterCommit`:

```csharp
internal sealed class ForgetCachedProductHandler(HybridCache cache, IAfterCommit afterCommit)
	: INotificationHandler<ProductUpdatedDomainEvent>
{
	public Task Handle(ProductUpdatedDomainEvent notification, CancellationToken cancellationToken)
	{
		afterCommit.Enqueue(ct => cache.RemoveAsync($"product:{notification.Id}", ct).AsTask());
		return Task.CompletedTask;
	}
}
```

Domain event handlers run before the commit. Removing the value there would leave a gap: a request reading before the commit could cache the old values again. `IAfterCommit` (`Infrastructure/Persistence/AfterCommit/`) queues the work during the save, runs it once the data is committed, and drops it if the save fails. A failing action is logged; the data is already saved. "After the commit" assumes each `SaveChangesAsync` is its own transaction, as everywhere in this solution: inside an explicit transaction you open yourself, the actions would run before your commit.

The entity raises the event in each method that changes it. In the sample features, `User` raises `UserUpdatedDomainEvent` and `ForgetCachedUserHandler` removes the cached user, read by `GetUserById`.

<!--#if (redis)-->
## Redis

- The AppHost runs Redis in a container (`AddRedis("cache")`); the API gets its connection string as `ConnectionStrings:cache`. Point it at an existing server by setting that connection string.
<!--#if (idempotency)-->
- `IDistributedCache` is Redis, so everything that uses it shares it across instances: idempotency keys too, and `HybridCache`'s second level.
<!--#else-->
- `IDistributedCache` is Redis, so everything that uses it shares it across instances, `HybridCache`'s second level included.
<!--#endif-->
- **License**: Redis 7.4 and later are source-available (RSALv2 / SSPLv1), and Redis 8 adds AGPLv3. The container is fine for development; check the terms before using it in production. [Valkey](https://valkey.io) and [Garnet](https://github.com/microsoft/garnet) speak the same protocol and are open source: only the AppHost line and its hosting package change (`AddValkey("cache")`, `AddGarnet("cache")`).
<!--#endif-->

## Several instances

<!--#if (fusioncache_redis)-->
FusionCache's backplane keeps the instances in step: removing or changing a value on one instance tells the others, through Redis, to drop their copy in memory. A test checks it with two instances (`Caching/HybridCacheTests.cs`).
<!--#elseif (redis)-->
Microsoft's `HybridCache` has **no backplane**: removing a value on one instance removes it from Redis and from that instance's memory, but the other instances keep their copy in memory until it expires (5 minutes by default). A test shows it with two instances (`Caching/HybridCacheTests.cs`). If that matters, shorten `LocalCacheExpiration`, or add the FusionCache option, which brings a backplane with Redis.
<!--#else-->
Each instance has its own memory: removing a value on one instance doesn't remove it from the others. With several instances, add the Redis option, which gives FusionCache a shared second level and a backplane.
<!--#endif-->
<!--#if (fusioncache)-->

## FusionCache

FusionCache (MIT) is registered as `HybridCache` (`AsHybridCache()`), so code only uses `HybridCache`. Its own interface, `IFusionCache`, adds what `HybridCache` doesn't have: **fail-safe** (keep serving the last good value when refreshing it fails), **timeouts** for slow factories, and **eager refresh** before a value expires. See its documentation; they're set per call or in `AddFusionCache()` in `InfrastructureRegistration.cs`. It's maintained mainly by one author and widely used.
<!--#endif-->

## Trade-offs

<!--#if (fusioncache_redis)-->
- A cached answer can be old: by up to its expiration if a change is missed, for example data changed outside the application, or a backplane message lost while an instance was disconnected.
<!--#else-->
- A cached answer can be old: by up to its expiration if a change is missed, for example data changed outside the application, or a removal on another instance.
<!--#endif-->
- Removing after the commit narrows the gap, but a read that started before the commit and finishes after the removal can still store the old values. Short expirations bound how long that lasts.
- "Not found" is cached too (a `null` value): fine for IDs that never come back, worth a shorter expiration for lookups by name.
