using System;
using System.Web;
using System.Web.Mvc;
using Sinergia.App_Helpers;
using Sinergia.Models;
using Sinergia.Model;
using System.Linq;
using Sinergia.ActionFilters;
using System.Collections.Generic;
using System.Text;
using OpenXmlPowerTools;

namespace Sinergia.Controllers
{
    [PermissionsActionFilter]
    public class HomeController : Controller
    {
        private SinergiaDB db = new SinergiaDB();
        #region Cruscotto
        public ActionResult Cruscotto(string idCliente = null, string filtroTrimestre = null, string sottoFiltro = "completo", int? annoSelezionato = null)
        {
            ViewData["controller"] = ControllerContext.RouteData.Values["controller"].ToString();
            ViewData["azione"] = ControllerContext.RouteData.Values["action"].ToString();

            int idUtente = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtente <= 0)
                return RedirectToAction("Login", "Account");

            // ✅ Esegui eventuali ricorrenze costi automatiche solo per Admin
            if (IsAdminUser_Dashboard())
            {
                RicorrenzeHelper.EseguiRicorrenzeCostiSeNecessario();
                CostiHelper.EseguiGenerazioneCosti();

                // ==========================================================
                // 🔔 Controlli periodici per notifiche automatiche
                // ==========================================================
                NotificheHelper.CreaNotificaPraticaFerma(30);           // Pratiche senza aggiornamenti da 30 giorni
                NotificheHelper.CreaNotificaPraticaSenzaAvviso();       // Pratiche chiuse/completate senza avviso parcella
                NotificheHelper.CreaNotificaPraticaScadenzaImminente(); // Pratiche con scadenza prossima
                NotificheHelper.VerificaCostiNonPagati(1);              // Costi non pagati da oltre 1 mese
            }

            using (var db = new SinergiaDB())
            {
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteAttivo);
                if (utente == null)
                    return RedirectToAction("Login", "Account");

                var clientiDisponibili = DashboardHelper.GetClientiDisponibiliPerNavbar(idUtente, UserManager.GetTipoUtente());
                Session["ClientiDisponibili"] = clientiDisponibili;
                if (clientiDisponibili == null || !clientiDisponibili.Any())
                    return View("NessunProfessionistaAssegnato");

                // ==========================================================
                // 📌 MAPPING PROFESSIONISTA CORRENTE
                // ==========================================================
                var operatore = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");

                int idClienteProfessionista = 0;
                string nomeCliente = "";
                if (operatore != null)
                {
                    idClienteProfessionista = operatore.ID_Cliente;
                    nomeCliente = $"{operatore.Nome} {operatore.Cognome}";
                    idCliente = "P_" + idClienteProfessionista;
                    Session["ID_ClienteSelezionato"] = idCliente;

                    var cookie = new HttpCookie("Cliente", idCliente) { Expires = DateTime.Now.AddDays(7) };
                    Response.Cookies.Add(cookie);
                }

                // ==========================================================
                // 📆 RANGE DI ANALISI PER TRIMESTRE
                // ==========================================================
                // 🩵 💡 👉 METTILO QUI SUBITO
                if (!string.IsNullOrEmpty(filtroTrimestre) && filtroTrimestre.All(char.IsDigit))
                    filtroTrimestre = "auto";

                DateTime oggi = DateTime.Today;
                int anno = annoSelezionato ?? oggi.Year;
                DateTime inizio;
                DateTime fine;

               
                if (string.IsNullOrEmpty(filtroTrimestre) || filtroTrimestre == "auto")
                {
                    if (oggi.Month >= 1 && oggi.Month <= 3)
                        filtroTrimestre = "Q1";
                    else if (oggi.Month >= 4 && oggi.Month <= 6)
                        filtroTrimestre = "Q2";
                    else if (oggi.Month >= 7 && oggi.Month <= 9)
                        filtroTrimestre = "Q3";
                    else
                        filtroTrimestre = "Q4";
                }

                switch (filtroTrimestre)
                {
                    case "Q1":
                        inizio = new DateTime(anno, 1, 1);
                        fine = new DateTime(anno, 3, 31);
                        break;
                    case "Q2":
                        inizio = new DateTime(anno, 4, 1);
                        fine = new DateTime(anno, 6, 30);
                        break;
                    case "Q3":
                        inizio = new DateTime(anno, 7, 1);
                        fine = new DateTime(anno, 9, 30);
                        break;
                    case "Q4":
                        inizio = new DateTime(anno, 10, 1);
                        fine = new DateTime(anno, 12, 31);
                        break;
                    case "anno":
                    default:
                        inizio = new DateTime(anno, 1, 1);
                        fine = new DateTime(anno, 12, 31);
                        break;
                }

                // 🔹 Gestione sottofiltri
                if (sottoFiltro == "mese1") fine = inizio.AddMonths(1).AddDays(-1);
                else if (sottoFiltro == "mese2") { inizio = inizio.AddMonths(1); fine = inizio.AddMonths(1).AddDays(-1); }
                else if (sottoFiltro == "mese3") { inizio = inizio.AddMonths(2); fine = inizio.AddMonths(1).AddDays(-1); }

                // ==========================================================
                // 📁 PRATICHE VISIBILI (Cluster + Compensi)
                // ==========================================================
                var idUtenteCollegato = db.OperatoriSinergia
                    .Where(o => o.ID_Cliente == idClienteProfessionista)
                    .Select(o => o.ID_UtenteCollegato)
                    .FirstOrDefault();

                var praticheCluster = db.Cluster
                    .Where(c =>
                        c.TipoCluster == "Collaboratore" &&
                        (c.ID_Utente == idUtenteAttivo || c.ID_Utente == idUtenteCollegato))
                    .Select(c => c.ID_Pratiche)
                    .Distinct()
                    .ToList();

                string idUtenteJson = $"\"ID_Collaboratore\":{idUtenteAttivo}";
                string idCollegatoJson = $"\"ID_Collaboratore\":{idUtenteCollegato}";

                var praticheCompensi = db.CompensiPraticaDettaglio
                    .Where(cd => (cd.Collaboratori.Contains(idUtenteJson) || cd.Collaboratori.Contains(idCollegatoJson)))
                    .Select(cd => cd.ID_Pratiche)
                    .Distinct()
                    .ToList();

                var idPraticheVisibili = praticheCluster.Union(praticheCompensi).Distinct().ToList();

                var pratiche = db.Pratiche
                    .Where(p =>
                        p.Stato != "Eliminato" &&
                        p.DataCreazione >= inizio && p.DataCreazione <= fine &&
                        (
                            p.ID_UtenteResponsabile == idUtenteAttivo ||
                            p.ID_Owner == idClienteProfessionista ||
                            idPraticheVisibili.Contains(p.ID_Pratiche)
                        ))
                    .ToList();

                var listaPratiche = pratiche.Select(p => new PraticaViewModel
                {
                    ID_Pratiche = p.ID_Pratiche,
                    Titolo = p.Titolo,
                    Stato = p.Stato,
                    Descrizione = p.Descrizione
                }).ToList();

                // ==========================================================
                // 🧾 AVVISI PARCELLA
                // ==========================================================
                var idPraticheVisibiliPerAvvisi = pratiche.Select(p => p.ID_Pratiche).ToList();
                idPraticheVisibiliPerAvvisi.AddRange(praticheCluster);
                idPraticheVisibiliPerAvvisi.AddRange(praticheCompensi);
                idPraticheVisibiliPerAvvisi = idPraticheVisibiliPerAvvisi.Distinct().ToList();

                var avvisiParcella = (
                    from a in db.AvvisiParcella
                    join p in db.Pratiche on a.ID_Pratiche equals p.ID_Pratiche
                    where a.Stato != "Annullato"
                          && idPraticheVisibiliPerAvvisi.Contains(p.ID_Pratiche)
                          && a.DataAvviso >= inizio && a.DataAvviso <= fine
                    orderby a.DataAvviso descending
                    select new AvvisoParcellaViewModel
                    {
                        ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                        TitoloAvviso = !string.IsNullOrEmpty(a.TitoloAvviso)
                            ? a.TitoloAvviso
                            : (string.IsNullOrEmpty(p.Titolo) ? "(Senza titolo)" : p.Titolo),
                        Importo = a.Importo,
                        Stato = a.Stato
                    }).Take(5).ToList();

                // ==========================================================
                // 🔔 NOTIFICHE
                // ==========================================================
                var notifiche = db.Notifiche
                    .Where(n => n.ID_Utente == idUtenteAttivo && n.DataLettura == null)
                    .OrderByDescending(n => n.DataCreazione)
                    .Take(5)
                    .Select(n => new NotificaViewModel
                    {
                        ID_Notifica = n.ID_Notifica,
                        Titolo = n.Titolo
                    }).ToList();

                // ==========================================================
                // 💰 KPI PERSONALI: Incassi / Costi / Utile
                // ==========================================================
                decimal incassiTotali = db.PlafondUtente
                    .Where(p =>
                        p.TipoPlafond == "Incasso" &&
                        (p.ID_Utente == idClienteProfessionista || p.ID_Utente == idUtenteCollegato) &&
                        p.DataVersamento >= inizio && p.DataVersamento <= fine)
                    .Sum(p => (decimal?)p.ImportoTotale) ?? 0;

                decimal costiTotali = db.GenerazioneCosti
                    .Where(c =>
                        (c.ID_Utente == idClienteProfessionista || c.ID_Utente == idUtenteCollegato) &&
                        c.Stato == "Pagato" &&
                        c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine)
                    .Sum(c => (decimal?)c.Importo) ?? 0;

                decimal utileNetto = incassiTotali - costiTotali;

                // ==========================================================
                // 📦 COSTRUZIONE MODEL
                // ==========================================================
                var model = new DashboardViewModel
                {
                    NomeUtente = utente.Nome,
                    NomeCliente = nomeCliente,
                    ID_ClienteSelezionato = idClienteProfessionista,
                    ClientiDisponibili = clientiDisponibili,
                    Pratiche = listaPratiche,
                    AvvisiParcella = avvisiParcella,
                    Notifiche = notifiche,
                    IncassiTotali = incassiTotali,
                    CostiTotali = costiTotali,
                    UtilePersonale = utileNetto,
                    FiltroTrimestre = filtroTrimestre,
                    SottoFiltro = sottoFiltro,
                    IsAdmin = IsAdminUser_Dashboard()
                };

                // ==========================================================
                // 📈 MINI-GRAFICO (ultimi 6 mesi)
                // ==========================================================
                DateTime seiMesiFa = oggi.AddMonths(-5);
                var serieIncassi = db.PlafondUtente
                    .Where(p => p.TipoPlafond == "Incasso" &&
                                (p.ID_Utente == idClienteProfessionista || p.ID_Utente == idUtenteCollegato) &&
                                p.DataVersamento >= seiMesiFa)
                    .GroupBy(p => new { Mese = p.DataVersamento.Value.Month, Anno = p.DataVersamento.Value.Year })
                    .Select(g => new { g.Key.Mese, g.Key.Anno, Totale = g.Sum(x => x.ImportoTotale) })
                    .OrderBy(g => g.Anno).ThenBy(g => g.Mese)
                    .ToList();

                var serieCosti = db.GenerazioneCosti
                    .Where(c => (c.ID_Utente == idClienteProfessionista || c.ID_Utente == idUtenteCollegato) &&
                                c.Stato == "Pagato" &&
                                c.DataRegistrazione >= seiMesiFa)
                    .GroupBy(c => new { Mese = c.DataRegistrazione.Value.Month, Anno = c.DataRegistrazione.Value.Year })
                    .Select(g => new { g.Key.Mese, g.Key.Anno, Totale = g.Sum(x => x.Importo ?? 0) })
                    .OrderBy(g => g.Anno).ThenBy(g => g.Mese)
                    .ToList();

                var mesi = Enumerable.Range(0, 6).Select(i => oggi.AddMonths(-5 + i)).ToList();

                model.MesiGrafico = mesi.Select(m => m.ToString("MMM yyyy")).ToList();
                model.AndamentoIncassi = mesi.Select(m =>
                {
                    var item = serieIncassi.FirstOrDefault(s => s.Mese == m.Month && s.Anno == m.Year);
                    return item?.Totale ?? 0;
                }).ToList();

                model.AndamentoCosti = mesi.Select(m =>
                {
                    var item = serieCosti.FirstOrDefault(s => s.Mese == m.Month && s.Anno == m.Year);
                    return item?.Totale ?? 0;
                }).ToList();

                // ==========================================================
                // 🏢 KPI SINERGIA (solo per Admin)
                // ==========================================================
                if (IsAdminUser_Dashboard())
                {
                    System.Diagnostics.Trace.WriteLine("========== [CRUSCOTTO ADMIN - KPI SINERGIA] ==========");

                    try
                    {
                        // ✅ Usa l’intervallo del trimestre già calcolato sopra
                        System.Diagnostics.Trace.WriteLine($"📅 Calcolo KPI Sinergia per periodo selezionato: {inizio:dd/MM/yyyy} → {fine:dd/MM/yyyy}");

                        // ======================================================
                        // 💰 ENTRATE (solo costi pagati, reali)
                        // ======================================================
                        var entrateQuery = db.GenerazioneCosti
                            .Where(c =>
                                c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine &&
                                c.Stato == "Pagato" &&
                                (c.Categoria == "Costo Generale" ||
                                 c.Categoria == "Costo Team" ||
                                 c.Categoria == "Costo Professionista" ||
                                 c.Categoria == "Costo Pratica") &&
                                !c.Descrizione.Contains("Owner Fee"))
                            .ToList();

                        decimal entrateTotali = entrateQuery.Sum(c => c.Importo ?? 0);
                        System.Diagnostics.Trace.WriteLine($"💰 Entrate effettive (Pagato): {entrateTotali:N2} € ({entrateQuery.Count} record)");

                        // ======================================================
                        // 💸 USCITE (ancora previsionali)
                        // ======================================================
                        var usciteQuery = db.GenerazioneCosti
                            .Where(c =>
                                c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine &&
                                c.Stato == "Previsionale" &&
                                (c.Categoria == "Costo Generale" ||
                                 c.Categoria == "Costo Team" ||
                                 c.Categoria == "Costo Professionista" ||
                                 c.Categoria == "Costo Pratica") &&
                                !c.Descrizione.Contains("Owner Fee"))
                            .ToList();

                        decimal usciteTotali = usciteQuery.Sum(c => c.Importo ?? 0);
                        System.Diagnostics.Trace.WriteLine($"💸 Uscite previsionali: {usciteTotali:N2} € ({usciteQuery.Count} record)");

                        // ======================================================
                        // 🏦 TRATTENUTE SINERGIA (solo Finanziarie)
                        // ======================================================
                        decimal trattenuteSinergia = db.BilancioProfessionista
                            .Where(b =>
                                b.Categoria == "Trattenuta Sinergia" &&
                                b.Stato == "Finanziario" &&
                                b.DataRegistrazione >= inizio && b.DataRegistrazione <= fine)
                            .Sum(b => (decimal?)b.Importo) ?? 0m;

                        System.Diagnostics.Trace.WriteLine($"🏦 Trattenute Finanziarie: {trattenuteSinergia:N2} €");

                        // ======================================================
                        // 📈 UTILE AZIENDALE
                        // ======================================================
                        decimal utileAziendale = (entrateTotali + trattenuteSinergia) - usciteTotali;
                        System.Diagnostics.Trace.WriteLine($"📈 Utile aziendale calcolato: {utileAziendale:N2} €");

                        // ======================================================
                        // 💾 Assegna ai KPI del model
                        // ======================================================
                        model.EntrateTotaliSinergia = entrateTotali;
                        model.UsciteTotaliSinergia = usciteTotali;
                        model.TrattenuteSinergiaTotali = trattenuteSinergia;
                        model.UtileAziendale = utileAziendale;

                        // ======================================================
                        // 📊 Andamento mensile (solo 3 mesi del trimestre)
                        // ======================================================
                        var andamentoMensile = db.GenerazioneCosti
                            .Where(c =>
                                c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine &&
                                (c.Categoria == "Costo Generale" ||
                                 c.Categoria == "Costo Team" ||
                                 c.Categoria == "Costo Professionista" ||
                                 c.Categoria == "Costo Pratica") &&
                                !c.Descrizione.Contains("Owner Fee"))
                            .ToList()
                            .GroupBy(c => new { c.DataRegistrazione.Value.Year, c.DataRegistrazione.Value.Month })
                            .Select(g => new
                            {
                                MeseAnno = $"{g.Key.Month:D2}/{g.Key.Year}",
                                Entrate = g.Where(x => x.Stato == "Pagato").Sum(x => x.Importo ?? 0),
                                Uscite = g.Where(x => x.Stato == "Previsionale").Sum(x => x.Importo ?? 0)
                            })
                            .Select(x => new
                            {
                                x.MeseAnno,
                                Utile = (x.Entrate) - (x.Uscite)
                            })
                            .OrderBy(x => x.MeseAnno)
                            .ToList();

                        model.MesiUtile = andamentoMensile.Select(x => x.MeseAnno).ToList();
                        model.UtileMensile = andamentoMensile.Select(x => x.Utile).ToList();

                        System.Diagnostics.Trace.WriteLine("✅ [KPI Sinergia] Calcolo trimestrale completato senza errori.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("❌ [ERRORE KPI SINERGIA]");
                        System.Diagnostics.Trace.WriteLine($"Messaggio: {ex.Message}");
                        if (ex.InnerException != null)
                            System.Diagnostics.Trace.WriteLine($"InnerException: {ex.InnerException.Message}");
                        System.Diagnostics.Trace.WriteLine($"StackTrace: {ex.StackTrace}");

                        // fallback
                        model.MesiUtile = Enumerable.Range(0, 3)
                            .Select(i => DateTime.Today.AddMonths(-2 + i).ToString("MM/yyyy"))
                            .ToList();
                        model.UtileMensile = Enumerable.Repeat(0m, model.MesiUtile.Count).ToList();
                    }

                    System.Diagnostics.Trace.WriteLine("========== [FINE KPI SINERGIA] ==========");
                }


                return View("Cruscotto", model);

            }
        }



