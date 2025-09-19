using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Sinergia
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error()
        {
            Exception exception = Server.GetLastError();
            Server.ClearError();

            // 🔎 Log immediato su file o output debug
            System.Diagnostics.Debug.WriteLine("🚨 Application_Error");
            System.Diagnostics.Debug.WriteLine("Messaggio: " + exception?.Message);
            System.Diagnostics.Debug.WriteLine("StackTrace: " + exception?.StackTrace);

            // Salva l’eccezione in HttpContext per usarla nella View Error
            HttpContext.Current.Items["LastException"] = exception;

            var httpContext = new HttpContextWrapper(System.Web.HttpContext.Current);
            var routeData = new RouteData();
            routeData.Values["controller"] = "Home";
            routeData.Values["action"] = "Error";

            var originalRoute = RouteTable.Routes.GetRouteData(httpContext);
            routeData.Values["originalController"] = originalRoute?.Values["controller"];
            routeData.Values["originalAction"] = originalRoute?.Values["action"];

            // Passo l'eccezione come parametro (così la View la può mostrare)
            routeData.Values["exception"] = exception;

            IController controller = new Sinergia.Controllers.HomeController();
            controller.Execute(new RequestContext(httpContext, routeData));
        }



    }
}
