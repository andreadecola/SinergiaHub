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

namespace Sinergia.Controllers
{
    [PermissionsActionFilter]
    public class HomeController : Controller
    {
        private SinergiaDB db = new SinergiaDB();
        #region Cruscotto
        public ActionResult Cruscotto(string idCliente = null, int? intervalloGiorni = 30)
        {
            ViewData["controller"] = ControllerContext.RouteData.Values["controller"].ToString();
            ViewData["azione"] = ControllerContext.RouteData.Values["action"].ToString();

            int idUtente = UserManager.GetIDUtenteCollegato();
            int idUtenteAttivo = UserManager.GetIDUtenteAttivo();
            if (idUtente <= 0) return RedirectToAction("Login", "Account");

            RicorrenzeHelper.EseguiRicorrenzeCostiSeNecessario();
            CostiHelper.EseguiGenerazioneCosti();

            using (var db = new SinergiaDB())
            {
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteAttivo);
                if (utente == null) return RedirectToAction("Login", "Account");

                var clientiDisponibili = DashboardHelper.GetClientiDisponibiliPerNavbar(idUtente, UserManager.GetTipoUtente());
                Session["ClientiDisponibili"] = clientiDisponibili;
                if (clientiDisponibili == null || !clientiDisponibili.Any()) return View("NessunProfessionistaAssegnato");

                string nomeCliente = "";
                int idClienteProfessionista = 0;

                var operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo);
                if (operatore != null)
                {
                    idClienteProfessionista = operatore.ID_Cliente;
                    nomeCliente = operatore.Nome + " " + (operatore.Cognome ?? "");
                    idCliente = "P_" + idClienteProfessionista;

                    Session["ID_ClienteSelezionato"] = idCliente;
                    var cookie = new HttpCookie("Cliente", idCliente) { Expires = DateTime.Now.AddDays(7) };
                    Response.Cookies.Add(cookie);
                }

                var praticheNonRegistrate = db.Pratiche
                    .Where(p => p.ID_UtenteResponsabile == idClienteProfessionista && p.Stato != "Eliminato")
                    .ToList()
                    .Where(p => !db.BilancioProfessionista.Any(b => b.ID_Pratiche == p.ID_Pratiche && b.Origine == "Pratica"))
                    .ToList();

                foreach (var pratica in praticheNonRegistrate)
                    RicorrenzeHelper.EseguiRegistrazioniDaPratica(pratica.ID_Pratiche);

                // ⏱ range: ultimi N giorni (default 30)
                DateTime fine = DateTime.Today.AddDays(1).AddTicks(-1); // fine giornata inclusa
                DateTime inizio = fine.AddDays(-(intervalloGiorni ?? 30));

                // ✅ pratiche per dashboard (con filtro date)
                var pratiche = db.Pratiche
                    .Where(p => p.ID_UtenteResponsabile == idClienteProfessionista
                             && p.Stato != "Eliminato"
                             && p.DataCreazione >= inizio && p.DataCreazione <= fine)
                    .ToList();

                // 👉 ID pratiche (serve per Economico/Finanziario)
                var idPratiche = pratiche.Select(x => x.ID_Pratiche).ToList();

                var listaPratiche = pratiche.Select(p => new PraticaViewModel
                {
                    ID_Pratiche = p.ID_Pratiche,
                    Titolo = p.Titolo,
                    Stato = p.Stato,
                    Descrizione = p.Descrizione,
                    DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
                    DataFineAttivitaStimata = p.DataFineAttivitaStimata,
                    Collaboratori = (
                        from r in db.RelazionePraticheUtenti
                        join u in db.Utenti on r.ID_Utente equals u.ID_Utente
                        where r.ID_Pratiche == p.ID_Pratiche
                        select new CollaboratorePraticaViewModel
                        {
                            ID_Utente = u.ID_Utente,
                            Nome = u.Nome + " " + u.Cognome,
                            Percentuale = 0
                        }).ToList()
                }).ToList();

                var documentiRecenti = (from d in db.DocumentiPratiche
                                        join p in db.Pratiche on d.ID_Pratiche equals p.ID_Pratiche
                                        where p.ID_UtenteResponsabile == idClienteProfessionista && d.Stato != "Eliminato"
                                        orderby d.DataCaricamento descending
                                        select new DocumentiPraticaViewModel
                                        {
                                            ID_Documento = d.ID_Documento,
                                            NomeFile = d.NomeFile,
                                            DataCaricamento = d.DataCaricamento,
                                            ID_Pratiche = d.ID_Pratiche
                                        }).Take(10).ToList();

                // 📌 Recupero ID cliente corrente
                int idClienteCorrente = GetIDClienteCorrente(idUtenteAttivo);
                bool isAdmin = IsAdminUser();

                // 📌 Query di base: solo notifiche non lette
                IQueryable<Notifiche> queryNotifiche = db.Notifiche
                    .Where(n => n.DataLettura == null);

                // 📌 Gestione visibilità in base al ruolo
                if (isAdmin && idClienteCorrente == idUtenteAttivo)
                {
                    // Admin NON impersonificato → vede tutte le notifiche
                }
                else if (isAdmin && idClienteCorrente != idUtenteAttivo)
                {
                    // Admin impersonificato → notifiche cliente + proprie
                    queryNotifiche = queryNotifiche.Where(n =>
                        n.ID_Utente == idClienteCorrente ||
                        n.ID_Utente == idUtenteAttivo);
                }
                else
                {
                    // Utente normale (professionista o collaboratore)
                    queryNotifiche = queryNotifiche.Where(n => n.ID_Utente == idClienteCorrente);
                }