        [HttpGet]
        public JsonResult AggiornaGraficoSinergia(string filtroTrimestre, string sottoFiltro, int? annoSelezionato = null)
        {
            using (var db = new SinergiaDB())
            {
                // ==========================================================
                // 🧩 Normalizza filtro e sottofiltro
                // ==========================================================

                // 🔒 Se arriva un numero tipo "3620", forza "auto"
                if (!string.IsNullOrEmpty(filtroTrimestre) && filtroTrimestre.All(char.IsDigit))
                {
                    System.Diagnostics.Trace.WriteLine($"⚠️ [AggiornaGraficoSinergia] Ricevuto valore numerico anomalo: {filtroTrimestre}, imposto 'auto'");
                    filtroTrimestre = "auto";
                }

                DateTime oggi = DateTime.Today;
                int anno = annoSelezionato ?? oggi.Year;
                DateTime inizio;
                DateTime fine;

                // ✅ Determina trimestre
                if (string.IsNullOrEmpty(filtroTrimestre) || filtroTrimestre == "auto")
                {
                    if (oggi.Month >= 1 && oggi.Month <= 3) filtroTrimestre = "Q1";
                    else if (oggi.Month >= 4 && oggi.Month <= 6) filtroTrimestre = "Q2";
                    else if (oggi.Month >= 7 && oggi.Month <= 9) filtroTrimestre = "Q3";
                    else filtroTrimestre = "Q4";
                }

                // ✅ Calcolo intervallo del trimestre
                switch (filtroTrimestre)
                {
                    case "Q1":
                        inizio = new DateTime(anno, 1, 1);
                        fine = new DateTime(anno, 3, 31);
                        break;
                    case "Q2":
                        inizio = new DateTime(anno, 4, 1);
                        fine = new DateTime(anno, 6, 30);
                        break;
                    case "Q3":
                        inizio = new DateTime(anno, 7, 1);
                        fine = new DateTime(anno, 9, 30);
                        break;
                    case "Q4":
                        inizio = new DateTime(anno, 10, 1);
                        fine = new DateTime(anno, 12, 31);
                        break;
                    default:
                        filtroTrimestre = "Anno";
                        inizio = new DateTime(anno, 1, 1);
                        fine = new DateTime(anno, 12, 31);
                        break;
                }

                // ✅ Applica sottofiltro (mese1, mese2, mese3)
                if (sottoFiltro == "mese1") fine = inizio.AddMonths(1).AddDays(-1);
                else if (sottoFiltro == "mese2") { inizio = inizio.AddMonths(1); fine = inizio.AddMonths(1).AddDays(-1); }
                else if (sottoFiltro == "mese3") { inizio = inizio.AddMonths(2); fine = inizio.AddMonths(1).AddDays(-1); }

                // ==========================================================
                // 💰 Calcolo KPI Sinergia SOLO PER IL PERIODO
                // ==========================================================
                decimal entrate = db.GenerazioneCosti
                    .Where(c =>
                        (c.Origine == "Ricorrenza" || c.Origine == "Progetto") &&
                        (c.Approvato == true || c.Stato == "Pagato") &&
                        (c.Categoria == "Costo Generale" ||
                         c.Categoria == "Costo Team" ||
                         c.Categoria == "Costo Professionista" ||
                         c.Categoria == "Costo Pratica") &&
                        c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine)
                    .Sum(c => (decimal?)c.Importo) ?? 0;

                decimal uscite = db.GenerazioneCosti
                    .Where(c =>
                        (c.Origine == "Ricorrenza" || c.Origine == "Progetto") &&
                        (c.Approvato == false || c.Stato == "Previsionale") &&
                        (c.Categoria == "Costo Generale" ||
                         c.Categoria == "Costo Team" ||
                         c.Categoria == "Costo Professionista" ||
                         c.Categoria == "Costo Pratica") &&
                        c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine)
                    .Sum(c => (decimal?)c.Importo) ?? 0;

                decimal trattenute = db.BilancioProfessionista
                    .Where(b =>
                        b.Categoria == "Trattenuta Sinergia" && b.Importo > 0 &&
                        b.DataRegistrazione >= inizio && b.DataRegistrazione <= fine)
                    .Sum(b => (decimal?)b.Importo) ?? 0;

                decimal utile = (entrate - uscite) + trattenute;

                // ==========================================================
                // 🧾 Log diagnostico
                // ==========================================================
                System.Diagnostics.Trace.WriteLine("========== [AggiornaGraficoSinergia] ==========");
                System.Diagnostics.Trace.WriteLine($"📅 Periodo calcolato: {filtroTrimestre} | {inizio:dd/MM/yyyy} → {fine:dd/MM/yyyy}");
                System.Diagnostics.Trace.WriteLine($"💰 Entrate: {entrate:N2} €");
                System.Diagnostics.Trace.WriteLine($"💸 Uscite: {uscite:N2} €");
                System.Diagnostics.Trace.WriteLine($"🏦 Trattenute: {trattenute:N2} €");
                System.Diagnostics.Trace.WriteLine($"📈 Utile: {utile:N2} €");
                System.Diagnostics.Trace.WriteLine("===============================================");

                // ✅ Restituisce il JSON al grafico
                return Json(new
                {
                    entrate,
                    uscite,
                    trattenute,
                    utile,
                    filtroTrimestre
                }, JsonRequestBehavior.AllowGet);
            }
        }


