using Sinergia.ActionFilters;
using System.Web.Mvc;

namespace Sinergia
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // ❌ Tolto perché nasconde l’eccezione
            // filters.Add(new HandleErrorAttribute());

            // ✅ Mantieni solo il filtro dei permessi
            filters.Add(new PermissionsActionFilter(), 0);
        }
    }
}