                // 📌 Caricamento effettivo notifiche
                var notifiche = queryNotifiche
                    .OrderByDescending(n => n.DataCreazione)
                    .Take(5)
                    .Select(n => new NotificaViewModel
                    {
                        ID_Notifica = n.ID_Notifica,
                        Titolo = n.Titolo,
                        Descrizione = n.Descrizione,
                        DataCreazione = n.DataCreazione,
                        Stato = n.Stato,
                        Tipo = n.Tipo
                    }).ToList();

                var Operatore = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");

                var collaboratoriAssegnati = new List<UtenteViewModel>();
                if (operatore != null)
                {
                    int IdClienteProfessionista = operatore.ID_Cliente;
                    collaboratoriAssegnati = (
                        from ru in db.RelazioneUtenti
                        join u in db.Utenti on ru.ID_UtenteAssociato equals u.ID_Utente
                        where ru.ID_Utente == IdClienteProfessionista && ru.Stato == "Attivo"
                        select new UtenteViewModel
                        {
                            ID_Utente = u.ID_Utente,
                            Nome = u.Nome,
                            Cognome = u.Cognome,
                            TipoUtente = u.TipoUtente,
                            Stato = ru.Stato
                        }).ToList();
                }

                // 📌 Recupero ID cliente corrente (professionista o collaboratore)
                int iDClienteCorrente = GetIDClienteCorrente(idUtenteAttivo);

                // 📌 Query Avvisi Parcella
                var avvisiParcella = (
                    from a in db.AvvisiParcella
                    join p in db.Pratiche on a.ID_Pratiche equals p.ID_Pratiche
                    where a.Stato != "Annullato"
                          && (p.ID_UtenteResponsabile == idClienteCorrente || a.ID_UtenteCreatore == idClienteCorrente)
                          && a.DataAvviso >= inizio && a.DataAvviso <= fine
                    orderby a.DataAvviso descending
                    select new AvvisoParcellaViewModel
                    {
                        ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                        ID_Pratiche = (int)a.ID_Pratiche,
                        DataAvviso = a.DataAvviso,
                        Importo = a.Importo,
                        MetodoPagamento = a.MetodoPagamento,
                        Stato = a.Stato,
                        ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                        ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                        AliquotaIVA = a.AliquotaIVA,
                        ImportoIVA = a.ImportoIVA,
                        NomePratica = p.Titolo
                    }
                ).Take(5).ToList();

                // ⚙️ Popola/aggiorna il PREVISIONALE
                if (idClienteProfessionista > 0)
                {
                    GiornaliHelper.GeneraPrevisionale(db, idClienteProfessionista, inizio, fine, idUtenteAttivo);
                }

                // ⚙️ Popola/aggiorna l’ECONOMICO
                if (idClienteProfessionista > 0)
                {
                    GiornaliHelper.GeneraEconomicoDaAvvisiNetto(db, idClienteProfessionista, inizio, fine, idUtenteAttivo);
                }

                // ⚙️ Popola/aggiorna il FINANZIARIO
                if (idClienteProfessionista > 0)
                {
                    GiornaliHelper.GeneraFinanziario(db, inizio, fine, idUtenteAttivo);
                }

                // 📌 Operazioni Previsionali
                var operazioniPrevisionali = (from o in db.Previsione
                                              join p in db.Pratiche on o.ID_Pratiche equals p.ID_Pratiche
                                              where o.Stato == "Previsionale"
                                                    && p.ID_UtenteResponsabile == idClienteProfessionista
                                                    && o.DataPrevisione >= inizio && o.DataPrevisione <= fine
                                              orderby o.DataPrevisione descending
                                              select new OperazioniPrevisionaliViewModel
                                              {
                                                  ID_Previsione = o.ID_Previsione,
                                                  ID_Pratiche = o.ID_Pratiche,
                                                  ID_Professionista = o.ID_Professionista,
                                                  TipoOperazione = o.TipoOperazione,
                                                  Descrizione = o.Descrizione,
                                                  ImportoPrevisto = o.ImportoPrevisto ?? 0,
                                                  DataPrevisione = o.DataPrevisione,
                                                  Stato = o.Stato,
                                                  ID_UtenteCreatore = o.ID_UtenteCreatore ?? 0,
                                                  DataArchiviazione = o.DataArchiviazione,
                                                  ID_UtenteArchiviazione = o.ID_UtenteArchiviazione,
                                                  NomeCliente = p.Titolo
                                              })
                                              .Take(5)
                                              .ToList();

                // Esempio semplice: se non ho pratiche, liste vuote
                var operazioniEconomiche = new List<OperazioniEconomicheViewModel>();
                var operazioniFinanziarie = new List<OperaziomoFinanziarieViewModel>();

