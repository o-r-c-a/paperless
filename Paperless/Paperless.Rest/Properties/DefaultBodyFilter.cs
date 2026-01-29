using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Paperless.Application.DTOs;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Paperless.Rest.Properties
{
    /// <summary>
    /// Defines default example values for request bodies in Swagger documentation.
    /// </summary>
    public class DefaultBodyFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type == typeof(UnmappedDocumentDTO))
            {
                schema.Example = new OpenApiObject
                {
                    ["name"] = new OpenApiString("defaultValue"),
                    ["contentType"] = new OpenApiString("application/xml"),
                };
            }
            else if(context.Type == typeof(UnmappedDocumentDTO))
            {
                schema.Example = new OpenApiObject
                {
                    ["name"] = new OpenApiString("updatedValue"),
                    ["contentType"] = new OpenApiString("application/pdf"),
                };
            }
        }
    }
}
