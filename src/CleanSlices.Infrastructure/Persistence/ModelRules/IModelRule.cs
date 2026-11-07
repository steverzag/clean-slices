#if (model_rules)
using Microsoft.EntityFrameworkCore;

namespace CleanSlices.Infrastructure.Persistence.ModelRules;

// A rule that applies to the whole model, after the type configurations: options add rules instead of editing them.
internal interface IModelRule
{
	public void Apply(ModelBuilder modelBuilder);
}
#endif
