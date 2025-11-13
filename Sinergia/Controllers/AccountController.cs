using Sinergia.App_Helpers;
using Sinergia.Models;
using Sinergia.Model;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System;
using Sinergia.ActionFilters;

namespace Sinergia.Controllers
{
    [PermissionsActionFilter]
    public class AccountController : Controller
    {
        // GET: Login
        public ActionResult Index()
        {
            // 🔹 Passa un modello vuoto per evitare NullReferenceException
            var model = new LoginViewModel();
            return View("Login", model);
        }


        // POST: Login
        [HttpPost]
        public ActionResult Index(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View("Login", model);

                var user = UserManager.AutenticaUtente(model.Nome, model.Password);

                if (user == null)
                {
                    System.Diagnostics.Trace.WriteLine($"❌ [LOGIN] Credenziali non valide per utente: {model.Nome}");
                    ModelState.AddModelError("", "Credenziali non valide.");
                    return View("Login", model);
                }

                // ======================================================
                // 1️⃣ Salva utente in sessione
                // ======================================================
                Session["User"] = user;
                Session["ID_Utente"] = user.ID_Utente;
                Session["TipoUtente"] = user.TipoUtente;

                // 🔍 DEBUG LOG DETTAGLI LOGIN
                System.Diagnostics.Trace.WriteLine("═══════════════════════════════════════════════");
                System.Diagnostics.Trace.WriteLine($"🔑 [LOGIN SUCCESS] Ore {DateTime.Now:HH:mm:ss}");
                System.Diagnostics.Trace.WriteLine($"👤 Nome utente: {user.Nome} {user.Cognome}");
                System.Diagnostics.Trace.WriteLine($"🏷️ TipoUtente: {user.TipoUtente}");
                System.Diagnostics.Trace.WriteLine($"🆔 ID_Utente: {user.ID_Utente}");
                System.Diagnostics.Trace.WriteLine($"💾 Session[\"User\"]: {(Session["User"] != null ? "OK" : "❌ null")}");
                System.Diagnostics.Trace.WriteLine($"💾 Session[\"ID_Utente\"]: {Session["ID_Utente"]}");
                System.Diagnostics.Trace.WriteLine($"💾 Session[\"TipoUtente\"]: {Session["TipoUtente"]}");
                System.Diagnostics.Trace.WriteLine("═══════════════════════════════════════════════");

                // ======================================================
                // 2️⃣ Autenticazione ASP.NET Forms
                // ======================================================
                FormsAuthentication.SetAuthCookie(user.Nome, false);

                // ======================================================
                // 3️⃣ Se ha una PasswordTemporanea, mostra modale
                // ======================================================
                if (!string.IsNullOrEmpty(user.PasswordTemporanea))
                {
                    System.Diagnostics.Trace.WriteLine($"⚠️ [LOGIN] L’utente {user.Nome} ha una password temporanea attiva.");
                    TempData["PasswordTemporanea"] = true;
                    return View("Login", model);
                }

                // ======================================================
                // 4️⃣ Carica menu dinamico
                // ======================================================
                Session["menuLinks"] = MenuHelper.GetMenuUtente(user.ID_Utente, user.TipoUtente);
                System.Diagnostics.Trace.WriteLine("📋 Menu utente caricato correttamente.");

                // ======================================================
                // 5️⃣ Imposta cookie azienda (se non esiste)
                // ======================================================
                using (var db = new SinergiaDB())
                {
                    var azienda = db.Clienti.FirstOrDefault(c => c.Stato == "Attivo" && c.TipoCliente == "Azienda");
                    if (azienda != null)
                    {
                        var cookie = new HttpCookie("SinergiaAzienda", azienda.Nome);
                        Response.Cookies.Add(cookie);
                        TempData["aziendaSelezionata"] = azienda.Nome;
                        System.Diagnostics.Trace.WriteLine($"🏢 Azienda impostata in cookie: {azienda.Nome}");
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("⚠️ Nessuna azienda attiva trovata nel DB.");
                    }
                }

                // ======================================================
                // ✅ 6️⃣ Pulizia log periodica
                // ======================================================
                Sinergia.App_Helpers.DatabaseMaintenanceHelper.PulisciLogSinergia();
                System.Diagnostics.Trace.WriteLine("🧹 Pulizia log completata (se necessaria).");

                // ======================================================
                // 7️⃣ Redirect alla Dashboard
                // ======================================================
                System.Diagnostics.Trace.WriteLine("🚀 Redirect verso Home/Cruscotto.\n\n");
                return RedirectToAction("Cruscotto", "Home");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ [LOGIN] Errore critico: {ex.Message}");
                throw;
            }
        }



        // Logout
        public ActionResult Logout()
        {
            Session.Clear();
            FormsAuthentication.SignOut();
            // 🔹 Torna sempre all'action che inizializza il model
            return RedirectToAction("Index", "Account");
        }