                if (idPratiche.Any())
                {
                    // Entrate
                    var entrate = (
                        from avv in db.AvvisiParcella
                        where avv.Stato != "Annullato"
                              && avv.ID_Pratiche.HasValue
                              && idPratiche.Contains(avv.ID_Pratiche.Value)
                              && avv.DataAvviso >= inizio && avv.DataAvviso <= fine
                        select new OperazioniEconomicheViewModel
                        {
                            ID_Transazione = avv.ID_AvvisoParcelle,
                            ID_Pratiche = avv.ID_Pratiche,
                            Importo = avv.Importo ?? 0,
                            Descrizione = "Avviso di Parcella",
                            DataOperazione = avv.DataAvviso
                        });

                    // Uscite
                    var statiPagati = new[] { "pagato", "pagata", "pagati" };
                    var uscite = (
                        from g in db.GenerazioneCosti
                        where statiPagati.Contains(g.Stato.Trim().ToLower())
                              && (!g.ID_Pratiche.HasValue || idPratiche.Contains(g.ID_Pratiche.Value))
                              && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                        select new OperazioniEconomicheViewModel
                        {
                            ID_Transazione = g.ID_GenerazioneCosto,
                            ID_Pratiche = g.ID_Pratiche,
                            Importo = -(g.Importo ?? 0),
                            Descrizione = g.Descrizione,
                            DataOperazione = g.DataRegistrazione
                        });

                    operazioniEconomiche = entrate
                        .Union(uscite)
                        .OrderByDescending(o => o.DataOperazione)
                        .Take(5)
                        .ToList();

                    // Operazioni Finanziarie
                    var idUtenteProfessionista = UserManager.GetIDUtenteAttivo();

                    var idsProfessionista = db.OperatoriSinergia
                        .Where(os => os.ID_UtenteCollegato == idUtenteProfessionista || os.ID_Cliente == idClienteProfessionista)
                        .Select(os => os.ID_Cliente)
                        .ToList();

                    if (!idsProfessionista.Contains(idClienteProfessionista))
                        idsProfessionista.Add(idClienteProfessionista);
                    if (!idsProfessionista.Contains(idUtenteProfessionista))
                        idsProfessionista.Add(idUtenteProfessionista);

                    operazioniFinanziarie = (
                        from o in db.Finanziario
                        join p in db.Pratiche on o.ID_Pratiche equals p.ID_Pratiche into pj
                        from pratica in pj.DefaultIfEmpty()
                        where o.Stato == "Finanziario"
                              && o.DataIncasso >= inizio && o.DataIncasso <= fine
                              && idsProfessionista.Contains((int)o.ID_Professionista)
                        orderby o.DataIncasso descending, o.ID_Finanziario descending
                        select new OperaziomoFinanziarieViewModel
                        {
                            ID_Finanza = o.ID_Finanziario,
                            ID_Pratiche = o.ID_Pratiche,
                            TipoOperazione = o.TipoOperazione,
                            Importo = o.ImportoFinanziario ?? 0,
                            Descrizione = o.Descrizione,
                            DataOperazione = o.DataIncasso,
                            NomePratica = (pratica != null ? pratica.Titolo : "Generale")
                        }
                    ).Take(5).ToList();
                }

                var model = new DashboardViewModel
                {
                    NomeUtente = utente.Nome,
                    NomeCliente = nomeCliente,
                    ID_ClienteSelezionato = idClienteProfessionista,
                    IntervalloGiorni = intervalloGiorni ?? 30,
                    ClientiDisponibili = clientiDisponibili,
                    Pratiche = listaPratiche,
                    DocumentiRecenti = documentiRecenti,
                    Notifiche = notifiche,
                    CollaboratoriAssegnati = collaboratoriAssegnati,
                    AvvisiParcella = avvisiParcella,
                    OperazioniPrevisionali = operazioniPrevisionali,
                    OperazioniEconomiche = operazioniEconomiche,
                    OperazioniFinanziarie = operazioniFinanziarie
                };

                Session["ID_Cliente"] = idClienteProfessionista;
                Session["ID_ClienteSelezionato"] = idCliente;
                ViewBag.IDUtenteCollegato = idUtenteAttivo;

                return View("Cruscotto", model);
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
                            // ✅ Trova il cliente in OperatoriSinergia
                            var cliente = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Cliente == idParsed && c.Stato == "Attivo");

