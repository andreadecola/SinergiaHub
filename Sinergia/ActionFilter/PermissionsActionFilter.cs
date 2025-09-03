using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sinergia.Model;
using Sinergia.Models;

namespace Sinergia.ActionFilters
{
    public class PermissionsActionFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.RouteData.Values["controller"].ToString().ToLower();
            var action = filterContext.RouteData.Values["action"].ToString().ToLower();

            // ✅ Escludi Login/Logout
            if (controller == "account" && (action== "index" || action == "logout" || action == "recuperapassword" || action == "visualizzapasswordtemporanea" || action == "avviarecuperopassword" || action == "creapasswordtemporanea" || action == "impostapassword"
                 ))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            // ✅ Controlla autenticazione
            var utente = filterContext.HttpContext.Session["User"] as Utenti;
            if (utente == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Index");
                return;
            }

            using (var ctx = new SinergiaDB())
            {
                try
                {
                    // ✅ Costruzione menu
                    var permessiUtente = ctx.Permessi
                        .Where(p => p.ID_Utente == utente.ID_Utente && p.Abilitato == "SI")
                        .ToList();

                    var menuLinks = (from p in permessiUtente
                                     join m in ctx.Menu on p.ID_Menu equals m.ID_Menu
                                     where m.MostraNelMenu == "SI" && m.ÈValido == "SI"
                                     orderby m.Ordine
                                     select new MenuViewModel
                                     {
                                         CategoriaMenu = m.CategoriaMenu,
                                         CategoriaMenu2 = m.CategoriaMenu2,
                                         Icona = m.Icona,
                                         Controller = m.Controller,
                                         Azione = m.Azione,
                                         NomeMenu = m.NomeMenu,
                                         VoceSingola = m.VoceSingola
                                     }).Distinct().ToList();

                    // ✅ Gestione azienda selezionata
                    string aziendaSelezionata = "";
                    var cookie = filterContext.HttpContext.Request.Cookies["SinergiaAzienda"];

                    if (cookie == null || string.IsNullOrEmpty(cookie.Value))
                    {
                        var azienda = ctx.Clienti.FirstOrDefault(c => c.Stato == "Attivo" && c.TipoCliente == "Azienda");
                        if (azienda != null)
                        {
                            aziendaSelezionata = azienda.Nome;
                            var nuovaCookie = new HttpCookie("SinergiaAzienda", aziendaSelezionata)
                            {
                                Expires = DateTime.Now.AddDays(7)
                            };
                            filterContext.HttpContext.Response.Cookies.Add(nuovaCookie);
                        }
                        else
                        {
                            // Nessuna azienda, ma NON blocchiamo l'accesso: lasciamo azienda vuota
                            aziendaSelezionata = "";
                        }
                    }
                    else
                    {
                        aziendaSelezionata = cookie.Value;
                    }

                    // ✅ Salvataggio menu e azienda in TempData
                    filterContext.Controller.TempData["menuLinks"] = menuLinks;
                    filterContext.Controller.TempData["aziendaSelezionata"] = aziendaSelezionata;
                }
                catch
                {
                    filterContext.Result = new HttpUnauthorizedResult("Errore nella gestione permessi.");
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
