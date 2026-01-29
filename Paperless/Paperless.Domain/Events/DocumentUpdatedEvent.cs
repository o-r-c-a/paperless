using Paperless.Domain.Common;
using Paperless.Domain.Entities;

namespace Paperless.Domain.Events
{
    public record DocumentUpdatedEvent(Document Document) : IDomainEvent;
}