                            if (cliente != null && cliente.ID_UtenteCollegato.HasValue)
                            {
                                Session["ID_UtenteImpers"] = cliente.ID_UtenteCollegato.Value; // ✅ Impersonificazione attiva
                            }
                        }
                        else if (tipo == "C_")
                        {
                            // Impersonificazione diretta su collaboratore
                            Session["ID_UtenteImpers"] = idParsed;
                        }
                    }

                    // Salva anche in cookie (facoltativo)
                    var cookie = new HttpCookie("Cliente", idCliente)
                    {
                        Expires = DateTime.Now.AddDays(7)
                    };
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
            IEnumerable<dynamic> lista = null;

            switch (nomeTabella)
            {
                case "AnagraficaCostiPratica":
                    lista = db.AnagraficaCostiPratica_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_AnagraficaCosto_a,
                            Data = (DateTime)x.DataArchiviazione,
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



                case "AvvisiParcella":
                    lista = db.AvvisiParcella_a
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


                case "Utenti":
                    lista = db.Utenti_a
                        .OrderByDescending(x => x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Utente,
                            Data = x.DataArchiviazione ?? x.UltimaModifica ?? x.DataCreazione ?? DateTime.Now,
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
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
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

                case "TipologieCosti":
                    lista = db.TipologieCosti_a
                        .OrderByDescending(x => x.DataUltimaModifica)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Storico,
                            Data = x.DataUltimaModifica ?? DateTime.Now,
                            ModificheTestuali = x.ModificheTestuali,
                            TipoModifica = x.Tipo,
                            NumeroVersione = x.NumeroVersione,
                            ID_UtenteUltimaModifica = x.ID_UtenteUltimaModifica.ToString(),
                            NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        }).ToList();
                    break;

                case "FinanziamentiProfessionisti":
                    lista = db.FinanziamentiProfessionisti_a
                        .OrderByDescending(x => x.DataArchiviazione)
                        .Select(x => new LogModificaViewModel
                        {
                            ID = x.ID_Finanziamento_Archivio,
                            Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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

                case "DatiBancari":
                    lista = db.DatiBancari_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_DatoBancario,
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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

                case "OrdiniFornitori":
                    lista = db.OrdiniFornitori_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Ordine,
                        Data = (DateTime)x.DataArchiviazione,
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

                case "Permessi":
                    lista = db.Permessi_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_Permesso,
                        Data = (DateTime)x.DataArchiviazione,
                        ModificheTestuali = x.ModificheTestuali,
                        NumeroVersione = x.NumeroVersione,
                        ID_UtenteUltimaModifica = x.ID_UtenteArchiviazione.ToString(),
                        NomeUtente = db.Utenti
                                .Where(u => u.ID_Utente == x.ID_UtenteArchiviazione)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                    }).ToList();
                    break;

                case "PermessiDelegatiProfessionista":
                    lista = db.PermessiDelegabiliPerProfessionista_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_PermessiDelegabiliPerProfessionista_a,
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

                case "PlafondUtente":
                    lista = db.PlafondUtente_a.Select(x => new LogModificaViewModel
                    {
                        ID = x.ID_PlannedPlafond_Archivio,
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataArchiviazione,
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
                        Data = (DateTime)x.DataCreazione, // Se DataArchiviazione è null, assegna 01/01/0001
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
        new SelectListItem { Text = "AnagraficaCostiPratica", Value = "AnagraficaCostiPratica" },
        new SelectListItem { Text = "AnagraficaCostiTeam", Value = "AnagraficaCostiTeam" },
        new SelectListItem { Text = "AvvisiParcella", Value = "AvvisiParcella" },
        new SelectListItem { Text = "Clienti", Value = "Clienti" },
        new SelectListItem { Text = "Cluster", Value = "Cluster" },
        new SelectListItem { Text = "CompensiPratica", Value = "CompensiPratica" },
        new SelectListItem { Text = "CostiPersonaliUtente", Value = "CostiPersonaliUtente" },
        new SelectListItem { Text = "CostiPratica", Value = "CostiPratica" },
        new SelectListItem { Text = "DatiBancari", Value = "DatiBancari" },
        new SelectListItem { Text = "DistribuzioneCostiTeam", Value = "DistribuzioneCostiTeam" },
        new SelectListItem { Text = "DocumentiAziende", Value = "DocumentiAziende" },
        new SelectListItem { Text = "DocumentiPratiche", Value = "DocumentiPratiche" },
        new SelectListItem { Text = "Economico", Value = "Economico" },
        new SelectListItem { Text = "FinanziamentiProfessionisti", Value = "FinanziamentiProfessionisti" },
        new SelectListItem { Text = "Finanziario", Value = "Finanziario" },
        new SelectListItem { Text = "Incassi", Value = "Incassi" },
        new SelectListItem { Text = "MembriTeam", Value = "MembriTeam" },
        new SelectListItem { Text = "MovimentiBancari", Value = "MovimentiBancari" },
        new SelectListItem { Text = "OperatoriSinergia", Value = "OperatoriSinergia" },
        new SelectListItem { Text = "OrdiniFornitori", Value = "OrdiniFornitori" },
        new SelectListItem { Text = "Permessi", Value = "Permessi" },
        new SelectListItem { Text = "PermessiDelegatiProfessionista", Value = "PermessiDelegatiProfessionista" },
        new SelectListItem { Text = "PlafondUtente", Value = "PlafondUtente" },
        new SelectListItem { Text = "Pratiche", Value = "Pratiche" },
        new SelectListItem { Text = "Previsione", Value = "Previsione" },
        new SelectListItem { Text = "Professioni", Value = "Professioni" },
        new SelectListItem { Text = "RelazionePraticheUtenti", Value = "RelazionePraticheUtenti" },
        new SelectListItem { Text = "RelazioneUtenti", Value = "RelazioneUtenti" },
        new SelectListItem { Text = "RicorrenzeCosti", Value = "RicorrenzeCosti" },
        new SelectListItem { Text = "RimborsiPratica", Value = "RimborsiPratica" },
        new SelectListItem { Text = "SettoriFornitori", Value = "SettoriFornitori" },
        new SelectListItem { Text = "TeamProfessionisti", Value = "TeamProfessionisti" },
        new SelectListItem { Text = "TemplateIncarichi", Value = "TemplateIncarichi" },
        new SelectListItem { Text = "TipologieCosti", Value = "TipologieCosti" },
        new SelectListItem { Text = "TipoRagioneSociale", Value = "TipoRagioneSociale" },
        new SelectListItem { Text = "Utenti", Value = "Utenti" }
    };

            return Json(tabelle, JsonRequestBehavior.AllowGet);
        }

        public ContentResult DettaglioModifica(string nomeTabella, int idArchivio)
        {
            string contenuto = "";

            switch (nomeTabella)
            {
                case "AnagraficaCostiPratica":
                    contenuto = db.AnagraficaCostiPratica_a.FirstOrDefault(x => x.ID_AnagraficaCosto_a == idArchivio)?.ModificheTestuali;
                    break;
                case "AvvisiParcella":
                    contenuto = db.AvvisiParcella_a.FirstOrDefault(x => x.ID_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Clienti":
                    contenuto = db.Clienti_a.FirstOrDefault(x => x.ID_Cliente_a == idArchivio)?.ModificheTestuali;
                    break;
                case "Cluster":
                    contenuto = db.Cluster_a.FirstOrDefault(x => x.ID_Cluster_a == idArchivio)?.ModificheTestuali;
                    break;
                case "CompensiPratica":
                    contenuto = db.CompensiPratica_a.FirstOrDefault(x => x.ID_CompensoArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "CostiPersonaliUtente":
                    contenuto = db.CostiPersonaliUtente_a.FirstOrDefault(x => x.IDVersioneCostoPersonale == idArchivio)?.ModificheTestuali;
                    break;
                case "AnagraficaCostiTeam":
                    contenuto = db.AnagraficaCostiTeam_a.FirstOrDefault(x => x.IDVersioneAnagraficaCostoTeam == idArchivio)?.ModificheTestuali;
                    break;
                case "CostiPratica":
                    contenuto = db.CostiPratica_a.FirstOrDefault(x => x.ID_CostoPratica_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "DatiBancari":
                    contenuto = db.DatiBancari_a.FirstOrDefault(x => x.ID_DatoBancario == idArchivio)?.ModificheTestuali;
                    break;
                case "DistribuzioneCostiTeam":
                    contenuto = db.DistribuzioneCostiTeam_a.FirstOrDefault(x => x.ID_DistribuzioneArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "DocumentiAziende":
                    contenuto = db.DocumentiAziende_a.FirstOrDefault(x => x.ID_Documento_A == idArchivio)?.ModificheTestuali;
                    break;
                case "DocumentiPratiche":
                    contenuto = db.DocumentiPratiche_a.FirstOrDefault(x => x.ID_Documento_a == idArchivio)?.ModificheTestuali;
                    break;
                case "Economico":
                    contenuto = db.Economico_a.FirstOrDefault(x => x.ID_EconomicoArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "FinanziamentiProfessionisti":
                    contenuto = db.FinanziamentiProfessionisti_a.FirstOrDefault(x => x.ID_Finanziamento_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Finanziario":
                    contenuto = db.Finanziario_a.FirstOrDefault(x => x.ID_FinanziarioArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Incassi":
                    contenuto = db.Incassi_a.FirstOrDefault(x => x.ID_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "MembriTeam":
                    contenuto = db.MembriTeam_a.FirstOrDefault(x => x.ID_VersioneMembroTeam == idArchivio)?.ModificheTestuali;
                    break;
                case "MovimentiBancari":
                    contenuto = db.MovimentiBancari_a.FirstOrDefault(x => x.ID_Movimento == idArchivio)?.ModificheTestuali;
                    break;
                case "OperatoriSinergia":
                    contenuto = db.OperatoriSinergia_a.FirstOrDefault(x => x.ID_Cliente == idArchivio)?.ModificheTestuali;
                    break;
                case "OrdiniFornitori":
                    contenuto = db.OrdiniFornitori_a.FirstOrDefault(x => x.ID_Ordine == idArchivio)?.ModificheTestuali;
                    break;
                case "Permessi":
                    contenuto = db.Permessi_a.FirstOrDefault(x => x.ID_Permesso == idArchivio)?.ModificheTestuali;
                    break;
                case "PermessiDelegatiProfessionista":
                    contenuto = db.PermessiDelegabiliPerProfessionista_a.FirstOrDefault(x => x.ID_PermessiDelegabiliPerProfessionista_a == idArchivio)?.ModificheTestuali;
                    break;
                case "PlafondUtente":
                    contenuto = db.PlafondUtente_a.FirstOrDefault(x => x.ID_PlannedPlafond_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Pratiche":
                    contenuto = db.Pratiche_a.FirstOrDefault(x => x.ID_Pratiche_a == idArchivio)?.ModificheTestuali;
                    break;
                case "Previsione":
                    contenuto = db.Previsione_a.FirstOrDefault(x => x.ID_PrevisioneArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Professioni":
                    contenuto = db.Professioni_a.FirstOrDefault(x => x.ID_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "RelazionePraticheUtenti":
                    contenuto = db.RelazionePraticheUtenti_a.FirstOrDefault(x => x.ID_Relazione_a == idArchivio)?.ModificheTestuali;
                    break;
                case "RelazioneUtenti":
                    contenuto = db.RelazioneUtenti_a.FirstOrDefault(x => x.ID_Relazione == idArchivio)?.ModificheTestuali;
                    break;
                case "RicorrenzeCosti":
                    contenuto = db.RicorrenzeCosti_a.FirstOrDefault(x => x.IDVersioneRicorrenza == idArchivio)?.ModificheTestuali;
                    break;
                case "RimborsiPratica":
                    contenuto = db.RimborsiPratica_a.FirstOrDefault(x => x.ID_RimborsoArchivio == idArchivio)?.ModificheTestuali;
                    break;
                case "SettoriFornitori":
                    contenuto = db.SettoriFornitori_a.FirstOrDefault(x => x.ID_Storico == idArchivio)?.ModificheTestuali;
                    break;
                case "TeamProfessionisti":
                    contenuto = db.TeamProfessionisti_a.FirstOrDefault(x => x.ID_VersioneTeam == idArchivio)?.ModificheTestuali;
                    break;
                case "TemplateIncarichi":
                    contenuto = db.TemplateIncarichi_a.FirstOrDefault(x => x.ID_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "TipologieCosti":
                    contenuto = db.TipologieCosti_a.FirstOrDefault(x => x.ID_Storico == idArchivio)?.ModificheTestuali;
                    break;
                case "TipoRagioneSociale":
                    contenuto = db.TipoRagioneSociale_a.FirstOrDefault(x => x.ID_Archivio == idArchivio)?.ModificheTestuali;
                    break;
                case "Utenti":
                    contenuto = db.Utenti_a.FirstOrDefault(x => x.IDVersioneUtenti == idArchivio)?.ModificheTestuali;
                    break;
                default:
                    contenuto = "⚠️ Tabella non gestita o record non trovato.";
                    break;
            }

            contenuto = string.IsNullOrWhiteSpace(contenuto) ? "Nessuna modifica registrata." : contenuto;

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

        private bool IsAdminUser()
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            var utenteCollegato = db.Utenti
                .FirstOrDefault(u => u.ID_Utente == idUtenteCollegato);
            return utenteCollegato?.TipoUtente == "Admin";
        }

        /// <summary>
        /// Restituisce l'ID del cliente professionista corrente.
        /// Se l'utente è collegato come collaboratore, risale a ID_Cliente passando per Utenti → OperatoriSinergia.
        /// Se non trova nulla, ritorna l'ID_Utente stesso (caso professionista diretto).
        /// </summary>
        public int GetIDClienteCorrente(int idUtenteCollegato)
        {
            System.Diagnostics.Debug.WriteLine($"[GetIDClienteCorrente] idUtenteCollegato: {idUtenteCollegato}");

            var idCliente = (from u in db.Utenti
                             join os in db.OperatoriSinergia
                                 on u.ID_Utente equals os.ID_UtenteCollegato
                             where u.ID_Utente == idUtenteCollegato
                             select os.ID_Cliente)
                            .FirstOrDefault();

            System.Diagnostics.Debug.WriteLine($"[GetIDClienteCorrente] idCliente trovato: {idCliente}");

            var result = idCliente > 0 ? idCliente : idUtenteCollegato;
            System.Diagnostics.Debug.WriteLine($"[GetIDClienteCorrente] risultato finale: {result}");

            return result;
        }

        private IQueryable<Notifiche> ApplyNotificaVisibility(
            IQueryable<Notifiche> query,
            int idUtenteCollegato,
            bool isAdmin)
        {
            System.Diagnostics.Debug.WriteLine($"[ApplyNotificaVisibility] idUtenteCollegato: {idUtenteCollegato}, isAdmin: {isAdmin}");

            int idClienteCorrente = GetIDClienteCorrente(idUtenteCollegato);
            System.Diagnostics.Debug.WriteLine($"[ApplyNotificaVisibility] idClienteCorrente: {idClienteCorrente}");

            if (isAdmin && idClienteCorrente == idUtenteCollegato)
            {
                System.Diagnostics.Debug.WriteLine("[ApplyNotificaVisibility] Admin non impersonificato → vede tutto");
                return query;
            }

            if (isAdmin)
            {
                System.Diagnostics.Debug.WriteLine("[ApplyNotificaVisibility] Admin impersonificato → notifiche cliente impersonato + proprie");
                return query.Where(n =>
                    n.ID_Utente == idClienteCorrente ||
                    n.ID_Utente == idUtenteCollegato);
            }

            System.Diagnostics.Debug.WriteLine("[ApplyNotificaVisibility] Utente normale → solo notifiche del cliente corrente");
            return query.Where(n => n.ID_Utente == idClienteCorrente);
        }

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

            // Filtri aggiuntivi
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

        [HttpPost]
        public ActionResult SegnaComeLetta(int id)
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            if (idUtenteCollegato <= 0) return new HttpStatusCodeResult(401);

            bool isAdmin = IsAdminUser();
            var query = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);

            var n = query.FirstOrDefault(x => x.ID_Notifica == id);
            if (n == null) return HttpNotFound();

            if (!n.Letto)
            {
                n.Letto = true;
                n.DataLettura = DateTime.Now;
                n.Stato = "Letta";
                db.SaveChanges();
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult SegnaTutteComeLette()
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            if (idUtenteCollegato <= 0) return new HttpStatusCodeResult(401);

            bool isAdmin = IsAdminUser();
            var query = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);

            foreach (var n in query.Where(x => !x.Letto))
            {
                n.Letto = true;
                n.DataLettura = DateTime.Now;
                n.Stato = "Letta";
            }
            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult EliminaNotifica(int id)
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
            if (idUtenteCollegato <= 0) return new HttpStatusCodeResult(401);

            bool isAdmin = IsAdminUser();
            var query = ApplyNotificaVisibility(db.Notifiche.AsQueryable(), idUtenteCollegato, isAdmin);

            var n = query.FirstOrDefault(x => x.ID_Notifica == id);
            if (n == null) return HttpNotFound();

            db.Notifiche.Remove(n);
            db.SaveChanges();
            return Json(new { success = true });
        }
        /* ===========================
           HELPER DI CREAZIONE NOTIFICHE
           =========================== */

        // Factory centrale
        private void AddNotifica(string titolo, string descrizione, string tipoCodice, int idDestinatarioUtente, int? idPratica = null)
        {
            db.Notifiche.Add(new Notifiche
            {
                Titolo = titolo,
                Descrizione = descrizione,
                Tipo = tipoCodice,         // es. PLAFOND_ASSENTE, PRATICA_SENZA_AVVISO, ...
                Stato = "Non letta",
                ID_Utente = idDestinatarioUtente,
                ID_Pratiche = idPratica,
                DataCreazione = DateTime.Now,
                Letto = false,
                Contatore = 1
            });
            db.SaveChanges();
        }

        /* --- PLAFOND --- */

        // Professionista senza plafond configurato
        private void CreaNotificaPlafondAssente(int idUtenteProfessionista, int? idPratica = null, string noteExtra = null)
        {
            var msg = "Plafond non configurato per il professionista." + (string.IsNullOrWhiteSpace(noteExtra) ? "" : " " + noteExtra);
            AddNotifica("Plafond assente", msg, "PLAFOND_ASSENTE", idUtenteProfessionista, idPratica);
        }

        // Plafond insufficiente per coprire costi
        private void CreaNotificaPlafondInsufficiente(int idUtenteProfessionista, decimal mancano, int? idPratica = null)
        {
            var msg = $"Plafond insufficiente: mancano {mancano:C} per coprire i costi previsti.";
            AddNotifica("Plafond insufficiente", msg, "PLAFOND_INSUFFICIENTE", idUtenteProfessionista, idPratica);
        }

        // Soglia plafond (early warning)
        private void CreaNotificaPlafondSoglia(int idUtenteProfessionista, decimal plafondResiduo, decimal soglia, int? idPratica = null)
        {
            var msg = $"Plafond residuo {plafondResiduo:C} inferiore alla soglia {soglia:C}.";
            AddNotifica("Plafond in soglia", msg, "PLAFOND_SOGLIA", idUtenteProfessionista, idPratica);
        }

        /* --- PRATICHE / ADMIN MONITORING --- */

        // Regime lavorazione non conforme
        private void CreaNotificaPraticaRegimeErrato(int idPratica, string motivo, int idDestinatarioAdmin)
        {
            var titolo = "Regime lavorazione non conforme";
            var msg = string.IsNullOrWhiteSpace(motivo) ? "Verifica la pratica." : motivo;
            AddNotifica(titolo, msg, "PRATICA_REGIME_ERRATO", idDestinatarioAdmin, idPratica);
        }

        // Pratica senza avviso di parcella
        private void CreaNotificaPraticaSenzaAvviso(int idPratica, int idDestinatarioAdmin)
        {
            AddNotifica("Pratica senza avviso di parcella", "Non risulta alcun avviso emesso.", "PRATICA_SENZA_AVVISO", idDestinatarioAdmin, idPratica);
        }

        // Pratica senza incasso
        private void CreaNotificaPraticaSenzaIncasso(int idPratica, int idDestinatarioAdmin)
        {
            AddNotifica("Pratica senza incasso", "Non risulta alcun incasso registrato.", "PRATICA_SENZA_INCASSO", idDestinatarioAdmin, idPratica);
        }

        // Avviso scaduto/non emesso entro X giorni dalla conclusione
        private void CreaNotificaAvvisoScaduto(int idPratica, DateTime dataScadenza, int idDestinatarioAdmin)
        {
            var msg = $"Avviso di parcella non emesso entro la scadenza ({dataScadenza:dd/MM/yyyy}).";
            AddNotifica("Avviso scaduto", msg, "PRATICA_AVVISO_SCADUTO", idDestinatarioAdmin, idPratica);
        }

        /* --- GENERAZIONE COSTI --- */

        // Generazione costo fallita
        private void CreaNotificaGenerazioneCostoFallita(int idDestinatarioAdmin, string descrErrore, int? idPratica = null)
        {
            var msg = "Errore in generazione costi: " + (descrErrore ?? "verificare log.");
            AddNotifica("Errore generazione costi", msg, "SYS_GENERAZIONE_COSTO_FALLITA", idDestinatarioAdmin, idPratica);
        }

        // Costo bloccato da eccezione
        private void CreaNotificaCostoBloccatoDaEccezione(int idDestinatarioAdmin, string categoria, DateTime dal, DateTime al, int? idPratica = null)
        {
            var msg = $"Costo '{categoria}' bloccato da eccezione nel periodo {dal:dd/MM/yyyy} – {al:dd/MM/yyyy}.";
            AddNotifica("Costo bloccato da eccezione", msg, "SYS_COSTO_BLOCCATO_ECCEZIONE", idDestinatarioAdmin, idPratica);
        }

        // Duplicato rilevato in generazione
        private void CreaNotificaDuplicatoGenerazione(int idDestinatarioAdmin, string chiaveLogica, int? idPratica = null)
        {
            var msg = $"Duplicato rilevato in generazione (chiave: {chiaveLogica}).";
            AddNotifica("Duplicato generazione costi", msg, "SYS_DUPLICATO_GENERAZIONE", idDestinatarioAdmin, idPratica);
        }

        /* --- TEAM / CONFIG --- */

        // Distribuzione team mancante o incompleta
        private void CreaNotificaTeamDistribuzioneMancante(int idDestinatarioAdmin, int idTeam, string nomeCosto)
        {
            var msg = $"Distribuzione mancante/incompleta per Team #{idTeam} sul costo '{nomeCosto}'.";
            AddNotifica("Distribuzione team mancante", msg, "SYS_TEAM_DISTRIBUZIONE_MANCANTE", idDestinatarioAdmin, null);
        }

        // Permessi incoerenti (es. utente senza permesso su azione richiesta)
        private void CreaNotificaPermessoIncoerente(int idDestinatarioAdmin, int idUtente, string azione)
        {
            var msg = $"Utente #{idUtente} ha tentato l'azione '{azione}' senza permessi.";
            AddNotifica("Permesso incoerente", msg, "SYS_PERMESSO_INCOERENTE", idDestinatarioAdmin, null);
        }

        /* --- PLAFOND --- */

        // Plafond negativo (sconfinamento)
        private void CreaNotificaPlafondNegativo(int idUtenteProfessionista, decimal saldo, int? idPratica = null)
        {
            var msg = $"Plafond in negativo: saldo attuale {saldo:C}. Intervenire per rientrare.";
            AddNotifica("Plafond negativo", msg, "PLAFOND_NEGATIVO", idUtenteProfessionista, idPratica);
        }

        /* --- PRATICHE --- */

        // Scadenza pratica imminente
        private void CreaNotificaPraticaScadenzaImminente(int idUtenteProfessionista, int idPratica, DateTime dataScadenza, int giorniResidui)
        {
            var msg = $"La pratica con scadenza {dataScadenza:dd/MM/yyyy} è imminente ({giorniResidui} giorni rimanenti).";
            AddNotifica("Scadenza pratica imminente", msg, "PRATICA_SCADENZA_IMMINENTE", idUtenteProfessionista, idPratica);
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

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? inizio.AddMonths(1).AddDays(-1);

            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtenteAttivo && o.TipoCliente == "Professionista");
            if (operatore == null)
                return PartialView("~/Views/Previsionale/_OperazioniPrevisionaliList.cshtml",
                    new List<OperazioniPrevisionaliViewModel>());

            int idClienteProfessionista = operatore.ID_Cliente;

            // ========= ENTRATE previste =========
            var entrateQuery =
                from p in db.Pratiche
                join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                join cl in db.Cluster
                     on new { p.ID_Pratiche, ID_Utente = p.ID_UtenteResponsabile }
                     equals new { ID_Pratiche = cl.ID_Pratiche, cl.ID_Utente } into clj
                from cluster in clj.DefaultIfEmpty()
                let dataPrev = (p.DataInizioAttivitaStimata.HasValue
                                ? p.DataInizioAttivitaStimata.Value
                                : (p.DataCreazione ?? DateTime.Now))
                let perc = (cluster != null ? cluster.PercentualePrevisione : 0m)
                let importoPrev = p.Budget * perc / 100m
                where p.ID_UtenteResponsabile == idClienteProfessionista
                      && p.Stato != "Eliminato"
                      && dataPrev >= inizio && dataPrev <= fine
                      && importoPrev != 0
                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = p.ID_Pratiche,
                    ID_Pratiche = p.ID_Pratiche,
                    ID_Professionista = p.ID_UtenteResponsabile,
                    Percentuale = perc,
                    TipoOperazione = "Entrata",
                    Descrizione = "Ricavo previsto da pratica",
                    ImportoPrevisto = importoPrev,
                    BudgetPratica = p.Budget,
                    DataPrevisione = dataPrev,
                    Stato = "Previsionale",
                    NomeCliente = c.TipoCliente == "Professionista" ? (c.Nome + " " + c.Cognome) : c.Nome,
                    NomePratica = p.Titolo
                };

            // ========= USCITE previste =========
            var usciteQuery =
                from g in db.GenerazioneCosti
                join p in db.Pratiche on g.ID_Pratiche equals p.ID_Pratiche into pj
                from pratica in pj.DefaultIfEmpty()
                where g.ID_Utente == idClienteProfessionista
                      && g.Approvato == false
                      && g.Stato == "Previsionale"
                      && g.DataRegistrazione >= inizio && g.DataRegistrazione <= fine
                select new OperazioniPrevisionaliViewModel
                {
                    ID_Previsione = g.ID_GenerazioneCosto,
                    ID_Pratiche = g.ID_Pratiche,
                    ID_Professionista = g.ID_Utente,
                    Percentuale = null,
                    TipoOperazione = "Uscita",
                    Descrizione = (g.Categoria ?? "Costo") +
                                  (string.IsNullOrEmpty(g.Descrizione) ? "" : " – " + g.Descrizione),
                    ImportoPrevisto = -(g.Importo ?? 0m),
                    BudgetPratica = null,
                    DataPrevisione = g.DataRegistrazione,
                    Stato = "Previsionale",
                    NomePratica = pratica != null ? pratica.Titolo : null
                };

            // Esecuzione query separata per evitare problemi EF
            var entrateList = entrateQuery.ToList();
            var usciteList = usciteQuery.ToList();

            // Unione in memoria
            var lista = entrateList;
            lista.AddRange(usciteList);

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

            int idClienteProfessionista = operatore.ID_Cliente;

            // ===== Team attivi
            var teamIds = db.MembriTeam
                .Where(mt => mt.ID_Professionista == idClienteProfessionista && mt.Attivo)
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
                 where p.ID_UtenteResponsabile == idClienteProfessionista
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
                           g.ID_Utente == idClienteProfessionista
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

            var clientiDelProfessionista = db.OperatoriSinergia
                .Where(o => o.TipoCliente == "Professionista" && o.ID_UtenteCollegato == idUtenteAttivo)
                .Select(o => o.ID_Cliente)
                .ToList();

            if (clientiDelProfessionista.Count == 0)
                return PartialView("~/Views/GiornaleFinanziario/_OperazioniFinanziarieList.cshtml",
                    new List<OperaziomoFinanziarieViewModel>());

            int idClienteProfessionista = clientiDelProfessionista[0];

            var teamIds = db.MembriTeam
                .Where(mt => clientiDelProfessionista.Contains(mt.ID_Professionista) && mt.Attivo)
                .Select(mt => mt.ID_Team)
                .Distinct()
                .ToList();

            // ===================== RECUPERO PRATICHE INCLUSE ANCHE DA COSTI PROGETTO =====================
            var tuttePraticheIds = (
                from p in db.Pratiche
                where (clientiDelProfessionista.Contains((int)p.ID_Owner) ||
                       clientiDelProfessionista.Contains(p.ID_UtenteResponsabile))
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
                where idPratica != null && db.Pratiche.Any(p => p.ID_Pratiche == idPratica && clientiDelProfessionista.Contains((int)p.ID_Owner))
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
                          g.ID_Utente == idClienteProfessionista ||
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
            var exception = HttpContext.Items["Exception"] as Exception;

            ViewBag.Exception = exception;
            ViewBag.Controller = RouteData.Values["controller"];
            ViewBag.Action = RouteData.Values["action"];

            return View("Error"); // chiama la View ~/Views/Shared/Error.cshtml
        }
    }
}