         [HttpGet]
                    public ActionResult GetDettaglioKPISinergia(string tipo)
            {
                System.Diagnostics.Trace.WriteLine("========== [GetDettaglioKPISinergia] AVVIO ==========");
                System.Diagnostics.Trace.WriteLine($"🟢 Tipo KPI richiesto: {tipo}");

                try
                {
                    using (var db = new SinergiaDB())
                    {
                        tipo = (tipo ?? "").Trim().ToLower();

                        // ======================================================
                        // 📅 Calcolo periodo del trimestre corrente
                        // (puoi poi sostituire con periodo scelto in dashboard)
                        // ======================================================
                        DateTime oggi = DateTime.Today;
                        int trimestre = (oggi.Month - 1) / 3 + 1;
                        DateTime inizio, fine;

                        switch (trimestre)
                        {
                            case 1: inizio = new DateTime(oggi.Year, 1, 1); fine = new DateTime(oggi.Year, 3, 31); break;
                            case 2: inizio = new DateTime(oggi.Year, 4, 1); fine = new DateTime(oggi.Year, 6, 30); break;
                            case 3: inizio = new DateTime(oggi.Year, 7, 1); fine = new DateTime(oggi.Year, 9, 30); break;
                            default: inizio = new DateTime(oggi.Year, 10, 1); fine = new DateTime(oggi.Year, 12, 31); break;
                        }

                        System.Diagnostics.Trace.WriteLine($"📆 Periodo KPI Sinergia: {inizio:dd/MM/yyyy} → {fine:dd/MM/yyyy}");

                        var dati = new List<dynamic>();

                        // ======================================================
                        // 💰 ENTRATE SINERGIA (effettive - solo Pagato)
                        // ======================================================
                        if (tipo == "entrate")
                        {
                            dati = db.GenerazioneCosti
                                .Where(c =>
                                    c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine &&
                                    c.Stato == "Pagato" &&
                                    (c.Categoria == "Costo Generale" ||
                                     c.Categoria == "Costo Team" ||
                                     c.Categoria == "Costo Professionista" ||
                                     c.Categoria == "Costo Pratica") &&
                                    !c.Descrizione.Contains("Owner Fee"))
                                .OrderByDescending(c => c.DataRegistrazione)
                                .Take(300)
                                .Select(c => new
                                {
                                    Data = c.DataRegistrazione,
                                    c.Descrizione,
                                    c.Categoria,
                                    c.Origine,
                                    Stato = c.Stato,
                                    Importo = c.Importo ?? 0
                                })
                                .ToList<dynamic>();

                            System.Diagnostics.Trace.WriteLine($"📗 Entrate (Pagato) trovate: {dati.Count}");
                        }

                        // ======================================================
                        // 💸 USCITE SINERGIA (ancora previsionali)
                        // ======================================================
                        else if (tipo == "uscite")
                        {
                            dati = db.GenerazioneCosti
                                .Where(c =>
                                    c.DataRegistrazione >= inizio && c.DataRegistrazione <= fine &&
                                    c.Stato == "Previsionale" &&
                                    (c.Categoria == "Costo Generale" ||
                                     c.Categoria == "Costo Team" ||
                                     c.Categoria == "Costo Professionista" ||
                                     c.Categoria == "Costo Pratica") &&
                                    !c.Descrizione.Contains("Owner Fee"))
                                .OrderByDescending(c => c.DataRegistrazione)
                                .Take(300)
                                .Select(c => new
                                {
                                    Data = c.DataRegistrazione,
                                    c.Descrizione,
                                    c.Categoria,
                                    c.Origine,
                                    Stato = c.Stato,
                                    Importo = c.Importo ?? 0
                                })
                                .ToList<dynamic>();

                            System.Diagnostics.Trace.WriteLine($"📕 Uscite (Previsionali) trovate: {dati.Count}");
                        }

                        // ======================================================
                        // 🏦 TRATTENUTE SINERGIA (solo finanziarie)
                        // ======================================================
                        else if (tipo == "trattenute")
                        {
                            dati = db.BilancioProfessionista
                                .Where(b =>
                                    b.Categoria == "Trattenuta Sinergia" &&
                                    b.Stato == "Finanziario" &&
                                    b.DataRegistrazione >= inizio && b.DataRegistrazione <= fine)
                                .OrderByDescending(b => b.DataRegistrazione)
                                .Take(300)
                                .Select(b => new
                                {
                                    Data = b.DataRegistrazione,
                                    b.Descrizione,
                                    b.Categoria,
                                    b.Origine,
                                    Stato = b.Stato,
                                    Importo = b.Importo
                                })
                                .ToList<dynamic>();

                            System.Diagnostics.Trace.WriteLine($"🏦 Trattenute Finanziarie trovate: {dati.Count}");
                        }

                        // ======================================================
                        // 📈 UTILE (spiegazione)
                        // ======================================================
                        else if (tipo == "utile")
                        {
                            return Content(
                                "<div class='alert alert-info mb-0'>" +
                                "L'utile aziendale è calcolato come <b>(Entrate + Trattenute) − Uscite</b>.<br>" +
                                "Non esistono record diretti in tabella.</div>"
                            );
                        }
                        else
                        {
                            return Content($"<div class='alert alert-warning mb-0'>Tipo KPI non riconosciuto: {tipo}</div>");
                        }

                        // ======================================================
                        // 🧱 TABELLA HTML RISULTATI
                        // ======================================================
                        if (!dati.Any())
                            return Content("<div class='alert alert-light text-center mb-0'>Nessun dato disponibile per questo KPI.</div>");

                        decimal totale = dati.Sum(x => (decimal)x.Importo);

                        var sb = new System.Text.StringBuilder();
                        sb.Append("<div class='table-responsive'>");
                        sb.Append("<table class='table table-sm table-striped align-middle mb-0'>");
                        sb.Append("<thead class='table-primary'><tr>");
                        sb.Append("<th>Data</th><th>Descrizione</th><th>Categoria</th><th>Origine</th><th>Stato</th><th class='text-end'>Importo (€)</th>");
                        sb.Append("</tr></thead><tbody>");

                        foreach (var r in dati)
                        {
                                    string importoColor = "text-secondary";
                                    switch (tipo)
                                    {
                                        case "entrate":
                                            importoColor = "text-success";
                                            break;
                                        case "uscite":
                                            importoColor = "text-danger";
                                            break;
                                        case "trattenute":
                                            importoColor = "text-warning";
                                            break;
                                    }

                                    sb.Append("<tr>");
                            sb.Append($"<td>{r.Data:dd/MM/yyyy}</td>");
                            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(r.Descrizione)}</td>");
                            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(r.Categoria)}</td>");
                            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(r.Origine)}</td>");
                            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(r.Stato)}</td>");
                            sb.Append($"<td class='text-end {importoColor}'><b>{r.Importo:N2}</b></td>");
                            sb.Append("</tr>");
                        }

                        sb.Append($"<tr class='fw-bold table-secondary'><td colspan='5' class='text-end'>Totale</td><td class='text-end'>{totale:N2} €</td></tr>");
                        sb.Append("</tbody></table></div>");

                        System.Diagnostics.Trace.WriteLine($"✅ Totale {tipo}: {totale:N2} €");
                        return Content(sb.ToString(), "text/html");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"❌ Errore generale GetDettaglioKPISinergia: {ex}");
                    return Content($"<div class='alert alert-danger mb-0'>Errore generale: {ex.Message}</div>");
                }
            }




        [HttpPost]
        public ActionResult SelezionaClientiDisponibili(string idCliente)
        {
            if (!string.IsNullOrEmpty(idCliente))
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                string tipoUtente = UserManager.GetTipoUtente();

                var clientiDisponibili = DashboardHelper.GetClientiDisponibiliPerNavbar(idUtente, tipoUtente);
                Session["ClientiDisponibili"] = clientiDisponibili;

                Session["ID_ClienteSelezionato"] = idCliente;

                string tipo = idCliente.Substring(0, 2); // A_, P_, C_
                string id = idCliente.Substring(2);      // numerico

                if (int.TryParse(id, out int idParsed))
                {
                    using (var db = new SinergiaDB())
                    {
                        if (tipo == "A_" || tipo == "P_")
                        {
                            var cliente = db.OperatoriSinergia
                                .FirstOrDefault(c => c.ID_Cliente == idParsed && c.Stato == "Attivo");

                            if (cliente != null)
                            {
                                // 🔹 Sempre salva ID cliente corrente
                                Session["IDClienteProfessionistaCorrente"] = cliente.ID_Cliente;

                                // 🔹 Se collegato, salva anche l'ID utente impersonificato
                                if (cliente.ID_UtenteCollegato.HasValue)
                                {
                                    Session["ID_UtenteImpers"] = cliente.ID_UtenteCollegato.Value;
                                }
                                else
                                {
                                    Session["ID_UtenteImpers"] = null;
                                }
                            }
                        }
                        else if (tipo == "C_")
                        {
                            // Per collaboratore: salvo solo utente impersonificato
                            Session["ID_UtenteImpers"] = idParsed;
                            Session["IDClienteProfessionistaCorrente"] = null;
                        }
                    }

                    var cookie = new HttpCookie("Cliente", idCliente)
                    {
                        Expires = DateTime.Now.AddDays(7)
                    };
                    Response.Cookies.Add(cookie);
                }
            }
            else
            {
                // ✅ Reset impersonificazione → ritorno a Admin
                Session["ID_ClienteSelezionato"] = null;
                Session["ID_UtenteImpers"] = null;
                Session["IDClienteProfessionistaCorrente"] = null;

                // cancella anche eventuale cookie
                if (Request.Cookies["Cliente"] != null)
                {
                    var cookie = new HttpCookie("Cliente") { Expires = DateTime.Now.AddDays(-1) };
                    Response.Cookies.Add(cookie);
                }
            }

            return RedirectToAction("Cruscotto");
        }


        public ActionResult TestImpersonificazione()
        {
            int idLoggato = UserManager.GetIDUtenteCollegato();
            int idAttivo = UserManager.GetIDUtenteAttivo();

            using (var db = new SinergiaDB())
            {
                var utenteLoggato = db.Utenti.Find(idLoggato);
                var utenteAttivo = db.Utenti.Find(idAttivo);

                ViewBag.NomeLoggato = utenteLoggato?.Nome + " " + utenteLoggato?.Cognome + $" (ID: {idLoggato})";
                ViewBag.NomeAttivo = utenteAttivo?.Nome + " " + utenteAttivo?.Cognome + $" (ID: {idAttivo})";
            }

            // 👉 Aggiungi queste per debug
            ViewBag.ID_UtenteImpers = Session["ID_UtenteImpers"];
            ViewBag.ID_ClienteSelezionato = Session["ID_ClienteSelezionato"];

            return View();
        }

        // ==========================================================
        // 👑 Metodo dedicato al Cruscotto per verificare se è Admin
        // ==========================================================
        private bool IsAdminUser_Dashboard()
        {
            try
            {
                int idUtenteCollegato = UserManager.GetIDUtenteCollegato();

                if (idUtenteCollegato <= 0)
                {
                    System.Diagnostics.Trace.WriteLine("⚠️ [IsAdminUser_Dashboard] Nessun utente collegato.");
                    return false;
                }

                using (var db = new SinergiaDB())
                {
                    var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCollegato);
                    if (utente == null)
                    {
                        System.Diagnostics.Trace.WriteLine("⚠️ [IsAdminUser_Dashboard] Utente non trovato nel DB.");
                        return false;
                    }

                    bool isAdmin = utente.TipoUtente?.Trim().Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

                    System.Diagnostics.Trace.WriteLine(
                        $"👤 [IsAdminUser_Dashboard] ID={utente.ID_Utente}, Tipo={utente.TipoUtente}, IsAdmin={isAdmin}"
                    );

                    return isAdmin;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ [IsAdminUser_Dashboard] Errore: {ex.Message}");
                return false;
            }
        }




        #endregion

        #region LOG MODIFICHE   
        // Vista principale per i log delle modifiche
        public ActionResult GestioneModifiche()
        {
            return View("~/Views/LogModifiche/GestioneModifiche.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneModificheList(string nomeTabella)
        {
            if (string.IsNullOrWhiteSpace(nomeTabella))
                return Json(new { success = false, message = "Nessuna tabella selezionata." }, JsonRequestBehavior.AllowGet);

            // 🔧 Normalizza: rimuove eventuale suffisso "_a" per compatibilità con i case esistenti
            if (nomeTabella.EndsWith("_a"))
                nomeTabella = nomeTabella.Replace("_a", "");

            IEnumerable<dynamic> lista = null;

            switch (nomeTabella)
            {
                case "AnagraficaCostiPratica":
                    lista = db.AnagraficaCostiPratica_a
                        .OrderByDescending(x => x.DataUltimaModifica)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_AnagraficaCosto_a,
                            Data = x.DataUltimaModifica,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;



                case "AvvisiParcella":
                    lista = db.AvvisiParcella_a
                        .OrderByDescending(x => x.DataModifica)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Archivio,
                            Data = x.DataModifica,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;

                case "BilancioProfessionista":
                    lista = db.BilancioProfessionista
                        .OrderByDescending(x => x.DataRegistrazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Bilancio,
                            Data = x.DataRegistrazione,
                            ModificheTestuali = x.Descrizione ?? "(Nessuna descrizione)",
                            TipoModifica = x.TipoVoce,
                            NumeroVersione = 1,
                            ID_UtenteUltimaModifica = x.ID_UtenteInserimento.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteInserimento)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList();
                    break;


                case "ClientiProfessionisti":
                    lista = db.ClientiProfessionisti_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_ClientiProfessionisti_a,
                            Data = x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Relazione Cliente-Professionista",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList();
                    break;

                case "CompensiPraticaDettaglio":
                    lista = db.CompensiPraticaDettaglio_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_RigaCompenso_a,
                            Data = x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Dettaglio Compenso",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList();
                    break;


                case "DocumentiProfessionisti":
                    lista = db.DocumentiProfessionisti_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Professionista,
                            Data = x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Documento Professionista",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList();
                    break;


                case "TipologieCosti":
                    lista = db.TipologieCosti_a
                        .OrderByDescending(x => x.DataUltimaModifica)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Storico,
                            Data = x.DataUltimaModifica,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = x.Tipo ?? "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList();
                    break;


                case "GenerazioneCosti":
                    lista = db.GenerazioneCosti
                        .OrderByDescending(x => x.DataCreazione)
                        .AsEnumerable() // 🔹 passa in memoria per evitare errori LINQ SQL
                        .Select(x =>
                        {
                            int idUtenteRif = x.ID_UtenteUltimaModifica ?? x.ID_UtenteCreatore ?? 0;
                            string nomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == idUtenteRif)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault() ?? "-";

                            return new LogModificaViewModel
                            {
                                ID = x.ID_GenerazioneCosto,
                                Data = x.DataUltimaModifica ?? x.DataCreazione,
                                ModificheTestuali =
                                    "Origine: " + (x.Origine ?? "N/D") + " | " +
                                    "Categoria: " + (x.Categoria ?? "N/D") + " | " +
                                    "Descrizione: " + (x.Descrizione ?? "N/D") + " | " +
                                    "Importo: " + ((decimal)(x.Importo ?? 0)).ToString("N2") + " € | " +
                                    "Stato: " + ((x.Approvato ?? false) ? "Approvato" : "Previsionale"),
                                TipoModifica = (x.Approvato ?? false) ? "Approvazione" : "Creazione",
                                NumeroVersione = 1,
                                ID_UtenteUltimaModifica = idUtenteRif.ToString(),
                                NomeUtente = nomeUtente
                            };
                        })
                        .ToList();
                    break;



                case "Utenti":
                    lista = db.Utenti_a
                        .OrderByDescending(x => x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.IDVersioneUtenti,
                            Data = x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione ?? DateTime.Now,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = x.NumeroVersione == 1 ? "Creazione" : "Modifica",
                            NumeroVersione = x.NumeroVersione,

                            // 👤 Chi ha fatto la modifica (archiviazione)
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault() ?? "-"
                        })
                        .ToList();
                    break;


                case "Pratiche":
                    lista = db.Pratiche_a
                        .OrderByDescending(x => x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Pratiche_a,
                            Data = x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione ?? DateTime.Now,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = x.Stato,
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.HasValue ? x.ID_UtenteUltimaModifica.Value.ToString() : "",
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;

                case "CostiPratica":
                    lista = db.CostiPratica_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_CostoPratica_Archivio,
                            Data = x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;

                case "AnagraficaCostiTeam":
                    lista = db.AnagraficaCostiTeam_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.IDVersioneAnagraficaCostoTeam,
                            Data = (DateTime)x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = (int)x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;

                case "TemplateIncarichi":
                    lista = db.TemplateIncarichi_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Archivio,
                            Data = x.DataArchiviazione ?? DateTime.Now,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;


                case "FinanziamentiProfessionisti":
                    lista = db.FinanziamentiProfessionisti_a
                        .OrderByDescending(x => x.DataUltimaModifica)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Finanziamento_Archivio,
                            Data = x.DataUltimaModifica,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;


                case "Cluster":
                    lista = db.Cluster_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Cluster_a,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;


                case "CompensiPratica":
                    lista = db.CompensiPratica_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_CompensoArchivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "CostiPersonaliUtente":
                    lista = db.CostiPersonaliUtente_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.IDVersioneCostoPersonale,
                        Data = x.DataUltimaModifica,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()

                    }).ToList();
                    break;

                case "DatiBancari":
                    lista = db.DatiBancari_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_DatoBancario,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "DocumentiAziende":
                    lista = db.DocumentiAziende_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Documento_A,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "DocumentiPratiche":
                    lista = db.DocumentiPratiche_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Documento_a,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "Economico":
                    lista = db.Economico_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_EconomicoArchivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

              
                case "Incassi":
                    lista = db.Incassi_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Archivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "MovimentiBancari":
                    lista = db.MovimentiBancari_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Movimento,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;


                case "OperatoriSinergia":
                    lista = db.OperatoriSinergia_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Cliente,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.TipoCliente,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;


                case "Permessi":
                    lista = db.Permessi_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Permesso,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

        

                case "PlafondUtente":
                    lista = db.PlafondUtente_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_PlannedPlafond_Archivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "Previsione":
                    lista = db.Previsione_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_PrevisioneArchivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "Professioni":
                    lista = db.Professioni_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Archivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.Codice,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "RelazionePraticheUtenti":
                    lista = db.RelazionePraticheUtenti_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Relazione_a,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.Ruolo,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "RelazioneUtenti":
                    lista = db.RelazioneUtenti_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Relazione,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.TipoRelazione,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "RimborsiPratica":
                    lista = db.RimborsiPratica_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_RimborsoArchivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "SettoriFornitori":
                    lista = db.SettoriFornitori_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Storico,
                        Data =x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.Nome,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                         NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "TipoRagioneSociale":
                    lista = db.TipoRagioneSociale_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Archivio,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.Descrizione,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "RicorrenzeCosti":
                    lista = db.RicorrenzeCosti_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.IDVersioneRicorrenza,
                        Data = x.DataCreazione, // Se DataArchiviazione è null, assegna 01/01/0001
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.Periodicita ?? "Ricorrenza",
                        NumeroVersione = x.NumeroVersione, // 0 se null
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(), // "" se null
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;

                case "Clienti":
                    lista = db.Clienti_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Cliente_a,
                        Data = x.DataArchiviazione ,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = x.RagioneSociale ?? "Modifica",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;

                case "DistribuzioneCostiTeam":
                    lista = db.DistribuzioneCostiTeam_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_DistribuzioneArchivio,
                        Data = x.DataArchiviazione ,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "Modifica",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;

                case "MembriTeam":
                    lista = db.MembriTeam_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_VersioneMembroTeam,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "Modifica",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;

                case "TeamProfessionisti":
                    lista = db.TeamProfessionisti_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_VersioneTeam,
                        Data = x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        TipoModifica = "Modifica",
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                            .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                    }).ToList();
                    break;

                case "AnagraficaCostiProfessionista":
                    lista = db.AnagraficaCostiProfessionista_a
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_AnagraficaCostoProfessionista_a,   // 🔑 chiave della tabella archivio
                            Data = x.DataUltimaModifica,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = x.Operazione ?? "Modifica",   // Inserimento / Modifica / Eliminazione
                            NumeroVersione = x.NumeroVersione ?? 0,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.HasValue
                                                        ? x.ID_UtenteArchiviazione.Value.ToString()
                                                        : null,
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .OrderByDescending(l => l.Data)
                        .ToList();
                    break;

                case "CostiGeneraliUtente":
                    lista = db.CostiGeneraliUtente_a
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.IDVersioneCostoGenerale,   // 🔑 chiave primaria archivio
                            Data = x.DataArchiviazione,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",        // qui non hai il campo Operazione → default
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .OrderByDescending(l => l.Data)
                        .ToList();
                    break;

                case "EccezioniRicorrenzeCosti":
                    lista = db.EccezioniRicorrenzeCosti_a
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Eccezione_a,                  // 🔑 chiave primaria archivio
                            Data = x.DataUltimaModifica,             // data archiviazione versione
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",              // non c’è campo Operazione, lo fisso
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .OrderByDescending(l => l.Data)
                        .ToList();
                    break;

                case "Finanziario":
                    lista = db.Finanziario_a
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_FinanziarioArchivio,         // 🔑 chiave archivio
                            Data = x.DataArchiviazione,            // data archiviazione
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = "Modifica",             // non c’è campo Operazione
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.HasValue
                                ? x.ID_UtenteArchiviazione.Value.ToString()
                                : null,
                            NomeUtente = x.ID_UtenteArchiviazione.HasValue
                                ? db.Utenti
                                    .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione.Value)
                                    .Select(u => u.Nome + " " + u.Cognome)
                                    .FirstOrDefault()
                                : "(utente non disponibile)"
                        })
                        .OrderByDescending(l => l.Data)
                        .ToList();
                    break;

                default:
                    return Json(new { success = false, message = "Tabella non riconosciuta." }, JsonRequestBehavior.AllowGet);
            }

            ViewBag.NomeTabella = nomeTabella;
            return PartialView("~/Views/LogModifiche/_GestioneModificheList.cshtml", lista);
        }


        public JsonResult GetTabelleArchivio()
        {
                        var tabelle = new List<SelectListItem>
                {
                    new SelectListItem { Text = "AnagraficaCostiPratica", Value = "AnagraficaCostiPratica_a" },
                    new SelectListItem { Text = "AnagraficaCostiProfessionista", Value = "AnagraficaCostiProfessionista_a" },
                    new SelectListItem { Text = "AnagraficaCostiTeam", Value = "AnagraficaCostiTeam_a" },
                    new SelectListItem { Text = "AvvisiParcella", Value = "AvvisiParcella_a" },
                    new SelectListItem { Text = "BilancioProfessionista", Value = "BilancioProfessionista" },
                    new SelectListItem { Text = "Clienti", Value = "Clienti_a" },
                    new SelectListItem { Text = "ClientiProfessionisti", Value = "ClientiProfessionisti_a" },
                    new SelectListItem { Text = "Cluster", Value = "Cluster_a" },
                    new SelectListItem { Text = "CompensiPratica", Value = "CompensiPratica_a" },
                    new SelectListItem { Text = "CompensiPraticaDettaglio", Value = "CompensiPraticaDettaglio_a" },
                    new SelectListItem { Text = "CostiGeneraliUtente", Value = "CostiGeneraliUtente_a" },
                    new SelectListItem { Text = "CostiPersonaliUtente", Value = "CostiPersonaliUtente_a" },
                    new SelectListItem { Text = "CostiPratica", Value = "CostiPratica_a" },
                    new SelectListItem { Text = "DatiBancari", Value = "DatiBancari_a" },
                    new SelectListItem { Text = "DistribuzioneCostiTeam", Value = "DistribuzioneCostiTeam_a" },
                    new SelectListItem { Text = "DocumentiAziende", Value = "DocumentiAziende_a" },
                    new SelectListItem { Text = "DocumentiPratiche", Value = "DocumentiPratiche_a" },
                    new SelectListItem { Text = "DocumentiProfessionisti", Value = "DocumentiProfessionisti_a" },
                    new SelectListItem { Text = "EccezioniRicorrenzeCosti", Value = "EccezioniRicorrenzeCosti_a" },
                    new SelectListItem { Text = "Economico", Value = "Economico_a" },
                    new SelectListItem { Text = "FinanziamentiProfessionisti", Value = "FinanziamentiProfessionisti_a" },
                    new SelectListItem { Text = "Finanziario", Value = "Finanziario_a" },
                    new SelectListItem { Text = "GenerazioneCosti", Value = "GenerazioneCosti" },
                    new SelectListItem { Text = "Incassi", Value = "Incassi_a" },
                    new SelectListItem { Text = "MembriTeam", Value = "MembriTeam_a" },
                    new SelectListItem { Text = "MovimentiBancari", Value = "MovimentiBancari_a" },
                    new SelectListItem { Text = "OperatoriSinergia", Value = "OperatoriSinergia_a" },
                    new SelectListItem { Text = "Permessi", Value = "Permessi_a" },
                    new SelectListItem { Text = "PlafondUtente", Value = "PlafondUtente_a" },
                    new SelectListItem { Text = "Pratiche", Value = "Pratiche_a" },
                    new SelectListItem { Text = "Previsione", Value = "Previsione_a" },
                    new SelectListItem { Text = "Professioni", Value = "Professioni_a" },
                    new SelectListItem { Text = "RelazionePraticheUtenti", Value = "RelazionePraticheUtenti_a" },
                    new SelectListItem { Text = "RelazioneUtenti", Value = "RelazioneUtenti_a" },
                    new SelectListItem { Text = "RicorrenzeCosti", Value = "RicorrenzeCosti_a" },
                    new SelectListItem { Text = "RimborsiPratica", Value = "RimborsiPratica_a" },
                    new SelectListItem { Text = "SettoriFornitori", Value = "SettoriFornitori_a" },
                    new SelectListItem { Text = "TeamProfessionisti", Value = "TeamProfessionisti_a" },
                    new SelectListItem { Text = "TemplateIncarichi", Value = "TemplateIncarichi_a" },
                    new SelectListItem { Text = "TipologieCosti", Value = "TipologieCosti_a" },
                    new SelectListItem { Text = "TipoRagioneSociale", Value = "TipoRagioneSociale_a" },
                    new SelectListItem { Text = "Utenti", Value = "Utenti_a" }
                };

            // 🔤 Ordina alfabeticamente per testo visualizzato
            var tabelleOrdinate = tabelle.OrderBy(t => t.Text).ToList();

            return Json(tabelleOrdinate, JsonRequestBehavior.AllowGet);
        }


        public ContentResult DettaglioModifica(string nomeTabella, int idArchivio)
        {
            string contenuto = "";

            switch (nomeTabella)
            {
                case "AnagraficaCostiPratica":
                    var ac = db.AnagraficaCostiPratica_a.FirstOrDefault(x => x.ID_AnagraficaCosto_a == idArchivio);
                    if (ac != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ac.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {ac.DataUltimaModifica:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ac.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {ac.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // qui non hai un campo Operazione, metto default
                            $"📝 Dettagli: {ac.ModificheTestuali}";
                    }
                    break;


                case "AvvisiParcella":
                    var ap = db.AvvisiParcella_a.FirstOrDefault(x => x.ID_Archivio == idArchivio);
                    if (ap != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ap.ID_UtenteModifica);
                        contenuto =
                            $"🗓 Data: {ap.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ap.ID_UtenteModifica})\n" +
                            $"🔢 Versione: {ap.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // non c’è un campo specifico, default
                            $"📝 Dettagli: {ap.ModificheTestuali}";
                    }
                    break;

                case "Clienti":
                    var cl = db.Clienti_a.FirstOrDefault(x => x.ID_Cliente_a == idArchivio);
                    if (cl != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == cl.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {cl.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {cl.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {cl.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // default perché non c’è un campo specifico
                            $"📝 Dettagli: {cl.ModificheTestuali}";
                    }
                    break;


                case "Cluster":
                    var clust = db.Cluster_a.FirstOrDefault(x => x.ID_Cluster_a == idArchivio);
                    if (clust != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == clust.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {clust.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {clust.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {clust.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // non esiste un campo Operazione → default
                            $"📝 Dettagli: {clust.ModificheTestuali}";
                    }
                    break;


                case "CompensiPratica":
                    var comp = db.CompensiPratica_a.FirstOrDefault(x => x.ID_CompensoArchivio == idArchivio);
                    if (comp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == comp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {comp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {comp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {comp.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // qui non hai un campo Operazione → default
                            $"📝 Dettagli: {comp.ModificheTestuali}";
                    }
                    break;


                case "CostiPersonaliUtente":
                    var cpu = db.CostiPersonaliUtente_a.FirstOrDefault(x => x.IDVersioneCostoPersonale == idArchivio);
                    if (cpu != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == cpu.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {cpu.DataUltimaModifica:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {cpu.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {cpu.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // non c’è campo Operazione → default
                            $"📝 Dettagli: {cpu.ModificheTestuali}";
                    }
                    break;


                case "AnagraficaCostiTeam":
                    var act = db.AnagraficaCostiTeam_a.FirstOrDefault(x => x.IDVersioneAnagraficaCostoTeam == idArchivio);
                    if (act != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == act.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {act.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {act.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {act.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // non c’è campo Operazione → default
                            $"📝 Dettagli: {act.ModificheTestuali}";
                    }
                    break;

                case "AnagraficaCostiProfessionista":
                    var acp = db.AnagraficaCostiProfessionista_a.FirstOrDefault(x => x.ID_AnagraficaCostoProfessionista_a == idArchivio);
                    if (acp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == acp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {acp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {acp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {acp.NumeroVersione}\n" +
                            $"⚙️ Operazione: {acp.Operazione ?? "Modifica"}\n" +
                            $"📝 Dettagli: {acp.ModificheTestuali}";
                    }
                    break;

                case "BilancioProfessionista":
                    var bil = db.BilancioProfessionista.FirstOrDefault(x => x.ID_Bilancio == idArchivio);
                    if (bil != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == bil.ID_UtenteInserimento);
                        contenuto =
                            $"🗓 Data: {bil.DataRegistrazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Registrato da: {utente?.Nome} {utente?.Cognome} (ID {bil.ID_UtenteInserimento})\n" +
                            $"💰 Importo: {bil.Importo:N2} €\n" +
                            $"🏷 Tipo Movimento: {bil.TipoVoce}\n" +
                            $"📂 Categoria: {bil.Categoria}\n" +
                            $"📝 Descrizione: {bil.Descrizione}";
                    }
                    break;


                case "ClientiProfessionisti":
                    var Cp = db.ClientiProfessionisti_a.FirstOrDefault(x => x.ID_ClientiProfessionisti_a == idArchivio);
                    if (Cp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == Cp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {Cp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {Cp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {Cp.NumeroVersione}\n" +
                            $"👥 ID Cliente: {Cp.ID_Cliente}\n" +
                            $"👤 ID Professionista: {Cp.ID_Professionista}\n" +
                            $"📝 Dettagli: {Cp.ModificheTestuali}";
                    }
                    break;

                case "CompensiPraticaDettaglio":
                    var cpd = db.CompensiPraticaDettaglio_a.FirstOrDefault(x => x.ID_RigaCompenso_a == idArchivio);
                    if (cpd != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == cpd.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {cpd.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {cpd.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {cpd.NumeroVersione}\n" +
                            $"💼 ID Compenso: {cpd.ID_RigaCompenso_a}\n" +
                            $"💰 Importo: {cpd.Importo:N2} €\n" +
                            $"📝 Dettagli: {cpd.ModificheTestuali}";
                    }
                    break;

                case "DocumentiProfessionisti":
                    var docp = db.DocumentiProfessionisti_a.FirstOrDefault(x => x.ID_DocumentoArchivio == idArchivio);
                    if (docp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == docp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {docp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {docp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {docp.NumeroVersione}\n" +
                            $"📄 Nome File: {docp.NomeDocumento ?? "N/D"}\n" +
                            $"📝 Dettagli: {docp.ModificheTestuali}";
                    }
                    break;


                case "CostiGeneraliUtente":
                    var cgu = db.CostiGeneraliUtente_a.FirstOrDefault(x => x.IDVersioneCostoGenerale == idArchivio);
                    if (cgu != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == cgu.ID_UtenteArchiviazione);
                        var professionista = db.Utenti.FirstOrDefault(u => u.ID_Utente == cgu.ID_Utente);

                        string nomeProfessionista = professionista != null
                            ? $"{professionista.Nome} {professionista.Cognome} (ID {professionista.ID_Utente})"
                            : $"ID {cgu.ID_Utente}";

                        contenuto =
                            $"🗓 Data: {cgu.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {cgu.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {cgu.NumeroVersione}\n" +
                            $"📝 Dettagli: {cgu.ModificheTestuali?.Replace($"professionista {cgu.ID_Utente}", $"professionista {nomeProfessionista}")}";
                    }
                    break;

                case "GenerazioneCosti":
                    var gc = db.GenerazioneCosti.FirstOrDefault(x => x.ID_GenerazioneCosto == idArchivio);
                    if (gc != null)
                    {
                        int idUtenteRiferimento = gc.ID_UtenteUltimaModifica ?? gc.ID_UtenteCreatore ?? 0;
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteRiferimento);
                        var professionista = gc.ID_Utente.HasValue
                            ? db.Utenti.FirstOrDefault(u => u.ID_Utente == gc.ID_Utente)
                            : null;

                        string nomeProfessionista = professionista != null
                            ? $"{professionista.Nome} {professionista.Cognome} (ID {professionista.ID_Utente})"
                            : (gc.ID_Utente.HasValue ? $"ID {gc.ID_Utente}" : "N/D");

                        contenuto =
                            $"🗓 Data: {(gc.DataUltimaModifica ?? gc.DataCreazione):dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {(utente != null ? utente.Nome + " " + utente.Cognome : "N/D")} (ID {idUtenteRiferimento})\n" +
                            $"🏷 Origine: {gc.Origine ?? "N/D"}\n" +
                            $"📂 Categoria: {gc.Categoria ?? "N/D"}\n" +
                            $"📝 Descrizione: {gc.Descrizione ?? "N/D"}\n" +
                            $"💰 Importo: {(gc.Importo ?? 0).ToString("N2")} €\n" +
                            $"⚙️ Stato: {(gc.Approvato == true ? "Approvato" : "Previsionale")}\n" +
                            $"👥 Professionista: {nomeProfessionista}\n" +
                            $"📝 Dettagli: Generazione automatica del costo ({(gc.Approvato == true ? "Approvato" : "In attesa di verifica")})";
                    }
                    break;
 


                case "EccezioniRicorrenzeCosti":
                    var ecc = db.EccezioniRicorrenzeCosti_a.FirstOrDefault(x => x.ID_Eccezione_a == idArchivio);
                    if (ecc != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ecc.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {ecc.DataUltimaModifica:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ecc.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {ecc.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +  // non ha campo Operazione → fisso "Modifica"
                            $"📝 Dettagli: {ecc.ModificheTestuali}";
                    }
                    break;


                case "CostiPratica":
                    var cp = db.CostiPratica_a.FirstOrDefault(x => x.ID_CostoPratica_Archivio == idArchivio);
                    if (cp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == cp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {cp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {cp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {cp.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +   // qui non c’è campo Operazione → imposto "Modifica"
                            $"📝 Dettagli: {cp.ModificheTestuali}";
                    }
                    break;


                case "DatiBancari":
                    var dbanc = db.DatiBancari_a.FirstOrDefault(x => x.ID_DatoBancario == idArchivio);
                    if (dbanc != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == dbanc.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {dbanc.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {dbanc.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {dbanc.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +  // non c’è campo Operazione → default "Modifica"
                            $"📝 Dettagli: {dbanc.ModificheTestuali}";
                    }
                    break;


                case "DistribuzioneCostiTeam":
                    var dct = db.DistribuzioneCostiTeam_a.FirstOrDefault(x => x.ID_DistribuzioneArchivio == idArchivio);
                    if (dct != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == dct.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {dct.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {dct.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {dct.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" +  // non c’è campo Operazione, imposto default
                            $"📝 Dettagli: {dct.ModificheTestuali}";
                    }
                    break;


                case "DocumentiAziende":
                    var daz = db.DocumentiAziende_a.FirstOrDefault(x => x.ID_Documento_A == idArchivio);
                    if (daz != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == daz.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {daz.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {daz.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {daz.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non c’è campo Operazione, uso default
                            $"📝 Dettagli: {daz.ModificheTestuali}";
                    }
                    break;

                case "DocumentiPratiche":
                    var dpr = db.DocumentiPratiche_a.FirstOrDefault(x => x.ID_Documento_a == idArchivio);
                    if (dpr != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == dpr.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {dpr.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {dpr.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {dpr.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non esiste campo Operazione, imposto default
                            $"📝 Dettagli: {dpr.ModificheTestuali}";
                    }
                    break;


                case "Economico":
                    var eco = db.Economico_a.FirstOrDefault(x => x.ID_EconomicoArchivio == idArchivio);
                    if (eco != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == eco.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {eco.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {eco.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {eco.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non c’è un campo Operazione → default
                            $"📝 Dettagli: {eco.ModificheTestuali}";
                    }
                    break;


                case "FinanziamentiProfessionisti":
                    var fin = db.FinanziamentiProfessionisti_a.FirstOrDefault(x => x.ID_Finanziamento_Archivio == idArchivio);
                    if (fin != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == fin.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {fin.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {fin.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {fin.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // fisso a Modifica, non c’è campo Operazione
                            $"📝 Dettagli: {fin.ModificheTestuali}";
                    }
                    break;


                case "Finanziario":
                    var fina = db.Finanziario_a.FirstOrDefault(x => x.ID_FinanziarioArchivio == idArchivio);
                    if (fina != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == fina.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {fina.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {fina.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {fina.NumeroVersione}\n" +
                            $"📝 Dettagli: {fina.ModificheTestuali}";
                    }
                    break;


                case "Incassi":
                    var inc = db.Incassi_a.FirstOrDefault(x => x.ID_Archivio == idArchivio);
                    if (inc != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == inc.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {inc.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {inc.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {inc.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non c’è campo Operazione
                            $"📝 Dettagli: {inc.ModificheTestuali}";
                    }
                    break;


                case "MembriTeam":
                    var mt = db.MembriTeam_a.FirstOrDefault(x => x.ID_VersioneMembroTeam == idArchivio);
                    if (mt != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == mt.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {mt.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {mt.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {mt.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non ha campo Operazione
                            $"📝 Dettagli: {mt.ModificheTestuali}";
                    }
                    break;


                case "MovimentiBancari":
                    var mb = db.MovimentiBancari_a.FirstOrDefault(x => x.ID_Movimento == idArchivio);
                    if (mb != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == mb.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {mb.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {mb.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {mb.NumeroVersione}\n" +
                            $"⚙️ Operazione: {"Modifica"}\n" + // non ha campo Operazione
                            $"📝 Dettagli: {mb.ModificheTestuali}";
                    }
                    break;

                case "OperatoriSinergia":
                    var os = db.OperatoriSinergia_a.FirstOrDefault(x => x.ID_Cliente == idArchivio);
                    if (os != null)
                    {
                        var utenteArchiviazione = db.Utenti.FirstOrDefault(u => u.ID_Utente == os.ID_UtenteArchiviazione);

                        // Recupero del nome completo in base al tipo cliente
                        Func<int, string> GetNomeCliente = (id) =>
                        {
                            var cli = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Cliente == id);
                            if (cli == null) return $"ID {id}";

                            if (cli.TipoCliente == "Professionista")
                                return $"{cli.Nome} {cli.Cognome} (ID {cli.ID_Cliente})";
                            if (cli.TipoCliente == "Azienda")
                                return !string.IsNullOrEmpty(cli.TipoRagioneSociale)
                                    ? $"{cli.TipoRagioneSociale} {cli.Nome} (ID {cli.ID_Cliente})"
                                    : $"{cli.Nome} (ID {cli.ID_Cliente})";

                            return $"{cli.Nome} {cli.Cognome} (ID {cli.ID_Cliente})";
                        };

                        string dettagli = os.ModificheTestuali ?? "";

                        // 🔄 sostituzione ID_Cliente
                        var regexCliente = new System.Text.RegularExpressions.Regex(@"ID_Cliente\s*=\s*(\d+)");
                        dettagli = regexCliente.Replace(dettagli, match =>
                        {
                            int idCli;
                            if (int.TryParse(match.Groups[1].Value, out idCli))
                                return $"Cliente: {GetNomeCliente(idCli)}";
                            return match.Value;
                        });

                        // 🔄 sostituzione ID_Professionista
                        var regexProf = new System.Text.RegularExpressions.Regex(@"ID_Professionista\s*=\s*(\d+)");
                        dettagli = regexProf.Replace(dettagli, match =>
                        {
                            int idProf;
                            if (int.TryParse(match.Groups[1].Value, out idProf))
                                return $"Professionista: {GetNomeCliente(idProf)}";
                            return match.Value;
                        });

                        contenuto =
                            $"🗓 Data: {os.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utenteArchiviazione?.Nome} {utenteArchiviazione?.Cognome} (ID {os.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {os.NumeroVersione}\n" +
                            $"🏷 Tipo Cliente: {os.TipoCliente}\n" +
                            $"📝 Dettagli: {dettagli}";
                    }
                    break;


                //case "OrdiniFornitori":
                //    contenuto = db.OrdiniFornitori_a
                //        .FirstOrDefault(x => x.ID_Ordine == idArchivio)?.ModificheTestuali;
                //    break;

                case "Permessi":
                    var perm = db.Permessi_a.FirstOrDefault(x => x.ID_Permesso == idArchivio);
                    if (perm != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == perm.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {perm.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {perm.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {perm.NumeroVersione}\n" +
                            $"📝 Dettagli: {perm.ModificheTestuali}";
                    }
                    break;


                //case "PermessiDelegatiProfessionista":
                //    contenuto = db.PermessiDelegabiliPerProfessionista_a
                //        .FirstOrDefault(x => x.ID_PermessiDelegabiliPerProfessionista_a == idArchivio)?.ModificheTestuali;
                //    break;

                case "PlafondUtente":
                    var plaf = db.PlafondUtente_a.FirstOrDefault(x => x.ID_PlannedPlafond_Archivio == idArchivio);
                    if (plaf != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == plaf.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {plaf.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {plaf.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {plaf.NumeroVersione}\n" +
                            $"📝 Dettagli: {plaf.ModificheTestuali}";
                    }
                    break;

                case "Pratiche":
                    var pr = db.Pratiche_a.FirstOrDefault(x => x.ID_Pratiche_a == idArchivio);
                    if (pr != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == pr.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {(pr.DataArchiviazione ?? pr.UltimaModifica ?? pr.DataCreazione):dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {pr.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {pr.NumeroVersione}\n" +
                            $"📌 Stato: {pr.Stato}\n" +
                            $"📝 Dettagli: {pr.ModificheTestuali}";
                    }
                    break;


                case "Previsione":
                    var prev = db.Previsione_a.FirstOrDefault(x => x.ID_PrevisioneArchivio == idArchivio);
                    if (prev != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == prev.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {prev.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {prev.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {prev.NumeroVersione}\n" +
                            $"📝 Dettagli: {prev.ModificheTestuali}";
                    }
                    break;


                case "Professioni":
                    var prof = db.Professioni_a.FirstOrDefault(x => x.ID_Archivio == idArchivio);
                    if (prof != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == prof.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {prof.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {prof.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {prof.NumeroVersione}\n" +
                            $"⚙️ Tipo: {prof.Codice}\n" +
                            $"📝 Dettagli: {prof.ModificheTestuali}";
                    }
                    break;


                case "RelazionePraticheUtenti":
                    var rpu = db.RelazionePraticheUtenti_a.FirstOrDefault(x => x.ID_Relazione_a == idArchivio);
                    if (rpu != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == rpu.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {rpu.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {rpu.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {rpu.NumeroVersione}\n" +
                            $"⚙️ Ruolo: {rpu.Ruolo}\n" +
                            $"📝 Dettagli: {rpu.ModificheTestuali}";
                    }
                    break;


                case "RelazioneUtenti":
                    var ru = db.RelazioneUtenti_a.FirstOrDefault(x => x.ID_Relazione == idArchivio);
                    if (ru != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ru.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {ru.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ru.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {ru.NumeroVersione}\n" +
                            $"🤝 Tipo relazione: {ru.TipoRelazione}\n" +
                            $"📝 Dettagli: {ru.ModificheTestuali}";
                    }
                    break;


                case "RicorrenzeCosti":
                    var rc = db.RicorrenzeCosti_a.FirstOrDefault(x => x.IDVersioneRicorrenza == idArchivio);
                    if (rc != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == rc.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {rc.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {rc.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {rc.NumeroVersione}\n" +
                            $"📆 Periodicità: {rc.Periodicita ?? "N/D"}\n" +
                            $"📝 Dettagli: {rc.ModificheTestuali}";
                    }
                    break;


                case "RimborsiPratica":
                    var rp = db.RimborsiPratica_a.FirstOrDefault(x => x.ID_RimborsoArchivio == idArchivio);
                    if (rp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == rp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {rp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {rp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {rp.NumeroVersione}\n" +
                            $"📝 Dettagli: {rp.ModificheTestuali}";
                    }
                    break;


                case "SettoriFornitori":
                    var sf = db.SettoriFornitori_a.FirstOrDefault(x => x.ID_Storico == idArchivio);
                    if (sf != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == sf.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {sf.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {sf.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {sf.NumeroVersione}\n" +
                            $"📝 Dettagli: {sf.ModificheTestuali}";
                    }
                    break;


                case "TeamProfessionisti":
                    var tp = db.TeamProfessionisti_a.FirstOrDefault(x => x.ID_VersioneTeam == idArchivio);
                    if (tp != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == tp.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {tp.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {tp.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {tp.NumeroVersione}\n" +
                            $"📝 Dettagli: {tp.ModificheTestuali}";
                    }
                    break;


                case "TemplateIncarichi":
                    var ti = db.TemplateIncarichi_a.FirstOrDefault(x => x.ID_Archivio == idArchivio);
                    if (ti != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ti.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {ti.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ti.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {ti.NumeroVersione}\n" +
                            $"📝 Dettagli: {ti.ModificheTestuali}";
                    }
                    break;


                case "TipologieCosti":
                    var tc = db.TipologieCosti_a.FirstOrDefault(x => x.ID_Storico == idArchivio);
                    if (tc != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == tc.ID_UtenteUltimaModifica);
                        contenuto =
                            $"🗓 Data: {tc.DataUltimaModifica:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {tc.ID_UtenteUltimaModifica})\n" +
                            $"🔢 Versione: {tc.NumeroVersione}\n" +
                            $"⚙️ Tipo: {tc.Tipo}\n" +
                            $"📝 Dettagli: {tc.ModificheTestuali}";
                    }
                    break;


                case "TipoRagioneSociale":
                    var trs = db.TipoRagioneSociale_a.FirstOrDefault(x => x.ID_Archivio == idArchivio);
                    if (trs != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == trs.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {trs.DataArchiviazione:dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {trs.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {trs.NumeroVersione}\n" +
                            $"⚙️ Descrizione: {trs.Descrizione}\n" +
                            $"📝 Dettagli: {trs.ModificheTestuali}";
                    }
                    break;


                case "Utenti":
                    var ut = db.Utenti_a.FirstOrDefault(x => x.IDVersioneUtenti == idArchivio);
                    if (ut != null)
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == ut.ID_UtenteArchiviazione);
                        contenuto =
                            $"🗓 Data: {(ut.DataArchiviazione ?? ut.UltimaModifica ?? ut.DataCreazione):dd/MM/yyyy HH:mm}\n" +
                            $"👤 Modificato da: {utente?.Nome} {utente?.Cognome} (ID {ut.ID_UtenteArchiviazione})\n" +
                            $"🔢 Versione: {ut.NumeroVersione}\n" +
                            $"📝 Dettagli: {ut.ModificheTestuali}";
                    }
                    break;


                default:
                    contenuto = "⚠️ Tabella non gestita o record non trovato.";
                    break;
            }

            contenuto = string.IsNullOrWhiteSpace(contenuto)? "Nessuna modifica registrata." : contenuto;

            return Content($"<pre>{contenuto}</pre>");
        }



        // LOG MODIFICHE DETTAGLIATO 
        [HttpGet]
        public JsonResult GetStoricoCliente(int idCliente)
        {
            // Recupero l’ultima versione archiviata del cliente
            var precedente = db.Clienti_a
                .Where(c => c.ID_Cliente_Originale == idCliente) // 👈 Attenzione: deve essere l'ID originale, non l'archivio stesso
                .OrderByDescending(c => c.DataArchiviazione)
                .FirstOrDefault();

            if (precedente == null)
                return Json(new { success = false, message = "Nessuna versione precedente trovata." }, JsonRequestBehavior.AllowGet);

            // Recupero l'operatore associato (professionista o azienda)
            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_Cliente == precedente.ID_Operatore);

            // Popoliamo il modello storico
            var model = new StoricoGenericoViewModel
            {
                ModificheTestuali = precedente.ModificheTestuali,
                NumeroVersione = precedente.NumeroVersione,
                ID_UtenteUltimaModifica = precedente.ID_UtenteArchiviazione.ToString(),
                NomeUtente = db.Utenti
                    .Where(u => u.ID_Utente == precedente.ID_UtenteArchiviazione)
                    .Select(u => u.Nome + " " + u.Cognome)
                    .FirstOrDefault(),
                DataUltimaModifica = precedente.DataArchiviazione 
            };

            // 🔹 Campi anagrafici del cliente
            model.AggiungiCampo("Nome", precedente.Nome);
            model.AggiungiCampo("Cognome", precedente.Cognome);
            model.AggiungiCampo("Ragione Sociale", precedente.RagioneSociale);
            model.AggiungiCampo("Codice Fiscale", precedente.CodiceFiscale);
            model.AggiungiCampo("Partita IVA", precedente.PIVA);
            model.AggiungiCampo("Indirizzo", precedente.Indirizzo);
            model.AggiungiCampo("Telefono", precedente.Telefono);
            model.AggiungiCampo("Email", precedente.Email);
            model.AggiungiCampo("Note", precedente.Note);
            model.AggiungiCampo("Stato", precedente.Stato);

            // ✅ Risolvo Nazione e Città
            string nomeNazione = db.Nazioni
                .Where(n => n.ID_BPCittaDN == precedente.ID_Nazione)
                .Select(n => n.NameNazione)
                .FirstOrDefault();

            string nomeCitta = db.Citta
                .Where(c => c.ID_BPCitta == precedente.ID_Citta)
                .Select(c => c.NameLocalita)
                .FirstOrDefault();

            model.AggiungiCampo("Nazione", nomeNazione ?? "");
            model.AggiungiCampo("Citta", nomeCitta ?? "");

            // 🔹 Operatore collegato (professionista o azienda)
            if (operatore != null)
            {
                string descrizioneOperatore = $"{operatore.Nome} ({operatore.TipoCliente})";
                model.AggiungiCampo("Operatore Assegnato", descrizioneOperatore);
            }

            return Json(new { success = true, storico = model }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStoricoUtente(int idUtente)
        {
            // Recupero ultima versione archiviata dell'utente
            var precedente = db.Utenti_a
                .Where(u => u.ID_UtenteOriginale == idUtente)
                .OrderByDescending(u => u.DataArchiviazione)
                .FirstOrDefault();

            if (precedente == null)
                return Json(new { success = false, message = "Nessuna versione precedente trovata." }, JsonRequestBehavior.AllowGet);

            var model = new StoricoGenericoViewModel
            {
                ModificheTestuali = precedente.ModificheTestuali,
                NumeroVersione = precedente.NumeroVersione,
                ID_UtenteUltimaModifica = precedente.ID_UtenteUltimaModifica?.ToString(),
                NomeUtente = db.Utenti
                    .Where(x => x.ID_Utente == precedente.ID_UtenteArchiviazione)
                    .Select(x => x.Nome + " " + x.Cognome)
                    .FirstOrDefault(),
                DataUltimaModifica = precedente.DataArchiviazione ?? DateTime.MinValue
            };

            // 🔹 Campi anagrafici utente (dinamici)
            model.AggiungiCampo("Nome", precedente.Nome);
            model.AggiungiCampo("Cognome", precedente.Cognome);
            model.AggiungiCampo("Codice Fiscale", precedente.CodiceFiscale);
            model.AggiungiCampo("Partita IVA", precedente.PIVA);
            model.AggiungiCampo("Codice Univoco", precedente.CodiceUnivoco);
            model.AggiungiCampo("Telefono", precedente.Telefono);
            model.AggiungiCampo("Cellulare 1", precedente.Cellulare1);
            model.AggiungiCampo("Cellulare 2", precedente.Cellulare2);
            model.AggiungiCampo("Email 1", precedente.MAIL1);
            model.AggiungiCampo("Email 2", precedente.MAIL2);
            model.AggiungiCampo("Stato", precedente.Stato);
            model.AggiungiCampo("Descrizione Attività", precedente.DescrizioneAttivita);
            model.AggiungiCampo("Note", precedente.Note);
            model.AggiungiCampo("Indirizzo", precedente.Indirizzo);
            model.AggiungiCampo("Ruolo", precedente.Ruolo);

            // ✅ Risolvo Nazione e Città dai rispettivi ID
            string nomeNazione = db.Nazioni
                .Where(n => n.ID_BPCittaDN == precedente.ID_Nazione)
                .Select(n => n.NameNazione)
                .FirstOrDefault();

            string nomeCitta = db.Citta
                .Where(c => c.ID_BPCitta == precedente.ID_CittaResidenza)
                .Select(c => c.NameLocalita)
                .FirstOrDefault();

            model.AggiungiCampo("Nazione", nomeNazione ?? "");
            model.AggiungiCampo("Citta", nomeCitta ?? "");

            return Json(new { success = true, storico = model }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStoricoProfessionista(int idProfessionista)
        {
            // Recupero il record attuale del professionista
            var professionista = db.OperatoriSinergia
                .FirstOrDefault(p => p.ID_Cliente == idProfessionista && p.TipoCliente == "Professionista");

            if (professionista == null)
                return Json(new { success = false, message = "Professionista non trovato." }, JsonRequestBehavior.AllowGet);

            // Qui uso l'archivio -> filtro per ID_ClienteOriginale = id corrente
            var precedente = db.OperatoriSinergia_a
                .Where(p => p.ID_ClienteOriginale == idProfessionista && p.TipoCliente == "Professionista")
                .OrderByDescending(p => p.DataArchiviazione)
                .FirstOrDefault();

            if (precedente == null)
                return Json(new { success = false, message = "Nessuna versione precedente trovata." }, JsonRequestBehavior.AllowGet);

            // Recupero descrizione città e nazione
            string nomeCitta = precedente.ID_Citta.HasValue
                ? db.Citta.Where(c => c.ID_BPCitta == precedente.ID_Citta.Value)
                          .Select(c => c.NameLocalita)
                          .FirstOrDefault()
                : null;

            string nomeNazione = precedente.ID_Nazione.HasValue
                ? db.Nazioni.Where(n => n.ID_BPCittaDN == precedente.ID_Nazione.Value)
                            .Select(n => n.NameNazione)
                            .FirstOrDefault()
                : null;

            var model = new StoricoGenericoViewModel
            {
                ModificheTestuali = precedente.ModificheTestuali,
                NumeroVersione = precedente.NumeroVersione,
                ID_UtenteUltimaModifica = precedente.ID_UtenteArchiviazione?.ToString(),
                NomeUtente = db.Utenti
                    .Where(x => x.ID_Utente == precedente.ID_UtenteArchiviazione)
                    .Select(x => x.Nome + " " + x.Cognome)
                    .FirstOrDefault(),
                DataUltimaModifica = precedente.DataArchiviazione ?? DateTime.MinValue
            };

            // 🔹 Campi anagrafici
            model.AggiungiCampo("Nome", precedente.Nome);
            model.AggiungiCampo("Cognome", precedente.Cognome);
            model.AggiungiCampo("Tipo Ragione Sociale", precedente.TipoRagioneSociale);
            model.AggiungiCampo("Partita IVA", precedente.PIVA);
            model.AggiungiCampo("Codice Fiscale", precedente.CodiceFiscale);
            model.AggiungiCampo("Codice Univoco", precedente.CodiceUnivoco);
            model.AggiungiCampo("Indirizzo", precedente.Indirizzo);
            model.AggiungiCampo("Telefono", precedente.Telefono);
            model.AggiungiCampo("Email 1", precedente.MAIL1);
            model.AggiungiCampo("Email 2", precedente.MAIL2);
            model.AggiungiCampo("Stato", precedente.Stato);
            model.AggiungiCampo("Descrizione Attività", precedente.DescrizioneAttivita);
            model.AggiungiCampo("Note", precedente.Note);
            model.AggiungiCampo("Città", nomeCitta ?? "");
            model.AggiungiCampo("Nazione", nomeNazione ?? "");

            return Json(new { success = true, storico = model }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetStoricoFornitore(int idAzienda)
        {
            // Recupero ultima versione archiviata del fornitore (Azienda)
            var precedente = db.OperatoriSinergia_a
                .Where(p => p.ID_ClienteOriginale == idAzienda && p.TipoCliente == "Azienda")
                .OrderByDescending(p => p.DataArchiviazione)
                .FirstOrDefault();

            if (precedente == null)
                return Json(new { success = false, message = "Nessuna versione precedente trovata." }, JsonRequestBehavior.AllowGet);

            // Recupero descrizione città e nazione
            string nomeCitta = null;
            if (precedente.ID_Citta.HasValue)
            {
                nomeCitta = db.Citta
                    .Where(c => c.ID_BPCitta == precedente.ID_Citta.Value)
                    .Select(c => c.NameLocalita)
                    .FirstOrDefault();
            }

            string nomeNazione = null;
            if (precedente.ID_Nazione.HasValue)
            {
                nomeNazione = db.Nazioni
                    .Where(n => n.ID_BPCittaDN == precedente.ID_Nazione.Value)
                    .Select(n => n.NameNazione)
                    .FirstOrDefault();
            }

            // Recupero Settore fornitore
            string nomeSettore = null;
            if (precedente.ID_SettoreFornitore.HasValue)
            {
                nomeSettore = db.SettoriFornitori
                    .Where(s => s.ID_Settore == precedente.ID_SettoreFornitore.Value)
                    .Select(s => s.Nome)
                    .FirstOrDefault();
            }

            // ViewModel generico storico
            var model = new StoricoGenericoViewModel
            {
                ModificheTestuali = precedente.ModificheTestuali,
                NumeroVersione = precedente.NumeroVersione,
                ID_UtenteUltimaModifica = precedente.ID_UtenteArchiviazione?.ToString(),
                NomeUtente = db.Utenti
                    .Where(x => x.ID_Utente == precedente.ID_UtenteArchiviazione)
                    .Select(x => x.Nome + " " + x.Cognome)
                    .FirstOrDefault(),
                DataUltimaModifica = precedente.DataArchiviazione ?? DateTime.MinValue
            };

            // 🔹 Campi anagrafici azienda
            model.AggiungiCampo("Nome", precedente.Nome);
            model.AggiungiCampo("Tipo Ragione Sociale", precedente.TipoRagioneSociale);
            model.AggiungiCampo("Partita IVA", precedente.PIVA);
            model.AggiungiCampo("Codice Fiscale", precedente.CodiceFiscale);
            model.AggiungiCampo("Codice Univoco", precedente.CodiceUnivoco);
            model.AggiungiCampo("Telefono", precedente.Telefono);
            model.AggiungiCampo("Email", precedente.MAIL1);
            model.AggiungiCampo("PEC", precedente.MAIL2); // 👈 PEC su MAIL2
            model.AggiungiCampo("Sito Web", precedente.SitoWEB);
            model.AggiungiCampo("Stato", precedente.Stato);
            model.AggiungiCampo("Descrizione Attività", precedente.DescrizioneAttivita);
            model.AggiungiCampo("Note", precedente.Note);

            // 🔹 Città / Nazione
            model.AggiungiCampo("Città", nomeCitta ?? "");
            model.AggiungiCampo("Nazione", nomeNazione ?? "");

            // 🔹 Settore fornitore
            model.AggiungiCampo("Settore Fornitore", nomeSettore ?? "");

            return Json(new { success = true, storico = model }, JsonRequestBehavior.AllowGet);
        }







        #endregion

        #region NOTIFICHE

        /* ============================================================
           🔐 GESTIONE VISIBILITÀ NOTIFICHE
           ============================================================ */

        private bool IsAdminUser()
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            var utenteCollegato = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCollegato);
            return utenteCollegato?.TipoUtente == "Admin";
        }

        /// <summary>
        /// Restituisce l'ID del cliente professionista corrente.
        /// Se l'utente è collegato come collaboratore, risale a ID_Cliente passando per Utenti → OperatoriSinergia.
        /// Se non trova nulla, ritorna l'ID_Utente stesso.
        /// </summary>
        public int GetIDClienteCorrente(int idUtenteCollegato)
        {
            var idCliente = (from u in db.Utenti
                             join os in db.OperatoriSinergia on u.ID_Utente equals os.ID_UtenteCollegato
                             where u.ID_Utente == idUtenteCollegato
                             select os.ID_Cliente).FirstOrDefault();

            return idCliente > 0 ? idCliente : idUtenteCollegato;
        }

        private IQueryable<Notifiche> ApplyNotificaVisibility(IQueryable<Notifiche> query, int idUtenteCollegato, bool isAdmin)
        {
            int idClienteCorrente = GetIDClienteCorrente(idUtenteCollegato);

            if (isAdmin && idClienteCorrente == idUtenteCollegato)
                return query; // Admin non impersonificato → vede tutto

            if (isAdmin)
                return query.Where(n => n.ID_Utente == idClienteCorrente || n.ID_Utente == idUtenteCollegato);

            return query.Where(n => n.ID_Utente == idClienteCorrente);
        }

        /* ============================================================
           📋 LISTA / DETTAGLIO / CRUD NOTIFICHE
           ============================================================ */

        [HttpGet]
        public ActionResult NotificheList(DateTime? da, DateTime? a, string stato = "Tutte", string tipo = "Tutti", string q = "", int page = 1, int pageSize = 50)
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            if (idUtenteCollegato <= 0) return new HttpStatusCodeResult(401);

            bool isAdmin = IsAdminUser();
            DateTime inizio = (da?.Date) ?? DateTime.Today.AddMonths(-1).Date;
            DateTime fine = ((a?.Date) ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = db.Notifiche.Where(n => n.DataCreazione >= inizio && n.DataCreazione <= fine);
            query = ApplyNotificaVisibility(query, idUtenteCollegato, isAdmin);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(n => n.Titolo.Contains(q) || n.Descrizione.Contains(q));

            if (!string.IsNullOrEmpty(stato) && stato != "Tutte")
                query = query.Where(n => n.Stato == stato);

            if (!string.IsNullOrEmpty(tipo) && tipo != "Tutti")
                query = query.Where(n => n.Tipo == tipo);

            var skip = Math.Max(0, (page - 1) * pageSize);

            var list = query
                .OrderByDescending(n => n.DataCreazione)
                .Skip(skip).Take(pageSize)
                .Select(n => new NotificaViewModel
                {
                    ID_Notifica = n.ID_Notifica,
                    Titolo = n.Titolo,
                    Descrizione = n.Descrizione,
                    DataCreazione = n.DataCreazione,
                    DataLettura = n.DataLettura,
                    ID_Utente = n.ID_Utente,
                    Tipo = n.Tipo,
                    Stato = n.Stato,
                    Contatore = n.Contatore,
                    Letto = n.Letto,
                    LinkPratica = n.ID_Pratiche != null ? ("/Pratiche/DettaglioPratica/" + n.ID_Pratiche) : null
                })
                .ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;

            return PartialView("~/Views/Notifiche/_NotificheList.cshtml", list);
        }

        [HttpGet]
        public JsonResult GetDettaglioNotifica(int id, bool autoMark = true)
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            if (idUtenteCollegato <= 0)
                return Json(new { success = false, message = "Non autorizzato." }, JsonRequestBehavior.AllowGet);

            bool isAdmin = IsAdminUser();
            var visQuery = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);

            var entity = visQuery.FirstOrDefault(x => x.ID_Notifica == id);
            if (entity == null)
                return Json(new { success = false, message = "Notifica non trovata." }, JsonRequestBehavior.AllowGet);

            if (autoMark && !entity.Letto)
            {
                entity.Letto = true;
                entity.DataLettura = DateTime.Now;
                entity.Stato = "Letta";
                db.SaveChanges();
            }

            string praticaTitolo = null;
            if (entity.ID_Pratiche.HasValue)
                praticaTitolo = db.Pratiche
                                  .Where(p => p.ID_Pratiche == entity.ID_Pratiche.Value)
                                  .Select(p => p.Titolo)
                                  .FirstOrDefault();

            var dettaglio = new
            {
                entity.ID_Notifica,
                entity.Titolo,
                entity.Descrizione,
                entity.DataCreazione,
                entity.DataLettura,
                entity.ID_Utente,
                entity.Tipo,
                entity.Stato,
                entity.Contatore,
                entity.Letto,
                Pratica = praticaTitolo ?? "-",
                LinkPratica = entity.ID_Pratiche != null
                    ? ("/Pratiche/DettaglioPratica/" + entity.ID_Pratiche)
                    : null
            };

            return Json(new { success = true, dettaglio }, JsonRequestBehavior.AllowGet);
        }

        /* ============================================================
           🧩 FACTORY CENTRALE
           ============================================================ */

        private void AddNotifica(string titolo, string descrizione, string tipoCodice, int idDestinatarioUtente, int? idPratica = null)
        {
            db.Notifiche.Add(new Notifiche
            {
                Titolo = titolo,
                Descrizione = descrizione,
                Tipo = tipoCodice,
                Stato = "Non letta",
                ID_Utente = idDestinatarioUtente,
                ID_Pratiche = idPratica,
                DataCreazione = DateTime.Now,
                Letto = false,
                Contatore = 1
            });
            db.SaveChanges();
        }

        /* ============================================================
     🟢 SEGNALAZIONE LETTURA NOTIFICHE
     ============================================================ */

        [HttpPost]
        public JsonResult SegnaComeLetta(int id)
        {
            System.Diagnostics.Trace.WriteLine($"========== [SegnaComeLetta] AVVIO ==========");
            System.Diagnostics.Trace.WriteLine($"🟡 ID notifica ricevuto: {id}");

            try
            {
                int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
                if (idUtenteCollegato <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool isAdmin = IsAdminUser();

                // 🔍 Applica visibilità
                var query = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);
                var notifica = query.FirstOrDefault(n => n.ID_Notifica == id);

                if (notifica == null)
                {
                    System.Diagnostics.Trace.WriteLine("⚠️ Notifica non trovata o non visibile.");
                    return Json(new { success = false, message = "Notifica non trovata o non autorizzato." });
                }

                if (!notifica.Letto)
                {
                    notifica.Letto = true;
                    notifica.Stato = "Letta";
                    notifica.DataLettura = DateTime.Now;
                    db.SaveChanges();

                    System.Diagnostics.Trace.WriteLine($"✅ Notifica {id} marcata come letta.");
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"ℹ️ Notifica {id} già letta.");
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ Errore SegnaComeLetta: {ex}");
                return Json(new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        public JsonResult SegnaTutteComeLette()
        {
            System.Diagnostics.Trace.WriteLine("========== [SegnaTutteComeLette] AVVIO ==========");

            try
            {
                int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
                if (idUtenteCollegato <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool isAdmin = IsAdminUser();
                var query = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);

                var nonLette = query.Where(n => !n.Letto).ToList();
                if (!nonLette.Any())
                {
                    System.Diagnostics.Trace.WriteLine("ℹ️ Nessuna notifica non letta trovata.");
                    return Json(new { success = true, message = "Nessuna notifica da aggiornare." });
                }

                foreach (var n in nonLette)
                {
                    n.Letto = true;
                    n.Stato = "Letta";
                    n.DataLettura = DateTime.Now;
                }

                db.SaveChanges();
                System.Diagnostics.Trace.WriteLine($"✅ {nonLette.Count} notifiche marcate come lette.");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ Errore SegnaTutteComeLette: {ex}");
                return Json(new { success = false, message = ex.Message });
            }
        }



        #endregion

        #region GIORNALE PREVISIONALE 

        [HttpGet]
        public ActionResult OperazioniPrevisionaliList(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0)
                return new HttpStatusCodeResult(401);

            // Range date
            DateTime inizio = da ?? DateTime.MinValue;
            DateTime fine = a ?? DateTime.MaxValue;

            // Recupero operatore attivo (professionista)
            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");
            if (operatore == null)
                return PartialView("~/Views/Previsionale/_OperazioniPrevisionaliList.cshtml",
                    new List<OperazioniPrevisionaliViewModel>());

            int idClienteProfessionista = operatore.ID_Cliente;  // per Previsione e Cluster
            int idUtenteProfessionista = idUtenteAttivo;         // per GenerazioneCosti

            // ========= PREVISIONI salvate =========
            var previsioniQuery =
                from pr in db.Previsione
                join p in db.Pratiche on pr.ID_Pratiche equals p.ID_Pratiche into pj
                from pratica in pj.DefaultIfEmpty()
                join c in db.Clienti on pratica.ID_Cliente equals c.ID_Cliente into cj
                from cliente in cj.DefaultIfEmpty()
                where pr.ID_Professionista == idClienteProfessionista
                      && pr.DataPrevisione >= inizio && pr.DataPrevisione <= fine
                      && pr.Stato == "Previsionale"
                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = pr.ID_Previsione,
                    ID_Pratiche = pr.ID_Pratiche,
                    ID_Professionista = pr.ID_Professionista,
                    Percentuale = pr.Percentuale,
                    TipoOperazione = pr.TipoOperazione,
                    Descrizione = pr.Descrizione,
                    ImportoPrevisto = pr.ImportoPrevisto,
                    DataPrevisione = pr.DataPrevisione,
                    Stato = pr.Stato,
                    NomeCliente = cliente != null
                        ? (cliente.TipoCliente == "Professionista"
                            ? cliente.Nome + " " + cliente.Cognome
                            : cliente.Nome)
                        : "",
                    NomePratica = pratica != null ? pratica.Titolo : ""
                };

            // ========= ENTRATE previste (da Pratiche: Owner o Responsabile) =========
            var entrateQuery =
                from p in db.Pratiche
                join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                let dataPrev = (p.DataInizioAttivitaStimata ?? p.DataCreazione ?? DateTime.Now)

                // 🔹 Trovo l'ID_Cliente collegato al RESPONSABILE
                let idClienteResponsabile = (
                    from os in db.OperatoriSinergia
                    where os.ID_UtenteCollegato == p.ID_UtenteResponsabile
                          && os.TipoCliente == "Professionista"
                    select os.ID_Cliente
                ).FirstOrDefault()

                // 🔹 Trovo l'ID_Cliente collegato all'OWNER (UtenteCreatore)
                let idClienteOwner = (
                    from os in db.OperatoriSinergia
                    where os.ID_UtenteCollegato == p.ID_UtenteCreatore
                          && os.TipoCliente == "Professionista"
                    select os.ID_Cliente
                ).FirstOrDefault()

                // 🔹 Professionista effettivo: prima il responsabile, altrimenti l’owner
                let idProfessionistaEntrata = idClienteResponsabile != 0
                    ? idClienteResponsabile
                    : idClienteOwner

                where p.Stato != "Eliminato"
                      && dataPrev >= inizio && dataPrev <= fine
                      && p.Budget > 0
                      && idProfessionistaEntrata == idClienteProfessionista   // ✅ filtro per il professionista loggato

                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = p.ID_Pratiche,
                    ID_Pratiche = p.ID_Pratiche,
                    ID_Professionista = idProfessionistaEntrata, // ✅ sempre un ID_Cliente
                    Percentuale = 100,
                    TipoOperazione = "Entrata",
                    Descrizione = "Ricavo previsto da pratica",
                    ImportoPrevisto = p.Budget,
                    BudgetPratica = p.Budget,
                    DataPrevisione = dataPrev,
                    Stato = "Previsionale",
                    NomeCliente = c.TipoCliente == "Professionista"
                        ? (c.Nome + " " + c.Cognome)
                        : c.Nome,
                    NomePratica = p.Titolo
                };



            // ========= ENTRATE previste (Cluster = Collaboratore) =========
            var clusterQuery =
                from p in db.Pratiche
                join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                join cl in db.Cluster on p.ID_Pratiche equals cl.ID_Pratiche
                let dataPrev = (p.DataInizioAttivitaStimata ?? p.DataCreazione ?? DateTime.Now)
                let importoPrev = p.Budget * cl.PercentualePrevisione / 100m
                where cl.ID_Utente == idClienteProfessionista
                      && p.Stato != "Eliminato"
                      && dataPrev >= inizio && dataPrev <= fine
                      && importoPrev > 0
                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = p.ID_Pratiche,
                    ID_Pratiche = p.ID_Pratiche,
                    ID_Professionista = cl.ID_Utente,
                    Percentuale = cl.PercentualePrevisione,
                    TipoOperazione = "Entrata",
                    Descrizione = "Quota collaboratore da pratica",
                    ImportoPrevisto = importoPrev,
                    BudgetPratica = p.Budget,
                    DataPrevisione = dataPrev,
                    Stato = "Previsionale",
                    NomeCliente = c.TipoCliente == "Professionista"
                        ? (c.Nome + " " + c.Cognome)
                        : c.Nome,
                    NomePratica = p.Titolo
                };

            // ========= USCITE previste =========
            int IdClienteProfessionista = operatore.ID_Cliente;   // es. 6
            int IdUtenteProfessionista = idUtenteAttivo;          // es. 15

            System.Diagnostics.Debug.WriteLine(
                $"[USCITE] Mapping iniziale → idClienteProfessionista={IdClienteProfessionista}, idUtenteProfessionista={IdUtenteProfessionista}"
            );

            var usciteQuery =
                from g in db.GenerazioneCosti
                join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche into pj
                from pratica in pj.DefaultIfEmpty()
                where g.ID_Utente == IdUtenteProfessionista   // usa ID_Utente
                      && g.Approvato == false
                      && g.Stato == "Previsionale"
                select new
                {
                    g,
                    pratica
                };

            // 🔍 Debugga subito le righe trovate
            var usciteList = usciteQuery.ToList();
            foreach (var row in usciteList)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[USCITE] RIGA DB → ID_GenerazioneCosto={row.g.ID_GenerazioneCosto}, " +
                    $"g.ID_Utente={row.g.ID_Utente}, Importo={row.g.Importo}, Categoria={row.g.Categoria}, " +
                    $"Stato={row.g.Stato}, Approvato={row.g.Approvato}, " +
                    $"Pratica={(row.pratica != null ? row.pratica.Titolo : "NULL")}"
                );
            }

            // 🔄 Proietto nel ViewModel
            var usciteFinal =
                from row in usciteList
                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = row.g.ID_GenerazioneCosto,
                    ID_Pratiche = row.g.ID_Pratiche,
                    ID_Professionista = idClienteProfessionista, // sempre ID_Cliente nel ViewModel
                    TipoOperazione = "Uscita",
                    Descrizione = (row.g.Categoria ?? "Costo") +
                                  (string.IsNullOrEmpty(row.g.Descrizione) ? "" : " – " + row.g.Descrizione),
                    ImportoPrevisto = -(row.g.Importo ?? 0m),
                    DataPrevisione = row.g.DataRegistrazione,
                    Stato = "Previsionale",
                    NomePratica = row.pratica != null ? row.pratica.Titolo : null,
                    DebugInfo =
                        "g.ID_Utente=" + row.g.ID_Utente +
                        " | idClienteProfessionista=" + idClienteProfessionista +
                        " | idUtenteProfessionista=" + idUtenteProfessionista
                };


            // === Unione di tutte le fonti
            var lista = new List<OperazioniPrevisionaliViewModel>();
            lista.AddRange(previsioniQuery.ToList());
            lista.AddRange(entrateQuery.ToList());
            lista.AddRange(clusterQuery.ToList());
            //lista.AddRange(usciteQuery.ToList());

            // Filtri opzionali
            if (tipo == "Entrate")
                lista = lista.Where(x => x.TipoOperazione == "Entrata").ToList();
            else if (tipo == "Uscite")
                lista = lista.Where(x => x.TipoOperazione == "Uscita").ToList();

            // Ordinamento
            lista = lista.OrderBy(x => x.DataPrevisione ?? DateTime.MinValue).ToList();

            return PartialView("~/Views/GiornalePrevisionale/_OperazioniPrevisionaliList.cshtml", lista);
        }


        [HttpGet]
        public JsonResult GetDettaglioOperazionePrevisionale(string tipo, int id)
        {
            // tipo: "Entrata" (pratica) | "Uscita" (costo)
            if (string.Equals(tipo, "Uscita", StringComparison.OrdinalIgnoreCase))
            {
                var c = (from g in db.GenerazioneCosti
                         join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche into pj
                         from pratica in pj.DefaultIfEmpty()
                         where g.ID_GenerazioneCosto == id
                         select new
                         {
                             g.ID_GenerazioneCosto,
                             g.Categoria,
                             g.Origine,
                             g.Descrizione,
                             g.Importo,
                             g.DataRegistrazione,
                             g.Stato,
                             Pratica = pratica != null ? pratica.Titolo : "-"
                         }).FirstOrDefault();

                if (c == null)
                    return Json(new { success = false, message = "Costo non trovato." }, JsonRequestBehavior.AllowGet);

                return Json(new { success = true, tipo = "Uscita", dettaglio = c }, JsonRequestBehavior.AllowGet);
            }
            else if (string.Equals(tipo, "Entrata", StringComparison.OrdinalIgnoreCase))
            {
                // Dati pratica + % previsione (Cluster)
                var e = (from p in db.Pratiche
                         join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                         join cl in db.Cluster
                              on new { p.ID_Pratiche, ID_Utente = p.ID_UtenteResponsabile }
                              equals new { ID_Pratiche = cl.ID_Pratiche, cl.ID_Utente } into clj
                         from cluster in clj.DefaultIfEmpty()
                         where p.ID_Pratiche == id
                         let perc = (cluster != null) ? cluster.PercentualePrevisione : 0m
                         let importoPrev = p.Budget * perc / 100m
                         select new
                         {
                             p.ID_Pratiche,
                             Pratica = p.Titolo,
                             Cliente = c.TipoCliente == "Professionista" ? (c.Nome + " " + c.Cognome) : c.Nome,
                             Budget = p.Budget,
                             Percentuale = perc,
                             ImportoPrevisto = importoPrev,
                             Data = (p.DataInizioAttivitaStimata ?? p.DataCreazione)
                         }).FirstOrDefault();

                if (e == null)
                    return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

                // Configurazioni percentuali/fisse
                var ownerPerc = (from r in db.RicorrenzeCosti
                                 where r.Categoria == "Owner Fee"
                                       && r.Attivo == true
                                       && r.TipoValore == "Percentuale"
                                 orderby r.DataInizio descending, r.ID_Ricorrenza descending
                                 select (decimal?)r.Valore).FirstOrDefault();

                var sinergiaPerc = (from r in db.RicorrenzeCosti
                                    where r.Categoria == "Trattenuta Sinergia"
                                          && r.Attivo == true
                                          && r.TipoValore == "Percentuale"
                                    orderby r.DataInizio descending, r.ID_Ricorrenza descending
                                    select (decimal?)r.Valore).FirstOrDefault();

                var residentFisso = (from r in db.RicorrenzeCosti
                                     where r.Categoria == "Costo Resident"
                                           && r.Attivo == true
                                           && r.TipoValore != "Percentuale"   // fisso
                                     orderby r.DataInizio descending, r.ID_Ricorrenza descending
                                     select (decimal?)r.Valore).FirstOrDefault();

                bool isOwnerQuota = ownerPerc.HasValue && Math.Abs(e.Percentuale - ownerPerc.Value) < 0.0001m;

                return Json(new
                {
                    success = true,
                    tipo = "Entrata",
                    dettaglio = e,
                    ownerPercent = ownerPerc,
                    sinergiaPercent = sinergiaPerc,
                    residentFixed = residentFisso,
                    isOwnerQuota = isOwnerQuota
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = false, message = "Tipo non valido." }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult EsportaOperazioniPrevisionaliCsv(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtente <= 0)
                return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");
            if (operatore == null)
                return new HttpStatusCodeResult(403);

            int idClienteProfessionista = operatore.ID_Cliente;

            // entrate
            var entrateList = (from p in db.Pratiche
                               join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                               join cl in db.Cluster
                                   on new { p.ID_Pratiche, ID_Utente = p.ID_UtenteResponsabile }
                                   equals new { ID_Pratiche = cl.ID_Pratiche, cl.ID_Utente } into clj
                               from cluster in clj.DefaultIfEmpty()
                               let dataPrev = (p.DataInizioAttivitaStimata ?? p.DataCreazione ?? DateTime.Now)
                               let perc = (cluster != null ? cluster.PercentualePrevisione : 0m)
                               let importoPrev = p.Budget * perc / 100m
                               where p.ID_UtenteResponsabile == idClienteProfessionista
                                     && p.Stato != "Eliminato"
                                     && dataPrev >= inizio && dataPrev <= fine
                                     && importoPrev != 0
                               select new OperazioniPrevisionaliViewModel
                               {
                                   TipoOperazione = "Entrata",
                                   Descrizione = "Ricavo previsto da pratica",
                                   ImportoPrevisto = importoPrev,
                                   DataPrevisione = dataPrev,
                                   NomeCliente = c.TipoCliente == "Professionista" ? (c.Nome + " " + c.Cognome) : c.Nome,
                                   NomePratica = p.Titolo
                               }).ToList();

            // uscite
            var usciteList = (from g in db.GenerazioneCosti
                              join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche into pj
                              from pratica in pj.DefaultIfEmpty()
                              where g.ID_Utente == idClienteProfessionista
                                    && g.Approvato == false
                                    && g.Stato == "Previsionale"
                                    && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                              select new OperazioniPrevisionaliViewModel
                              {
                                  TipoOperazione = "Uscita",
                                  Descrizione = (g.Categoria ?? "Costo") +
                                                (string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione),
                                  ImportoPrevisto = -(g.Importo ?? 0m),
                                  DataPrevisione = g.DataRegistrazione,
                                  NomePratica = pratica != null ? pratica.Titolo : null
                              }).ToList();

            var lista = entrateList.Concat(usciteList).OrderBy(x => x.DataPrevisione).ToList();

            if (tipo == "Entrate") lista = lista.Where(x => x.TipoOperazione == "Entrata").ToList();
            else if (tipo == "Uscite") lista = lista.Where(x => x.TipoOperazione == "Uscita").ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Tipo;Descrizione;Importo;Data;Cliente;Pratica");

            foreach (var op in lista)
            {
                sb.AppendLine($"{op.TipoOperazione};" +
                              $"{op.Descrizione};" +
                              $"{op.ImportoPrevisto:N2};" +
                              $"{(op.DataPrevisione.HasValue ? op.DataPrevisione.Value.ToString("dd/MM/yyyy") : "-")};" +
                              $"{op.NomeCliente};" +
                              $"{op.NomePratica}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
                $"OperazioniPrevisionali_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaOperazioniPrevisionaliPdf(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtente <= 0)
                return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");
            if (operatore == null)
                return new HttpStatusCodeResult(403);

            int idClienteProfessionista = operatore.ID_Cliente;

            // Recupero lista (entrate + uscite) simile a CSV
            var lista = new List<OperazioniPrevisionaliViewModel>();

            // Entrate
            lista.AddRange((from p in db.Pratiche
                            join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                            join cl in db.Cluster
                                on new { p.ID_Pratiche, ID_Utente = p.ID_UtenteResponsabile }
                                equals new { ID_Pratiche = cl.ID_Pratiche, cl.ID_Utente } into clj
                            from cluster in clj.DefaultIfEmpty()
                            let dataPrev = (p.DataInizioAttivitaStimata ?? p.DataCreazione ?? DateTime.Now)
                            let perc = (cluster != null ? cluster.PercentualePrevisione : 0m)
                            let importoPrev = p.Budget * perc / 100m
                            where p.ID_UtenteResponsabile == idClienteProfessionista
                                  && p.Stato != "Eliminato"
                                  && dataPrev >= inizio && dataPrev <= fine
                                  && importoPrev != 0
                            select new OperazioniPrevisionaliViewModel
                            {
                                TipoOperazione = "Entrata",
                                Descrizione = "Ricavo previsto da pratica",
                                ImportoPrevisto = importoPrev,
                                DataPrevisione = dataPrev,
                                NomeCliente = c.TipoCliente == "Professionista" ? (c.Nome + " " + c.Cognome) : c.Nome,
                                NomePratica = p.Titolo
                            }).ToList());

            // Uscite
            lista.AddRange((from g in db.GenerazioneCosti
                            join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche into pj
                            from pratica in pj.DefaultIfEmpty()
                            where g.ID_Utente == idClienteProfessionista
                                  && g.Approvato == false
                                  && g.Stato == "Previsionale"
                                  && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                            select new OperazioniPrevisionaliViewModel
                            {
                                TipoOperazione = "Uscita",
                                Descrizione = (g.Categoria ?? "Costo") +
                                              (string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione),
                                ImportoPrevisto = -(g.Importo ?? 0m),
                                DataPrevisione = g.DataRegistrazione,
                                NomePratica = pratica != null ? pratica.Titolo : null
                            }).ToList());

            if (tipo == "Entrate") lista = lista.Where(x => x.TipoOperazione == "Entrata").ToList();
            else if (tipo == "Uscite") lista = lista.Where(x => x.TipoOperazione == "Uscita").ToList();

            lista = lista.OrderBy(x => x.DataPrevisione).ToList();

            return new Rotativa.ViewAsPdf("~/Views/GiornalePrevisionale/ReportOperazioniPrevisionaliPdf.cshtml", lista)
            {
                FileName = $"OperazioniPrevisionali_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }


        #endregion

        #region GIORNALE ECONOMICO

        [HttpGet]
        public ActionResult OperazioniEconomicheList(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0) return new HttpStatusCodeResult(401);

            DateTime inizio = da?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = (a?.Date ?? inizio.AddMonths(1).AddDays(-1)).AddDays(1).AddTicks(-1);

            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");
            if (operatore == null)
                return PartialView("~/Views/GiornaleEconomico/_OperazioniEconomicheList.cshtml",
                    new List<OperazioniEconomicheViewModel>());

            // ✅ Uso ID_Utente (non ID_Cliente)
            int idProfessionista = (int)operatore.ID_UtenteCollegato;

            // ===== Team attivi
            var teamIds = db.MembriTeam
                .Where(mt => mt.ID_Professionista == idProfessionista && mt.Attivo)
                .Select(mt => mt.ID_Team)
                .Distinct()
                .ToList();

            // ===== Pratiche di progetto
            var praticheProgettoIds = (
                from cp in db.CostiPratica
                join pr in db.Pratiche on cp.ID_Pratiche equals pr.ID_Pratiche
                where pr.Stato != "Eliminato"
                select pr.ID_Pratiche
            ).Distinct().ToList();

            // ========= ENTRATE
            var entrateDto =
                (from avv in db.AvvisiParcella
                 join p in db.Pratiche on avv.ID_Pratiche equals p.ID_Pratiche
                 where p.ID_UtenteResponsabile == idProfessionista
                       && avv.Stato != "Annullato"
                       && avv.DataAvviso >= inizio && avv.DataAvviso <= fine
                 select new
                 {
                     avv.ID_AvvisoParcelle,
                     avv.ID_Pratiche,
                     avv.DataAvviso,
                     avv.Importo,
                     avv.ContributoIntegrativoImporto,
                     avv.ImportoIVA,
                     avv.TotaleAvvisiParcella,
                     avv.Note,
                     avv.ID_UtenteCreatore
                 }).ToList();

            var entrateList = entrateDto.Select(avv =>
            {
                decimal tot = avv.TotaleAvvisiParcella ?? 0m;
                decimal iva = avv.ImportoIVA ?? 0m;
                decimal baseI = avv.Importo ?? 0m;
                decimal ci = avv.ContributoIntegrativoImporto ?? 0m;
                decimal netto = (tot > 0m || iva > 0m) ? (tot - iva) : (baseI + ci);

                return new OperazioniEconomicheViewModel
                {
                    ID_Transazione = avv.ID_AvvisoParcelle,
                    ID_Pratiche = avv.ID_Pratiche,
                    Importo = netto,
                    Descrizione = string.IsNullOrWhiteSpace(avv.Note) ? "Avviso di Parcella (netto)" : avv.Note,
                    DataOperazione = avv.DataAvviso,
                    Stato = "Economico",
                    ID_UtenteCreatore = avv.ID_UtenteCreatore ?? 0,
                    TipoOperazione = "Entrata"
                };
            }).ToList();

            // ========= USCITE
            var statiPagati = new[] { "pagato", "pagata", "pagati" };

            var usciteDto =
                (from g in db.GenerazioneCosti
                 where statiPagati.Contains(g.Stato.Trim().ToLower())
                       && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                       && (
                           g.ID_Utente == idProfessionista
                           || g.ID_Utente == idUtenteAttivo
                           || (g.ID_Team != null && teamIds.Contains(g.ID_Team.Value))
                           || (g.Categoria == "Costo Progetto" && g.ID_Pratiche != null && praticheProgettoIds.Contains(g.ID_Pratiche.Value))
                       )
                 select new
                 {
                     g.ID_GenerazioneCosto,
                     g.ID_Pratiche,
                     g.Categoria,
                     g.Descrizione,
                     g.Importo,
                     g.DataRegistrazione,
                     g.ID_UtenteCreatore
                 })
                .Distinct()
                .ToList();

            var usciteList = usciteDto.Select(g => new OperazioniEconomicheViewModel
            {
                ID_Transazione = g.ID_GenerazioneCosto,
                ID_Pratiche = g.ID_Pratiche,
                Importo = -(g.Importo ?? 0m),
                Descrizione = (g.Categoria ?? "Costo") + (string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione),
                DataOperazione = g.DataRegistrazione,
                Stato = "Economico",
                ID_UtenteCreatore = g.ID_UtenteCreatore ?? 0,
                TipoOperazione = "Uscita"
            }).ToList();

            // ===== Unione + filtro
            var lista = new List<OperazioniEconomicheViewModel>();
            lista.AddRange(entrateList);
            lista.AddRange(usciteList);

            if (tipo == "Entrate")
                lista = lista.Where(x => x.TipoOperazione == "Entrata").ToList();
            else if (tipo == "Uscite")
                lista = lista.Where(x => x.TipoOperazione == "Uscita").ToList();

            lista = lista.OrderBy(x => x.DataOperazione ?? DateTime.MinValue).ToList();

            return PartialView("~/Views/GiornaleEconomico/_OperazioniEconomicheList.cshtml", lista);
        }




        [HttpGet]
        public JsonResult GetDettaglioOperazioneEconomica(string tipo, int id)
        {
            // tipo: "Entrata" (AvvisoParcella) | "Uscita" (GenerazioneCosti)
            if (string.Equals(tipo, "Entrata", StringComparison.OrdinalIgnoreCase))
            {
                var a = (from av in db.AvvisiParcella
                         join p in db.Pratiche on av.ID_Pratiche equals p.ID_Pratiche
                         where av.ID_AvvisoParcelle == id
                         select new
                         {
                             av.ID_AvvisoParcelle,
                             av.ID_Pratiche,
                             av.DataAvviso,
                             av.Importo,
                             av.ContributoIntegrativoPercentuale,
                             av.ContributoIntegrativoImporto,
                             av.AliquotaIVA,
                             av.ImportoIVA,
                             av.TotaleAvvisiParcella,
                             av.MetodoPagamento,
                             av.Note,
                             av.Stato,
                             Pratica = p.Titolo
                         }).FirstOrDefault();

                if (a == null)
                    return Json(new { success = false, message = "Avviso di parcella non trovato." }, JsonRequestBehavior.AllowGet);

                decimal tot = a.TotaleAvvisiParcella ?? 0m;
                decimal iva = a.ImportoIVA ?? 0m;
                decimal baseI = a.Importo ?? 0m;
                decimal ci = a.ContributoIntegrativoImporto ?? 0m;
                decimal netto = (tot > 0m || iva > 0m) ? (tot - iva) : (baseI + ci);

                return Json(new
                {
                    success = true,
                    tipo = "Entrata",
                    dettaglio = new
                    {
                        a.ID_AvvisoParcelle,
                        a.ID_Pratiche,
                        Pratica = a.Pratica,
                        Data = a.DataAvviso,
                        ImportoBase = baseI,
                        ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                        ContributoIntegrativoImporto = ci,
                        AliquotaIVA = a.AliquotaIVA,
                        ImportoIVA = iva,
                        Totale = (tot > 0m ? tot : (baseI + ci + iva)),
                        Netto = netto, // quello che mostri in lista
                        MetodoPagamento = a.MetodoPagamento,
                        Note = a.Note,
                        Stato = a.Stato
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            else if (string.Equals(tipo, "Uscita", StringComparison.OrdinalIgnoreCase))
            {
                var g = (from c in db.GenerazioneCosti
                         join p in db.Pratiche on c.ID_Pratiche equals p.ID_Pratiche into pj
                         from pratica in pj.DefaultIfEmpty()
                         where c.ID_GenerazioneCosto == id
                         select new
                         {
                             c.ID_GenerazioneCosto,
                             c.ID_Pratiche,
                             c.Categoria,
                             c.Origine,
                             c.Descrizione,
                             c.Importo,
                             c.DataRegistrazione,
                             c.Stato,
                             Pratica = pratica != null ? pratica.Titolo : "-"
                         }).FirstOrDefault();

                if (g == null)
                    return Json(new { success = false, message = "Costo pagato non trovato." }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    tipo = "Uscita",
                    dettaglio = new
                    {
                        g.ID_GenerazioneCosto,
                        g.ID_Pratiche,
                        g.Categoria,
                        g.Origine,
                        g.Descrizione,
                        Importo = g.Importo ?? 0m,
                        DataRegistrazione = g.DataRegistrazione,
                        g.Stato,
                        Pratica = g.Pratica
                    }
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { success = false, message = "Tipo non valido." }, JsonRequestBehavior.AllowGet);
        }

        // 📥 EXPORT OPERAZIONI ECONOMICHE CSV
        [HttpGet]
        public ActionResult EsportaOperazioniEconomicheCsv(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0) return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            // 🔄 Recupero la lista con il metodo già esistente
            var lista = OperazioniEconomicheList(da, a, tipo) as PartialViewResult;
            var model = lista?.Model as List<OperazioniEconomicheViewModel> ?? new List<OperazioniEconomicheViewModel>();

            var sb = new StringBuilder();
            sb.AppendLine("Data;Tipo;Descrizione;Pratica;Importo;Stato");

            foreach (var op in model)
            {
                sb.AppendLine(
                    $"{(op.DataOperazione.HasValue ? op.DataOperazione.Value.ToString("dd/MM/yyyy") : "-")};" +
                    $"{op.TipoOperazione};" +
                    $"{op.Descrizione};" +
                    $"{(op.ID_Pratiche.HasValue ? op.ID_Pratiche.Value.ToString() : "-")};" +
                    $"{op.Importo.ToString("N2")};" +
                    $"{op.Stato}"
                );
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"OperazioniEconomiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }


        // 📥 EXPORT OPERAZIONI ECONOMICHE PDF
        [HttpGet]
        public ActionResult EsportaOperazioniEconomichePdf(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0) return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var lista = OperazioniEconomicheList(da, a, tipo) as PartialViewResult;
            var model = lista?.Model as List<OperazioniEconomicheViewModel> ?? new List<OperazioniEconomicheViewModel>();

            return new Rotativa.ViewAsPdf("~/Views/GiornaleEconomico/ReportOperazioniEconomichePdf.cshtml", model)
            {
                FileName = $"OperazioniEconomiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }


        #endregion

        #region GIORNALE FINANZIARIO

        [HttpGet]
        public ActionResult OperazioniFinanziarieList(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0) return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? inizio.AddMonths(1).AddDays(-1);

            // ✅ uso ID_UtenteCollegato
            var professionistiIds = db.OperatoriSinergia
                .Where(o => o.TipoCliente == "Professionista" && o.ID_UtenteCollegato == idUtenteAttivo)
                .Select(o => o.ID_UtenteCollegato)
                .ToList();

            if (professionistiIds.Count == 0)
                return PartialView("~/Views/GiornaleFinanziario/_OperazioniFinanziarieList.cshtml",
                    new List<OperaziomoFinanziarieViewModel>());

            int idProfessionista = (int)professionistiIds[0];

            // ===== Team attivi
            var teamIds = db.MembriTeam
                .Where(mt => professionistiIds.Contains(mt.ID_Professionista) && mt.Attivo)
                .Select(mt => mt.ID_Team)
                .Distinct()
                .ToList();

            // ===== Pratiche (anche da costi progetto)
            var tuttePraticheIds = (
                from p in db.Pratiche
                where (professionistiIds.Contains((int)p.ID_Owner) ||
                       professionistiIds.Contains(p.ID_UtenteResponsabile))
                      && p.Stato != "Eliminato"
                select p.ID_Pratiche
            )
            .Union(
                from g in db.GenerazioneCosti
                where g.Categoria == "Costo Progetto"
                let idPraticaCp = db.CostiPratica
                    .Where(cp => cp.ID_CostoPratica == g.ID_Riferimento)
                    .Select(cp => (int?)cp.ID_Pratiche)
                    .FirstOrDefault()
                let idPratica = (int?)(g.ID_Pratiche ?? idPraticaCp)
                where idPratica != null
                select idPratica.Value
            )
            .Distinct()
            .ToList();

            var praticheDict = db.Pratiche
                .Where(p => tuttePraticheIds.Contains(p.ID_Pratiche))
                .Select(p => new { p.ID_Pratiche, p.Titolo })
                .ToList()
                .ToDictionary(x => x.ID_Pratiche, x => x.Titolo);

            // ========= ENTRATE =========
            var incassiDto = (
                from i in db.Incassi
                join avv in db.AvvisiParcella on i.ID_AvvisoParcella equals avv.ID_AvvisoParcelle into aj
                from av in aj.DefaultIfEmpty()
                where i.DataIncasso >= inizio && i.DataIncasso <= fine
                let idPratica = (int?)(i.ID_Pratiche ?? (av != null ? av.ID_Pratiche : (int?)null))
                where idPratica != null && db.Pratiche.Any(p => p.ID_Pratiche == idPratica && professionistiIds.Contains((int)p.ID_Owner))
                select new
                {
                    i.ID_Incasso,
                    ID_Pratiche = idPratica,
                    i.DataIncasso,
                    i.Importo,
                    i.ModalitaPagamento,
                    ID_Avviso = (int?)(av != null ? av.ID_AvvisoParcelle : (int?)null)
                }
            ).ToList();

            var entrateList = incassiDto
                .Select(x => new OperaziomoFinanziarieViewModel
                {
                    ID_Operazione = x.ID_Incasso,
                    ID_Pratiche = x.ID_Pratiche,
                    TipoOperazione = "Entrata",
                    Descrizione = $"Incasso{(x.ID_Avviso.HasValue ? $" avviso #{x.ID_Avviso.Value}" : "")} ({x.ModalitaPagamento ?? "—"})",
                    Importo = x.Importo,
                    DataOperazione = x.DataIncasso,
                    Stato = "Finanziario",
                    NomePratica = (x.ID_Pratiche.HasValue && praticheDict.ContainsKey(x.ID_Pratiche.Value))
                                        ? praticheDict[x.ID_Pratiche.Value] : "—",
                    Categoria = "Incasso"
                })
                .ToList();

            // ========= USCITE =========
            var statiPagati = new[] { "Pagato", "Pagata", "Pagati" };

            var usciteDto = (
                from g in db.GenerazioneCosti
                where statiPagati.Contains(g.Stato)
                      && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                      && (
                          g.ID_Utente == idUtenteAttivo ||
                          g.ID_Utente == idProfessionista ||
                          (g.ID_Team != null && teamIds.Contains(g.ID_Team.Value)) ||
                          g.Categoria == "Costo Progetto"
                      )
                let idPraticaCp = db.CostiPratica
                    .Where(cp => cp.ID_CostoPratica == g.ID_Riferimento)
                    .Select(cp => (int?)cp.ID_Pratiche)
                    .FirstOrDefault()
                let idPraticaFinale = g.ID_Pratiche ?? idPraticaCp
                select new
                {
                    g.ID_GenerazioneCosto,
                    ID_Pratiche = idPraticaFinale,
                    g.Categoria,
                    g.Descrizione,
                    Importo = (decimal?)g.Importo,
                    g.DataRegistrazione
                }
            )
            .ToList()
            .GroupBy(x => x.ID_GenerazioneCosto)
            .Select(g => g.First())
            .ToList();

            var usciteList = usciteDto
                .Select(g => new OperaziomoFinanziarieViewModel
                {
                    ID_Operazione = g.ID_GenerazioneCosto,
                    ID_Pratiche = g.ID_Pratiche,
                    TipoOperazione = "Uscita",
                    Descrizione = (g.Categoria ?? "Costo") + (string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione),
                    Importo = -(g.Importo ?? 0m),
                    DataOperazione = g.DataRegistrazione,
                    Stato = "Finanziario",
                    NomePratica = (g.ID_Pratiche.HasValue && praticheDict.ContainsKey(g.ID_Pratiche.Value))
                                  ? praticheDict[g.ID_Pratiche.Value]
                                  : "-",
                    Categoria = g.Categoria ?? "-"
                })
                .ToList();

            // ========= COMBINO E FILTRO =========
            var lista = new List<OperaziomoFinanziarieViewModel>();
            lista.AddRange(entrateList);
            lista.AddRange(usciteList);

            if (tipo == "Entrate")
                lista = lista.Where(x => x.TipoOperazione == "Entrata").ToList();
            else if (tipo == "Uscite")
                lista = lista.Where(x => x.TipoOperazione == "Uscita").ToList();

            lista = lista
                .OrderByDescending(x => x.DataOperazione ?? DateTime.MinValue)
                .ThenByDescending(x => x.ID_Operazione)
                .ToList();

            return PartialView("~/Views/GiornaleFinanziario/_OperazioniFinanziarieList.cshtml", lista);
        }


        [HttpGet]
        public JsonResult GetDettaglioOperazioneFinanziaria(int id)
        {
            // 1️⃣ Prima provo come incasso
            var inc = db.Incassi.FirstOrDefault(i => i.ID_Incasso == id);
            if (inc != null)
            {
                int? idPratica = inc.ID_Pratiche;
                int? idAvviso = inc.ID_AvvisoParcella;
                DateTime? dataAvviso = null;

                // 🔍 Recupera sempre l'avviso, se esiste
                if (idAvviso.HasValue)
                {
                    var av = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso.Value);
                    if (av != null)
                    {
                        if (!idPratica.HasValue)
                            idPratica = av.ID_Pratiche;

                        dataAvviso = av.DataAvviso;
                    }
                }
                else if (idPratica.HasValue)
                {
                    // Se non c'è ID_AvvisoParcella nell'incasso, prova a cercarlo tramite la pratica
                    var av = db.AvvisiParcella
                        .Where(a => a.ID_Pratiche == idPratica.Value)
                        .OrderByDescending(a => a.DataAvviso)
                        .FirstOrDefault();
                    if (av != null)
                    {
                        idAvviso = av.ID_AvvisoParcelle;
                        dataAvviso = av.DataAvviso;
                    }
                }

                string nomePratica = "-";
                if (idPratica.HasValue)
                    nomePratica = db.Pratiche
                                    .Where(p => p.ID_Pratiche == idPratica.Value)
                                    .Select(p => p.Titolo)
                                    .FirstOrDefault() ?? "-";

                var dettaglio = new
                {
                    inc.ID_Incasso,
                    ID_Pratiche = idPratica,
                    Pratica = nomePratica,
                    DataIncasso = inc.DataIncasso,
                    Importo = inc.Importo,
                    ModalitaPagamento = inc.ModalitaPagamento,
                    VersaInPlafond = inc.VersaInPlafond,
                    Utile = inc.Utile,
                    ID_AvvisoParcella = idAvviso,
                    DataAvviso = dataAvviso
                };

                return Json(new { success = true, tipo = "Entrata", dettaglio }, JsonRequestBehavior.AllowGet);
            }

            // 2️⃣ Se non è un incasso → provo come costo
            var q = (
                from g in db.GenerazioneCosti
                where g.ID_GenerazioneCosto == id
                let idPraticaCp = db.CostiPratica
                    .Where(cp => cp.ID_CostoPratica == g.ID_Riferimento)
                    .Select(cp => (int?)cp.ID_Pratiche)
                    .FirstOrDefault()
                let idPratica = (int?)(g.ID_Pratiche ?? idPraticaCp)
                let titolo = db.Pratiche
                    .Where(p => p.ID_Pratiche == idPratica)
                    .Select(p => p.Titolo)
                    .FirstOrDefault()
                select new
                {
                    g.ID_GenerazioneCosto,
                    ID_Pratiche = idPratica,
                    Pratica = titolo ?? "-",
                    g.ID_Team,
                    g.ID_Utente,
                    g.Categoria,
                    g.Origine,
                    g.Descrizione,
                    Importo = (decimal?)g.Importo ?? 0m,
                    g.DataRegistrazione,
                    g.Stato
                }
            ).FirstOrDefault();

            if (q != null)
            {
                string origineCalcolata =
                    q.ID_Team.HasValue ? "Team" :
                    (string.Equals(q.Categoria, "Costo Progetto", StringComparison.OrdinalIgnoreCase) && q.ID_Pratiche.HasValue) ? "Progetto" :
                    (q.ID_Utente.HasValue ? "Generale/Professionista" : (q.Origine ?? "-"));

                var dettaglio = new
                {
                    q.ID_GenerazioneCosto,
                    q.ID_Pratiche,
                    q.Pratica,
                    Origine = origineCalcolata,
                    Categoria = q.Categoria,
                    q.Descrizione,
                    q.Importo,
                    DataRegistrazione = q.DataRegistrazione,
                    q.Stato
                };

                return Json(new { success = true, tipo = "Uscita", dettaglio }, JsonRequestBehavior.AllowGet);
            }

            // 3️⃣ Nessun risultato
            return Json(new { success = false, message = "Operazione non trovata." }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult EsportaOperazioniFinanziarieCsv(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0)
                return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today;

            // Recupera lista chiamando già il metodo OperazioniFinanziarieList
            var lista = (ActionResult)OperazioniFinanziarieList(da, a, tipo);
            var partialViewResult = lista as PartialViewResult;
            var model = partialViewResult?.Model as List<OperaziomoFinanziarieViewModel> ?? new List<OperaziomoFinanziarieViewModel>();

            var sb = new StringBuilder();
            sb.AppendLine("ID;Pratica;Tipo Operazione;Descrizione;Importo;Data;Categoria;Stato");

            foreach (var op in model)
            {
                sb.AppendLine($"{op.ID_Operazione};" +
                              $"{(string.IsNullOrWhiteSpace(op.NomePratica) ? "-" : op.NomePratica)};" +
                              $"{op.TipoOperazione};" +
                              $"{(string.IsNullOrWhiteSpace(op.Descrizione) ? "-" : op.Descrizione)};" +
                              $"{op.Importo.ToString("0.00")};" +  // ✅ formato sicuro
                              $"{(op.DataOperazione.HasValue ? op.DataOperazione.Value.ToString("dd/MM/yyyy") : "-")};" +
                              $"{(string.IsNullOrWhiteSpace(op.Categoria) ? "-" : op.Categoria)};" +
                              $"{op.Stato}");
            }

            // ✅ Usa BOM per compatibilità con Excel
            byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(buffer, "text/csv", $"OperazioniFinanziarie_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaOperazioniFinanziariePdf(DateTime? da, DateTime? a, string tipo = "Tutte")
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtenteLoggato <= 0)
                return new HttpStatusCodeResult(401);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today;

            var lista = (ActionResult)OperazioniFinanziarieList(da, a, tipo);
            var partialViewResult = lista as PartialViewResult;
            var model = partialViewResult?.Model as List<OperaziomoFinanziarieViewModel> ?? new List<OperaziomoFinanziarieViewModel>();

            return new Rotativa.ViewAsPdf("~/Views/GiornaleFinanziario/ReportOperazioniFinanziariePdf.cshtml", model)
            {
                FileName = $"OperazioniFinanziarie_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }


        #endregion

        public ActionResult Error()
        {
            var exception = System.Web.HttpContext.Current.Items["LastException"] as Exception;

            // 🔎 Se vuoi loggare sempre l’errore
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine("🚨 ERRORE CATTURATO:");
                System.Diagnostics.Debug.WriteLine("Messaggio: " + exception.Message);
                System.Diagnostics.Debug.WriteLine("StackTrace: " + exception.StackTrace);
                if (exception.InnerException != null)
                    System.Diagnostics.Debug.WriteLine("Inner: " + exception.InnerException);
            }

            ViewBag.MessaggioPrincipale = exception?.Message ?? "Errore sconosciuto";
            ViewBag.StackTrace = exception?.StackTrace;
            ViewBag.Inner = exception?.InnerException?.ToString();
            ViewBag.Controller = RouteData.Values["originalController"];
            ViewBag.Action = RouteData.Values["originalAction"];

            // 🔧 Puoi anche passare un model tipizzato invece di ViewBag
            return View("Error");
        }

    }
}
