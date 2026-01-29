using FluentValidation;
using Paperless.Rest.Models;
using Paperless.Shared.Utils;
using System.IO;
using System.Linq;

namespace Paperless.Rest.Validators
{
    public class CreateDocumentRequestValidator : AbstractValidator<CreateDocumentRequest>
    {
        public CreateDocumentRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                    .WithMessage("Name must not be empty.")
                .MinimumLength(ValidationRules.NameMinLength)
                    .WithMessage(ValidationRules.NameTooShortError)
                .MaximumLength(ValidationRules.NameMaxLength)
                    .WithMessage(ValidationRules.NameTooLongError);
            RuleFor(x => x.File)
                .NotNull()
                    .WithMessage("File must be provided.");
            RuleFor(x => x.File.ContentType)
                .NotEmpty()
                    .WithMessage("ContentType must not be empty.")
                .Must(ct => ValidationRules.AllowedContentTypes
                    .Contains(ct, System.StringComparer.OrdinalIgnoreCase))
                    .WithMessage("Unsupported content type.");
            RuleFor(x => x.File.FileName)
                .NotEmpty()
                    .WithMessage("File name must not be empty.")
                .Must(fn =>
                {
                    var ext = Path.GetExtension(fn);
                    return !string.IsNullOrWhiteSpace(ext) &&
                           ValidationRules.AllowedFileExtensions.Contains(ext, System.StringComparer.OrdinalIgnoreCase);
                })
                .WithMessage($"Unsupported file extension. Allowed: {string.Join(", ", ValidationRules.AllowedFileExtensions)}");
            RuleFor(x => x.File.Length)
                .NotEmpty()
                    .WithMessage("File must not be empty.")
                .LessThan(ValidationRules.SizeBytesMaxLength)
                    .WithMessage(ValidationRules.SizeBytesTooLargeError);
            RuleFor(x => x.Title)
                .NotEmpty()
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
