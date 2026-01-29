using AutoMapper;
using Paperless.Application.DTOs;
using Paperless.Contracts.Messages;
using Paperless.Domain.Entities;
using Paperless.Domain.ValueObjects;

namespace Paperless.Application.Mapper
{
    public class DocumentProfile : Profile
    {
        public DocumentProfile()
        {
            CreateMap<Document, DocumentDTO>();

            CreateMap<UpdateDocumentDTO, Document>()
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.UploadedAt, opt => opt.Ignore())
                .ForMember(d => d.ContentType, opt => opt.Ignore())
                .ForMember(d => d.SizeBytes, opt => opt.Ignore())
                .ForMember(d => d.Tags, opt => opt.Ignore())
                //.ForMember(d => d.Authors, opt => opt.Ignore())
                .AfterMap((src, dest) =>
                {
                    if(src.Tags != null) dest.SetTags(src.Tags);
                    //if(src.Authors != null) dest.SetAuthors(src.Authors);
                })
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<UpdateDocumentDTO, DocumentUpdate>()
                //.ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null))
                  .ForMember(dest => dest.Tags, opt =>
                  {
                      opt.PreCondition(src => src.Tags != null);
                      opt.MapFrom(src => src.Tags);
                  });

            // to-do!
            CreateMap<Document, OcrJobMessage>()
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags.Select(t => t.Name)));
        }
    }
}
