using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Sinergia.App_Helpers
{
    public class DashRouteHandler : MvcRouteHandler
    {
        /// <summary>
        /// Gestore personalizzato delle route che rimuove i trattini dai nomi controller/action.
        /// </summary>
        protected override IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var routeValues = requestContext.RouteData.Values;

            routeValues["action"] = routeValues["action"].UnDash();
            routeValues["controller"] = routeValues["controller"].UnDash();

            return base.GetHttpHandler(requestContext);
        }
    }
}
