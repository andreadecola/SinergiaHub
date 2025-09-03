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

            // Salviamo l'eccezione per la view
            HttpContext.Current.Items["Exception"] = exception;

            // Prepariamo la route per andare alla pagina Error
            var routeData = new RouteData();
            routeData.Values["controller"] = "Home"; // cambia se la tua Error view sta in altro controller
            routeData.Values["action"] = "Error";

            // (opzionale) Passa nome controller e action originali
            routeData.Values["originalController"] = RouteTable.Routes.GetRouteData(new HttpContextWrapper(HttpContext.Current))?.Values["controller"];
            routeData.Values["originalAction"] = RouteTable.Routes.GetRouteData(new HttpContextWrapper(HttpContext.Current))?.Values["action"];

            Server.ClearError();

            // Chiamiamo manualmente l'action Error
            IController controller = new Sinergia.Controllers.HomeController(); // o il tuo controller reale
            controller.Execute(new RequestContext(new HttpContextWrapper(HttpContext.Current), routeData));
        }

    }
}
