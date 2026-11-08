#if (audit)
namespace CleanSlices.Core.Domain;

// Every change to the entity is recorded in AuditEntries, with snapshots before and after.
public interface IAuditable;

// Left out of the audit snapshots, for personal or bulky data.
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAuditedAttribute : Attribute;
#endif
