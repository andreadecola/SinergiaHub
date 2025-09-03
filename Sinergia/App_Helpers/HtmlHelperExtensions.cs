using System;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;

namespace Sinergia.App_Helpers
{
    public static class HtmlHelperExtensions
    {
        private static string _displayVersion;

        /// <summary>
        /// Restituisce una stringa HTML non codificata contenente la versione dell'assembly in formato:
        /// es. "1.0.0 (build 1234)"
        /// </summary>
        public static IHtmlString AssemblyVersion(this HtmlHelper helper)
        {
            if (string.IsNullOrWhiteSpace(_displayVersion))
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                _displayVersion = string.Format("{0}.{1}.{2} (build {3})",
                    version.Major, version.Minor, version.Build, version.Revision);
            }

            return helper.Raw(_displayVersion);
        }

        /// <summary>
        /// Mostra "active" o qualsiasi altro attributo passato, se il controller o l'action corrente corrisponde al valore dato.
        /// Utile per assegnare classi CSS attive nei menu.
        /// </summary>
        public static IHtmlString RouteIf(this HtmlHelper helper, string value, string attribute)
        {
            var currentController = (helper.ViewContext.RouteData.Values["controller"] ?? "").ToString();
            var currentAction = (helper.ViewContext.RouteData.Values["action"] ?? "").ToString();

            bool match = value.Equals(currentController, StringComparison.InvariantCultureIgnoreCase)
                      || value.Equals(currentAction, StringComparison.InvariantCultureIgnoreCase);

            return match ? new HtmlString(attribute) : new HtmlString(string.Empty);
        }

        /// <summary>
        /// Renderizza una partial view se la condizione specificata è vera.
        /// Utile per componenti opzionali nella UI.
        /// </summary>
        public static void RenderPartialIf(this HtmlHelper htmlHelper, string partialViewName, bool condition)
        {
            if (condition)
                htmlHelper.RenderPartial(partialViewName);
        }

        /// <summary>
        /// Mostra errori di validazione in stile Bootstrap (alert rosso).
        /// </summary>
        public static HtmlString ValidationBootstrap(this HtmlHelper htmlHelper, string alertType = "danger", string heading = "")
        {
            if (htmlHelper.ViewData.ModelState.IsValid)
                return new HtmlString(string.Empty);

            var sb = new StringBuilder();
            sb.AppendFormat("<div class=\"alert alert-{0} alert-block\">", alertType);
            sb.Append("<button class=\"close\" data-dismiss=\"alert\" aria-hidden=\"true\">&times;</button>");

            if (!string.IsNullOrWhiteSpace(heading))
                sb.AppendFormat("<h4 class=\"alert-heading\">{0}</h4>", heading);

            sb.Append(htmlHelper.ValidationSummary());
            sb.Append("</div>");

            return new HtmlString(sb.ToString());
        }

        /// <summary>
        /// Mostra la versione dell'app e il copyright Sinergia.
        /// Esempio: "1.0.0 (build 1234) © 2025 Sinergia"
        /// </summary>
        public static IHtmlString Copyright(this HtmlHelper helper)
        {
            var version = helper.AssemblyVersion().ToHtmlString();
            var year = DateTime.Now.Year;

            return helper.Raw($"{version} &copy; {year} Sinergia");
        }
    }
}
