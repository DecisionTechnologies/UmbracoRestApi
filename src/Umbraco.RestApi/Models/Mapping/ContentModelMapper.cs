using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Examine;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Mapping;

namespace Umbraco.RestApi.Models
{
    public class ContentModelMapper : MapperConfiguration
    {
        public override void ConfigureMappings(IConfiguration config, ApplicationContext applicationContext)
        {
            config.CreateMap<IContent, ContentRepresentation>()
                .ForMember(representation => representation.HasChildren, expression => expression.MapFrom(content =>
                    applicationContext.Services.ContentService.HasChildren(content.Id)))
                .ForMember(representation => representation.Properties, expression => expression.ResolveUsing<ContentPropertiesResolver>());

            config.CreateMap<IMedia, MediaRepresentation>()
                .ForMember(representation => representation.HasChildren, expression => expression.MapFrom(content =>
                    applicationContext.Services.MediaService.HasChildren(content.Id)))
                .ForMember(representation => representation.Properties, expression => expression.ResolveUsing<ContentPropertiesResolver>());

            config.CreateMap<IMember, MemberRepresentation>()
                .ForMember(representation => representation.Properties, expression => expression.ResolveUsing<ContentPropertiesResolver>());

            config.CreateMap<IContent, ContentTemplate>()
                .IgnoreAllUnmapped()
                .ForMember(representation => representation.Properties, expression => expression.ResolveUsing(content =>
                {
                    return content.PropertyTypes.ToDictionary<PropertyType, string, object>(propertyType => propertyType.Alias, propertyType => "");
                }));

            config.CreateMap<IContent, IDictionary<string, ContentPropertyInfo>>()
                .ConstructUsing(content =>
                {
                    var result = new Dictionary<string, ContentPropertyInfo>();
                    foreach (var propertyType in content.PropertyTypes)
                    {
                        result[propertyType.Alias] = new ContentPropertyInfo
                        {
                            Label = propertyType.Name,
                            ValidationRegexExp = propertyType.ValidationRegExp,
                            ValidationRequired = propertyType.Mandatory
                        };
                    }
                    return result;
                });

            config.CreateMap<ContentRepresentation, IContent>()
                .IgnoreAllUnmapped()
                .ForMember(content => content.Name, expression => expression.MapFrom(representation => representation.Name))
                //TODO: This could be used to 'Move' an item but we'd have to deal with that, not sure we should deal with that in a mapping
                //.ForMember(content => content.ParentId, expression => expression.MapFrom(representation => representation.ParentId))
                //TODO: This could be used to 'Sort' an item but we'd have to deal with that, not sure we should deal with that in a mapping
                //.ForMember(content => content.SortOrder, expression => expression.MapFrom(representation => representation.SortOrder))
                .AfterMap((representation, content) =>
                {
                    //TODO: Map template;
                    
                    foreach (var propertyRepresentation in representation.Properties)
                    {
                        var found = content.HasProperty(propertyRepresentation.Key) ? content.Properties[propertyRepresentation.Key] : null;
                        if (found != null)
                        {
                            found.Value = propertyRepresentation.Value;
                        }
                    }
                });

            config.CreateMap<IPublishedContent, ContentRepresentation>()
                .ForMember(representation => representation.HasChildren, expression => expression.MapFrom(content => content.Children.Any()))
                .ForMember(representation => representation.Properties, expression => expression.ResolveUsing(content =>
                {
                    return content.Properties.ToDictionary(property => property.PropertyTypeAlias,
                        property => property.GetSerializableValue());
                }));

            //config.CreateMap<SearchResult, ContentRepresentation>()
            //    //TODO: Lookup children
            //    .ForMember(content => content.HasChildren, expression => expression.Ignore())
            //    .ForMember(content => content.ContentType, expression => expression.Ignore())
        }
    }

    public static class PublishedPropertyExtensions
    {
        public static object GetSerializableValue(this IPublishedProperty property)
        {
            return property.HasValue && property.Value.GetType().IsSerializable ? property.Value : property.Value?.ToString();
        }
    }
}