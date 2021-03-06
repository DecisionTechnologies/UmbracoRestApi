using System;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;
using System.Web.Security;
using Examine.Providers;
using Moq;
using Semver;
using Umbraco.Core;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Profiling;
using Umbraco.Core.Security;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;
using Umbraco.Web.WebApi;

namespace Umbraco.RestApi.Tests.TestHelpers
{
    public abstract class TestControllerActivatorBase : DefaultHttpControllerActivator, IHttpControllerActivator
    {
        IHttpController IHttpControllerActivator.Create(HttpRequestMessage request, HttpControllerDescriptor controllerDescriptor, Type controllerType)
        {
            if (typeof(UmbracoApiControllerBase).IsAssignableFrom(controllerType))
            {
                var owinContext = request.GetOwinContext();

                //Create mocked services that we are going to pass to the callback for unit tests to modify
                // before passing these services to the main container objects
                var mockedTypedContent = Mock.Of<ITypedPublishedContentQuery>();
                var mockedContentService = Mock.Of<IContentService>();
                var mockedContentTypeService = Mock.Of<IContentTypeService>();
                var mockedMemberTypeService = Mock.Of<IMemberTypeService>();
                var mockedMediaService = Mock.Of<IMediaService>();
                var mockedMemberService = Mock.Of<IMemberService>();
                var mockedTextService = Mock.Of<ILocalizedTextService>();
                var mockedDataTypeService = Mock.Of<IDataTypeService>();
                var mockedRelationService = Mock.Of<IRelationService>();
                var mockedMigrationService = new Mock<IMigrationEntryService>();
                //set it up to return anything so that the app ctx is 'Configured'
                mockedMigrationService.Setup(x => x.FindEntry(It.IsAny<string>(), It.IsAny<SemVersion>())).Returns(Mock.Of<IMigrationEntry>());

                var serviceContext = new ServiceContext(
                    dataTypeService:mockedDataTypeService,
                    contentTypeService:mockedContentTypeService,
                    contentService: mockedContentService, 
                    mediaService: mockedMediaService, 
                    memberService: mockedMemberService, 
                    localizedTextService: mockedTextService,
                    memberTypeService:mockedMemberTypeService,
                    relationService:mockedRelationService,
                    migrationEntryService:mockedMigrationService.Object);

                //new app context
                var dbCtx = new Mock<DatabaseContext>(Mock.Of<IDatabaseFactory>(), Mock.Of<ILogger>(), Mock.Of<ISqlSyntaxProvider>(), "test");
                //ensure these are set so that the appctx is 'Configured'
                dbCtx.Setup(x => x.CanConnect).Returns(true);
                dbCtx.Setup(x => x.IsDatabaseConfigured).Returns(true);
                var appCtx = ApplicationContext.EnsureContext(
                    dbCtx.Object,
                    //pass in mocked services
                    serviceContext,
                    CacheHelper.CreateDisabledCacheHelper(),
                    new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()),
                    true);

                //httpcontext with an auth'd user
                var httpContext = Mock.Of<HttpContextBase>(http => http.User == owinContext.Authentication.User);
                //chuck it into the props since this is what MS does when hosted
                request.Properties["MS_HttpContext"] = httpContext;

                var backofficeIdentity = (UmbracoBackOfficeIdentity)owinContext.Authentication.User.Identity;

                var webSecurity = new Mock<WebSecurity>(null, null);

                //mock CurrentUser
                webSecurity.Setup(x => x.CurrentUser)
                    .Returns(Mock.Of<IUser>(u => u.IsApproved == true
                                                 && u.IsLockedOut == false
                                                 && u.AllowedSections == backofficeIdentity.AllowedApplications
                                                 && u.Email == "admin@admin.com"
                                                 && u.Id == (int)backofficeIdentity.Id
                                                 && u.Language == "en"
                                                 && u.Name == backofficeIdentity.RealName
                                                 && u.StartContentId == backofficeIdentity.StartContentNode
                                                 && u.StartMediaId == backofficeIdentity.StartMediaNode
                                                 && u.Username == backofficeIdentity.Username));

                //mock Validate
                webSecurity.Setup(x => x.ValidateCurrentUser())
                    .Returns(() => true);

                var umbCtx = UmbracoContext.EnsureContext(
                    //set the user of the HttpContext
                    httpContext,
                    appCtx,
                    webSecurity.Object,
                    Mock.Of<IUmbracoSettingsSection>(section => section.WebRouting == Mock.Of<IWebRoutingSection>(routingSection => routingSection.UrlProviderMode == UrlProviderMode.Auto.ToString())),
                    Enumerable.Empty<IUrlProvider>(),
                    true); //replace it

                var urlHelper = new Mock<IUrlProvider>();
                urlHelper.Setup(provider => provider.GetUrl(It.IsAny<UmbracoContext>(), It.IsAny<int>(), It.IsAny<Uri>(), It.IsAny<UrlProviderMode>()))
                    .Returns("/hello/world/1234");

                var membershipHelper = new MembershipHelper(umbCtx, Mock.Of<MembershipProvider>(), Mock.Of<RoleProvider>());

                var umbHelper = new UmbracoHelper(umbCtx,
                    Mock.Of<IPublishedContent>(),
                    mockedTypedContent,
                    Mock.Of<IDynamicPublishedContentQuery>(),
                    Mock.Of<ITagQuery>(),
                    Mock.Of<IDataTypeService>(),
                    new UrlProvider(umbCtx, new[]
                    {
                        urlHelper.Object
                    }, UrlProviderMode.Auto),
                    Mock.Of<ICultureDictionary>(),
                    Mock.Of<IUmbracoComponentRenderer>(),
                    membershipHelper);

                var searchProvider = Mock.Of<BaseSearchProvider>();

                return CreateController(controllerType, request, umbHelper, mockedTypedContent, serviceContext, searchProvider);
            }
            //default
            return base.Create(request, controllerDescriptor, controllerType);
        }

        protected abstract ApiController CreateController(Type controllerType, HttpRequestMessage msg, UmbracoHelper helper, ITypedPublishedContentQuery qry, ServiceContext serviceContext, BaseSearchProvider searchProvider);
    }
}