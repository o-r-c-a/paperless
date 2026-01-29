using FluentValidation;
using Paperless.Rest.Models;
using Paperless.Shared.Utils;

namespace Paperless.Rest.Validators
{
    public class UpdateDocumentRequestValidator : AbstractValidator<UpdateDocumentRequest>
    {
        public UpdateDocumentRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                    .WithMessage("Name must not be empty.")
                .MinimumLength(ValidationRules.NameMinLength)
                    .WithMessage(ValidationRules.NameTooShortError)
                .MaximumLength(ValidationRules.NameMaxLength)
                    .WithMessage(ValidationRules.NameTooLongError)
                .When(x => x.Name != null);
            RuleFor(x => x.Title)
                .NotEmpty()
                    .WithMessage("Title must not be empty.")
                .MinimumLength(ValidationRules.TitleMinLength)
                    .WithMessage(ValidationRules.TitleTooShortError)
                .MaximumLength(ValidationRules.TitleMaxLength)
                    .WithMessage(ValidationRules.TitleTooLongError)
                .When(x => x.Title != null);
            RuleForEach(x => x.Tags)
                .ChildRules(v =>
                {
                    v.RuleFor(t => t)
                        .NotEmpty()
                        .MinimumLength(ValidationRules.TagMinLength)
                            .WithMessage(ValidationRules.TagTooShortError)
                        .MaximumLength(ValidationRules.TagMaxLength)
                            .WithMessage(ValidationRules.TagTooLongError);
                })
                .When(x => x.Tags != null && x.Tags.Any());
            //RuleForEach(x => x.Authors)
        }
    }
}
