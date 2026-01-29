using AutoMapper;
using Paperless.Application.Commands;
using Paperless.Application.DTOs;
using Paperless.Domain.Entities;
using Paperless.Rest.Models;
using System.Collections;

namespace Paperless.Rest.Mapper
{
    public class RequestDocumentProfile : Profile
    {
        public RequestDocumentProfile()
        {
            CreateMap<string, Tag>()
                .ConvertUsing(src => new Tag { Name = src.ToLower().Trim() });

            CreateMap<Tag, string>()
                .ConvertUsing(src => src.Name);

            CreateMap<IEnumerable<string>?, IEnumerable<Tag>?>()
                .ConvertUsing(src =>
                src == null
                ? null
                : (!src.ToList().Any()
                    ? Enumerable.Empty<Tag>()
                    : src
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => new Tag { Name = s.ToLower().Trim() })
                        .GroupBy(t => t.Name)
                        .Select(g => g.First())
                        .ToList()));

            CreateMap<DocumentDTO, DocumentResponse>();

            CreateMap<CreateDocumentRequest, UnmappedDocumentDTO>()
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.File.ContentType ?? "application/octet-stream"))
                .ForMember(dest => dest.SizeBytes, opt => opt.MapFrom(src => src.File.Length))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags));

            CreateMap<UpdateDocumentRequest, UpdateDocumentDTO>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<CreateDocumentRequest, UploadDocumentCommand>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.File, opt => opt.MapFrom(src => src.File))
                .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Title))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags));

        }
    }
}
