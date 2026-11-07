#if (model_rules)
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.ModelRules;

internal static class ModelRuleExtensions
{
	extension(ModelBuilder modelBuilder)
	{
		public void ApplyModelRules()
		{
			var rules = typeof(ModelRuleExtensions).Assembly.GetTypes()
				.Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IModelRule).IsAssignableFrom(t))
				.OrderBy(t => t.FullName, StringComparer.Ordinal);

			foreach (var rule in rules)
			{
				((IModelRule)Activator.CreateInstance(rule)!).Apply(modelBuilder);
			}
		}
	}
}
#endif
