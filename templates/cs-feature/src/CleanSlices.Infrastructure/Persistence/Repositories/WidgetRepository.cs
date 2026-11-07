using CleanSlices.Core.Domain.Widgets;

namespace CleanSlices.Infrastructure.Persistence.Repositories;

internal sealed class WidgetRepository(AppDbContext context) : Repository<Widget>(context), IWidgetRepository;
