using Sinergia.ActionFilters;
using System.Web;
using System.Web.Mvc;

namespace Sinergia
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Applica filtro solo se non è il controller Account
            filters.Add(new PermissionsActionFilter(), 0); // ma lo gestiamo in PermissionsActionFilter direttamente
        }
    }
}
