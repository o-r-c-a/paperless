using Paperless.Domain.Common;
using Paperless.Domain.Entities;

namespace Paperless.Domain.Events
{
    public record DocumentUploadedEvent(Document Document) : IDomainEvent;
}
