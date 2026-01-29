using Paperless.Domain.Common;

namespace Paperless.Domain.Events
{
    public record DocumentDeletedEvent(Guid Id) : IDomainEvent;
}