        [HttpPost]
        public JsonResult ImpostaPassword(string nuovaPassword)
        {
            var user = Session["User"] as Utenti;

            if (user == null || string.IsNullOrEmpty(user.PasswordTemporanea))
            {
                return Json(new { success = false, message = "Sessione utente non valida." });
            }

            if (string.IsNullOrWhiteSpace(nuovaPassword) || nuovaPassword.Length < 6)
            {
                return Json(new { success = false, message = "La password deve contenere almeno 6 caratteri." });
            }

            using (var db = new SinergiaDB())
            {
                var dbUser = db.Utenti.FirstOrDefault(u => u.ID_Utente == user.ID_Utente);
                if (dbUser == null)
                {
                    Session.Clear();
                    return Json(new { success = false, message = "Utente non trovato." });
                }

                string nuovoSalt = Guid.NewGuid().ToString("N");
                dbUser.Salt = nuovoSalt;
                dbUser.PasswordHash = UserManager.CriptPassword(nuovaPassword, nuovoSalt);
                dbUser.PasswordTemporanea = null; // ✅ Reset password temporanea

                db.SaveChanges();

                // Aggiorna la sessione
                Session["User"] = dbUser;
                Session["menuLinks"] = MenuHelper.GetMenuUtente(dbUser.ID_Utente, dbUser.TipoUtente);

                return Json(new { success = true, redirectUrl = Url.Action("Cruscotto", "Home") });
            }
        }

        // gestione password temporanea 

        [HttpGet]
        public ActionResult VisualizzaPasswordTemporanea(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                return Json(new { success = false, message = "Inserisci il nome utente." }, JsonRequestBehavior.AllowGet);
            }

            var nomeCognome = nome.Split('.');

            if (nomeCognome.Length < 2)
            {
                return Json(new { success = false, message = "Formato nome utente non valido." }, JsonRequestBehavior.AllowGet);
            }

            string nomeUtente = nomeCognome[0].Trim().ToLower();
            string cognome = nomeCognome[1].Trim().ToLower();
            string nomeAccount = $"{nomeUtente}.{cognome}";

            using (var db = new SinergiaDB())
            {
                var utenti = db.Utenti.ToList();

                var utente = utenti.FirstOrDefault(u =>
                    !string.IsNullOrEmpty(u.NomeAccount) &&
                    u.NomeAccount.ToLower() == nomeAccount);

                if (utente == null)
                    return Json(new { success = false, message = "Utente non trovato." }, JsonRequestBehavior.AllowGet);

                if (string.IsNullOrEmpty(utente.PasswordTemporanea))
                {
                    return Json(new { success = false, message = "Non è stata generata una password temporanea per questo utente." }, JsonRequestBehavior.AllowGet);
                }
                // ✅ Memorizza in sessione l'utente per il successivo ImpostaPassword
                Session["User"] = utente;

                return Json(new { success = true, password = utente.PasswordTemporanea }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult RecuperaPassword(string nuovaPassword)
        {
            var nomeUtente = Session["Recupero_NomeUtente"] as string;

            if (string.IsNullOrEmpty(nomeUtente))
            {
                return Json(new { success = false, message = "Sessione recupero password non valida." });
            }

            if (string.IsNullOrWhiteSpace(nuovaPassword) || nuovaPassword.Length < 6)
            {
                return Json(new { success = false, message = "La nuova password deve contenere almeno 6 caratteri." });
            }

            using (var db = new SinergiaDB())
            {
                var utente = db.Utenti.FirstOrDefault(u => u.NomeAccount.ToLower() == nomeUtente.ToLower());

                if (utente == null)
                {
                    Session.Clear();
                    return Json(new { success = false, message = "Utente non trovato." });
                }

                string nuovoSalt = Guid.NewGuid().ToString("N");
                utente.Salt = nuovoSalt;
                utente.PasswordHash = UserManager.CriptPassword(nuovaPassword, nuovoSalt);
                utente.PasswordTemporanea = null; // ✅ Cancella la password temporanea!

                db.SaveChanges();

                // 🔥 Login automatico dopo il recupero
                Session["User"] = utente;
                Session["menuLinks"] = MenuHelper.GetMenuUtente(utente.ID_Utente, utente.TipoUtente);

                // 🔥 Pulizia sessione temporanea
                Session.Remove("Recupero_NomeUtente");

                return Json(new { success = true, redirectUrl = Url.Action("Cruscotto", "Home") });
            }
        }

        [HttpPost]
        [AllowAnonymous] // 👈 AGGIUNGI QUESTO
        public JsonResult AvviaRecuperoPassword(string nomeAccount)
        {
            if (string.IsNullOrWhiteSpace(nomeAccount))
                return Json(new { success = false, message = "Inserisci il Nome Account." });

            using (var db = new SinergiaDB())
            {
                var utente = db.Utenti.FirstOrDefault(u => u.NomeAccount.ToLower() == nomeAccount.ToLower());
                if (utente == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                if (string.IsNullOrEmpty(utente.PasswordTemporanea))
                    return Json(new { success = false, message = "Nessuna password temporanea generata. Contatta il supporto." });

                Session["Recupero_NomeUtente"] = nomeAccount; // 👈 qui salva NomeAccount nella sessione

                return Json(new { success = true });
            }
        }

        [HttpPost]
        public JsonResult CreaPasswordTemporanea(string nomeAccount)
        {
            if (string.IsNullOrWhiteSpace(nomeAccount))
                return Json(new { success = false, message = "Inserisci il Nome Account." });

            using (var db = new SinergiaDB())
            {
                var utente = db.Utenti.FirstOrDefault(u => u.NomeAccount.ToLower() == nomeAccount.ToLower());
                if (utente == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                // ✅ Salva in sessione il Nome Account per il recupero
                Session["Recupero_NomeUtente"] = nomeAccount;

                // ✅ Genera password temporanea
                var random = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var passwordTemporanea = new string(Enumerable.Repeat(chars, 4)
                  .Select(s => s[random.Next(s.Length)]).ToArray());

                utente.PasswordTemporanea = passwordTemporanea;
                db.SaveChanges();

                return Json(new { success = true, passwordTemporanea = passwordTemporanea });
            }
        }



    }
}
