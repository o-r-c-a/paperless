using FluentValidation;
using Paperless.Domain.Entities;
using Paperless.Shared.Utils;

namespace Paperless.Rest.Validators
{
    public class TagValidator : AbstractValidator<Tag>
    {
        public TagValidator()
        {
            RuleFor(t => t.Name)
                .NotEmpty()
                    .WithMessage("Tag name must not be empty.")
                .MinimumLength(ValidationRules.TagMinLength)
                    .WithMessage(ValidationRules.TagTooShortError)
                .MaximumLength(ValidationRules.TagMaxLength)
                    .WithMessage(ValidationRules.TagTooLongError);
        }
    }
}
