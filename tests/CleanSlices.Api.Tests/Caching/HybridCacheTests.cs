#if (hybrid_cache)
using CleanSlices.Api.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
#if (redis)
using Microsoft.Extensions.Caching.Distributed;
#endif
using Microsoft.Extensions.DependencyInjection;

namespace CleanSlices.Api.Tests.Caching;

public class HybridCacheTests(ApiFactory factory)
{
	private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

	[Fact]
	public async Task A_value_is_created_once_then_read_from_the_cache()
	{
		var cache = factory.Services.GetRequiredService<HybridCache>();
		var key = NewKey();
		var created = 0;

		for (var i = 0; i < 3; i++)
		{
			await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++created), cancellationToken: CancellationToken);
		}

		Assert.Equal(1, created);
	}
#if (fusioncache)

	[Fact]
	public void HybridCache_is_FusionCache()
	{
		Assert.Contains("Fusion", factory.Services.GetRequiredService<HybridCache>().GetType().FullName);
	}
#endif
#if (redis)

	[Fact]
	public async Task The_distributed_cache_is_Redis()
	{
		var distributed = factory.Services.GetRequiredService<IDistributedCache>();
		var key = NewKey();

		await distributed.SetStringAsync(key, "stored", CancellationToken);

		Assert.Contains("Redis", distributed.GetType().FullName);
		Assert.Equal("stored", await distributed.GetStringAsync(key, CancellationToken));
	}

#if (fusioncache_redis)
	[Fact]
	public async Task A_value_removed_on_one_instance_is_removed_on_the_others()
	{
		// Two instances of the app: each has its own memory, and they share Redis.
		await using var other = factory.WithWebHostBuilder(_ => { });
		var first = factory.Services.GetRequiredService<HybridCache>();
		var second = other.Services.GetRequiredService<HybridCache>();
		var key = NewKey();

		await first.GetOrCreateAsync(key, _ => ValueTask.FromResult("old"), cancellationToken: CancellationToken);
		Assert.Equal("old", await second.GetOrCreateAsync(key, _ => ValueTask.FromResult("not shared"), cancellationToken: CancellationToken));

		await second.RemoveAsync(key, CancellationToken);

		Assert.Equal("new", await ReadUntilAsync(first, key, "new"));
	}
#else

	[Fact]
	public async Task A_value_removed_on_one_instance_stays_on_the_others_until_it_expires()
	{
		// Microsoft's HybridCache has no backplane: the other instances keep their copy in memory.
		// FusionCache with Redis (its glue unit) adds one; see docs/caching.md.
		await using var other = factory.WithWebHostBuilder(_ => { });
		var first = factory.Services.GetRequiredService<HybridCache>();
		var second = other.Services.GetRequiredService<HybridCache>();
		var key = NewKey();

		await first.GetOrCreateAsync(key, _ => ValueTask.FromResult("old"), cancellationToken: CancellationToken);
		await WaitUntilInRedisAsync(key);
		await second.RemoveAsync(key, CancellationToken);

		Assert.Equal("new", await second.GetOrCreateAsync(key, _ => ValueTask.FromResult("new"), cancellationToken: CancellationToken));
		Assert.Equal("old", await first.GetOrCreateAsync(key, _ => ValueTask.FromResult("new"), cancellationToken: CancellationToken));
	}

	// Microsoft's HybridCache can finish writing Redis after GetOrCreateAsync returns: wait for it,
	// or that late write could put the old value back after the removal.
	private async Task WaitUntilInRedisAsync(string key)
	{
		var redis = factory.Services.GetRequiredService<IDistributedCache>();
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (await redis.GetAsync(key, CancellationToken) is null)
		{
			Assert.True(DateTime.UtcNow < deadline, $"'{key}' never reached Redis.");
			await Task.Delay(50, CancellationToken);
		}
	}
#endif
#if (fusioncache_redis)

	// The other instances hear about the removal asynchronously, so read for a few seconds.
	private static async Task<string> ReadUntilAsync(HybridCache cache, string key, string expected)
	{
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (true)
		{
			var value = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(expected), cancellationToken: CancellationToken);
			if (value == expected || DateTime.UtcNow >= deadline)
			{
				return value;
			}

			await Task.Delay(100, CancellationToken);
		}
	}
#endif
#endif

	private static string NewKey() => $"test:{Guid.NewGuid()}";
}
#endif
