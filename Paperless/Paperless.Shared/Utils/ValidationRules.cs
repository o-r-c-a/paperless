using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Shared.Utils
{
    public static class ValidationRules
    {
        public static readonly string[] AllowedFileExtensions = new string[]
        {
            // documents
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".rtf", ".odt",
            // images
            ".png", ".jpg", ".jpeg", ".gif", ".tif", ".tiff", ".bmp"
        };
        // Allowed MIME types (whitelist)
        public static readonly string[] AllowedContentTypes = new string[]
        {
            // documents
            "application/pdf",
            "text/plain",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/rtf",
            "application/vnd.oasis.opendocument.text",
            // images
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/tiff",
            "image/bmp"
        };
        #region Bounds
        public static readonly int NameMinLength = 5;
        public static readonly int NameMaxLength = 127;

        public static readonly long SizeBytesMaxLength = 50 * 1024 * 1024; // 50 MB

        public static readonly int TagMinLength = 2;
        public static readonly int TagMaxLength = 30;

        public static readonly int TitleMinLength = 5;
        public static readonly int TitleMaxLength = 100;

        public static readonly int AuthorMinLength = 2;
        public static readonly int AuthorMaxLength = 50;
        #endregion
        #region Error Messages
        public static readonly string NameTooShortError = $"Name minimum length is {NameMinLength} characters.";
        public static readonly string NameTooLongError = $"Name maximum length is {NameMaxLength} characters.";
        
        public static readonly string SizeBytesTooLargeError = $"File size must be less than {SizeBytesMaxLength} bytes.";

        public static readonly string TagTooShortError = $"Each tag minimum length is {TagMinLength} characters.";
        public static readonly string TagTooLongError = $"Each tag maximum length is {TagMaxLength} characters.";

        public static readonly string TitleTooShortError = $"Title minimum length is {TitleMinLength} characters.";
        public static readonly string TitleTooLongError = $"Title maximum length is {TitleMaxLength} characters.";

        public static readonly string AuthorTooShortError = $"Author minimum length is {AuthorMinLength} characters.";
        public static readonly string AuthorTooLongError = $"Author maximum length is {AuthorMaxLength} characters.";
        #endregion
    }
}
