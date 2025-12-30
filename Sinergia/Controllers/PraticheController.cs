using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Packaging;   // WordprocessingDocument
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using OpenXmlPowerTools;                  // HtmlConverter 
using Rotativa;
using Sinergia.ActionFilters;
using Sinergia.App_Helpers;
using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using static Sinergia.Models.PraticaViewModel;

namespace SinergiaMvc.Controllers
{
    [PermissionsActionFilter]
    public class PraticheController : Controller
    {
        private SinergiaDB db = new SinergiaDB();
        #region GESTIONE PRATICHE

        // ✅ View principale "GestionePratiche"
        public ActionResult GestionePratiche()
        {
            return View("~/Views/Pratiche/GestionePratiche.cshtml");
        }

        [HttpGet]
        public ActionResult GestionePraticheList(string ricerca = "", string tipoFiltro = "Tutti")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utenteCorrente == null)
                return Json(new { success = false, message = "Utente non autenticato." }, JsonRequestBehavior.AllowGet);

            var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Inviata","Conclusa" };

            var query = db.Pratiche.Where(p =>
                p.Stato != "Eliminato" &&
                statiValidi.Contains(p.Stato));

            if (!string.IsNullOrWhiteSpace(ricerca))
            {
                query = query.Where(p =>
                    p.Titolo.Contains(ricerca) ||
                    p.Descrizione.Contains(ricerca));
            }

            // dizionari per lookup
            var clientiEsterniIds = db.Clienti.Select(c => c.ID_Cliente).ToHashSet();
            var operatoriDict = db.OperatoriSinergia.ToDictionary(c => c.ID_Operatore);
            var utentiDict = db.Utenti.ToDictionary(u => u.ID_Utente);

            // 👤 Se Admin o Collaboratore impersonificano un professionista → filtro
            int idClienteProfessionistaSelezionato = UserManager.GetIDClienteCorrente();
            System.Diagnostics.Debug.WriteLine($"🟡 Sessione IDClienteProfessionistaCorrente = {idClienteProfessionistaSelezionato}");

            // === ADMIN (con impersonificazione) ===
            if (utenteCorrente.TipoUtente == "Admin" && idClienteProfessionistaSelezionato > 0)
            {
                int idOperatoreSelezionato = idClienteProfessionistaSelezionato;

                var op = db.OperatoriSinergia
                    .FirstOrDefault(o =>
                        o.ID_Operatore == idOperatoreSelezionato &&
                        o.Stato == "Attivo"
                    );

                if (op == null || !op.ID_UtenteCollegato.HasValue)
                {
                    query = query.Where(p => false);
                }
                else
                {
                    int idUtenteProfessionista = op.ID_UtenteCollegato.Value;

                    // ⬇️ STESSA IDENTICA LOGICA DEL PROFESSIONISTA
                    var operatoriDelProf = db.OperatoriSinergia
                        .Where(o =>
                            o.ID_UtenteCollegato == idUtenteProfessionista &&
                            o.Stato == "Attivo"
                        )
                        .Select(o => o.ID_Operatore)
                        .ToList();

                    query = query.Where(p =>
                        // OWNER → ID_OPERATORE
                        operatoriDelProf.Contains((int)p.ID_Owner)

                        // RESPONSABILE → ID_UTENTE
                        || p.ID_UtenteResponsabile == idUtenteProfessionista

                        // CLUSTER → ID_UTENTE
                        || db.Cluster.Any(c =>
                            c.ID_Pratiche == p.ID_Pratiche &&
                            operatoriDelProf.Contains(c.ID_Utente)
                        )

                        // RELAZIONI → ID_UTENTE
                        || db.RelazionePraticheUtenti.Any(r =>
                            r.ID_Pratiche == p.ID_Pratiche &&
                            operatoriDelProf.Contains(r.ID_Utente)
                        )
                    );
                }
            }


            else if (utenteCorrente.TipoUtente == "Professionista")
            {
                int idUtenteProfessionista = idUtente;

                // recupero UNA SOLA VOLTA gli operatori del professionista loggato
                var operatoriDelProf = db.OperatoriSinergia
                    .Where(o =>
                        o.ID_UtenteCollegato == idUtenteProfessionista &&
                        o.Stato == "Attivo"
                    )
                    .Select(o => o.ID_Operatore)
                    .ToList();

                // se non ha operatori → non vede nulla
                if (!operatoriDelProf.Any())
                {
                    query = query.Where(p => false);
                }
                else
                {
                    query = query.Where(p =>
                        // 1️⃣ OWNER (ID_Owner è ID_OPERATORE)
                        operatoriDelProf.Contains((int)p.ID_Owner)

                        // 2️⃣ RESPONSABILE (se è utente diretto)
                        || p.ID_UtenteResponsabile == idUtenteProfessionista

                        // 3️⃣ CLUSTER (ID_Utente = ID_OPERATORE)
                        || db.Cluster.Any(c =>
                            c.ID_Pratiche == p.ID_Pratiche &&
                            operatoriDelProf.Contains(c.ID_Utente)
                        )

                        // 4️⃣ RELAZIONI (stessa logica)
                        || db.RelazionePraticheUtenti.Any(r =>
                            r.ID_Pratiche == p.ID_Pratiche &&
                            operatoriDelProf.Contains(r.ID_Utente)
                        )
                    );
                }
            }

            else if (utenteCorrente.TipoUtente == "Collaboratore")
            {
                int idUtenteCollaboratore = UserManager.GetIDUtenteCollegato();

                // 🔗 Professionisti collegati al collaboratore
                var operatoriCollegati = (
                    from r in db.RelazioneUtenti
                    join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                    where r.ID_UtenteAssociato == idUtenteCollaboratore
                          && r.Stato == "Attivo"
                          && o.TipoCliente == "Professionista"
                          && o.Stato == "Attivo"
                    select o
                ).ToList();

                if (!operatoriCollegati.Any())
                {
                    query = query.Where(p => false);
                }
                else
                {
                    var listaIdOperatori = operatoriCollegati
                        .Select(o => o.ID_Operatore)
                        .ToList();

                    var listaIdUtenti = operatoriCollegati
                        .Where(o => o.ID_UtenteCollegato.HasValue)
                        .Select(o => o.ID_UtenteCollegato.Value)
                        .ToList();

                    // 🔥 Se selezionato uno specifico professionista
                    if (idClienteProfessionistaSelezionato > 0 &&
                        listaIdOperatori.Contains(idClienteProfessionistaSelezionato))
                    {
                        var opSel = operatoriCollegati
                            .First(o => o.ID_Operatore == idClienteProfessionistaSelezionato);

                        int idUtenteProfSel = opSel.ID_UtenteCollegato.Value;

                        // operatori DEL PROFESSIONISTA SELEZIONATO
                        var operatoriDelProf = operatoriCollegati
                            .Where(o => o.ID_UtenteCollegato == idUtenteProfSel)
                            .Select(o => o.ID_Operatore)
                            .ToList();

                        query = query.Where(p =>
                            // OWNER → ID_OPERATORE
                            (p.ID_Owner.HasValue && operatoriDelProf.Contains(p.ID_Owner.Value))

                            // RESPONSABILE → ID_UTENTE
                            || p.ID_UtenteResponsabile == idUtenteProfSel

                            // CLUSTER → ID_UTENTE
                            || db.Cluster.Any(c =>
                                c.ID_Pratiche == p.ID_Pratiche &&
                                c.ID_Utente == idUtenteProfSel
                            )

                            // RELAZIONI → ID_UTENTE
                            || db.RelazionePraticheUtenti.Any(r =>
                                r.ID_Pratiche == p.ID_Pratiche &&
                                r.ID_Utente == idUtenteProfSel
                            )
                        );
                    }
                    else
                    {
                        // ✔ TUTTI i professionisti collegati
                        query = query.Where(p =>
                            // OWNER → ID_OPERATORE
                            (p.ID_Owner.HasValue && listaIdOperatori.Contains(p.ID_Owner.Value))

                            // RESPONSABILE → ID_UTENTE
                            || listaIdUtenti.Contains(p.ID_UtenteResponsabile)

                            // CLUSTER → ID_UTENTE
                            || db.Cluster.Any(c =>
                                c.ID_Pratiche == p.ID_Pratiche &&
                                listaIdUtenti.Contains(c.ID_Utente)
                            )

                            // RELAZIONI → ID_UTENTE
                            || db.RelazionePraticheUtenti.Any(r =>
                                r.ID_Pratiche == p.ID_Pratiche &&
                                listaIdUtenti.Contains(r.ID_Utente)
                            )
                        );
                    }
                }
            }

            // === MAPPING IN VIEWMODEL ===
            var praticheList = query.ToList().Select(p =>
            {
                string tipoCliente = "";
                string nomeCliente = "";
                string ragioneSociale = "";
                string nomeCompleto = "";

                if (clientiEsterniIds.Contains(p.ID_Cliente))
                {
                    tipoCliente = "ClienteEsterno";
                    var cliEst = db.Clienti.FirstOrDefault(c => c.ID_Cliente == p.ID_Cliente);
                    if (cliEst != null)
                    {
                        ragioneSociale = cliEst.RagioneSociale;
                        nomeCompleto = (cliEst.Nome + " " + cliEst.Cognome).Trim();
                        nomeCliente = string.IsNullOrEmpty(cliEst.RagioneSociale)
                            ? nomeCompleto
                            : cliEst.RagioneSociale;
                    }
                }
                else if (operatoriDict.ContainsKey(p.ID_Cliente))
                {
                    var op = operatoriDict[p.ID_Cliente];
                    tipoCliente = op.TipoCliente ?? "";
                    ragioneSociale = op.TipoRagioneSociale;
                    nomeCompleto = (op.Nome + " " + op.Cognome).Trim();
                    nomeCliente = string.IsNullOrEmpty(op.TipoRagioneSociale)
                        ? nomeCompleto
                        : op.TipoRagioneSociale;
                }

                string nomeOwner = "";

                if (p.ID_Owner.HasValue)
                {
                    int id = p.ID_Owner.Value;

                    // 1️⃣ PROVA COME OPERATORE (caso corretto)
                    var ownerOp =
                        (from o in db.OperatoriSinergia
                         join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente into joined
                         from u in joined.DefaultIfEmpty()
                         where o.ID_Operatore == id
                         select new
                         {
                             Nome = (u != null ? u.Nome : o.Nome),
                             Cognome = (u != null ? u.Cognome : o.Cognome)
                         }).FirstOrDefault();

                    if (ownerOp != null)
                    {
                        nomeOwner = ownerOp.Nome + " " + ownerOp.Cognome;
                    }
                    else
                    {
                        // 2️⃣ PROVA COME UTENTE (caso corrotto → valido per Riccardo)
                        var ownerUser = db.Utenti
                            .Where(u => u.ID_Utente == id)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(ownerUser))
                            nomeOwner = ownerUser;
                    }
                }


                string nomeResponsabile = "";
                if (utentiDict.ContainsKey(p.ID_UtenteResponsabile))
                {
                    var u = utentiDict[p.ID_UtenteResponsabile];
                    nomeResponsabile = $"{u.Nome} {u.Cognome}";
                }

                // ============================================================
                // 📄 Recupero documenti incarico (per tipo)
                // ============================================================

                // "Incarico Fisso"
                var incaricoFisso = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == p.ID_Pratiche &&
                                d.CategoriaDocumento == "Incarico Fisso" &&
                                (d.Stato == "Attivo" || d.Stato == "Da firmare"))
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();

                // "Incarico A Ore"
                var incaricoAOre = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == p.ID_Pratiche &&
                                d.CategoriaDocumento == "Incarico A Ore" &&
                                (d.Stato == "Attivo" || d.Stato == "Da firmare"))
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();

                // "Incarico Giudiziale"
                var incaricoGiudiziale = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == p.ID_Pratiche &&
                                d.CategoriaDocumento == "Incarico Giudiziale" &&
                                (d.Stato == "Attivo" || d.Stato == "Da firmare"))
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();

                // "Incarico Firmato (PDF)"
                var incaricoFirmato = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == p.ID_Pratiche &&
                                d.Stato == "Firmato")
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();



                bool haIncaricoGenerato = incaricoFisso != null || incaricoAOre != null || incaricoGiudiziale != null;

                return new PraticaViewModel
                {
                    ID_Pratiche = p.ID_Pratiche,
                    Titolo = p.Titolo,
                    Descrizione = p.Descrizione,
                    Tipologia = p.Tipologia,
                    DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
                    DataFineAttivitaStimata = p.DataFineAttivitaStimata,
                    Stato = p.Stato,
                    ID_Cliente = p.ID_Cliente,
                    TipoCliente = tipoCliente,

                    NomeCliente = nomeCliente,
                    ClienteRagioneSociale = ragioneSociale,
                    ClienteNomeCompleto = nomeCompleto,

                    ID_UtenteResponsabile = p.ID_UtenteResponsabile,
                    NomeUtenteResponsabile = nomeResponsabile,
                    Budget = p.Budget,
                    DataCreazione = p.DataCreazione,
                    UltimaModifica = p.UltimaModifica,
                    Note = p.Note,
                    NomeOwner = nomeOwner,
                    ImportoFisso = p.ImportoFisso,
                    TariffaOraria = p.TariffaOraria,
                    AccontoGiudiziale = p.AccontoGiudiziale,
                    GradoGiudizio = p.GradoGiudizio,
                    TerminiPagamento = p.TerminiPagamento,
                    OrePreviste = p.OrePreviste,
                    OreEffettive = p.OreEffettive,

                    // ✅ nuovi campi incarichi
                    HaIncaricoGenerato = haIncaricoGenerato,
                    ID_IncaricoFisso = incaricoFisso?.ID_Documento,
                    ID_IncaricoAOre = incaricoAOre?.ID_Documento,
                    ID_IncaricoGiudiziale = incaricoGiudiziale?.ID_Documento,

                    NomeFileIncaricoFisso = incaricoFisso?.NomeFile,
                    NomeFileIncaricoAOre = incaricoAOre?.NomeFile,
                    NomeFileIncaricoGiudiziale = incaricoGiudiziale?.NomeFile,
                    // ✅ Nuovo: incarico firmato
                    ID_IncaricoFirmato = incaricoFirmato?.ID_Documento,
                    NomeFileIncaricoFirmato = incaricoFirmato != null
                        ? (incaricoFirmato.NomeFile + incaricoFirmato.Estensione)
                        : null

                };
            }).ToList();


            if (tipoFiltro == "Azienda")
                praticheList = praticheList.Where(p => p.TipoCliente == "Azienda").ToList();
            else if (tipoFiltro == "Professionista")
                praticheList = praticheList.Where(p => p.TipoCliente == "Professionista").ToList();

            // 🔐 Gestione permessi
            var permessiUtente = new PermessiViewModel { Permessi = new List<PermessoSingoloViewModel>() };

            if (utenteCorrente.TipoUtente == "Admin")
            {
                permessiUtente.Permessi.Add(new PermessoSingoloViewModel { Aggiungi = true, Modifica = true, Elimina = true });
            }
            else
            {
                var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtente).ToList();
                foreach (var p in permessiDb)
                {
                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = p.Aggiungi ?? false,
                        Modifica = p.Modifica ?? false,
                        Elimina = p.Elimina ?? false
                    });
                }
            }

            ViewBag.PuoModificare = permessiUtente?.Permessi.Any(p => p.Modifica) == true;
            ViewBag.PuoEliminare = permessiUtente?.Permessi.Any(p => p.Elimina) == true;
            ViewBag.MostraAzioni = (ViewBag.PuoModificare || ViewBag.PuoEliminare);

            ViewBag.Permessi = permessiUtente;
            ViewBag.Professioni = db.Professioni
                .Select(p => new SelectListItem
                {
                    Value = p.ProfessioniID.ToString(),
                    Text = p.Descrizione
                }).ToList();

            ViewBag.TipoUtente = utenteCorrente.TipoUtente;
            // ✅ Se il professionista selezionato è > 0 lo passo alla ViewBag, altrimenti metto null
            ViewBag.IDClienteProfessionistaCorrente = idClienteProfessionistaSelezionato > 0
                ? (int?)idClienteProfessionistaSelezionato
                : null;

            return PartialView("~/Views/Pratiche/_GestionePraticheList.cshtml", praticheList);
        }

        [HttpPost]
        public ActionResult CreaPratica(PraticaViewModel model)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            System.Diagnostics.Debug.WriteLine("⏱ [CreaPratica] Inizio esecuzione");
            // 🔍 Debug: valore raw dal form (senza binding)
            System.Diagnostics.Debug.WriteLine(">>> Raw form Budget = " + Request.Form["Budget"]);

            // 🔍 Debug: valore già bindato nel model
            System.Diagnostics.Debug.WriteLine($"📥 [CreaPratica] Budget ricevuto dal form = {model.Budget}");

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    int idUtente = UserManager.GetIDUtenteAttivo();
                    DateTime now = DateTime.Now;

                    // 1️⃣ Carica cliente esterno
                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == model.ID_Cliente);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente esterno non trovato." });

                    // 2️⃣ Recupera Operatore (professionista)
                    var operatore = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Operatore == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);
                    if (operatore == null)
                        return Json(new { success = false, message = "Professionista collegato non trovato." });

                    if (operatore.ID_Owner == null)
                    {
                        operatore.ID_Owner = operatore.ID_Operatore;
                        db.SaveChanges();
                    }

                    int idOwner = operatore.ID_Owner.Value;

                    // ⚠️ Metodo di compenso obbligatorio SOLO se nuova pratica
                    if (model.ID_Pratiche == 0 && string.IsNullOrEmpty(model.MetodoCompenso))
                    {
                        return Json(new { success = false, message = "⚠️ Seleziona il metodo di compenso." });
                    }

                    // 3️⃣ Crea pratica
                    var pratica = new Pratiche
                    {
                        Titolo = model.Titolo,
                        Descrizione = model.Descrizione,
                        DataInizioAttivitaStimata = model.DataInizioAttivitaStimata,
                        DataFineAttivitaStimata = model.DataFineAttivitaStimata,
                        Stato = model.Stato,
                        ID_Cliente = cliente.ID_Cliente,
                        ID_UtenteResponsabile = model.ID_UtenteResponsabile > 0 ? model.ID_UtenteResponsabile : idOwner,
                        ID_UtenteCreatore = idUtente,
                        ID_Owner = idOwner,
                        Budget = model.Budget,
                        Note = model.Note,
                        DataCreazione = now,
                        UltimaModifica = now,
                        TrattenutaPersonalizzata = model.TrattenutaPersonalizzata,
                        Tipologia = model.Tipologia,
                        OggettoPratica = model.OggettoPratica
                    };

                    db.Pratiche.Add(pratica);
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"⏱ Dopo salvataggio Pratica: {stopwatch.ElapsedMilliseconds}ms");

                    // ======================================================
                    // 🗂️ Inserimento in tabella archivio PRATICHE_A
                    // ======================================================
                    try
                    {
                        var pratica_a = new Pratiche_a
                        {
                            ID_Pratica_Originale = pratica.ID_Pratiche,
                            Titolo = pratica.Titolo,
                            Descrizione = pratica.Descrizione,
                            DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                            DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                            Stato = pratica.Stato,
                            ID_Cliente = pratica.ID_Cliente,
                            ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                            ID_UtenteCreatore = pratica.ID_UtenteCreatore,
                            ID_Owner = pratica.ID_Owner,
                            Budget = pratica.Budget,
                            Note = pratica.Note,
                            DataCreazione = pratica.DataCreazione,
                            UltimaModifica = pratica.UltimaModifica,
                            TrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata,
                            Tipologia = pratica.Tipologia,
                            OggettoPratica = pratica.OggettoPratica,
                            NumeroVersione = 1,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            ModificheTestuali = "Creazione pratica"
                        };

                        db.Pratiche_a.Add(pratica_a);
                        db.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("✅ [CreaPratica] Inserita versione archivio in Pratiche_a.");
                    }
                    catch (Exception exArch)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ [CreaPratica] Errore inserimento in Pratiche_a: {exArch.Message}");
                    }

                    // 🔄 Inserisci nuovi compensi dal JSON
                    if (!string.IsNullOrEmpty(model.CompensiJSON))
                    {
                        var compensiRaw = Newtonsoft.Json.JsonConvert
                            .DeserializeObject<List<Dictionary<string, object>>>(model.CompensiJSON);

                        if (compensiRaw != null && compensiRaw.Any())
                        {
                            int ordine = 1;
                            foreach (var c in compensiRaw)
                            {
                                var dettaglio = new CompensiPraticaDettaglio
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    TipoCompenso = c.ContainsKey("Metodo") ? c["Metodo"]?.ToString() : null,

                                    Descrizione =
                                        (c.ContainsKey("Descrizione") ? c["Descrizione"]?.ToString() :
                                        (c.ContainsKey("Descrizione_1") ? c["Descrizione_1"]?.ToString() : null))
                                        ?? (c.ContainsKey("Ruolo_1") ? c["Ruolo_1"]?.ToString() : null)
                                        ?? (c.ContainsKey("Ruolo") ? c["Ruolo"]?.ToString() : null),

                                    Importo =
                                        (c.ContainsKey("Importo") && decimal.TryParse(c["Importo"]?.ToString(), out var imp) ? imp :
                                        (c.ContainsKey("Importo_1") && decimal.TryParse(c["Importo_1"]?.ToString(), out var imp1) ? imp1 : (decimal?)null))
                                        ?? (c.ContainsKey("Tariffa_1") && decimal.TryParse(c["Tariffa_1"]?.ToString(), out var impT) ? impT : (decimal?)null),

                                    Categoria = c.ContainsKey("Tipologia") ? c["Tipologia"]?.ToString()
                                              : (c.ContainsKey("Tipologia_1") ? c["Tipologia_1"]?.ToString() : "Contrattuale"),

                                    ValoreStimato =
                                        (c.ContainsKey("ValoreStimato") && decimal.TryParse(c["ValoreStimato"]?.ToString(), out var val) ? val :
                                        (c.ContainsKey("ValoreStimato_1") && decimal.TryParse(c["ValoreStimato_1"]?.ToString(), out var val1) ? val1 : (decimal?)null)),

                                    Ordine = ordine++,
                                    EstremiGiudizio = c.ContainsKey("EstremiGiudizio") ? c["EstremiGiudizio"]?.ToString() : null,
                                    OggettoIncarico = c.ContainsKey("OggettoIncarico") ? c["OggettoIncarico"]?.ToString() : null,
                                    DataCreazione = now,
                                    ID_UtenteCreatore = idUtente,

                                    // 👇 Intestatario
                                    ID_ProfessionistaIntestatario =
                                        (c.ContainsKey("ID_ProfessionistaIntestatario") && int.TryParse(c["ID_ProfessionistaIntestatario"]?.ToString(), out var idProf))
                                        ? (int?)idProf : null,

                                    // 👇 Collaboratori salvati come stringa JSON
                                    Collaboratori = c.ContainsKey("Collaboratori") && c["Collaboratori"] != null
                                        ? Newtonsoft.Json.JsonConvert.SerializeObject(c["Collaboratori"])
                                        : null
                                };

                                db.CompensiPraticaDettaglio.Add(dettaglio);
                            }

                            db.SaveChanges();
                        }
                    }
                    // ✅ Salvataggio file incarico (robusto con accenti, doppie estensioni, e fallback sicuri)
                    if (model.IncaricoProfessionale != null && model.IncaricoProfessionale.ContentLength > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("📂 [UPLOAD INCARICO] Inizio caricamento...");

                        using (var ms = new MemoryStream())
                        {
                            model.IncaricoProfessionale.InputStream.CopyTo(ms);
                            var nomeOriginale = model.IncaricoProfessionale.FileName;
                            System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] File originale: {nomeOriginale}");

                            // 🧩 1️⃣ Estrazione e normalizzazione nome ed estensione
                            var nomeFile = Path.GetFileName(nomeOriginale) ?? "Incarico";
                            var estensione = Path.GetExtension(nomeFile)?.ToLower();

                            // 🔧 Gestione file firmati digitalmente
                            if (nomeFile.EndsWith(".p7m.pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                estensione = ".pdf";
                                nomeFile = Path.GetFileNameWithoutExtension(nomeFile.Replace(".p7m", ""));
                                System.Diagnostics.Debug.WriteLine("[UPLOAD INCARICO] File .p7m.pdf → corretto come PDF");
                            }
                            else if (nomeFile.EndsWith(".xml.p7m", StringComparison.OrdinalIgnoreCase))
                            {
                                estensione = ".p7m";
                                nomeFile = Path.GetFileNameWithoutExtension(nomeFile);
                                System.Diagnostics.Debug.WriteLine("[UPLOAD INCARICO] File .xml.p7m → corretto come P7M");
                            }

                            // 🧩 2️⃣ Sanificazione nome file da accenti, apostrofi e simboli
                            nomeFile = System.Text.RegularExpressions.Regex.Replace(
                                nomeFile.Normalize(System.Text.NormalizationForm.FormC),
                                @"[^\w\.\- ]", "_"
                            );

                            // 🧩 3️⃣ Fallback per estensione e content-type
                            if (string.IsNullOrWhiteSpace(estensione))
                                estensione = ".pdf";

                            var tipoContenuto = model.IncaricoProfessionale.ContentType;
                            if (string.IsNullOrWhiteSpace(tipoContenuto) || tipoContenuto == "application/octet-stream")
                                tipoContenuto = "application/pdf";

                            // 🧩 4️⃣ Controlla se esiste già un incarico per questa pratica (stesso nome)
                            var incaricoEsistente = db.DocumentiPratiche
                                .FirstOrDefault(d => d.ID_Pratiche == pratica.ID_Pratiche && d.NomeFile == nomeFile);

                            if (incaricoEsistente != null)
                            {
                                // 🔄 Aggiorna file esistente
                                incaricoEsistente.Documento = ms.ToArray();
                                incaricoEsistente.DataCaricamento = DateTime.Now;
                                incaricoEsistente.ID_UtenteCaricamento = idUtente;
                                incaricoEsistente.TipoContenuto = tipoContenuto;
                                incaricoEsistente.Estensione = estensione;
                                incaricoEsistente.Stato = "Firmato";
                                incaricoEsistente.Note = "Aggiornato incarico firmato (p7m/pdf)";
                                System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Aggiornato documento esistente: {nomeFile}");
                            }
                            else
                            {
                                // ➕ Inserisci nuovo incarico
                                var doc = new DocumentiPratiche
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    NomeFile = nomeFile,
                                    Estensione = estensione,
                                    TipoContenuto = tipoContenuto,
                                    Documento = ms.ToArray(),
                                    DataCaricamento = DateTime.Now,
                                    ID_UtenteCaricamento = idUtente,
                                    Stato = "Firmato",
                                    Note = "Incarico firmato (upload p7m/pdf)"
                                };

                                db.DocumentiPratiche.Add(doc);
                                System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Nuovo documento aggiunto: {nomeFile}");
                            }

                            // 🧩 5️⃣ Log dettagliato per debug
                            System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Estensione={estensione}, Tipo={tipoContenuto}, Bytes={ms.Length}");

                            db.SaveChanges();
                            System.Diagnostics.Debug.WriteLine("📁 [UPLOAD INCARICO] SaveChanges completato ✅");
                        }
                    }

                    // 6️⃣ Inserisci solo Owner nel cluster
                    var ownerFee = db.TipologieCosti
                        .FirstOrDefault(t => t.Nome == "Owner Fee" && t.Stato == "Attivo" && t.Tipo == "Percentuale");
                    decimal percentualeOwner = ownerFee?.ValorePercentuale ?? 5;

                    // 6️⃣ Inserisci OWNER nel cluster (ID_UTENTE, NON ID_OPERATORE)
                    db.Cluster.Add(new Cluster
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = operatore.ID_UtenteCollegato.Value, // ✅ ID_UTENTE
                        TipoCluster = "Owner",
                        PercentualePrevisione = percentualeOwner,
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    });


                    // 7️⃣ Collaboratori → solo quelli selezionati dall’utente
                    if (model.UtentiAssociati != null)
                    {
                        foreach (var collab in model.UtentiAssociati)
                        {
                            // evita duplicato owner
                            if (collab.ID_Utente == operatore.ID_UtenteCollegato.Value)
                                continue;

                            // recupero operatore del collaboratore
                            var opCollab = db.OperatoriSinergia
                                .FirstOrDefault(o => o.ID_UtenteCollegato == collab.ID_Utente && o.Stato == "Attivo");

                            if (opCollab == null)
                                continue;

                            // normalizzazione percentuale
                            decimal percentuale = collab.PercentualePrevisione;

                            if (percentuale == 0)
                            {
                                var raw = Request.Form[$"UtentiAssociati[{model.UtentiAssociati.IndexOf(collab)}].PercentualePrevisione"];
                                if (!string.IsNullOrWhiteSpace(raw))
                                {
                                    raw = raw.Replace(',', '.').Replace("%", "").Trim();
                                    decimal.TryParse(
                                        raw,
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out percentuale
                                    );
                                }
                            }

                            // ✅ CLUSTER → ID_UTENTE
                            db.Cluster.Add(new Cluster
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = collab.ID_Utente, // ✅ ID_UTENTE
                                TipoCluster = string.IsNullOrEmpty(collab.TipoCluster)
                                    ? "Collaboratore"
                                    : collab.TipoCluster,
                                PercentualePrevisione = percentuale,
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });

                            // ✅ RELAZIONE → ID_UTENTE
                            db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = collab.ID_Utente, // ✅ ID_UTENTE
                                Ruolo = "Collaboratore",
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }

                    db.SaveChanges();

                    // 8️⃣ Rimborsi
                    if (model.Rimborsi != null)
                    {
                        foreach (var rim in model.Rimborsi)
                        {
                            db.RimborsiPratica.Add(new RimborsiPratica
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                Descrizione = rim.Descrizione,
                                Importo = rim.Importo,
                                ID_UtenteCreatore = idUtente,
                                DataInserimento = now
                            });
                        }
                    }

                    // 9️⃣ Costi pratica
                    if (model.CostiPratica != null && model.CostiPratica.Any())
                    {
                        foreach (var cp in model.CostiPratica)
                        {
                            var anagrafica = db.AnagraficaCostiPratica
                                .FirstOrDefault(a => a.ID_AnagraficaCosto == cp.ID_AnagraficaCosto);

                            // 🔹 Normalizzazione date
                            DateTime dataInserimento = cp.DataInserimento != default(DateTime)
                                ? cp.DataInserimento
                                : DateTime.Now;

                            DateTime? dataCompetenza = cp.DataCompetenzaEconomica != default(DateTime)
                                ? cp.DataCompetenzaEconomica
                                : (DateTime?)null;

                            // 🔹 Se c'è l’anagrafica, crea il costo collegato
                            if (anagrafica != null)
                            {
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    ID_AnagraficaCosto = anagrafica.ID_AnagraficaCosto,
                                    Descrizione = $"{anagrafica.Nome} - {anagrafica.Descrizione}",
                                    Importo = cp.Importo,
                                    ID_Fornitore = cp.ID_Fornitore, // ✅ nuovo campo
                                    DataCompetenzaEconomica = dataCompetenza, // ✅ nuovo campo
                                    ID_UtenteCreatore = idUtente,
                                    DataInserimento = dataInserimento
                                });
                            }
                            else
                            {
                                // 🔹 In caso non ci sia l’anagrafica
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    Descrizione = cp.Descrizione ?? "Voce non trovata",
                                    Importo = cp.Importo,
                                    ID_Fornitore = cp.ID_Fornitore, // ✅ nuovo campo
                                    DataCompetenzaEconomica = dataCompetenza, // ✅ nuovo campo
                                    ID_UtenteCreatore = idUtente,
                                    DataInserimento = dataInserimento
                                });
                            }
                        }
                    }

                    db.SaveChanges();


                    // ======================================================
                    // 📊 [PREVISIONALE] Creazione automatica alla CreaPratica
                    // ======================================================
                    try
                    {
                        System.Diagnostics.Trace.WriteLine("🔹 [CreaPratica] Avvio creazione previsionale automatica...");

                        int idPratica = pratica.ID_Pratiche;
                        int idUtenteCreatore = idUtente;
                        DateTime dataPrev = pratica.DataInizioAttivitaStimata ?? DateTime.Now;

                        decimal budget = pratica.Budget;
                        if (budget <= 0)
                        {
                            System.Diagnostics.Trace.WriteLine("⚠️ [CreaPratica] Budget nullo, nessuna previsione creata.");
                        }
                        else
                        {
                            // =====================================================
                            // 🏛️ Recupero parametri economici base
                            // =====================================================
                            decimal percOwnerFee = db.RicorrenzeCosti
                                .Where(r => r.Categoria == "Owner Fee" && r.Attivo && r.TipoValore == "Percentuale")
                                .OrderByDescending(r => r.DataInizio)
                                .Select(r => (decimal?)r.Valore)
                                .FirstOrDefault() ?? 0m;

                            decimal percTrattenuta = db.RicorrenzeCosti
                                .Where(r => r.Categoria == "Trattenuta Sinergia" && r.Attivo && r.TipoValore == "Percentuale")
                                .OrderByDescending(r => r.DataInizio)
                                .Select(r => (decimal?)r.Valore)
                                .FirstOrDefault() ?? 0m;

                            // =====================================================
                            // 👥 Cluster collegati alla pratica
                            // =====================================================
                            var clusterList = db.Cluster
                                .Where(c => c.ID_Pratiche == idPratica && c.TipoCluster == "Collaboratore")
                                .ToList();

                            decimal sommaCluster = clusterList.Sum(c => c.PercentualePrevisione);

                            // ✅ Include anche la Trattenuta Sinergia nel bilanciamento del 100%
                            decimal percResponsabile = Math.Max(0, 100 - percOwnerFee - percTrattenuta - sommaCluster);

                            // 🔍 Controllo bilanciamento
                            decimal sommaTotale = percResponsabile + percOwnerFee + percTrattenuta + sommaCluster;
                            if (Math.Round(sommaTotale, 2) != 100m)
                            {
                                System.Diagnostics.Trace.WriteLine($"⚠️ [CreaPratica] Percentuali non bilanciate: somma = {sommaTotale:N2}% (Resp={percResponsabile}, Owner={percOwnerFee}, Tratt={percTrattenuta}, Cluster={sommaCluster})");
                            }

                            System.Diagnostics.Trace.WriteLine($"📈 [CreaPratica] Percentuali → Resp={percResponsabile}%, Owner={percOwnerFee}%, Tratt={percTrattenuta}%, ClusterTot={sommaCluster}%");

                            // =====================================================
                            // 📌 Funzione helper locale
                            // =====================================================
                            void AggiungiPrevisione(string tipo, int? idProfessionista, decimal percentuale, decimal importo, string descrizione)
                            {
                                if (idProfessionista == null || importo <= 0)
                                {
                                    System.Diagnostics.Trace.WriteLine($"⚠️ [Previsione] Scartata riga {descrizione} (id={idProfessionista}, importo={importo})");
                                    return;
                                }

                                var prev = new Previsione
                                {
                                    ID_Pratiche = idPratica,
                                    ID_Professionista = idProfessionista,
                                    Percentuale = percentuale,
                                    TipoOperazione = tipo,
                                    Descrizione = descrizione,
                                    ImportoPrevisto = Math.Round(importo, 2),
                                    DataPrevisione = dataPrev,
                                    Stato = "Previsionale",
                                    ID_UtenteCreatore = idUtenteCreatore
                                };

                                db.Previsione.Add(prev);
                                db.SaveChanges();

                                db.Previsione_a.Add(new Previsione_a
                                {
                                    ID_PrevisioneOriginale = prev.ID_Previsione,
                                    ID_Pratiche = prev.ID_Pratiche,
                                    ID_Professionista = prev.ID_Professionista,
                                    Percentuale = prev.Percentuale,
                                    TipoOperazione = prev.TipoOperazione,
                                    Descrizione = prev.Descrizione,
                                    ImportoPrevisto = prev.ImportoPrevisto,
                                    DataPrevisione = prev.DataPrevisione,
                                    Stato = prev.Stato,
                                    ID_UtenteCreatore = prev.ID_UtenteCreatore,
                                    NumeroVersione = 1,
                                    DataArchiviazione = DateTime.Now,
                                    ID_UtenteArchiviazione = idUtenteCreatore,
                                    ModificheTestuali = "Creazione automatica previsionale da pratica"
                                });

                                db.SaveChanges();
                                System.Diagnostics.Trace.WriteLine($"✅ [Previsione] Aggiunta → {descrizione}, {importo:N2} €, {percentuale:N2}% (tipo={tipo})");
                            }

                            // =====================================================
                            // 👤 1️⃣ RESPONSABILE – quota netta
                            // =====================================================
                            decimal quotaResp = budget * percResponsabile / 100m;
                            AggiungiPrevisione("Entrata", pratica.ID_UtenteResponsabile, percResponsabile, quotaResp,
                                $"Ricavo previsto (Responsabile): {pratica.Titolo}");

                            // =====================================================
                            // 👑 2️⃣ OWNER FEE
                            // =====================================================
                            if (pratica.ID_Owner != null && percOwnerFee > 0)
                            {
                                decimal quotaOwner = budget * percOwnerFee / 100m;
                                AggiungiPrevisione("Entrata", pratica.ID_Owner, percOwnerFee, quotaOwner,
                                    $"Quota Owner Fee ({percOwnerFee:N2}%): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 💼 3️⃣ TRATTENUTA SINERGIA (uscita)
                            // =====================================================
                            if (percTrattenuta > 0)
                            {
                                decimal quotaTratt = budget * percTrattenuta / 100m;
                                AggiungiPrevisione("Uscita", pratica.ID_UtenteResponsabile, percTrattenuta, quotaTratt,
                                    $"Trattenuta Sinergia ({percTrattenuta:N2}%): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 👥 4️⃣ COLLABORATORI CLUSTER
                            // =====================================================
                            foreach (var c in clusterList)
                            {
                                decimal importoCluster = budget * c.PercentualePrevisione / 100m;
                                AggiungiPrevisione("Entrata", c.ID_Utente, c.PercentualePrevisione, importoCluster,
                                    $"Quota Collaboratore (Cluster): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 🧾 5️⃣ COLLABORATORI DEI COMPENSI DETTAGLIO
                            // =====================================================
                            // ⚠️ NOTA:
                            // Le percentuali dei collaboratori nei CompensiPraticaDettaglio
                            // NON si sommano alle percentuali del previsionale principale.
                            // Rappresentano solo ripartizioni interne del singolo compenso,
                            // non quote del budget complessivo della pratica.
                            // =====================================================
                            var compensiDettaglio = db.CompensiPraticaDettaglio
                                .Where(cDett => cDett.ID_Pratiche == idPratica)
                                .ToList();

                            foreach (var comp in compensiDettaglio)
                            {
                                if (string.IsNullOrWhiteSpace(comp.Collaboratori))
                                    continue;

                                try
                                {
                                    var listaColl = Newtonsoft.Json.Linq.JArray.Parse(comp.Collaboratori);

                                    foreach (Newtonsoft.Json.Linq.JObject coll in listaColl)
                                    {
                                        // ✅ Percentuale interna al compenso
                                        decimal perc = 0m;
                                        if (coll["Percentuale"] != null && decimal.TryParse(coll["Percentuale"].ToString(), out decimal tmpPerc))
                                            perc = tmpPerc;
                                        if (perc <= 0) continue;

                                        decimal baseImporto = comp.Importo ?? 0m;
                                        if (baseImporto <= 0) continue;

                                        decimal quota = Math.Round(baseImporto * (perc / 100m), 2);

                                        // ✅ ID collaboratore
                                        int? idCollab = null;
                                        if (coll["ID_Collaboratore"] != null && int.TryParse(coll["ID_Collaboratore"].ToString(), out int tmpId))
                                            idCollab = tmpId;
                                        else if (coll["ID_Utente"] != null && int.TryParse(coll["ID_Utente"].ToString(), out tmpId))
                                            idCollab = tmpId;

                                        string nomeCollab = coll["NomeCollaboratore"]?.ToString() ?? "-";

                                        if (idCollab == null)
                                        {
                                            System.Diagnostics.Trace.WriteLine(
                                                $"⚠️ [Previsione] Scartata riga Quota Collaboratore Compenso: {comp.Descrizione} (id=null, importo={quota:N2})");
                                            continue;
                                        }

                                        // ✅ Evita duplicati
                                        bool esisteGia = db.Previsione.Any(prev =>
                                            prev.ID_Pratiche == idPratica &&
                                            prev.ID_Professionista == idCollab &&
                                            prev.Descrizione.Contains(comp.Descrizione));

                                        if (esisteGia)
                                        {
                                            System.Diagnostics.Trace.WriteLine(
                                                $"⚠️ [Previsione] Saltata (duplicato) Quota Collaboratore Compenso: {comp.Descrizione} (coll={nomeCollab})");
                                            continue;
                                        }

                                        // ✅ Crea previsione (solo per quella quota specifica)
                                        AggiungiPrevisione("Entrata", idCollab, perc, quota,
                                            $"Quota Collaboratore Compenso: {comp.Descrizione} (ID_Compenso={comp.ID_RigaCompenso})");

                                        System.Diagnostics.Trace.WriteLine(
                                            $"✅ [Previsione] Aggiunta → Quota Collaboratore Compenso: {comp.Descrizione}, {quota:N2} €, {perc:N2}% (ID_Compenso={comp.ID_RigaCompenso}, ID_Collab={idCollab}, Nome={nomeCollab})");
                                    }
                                }
                                catch (Exception exJson)
                                {
                                    System.Diagnostics.Trace.WriteLine(
                                        $"⚠️ [Previsione] Errore JSON per compenso '{comp.Descrizione}': {exJson.Message}");
                                }
                            }

                            System.Diagnostics.Trace.WriteLine("✅ [CreaPratica] Creazione previsionale completata con successo.");
                        }
                    }
                    catch (Exception exPrev)
                    {
                        System.Diagnostics.Trace.WriteLine($"❌ [CreaPratica] Errore durante creazione previsionale: {exPrev}");
                    }


                    // 1️⃣4️⃣ Versionamento - CLUSTER (ID_Utente = ID_UTENTE)
                    var clusterSalvati = db.Cluster
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche)
                        .ToList();

                    foreach (var c in clusterSalvati)
                    {
                        db.Cluster_a.Add(new Cluster_a
                        {
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = c.ID_Utente, // ✅ ID_UTENTE, SENZA CONVERSIONI
                            TipoCluster = c.TipoCluster,
                            PercentualePrevisione = c.PercentualePrevisione,
                            DataAssegnazione = c.DataAssegnazione,
                            ID_UtenteArchiviazione = idUtente,
                            DataArchiviazione = now,
                            NumeroVersione = 1,
                            ModificheTestuali = "Creazione Cluster"
                        });
                    }


                    // 1️⃣5️⃣ Versionamento - RELAZIONI
                    var relazioni = db.RelazionePraticheUtenti.Where(r => r.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var r in relazioni)
                    {
                        db.RelazionePraticheUtenti_a.Add(new RelazionePraticheUtenti_a
                        {
                            ID_Relazione_a = r.ID_Relazione,
                            ID_Pratiche = r.ID_Pratiche,
                            ID_Utente = r.ID_Utente,
                            Ruolo = r.Ruolo,
                            DataAssegnazione = r.DataAssegnazione,
                            ID_UtenteArchiviazione = idUtente,
                            DataArchiviazione = now,
                            NumeroVersione = 1,
                            ModificheTestuali = "Creazione Relazione"
                        });
                    }

                    // 1️⃣6️⃣ Versionamento - COSTI PRATICA
                    var costiPratica = db.CostiPratica .Where(c => c.ID_Pratiche == pratica.ID_Pratiche) .ToList();

                    foreach (var c in costiPratica)
                    {
                        db.CostiPratica_a.Add(new CostiPratica_a
                        {
                            ID_Pratiche = c.ID_Pratiche,
                            ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            ID_Fornitore = c.ID_Fornitore, // ✅ nuovo campo
                            DataCompetenzaEconomica = c.DataCompetenzaEconomica, // ✅ nuovo campo
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            DataInserimento = c.DataInserimento,
                            ID_UtenteArchiviazione = idUtente,
                            DataArchiviazione = now,
                            NumeroVersione = 1,
                            ModificheTestuali = "Creazione Costo Pratica"
                        });
                    }


                    // 1️⃣8️⃣ Versionamento - RIMBORSI
                    var rimborsi = db.RimborsiPratica.Where(r => r.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var r in rimborsi)
                    {
                        db.RimborsiPratica_a.Add(new RimborsiPratica_a
                        {
                            ID_Pratiche = r.ID_Pratiche,
                            Descrizione = r.Descrizione,
                            Importo = r.Importo,
                            ID_UtenteCreatore = r.ID_UtenteCreatore,
                            DataInserimento = r.DataInserimento,
                            ID_UtenteArchiviazione = idUtente,
                            DataArchiviazione = now,
                            NumeroVersione = 1,
                            ModificheTestuali = "Creazione Rimborso"
                        });
                    }

                    // 1️⃣9️⃣ Versionamento - COMPENSI PRATICA DETTAGLIO
                    var compensiSalvati = db.CompensiPraticaDettaglio
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche)
                        .ToList();

                    foreach (var c in compensiSalvati)
                    {
                        db.CompensiPraticaDettaglio_a.Add(new CompensiPraticaDettaglio_a
                        {
                            ID_RigaCompensoOriginale = c.ID_RigaCompenso,
                            ID_Pratiche = c.ID_Pratiche,
                            TipoCompenso = c.TipoCompenso,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            Categoria = c.Categoria,
                            ValoreStimato = c.ValoreStimato,
                            Ordine = c.Ordine,
                            EstremiGiudizio = c.EstremiGiudizio,
                            OggettoIncarico = c.OggettoIncarico,
                            DataCreazione = c.DataCreazione,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            UltimaModifica = c.UltimaModifica,
                            ID_UtenteUltimaModifica = c.ID_UtenteUltimaModifica,
                            NumeroVersione = 1, // 🚨 se ti serve incrementare, calcolalo come fai per Pratiche_a
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            ModificheTestuali = "Creazione Compenso Pratica",
                            ID_ProfessionistaIntestatario = c.ID_ProfessionistaIntestatario
                        });
                    }

                    transaction.Commit();
                    stopwatch.Stop();
                    System.Diagnostics.Debug.WriteLine($"⏱ [CreaPratica] Fine metodo - Totale: {stopwatch.ElapsedMilliseconds}ms");


                    return Json(new { success = true, message = "✅ Pratica creata correttamente!" });
                }
            }
            catch (DbEntityValidationException ex)
            {
                // ======================================================
                // 🔍 DEBUG DETTAGLIATO ERRORI DI VALIDAZIONE ENTITY FRAMEWORK
                // ======================================================
                var errors = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"Campo: {e.PropertyName} - Errore: {e.ErrorMessage}")
                    .ToList();

                System.Diagnostics.Debug.WriteLine("❌ [CreaPratica] ERRORE DI VALIDAZIONE");
                System.Diagnostics.Debug.WriteLine($"🔢 Numero errori trovati: {errors.Count}");

                foreach (var errore in errors)
                    System.Diagnostics.Debug.WriteLine("   • " + errore);

                // ======================================================
                // ⚙️ IMPOSTAZIONI DI RISPOSTA
                // ======================================================
                Response.StatusCode = 400; // Bad Request
                Response.TrySkipIisCustomErrors = true; // evita pagina HTML di IIS

                // ======================================================
                // 📤 RESTITUZIONE RISPOSTA JSON
                // ======================================================
                return Json(new
                {
                    success = false,
                    message = "⚠️ Errore di validazione dei dati.",
                    dettagli = errors
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ [CreaPratica] ERRORE GENERICO");
                System.Diagnostics.Debug.WriteLine(ex.ToString());

                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;

                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante l'aggiornamento delle voci.",
                    dettaglio = ex.Message,
                    stack = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }

        }

        [HttpPost]
        public ActionResult ModificaPratica(PraticaViewModel model)
        {
            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                    if (pratica == null)
                        return Json(new { success = false, message = "Pratica non trovata." });

                    int idUtente = UserManager.GetIDUtenteAttivo();
                    DateTime now = DateTime.Now;

                    // 🔎 LOG per debug Budget
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] --- MODIFICA PRATICA ---");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Model.ID_Pratiche = {model.ID_Pratiche}");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Model.Budget = {model.Budget}");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Pratica DB prima = {pratica.Budget}");

                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == model.ID_Cliente);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente esterno non trovato." });

                    var operatore = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Operatore == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);
                    if (operatore == null)
                        return Json(new { success = false, message = "Professionista collegato non trovato." });

                    if (operatore.ID_Owner == null)
                    {
                        operatore.ID_Owner = operatore.ID_Operatore;
                        db.SaveChanges();
                    }

                    int idOwner = operatore.ID_Owner.Value;

                    var modifiche = new List<string>();
                    void Confronta(string campo, object val1, object val2)
                    {
                        if ((val1 ?? "").ToString().Trim() != (val2 ?? "").ToString().Trim())
                            modifiche.Add($"{campo}: '{val1}' → '{val2}'");
                    }

                    Confronta("Titolo", pratica.Titolo, model.Titolo);
                    Confronta("Descrizione", pratica.Descrizione, model.Descrizione);
                    Confronta("DataInizio", pratica.DataInizioAttivitaStimata, model.DataInizioAttivitaStimata);
                    Confronta("DataFine", pratica.DataFineAttivitaStimata, model.DataFineAttivitaStimata);
                    Confronta("Stato", pratica.Stato, model.Stato);
                    Confronta("ID_Cliente", pratica.ID_Cliente, model.ID_Cliente);
                    Confronta("Budget", pratica.Budget, model.Budget);
                    Confronta("Note", pratica.Note, model.Note);
                    Confronta("Tipologia", pratica.Tipologia, model.Tipologia);
                    Confronta("OggettoPratica", pratica.OggettoPratica, model.OggettoPratica);
                    Confronta("ImportoFisso", pratica.ImportoFisso, model.ImportoFisso);
                    Confronta("TariffaOraria", pratica.TariffaOraria, model.TariffaOraria);
                    Confronta("AccontoGiudiziale", pratica.AccontoGiudiziale, model.AccontoGiudiziale);
                    Confronta("GradoGiudizio", pratica.GradoGiudizio, model.GradoGiudizio);
                    Confronta("TerminiPagamento", pratica.TerminiPagamento, model.TerminiPagamento);
                    Confronta("OrePreviste", pratica.OrePreviste, model.OrePreviste);
                    Confronta("OreEffettive", pratica.OreEffettive, model.OreEffettive);


                    if (modifiche.Any())
                    {
                        int ultimaVersione = db.Pratiche_a.Where(p => p.ID_Pratica_Originale == pratica.ID_Pratiche)
                            .Select(p => (int?)p.NumeroVersione).Max() ?? 0;

                        int nuovaVersione = ultimaVersione + 1;

                        var archivio = new Pratiche_a
                        {
                            ID_Pratica_Originale = pratica.ID_Pratiche,
                            Titolo = pratica.Titolo,
                            Descrizione = pratica.Descrizione,
                            DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                            DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                            Stato = pratica.Stato,
                            ID_Cliente = pratica.ID_Cliente,
                            ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                            ID_Owner = pratica.ID_Owner,
                            Budget = pratica.Budget,
                            Note = pratica.Note,
                            Tipologia = pratica.Tipologia,
                            OggettoPratica = pratica.OggettoPratica,
                            ImportoFisso = pratica.ImportoFisso,
                            TariffaOraria = pratica.TariffaOraria,
                            AccontoGiudiziale = pratica.AccontoGiudiziale,
                            GradoGiudizio = pratica.GradoGiudizio,
                            TerminiPagamento = pratica.TerminiPagamento,
                            OrePreviste = pratica.OrePreviste,
                            OreEffettive = pratica.OreEffettive,
                            TrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = nuovaVersione,
                            ModificheTestuali = $"Modifica effettuata da ID_Utente = {idUtente} il {now:g}:\n- " + string.Join("\n- ", modifiche)
                        };

                        db.Pratiche_a.Add(archivio);
                        db.SaveChanges();
                    }

                    pratica.Titolo = model.Titolo;
                    pratica.Descrizione = model.Descrizione;
                    pratica.DataInizioAttivitaStimata = model.DataInizioAttivitaStimata;
                    pratica.DataFineAttivitaStimata = model.DataFineAttivitaStimata;
                    pratica.TrattenutaPersonalizzata = model.TrattenutaPersonalizzata;
                    pratica.Stato = model.Stato?.Trim();
                    pratica.ID_Cliente = model.ID_Cliente;
                    pratica.ID_UtenteUltimaModifica = idUtente;
                    pratica.Budget = model.Budget;
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Pratica DB dopo assegnazione = {pratica.Budget}");
                    pratica.Note = model.Note;
                    pratica.Tipologia = model.Tipologia?.Trim();
                    pratica.OggettoPratica = model.OggettoPratica?.Trim();
                    // Responsabile: se passato dal model, usa quello, altrimenti fallback su owner
                    if (model.ID_UtenteResponsabile > 0)
                        pratica.ID_UtenteResponsabile = model.ID_UtenteResponsabile;
                    else
                        pratica.ID_UtenteResponsabile = idOwner;
                    pratica.ID_Owner = idOwner;
                    pratica.UltimaModifica = now;
                    pratica.OrePreviste = model.OrePreviste;
                    pratica.OreEffettive = model.OreEffettive;

                    // ✅ 5️⃣ Salva o aggiorna file incarico nel DB (robusto con supporto PDF/P7M e nomi speciali)
                    if (model.IncaricoProfessionale != null && model.IncaricoProfessionale.ContentLength > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("📂 [UPLOAD INCARICO] Inizio caricamento in ModificaPratica...");

                        using (var ms = new MemoryStream())
                        {
                            model.IncaricoProfessionale.InputStream.CopyTo(ms);
                            System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] File letto: {ms.Length / 1024} KB");

                            // 🧩 1️⃣ Estrazione e normalizzazione nome file ed estensione
                            var nomeOriginale = model.IncaricoProfessionale.FileName;
                            System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] File originale: {nomeOriginale}");

                            var nomeFile = Path.GetFileName(nomeOriginale) ?? "Incarico";
                            var estensione = Path.GetExtension(nomeFile)?.ToLower();

                            // 🔧 Gestione file firmati digitalmente
                            if (nomeFile.EndsWith(".p7m.pdf", StringComparison.OrdinalIgnoreCase))
                            {
                                estensione = ".pdf";
                                nomeFile = Path.GetFileNameWithoutExtension(nomeFile.Replace(".p7m", ""));
                                System.Diagnostics.Debug.WriteLine("[UPLOAD INCARICO] File .p7m.pdf → corretto come PDF");
                            }
                            else if (nomeFile.EndsWith(".xml.p7m", StringComparison.OrdinalIgnoreCase))
                            {
                                estensione = ".p7m";
                                nomeFile = Path.GetFileNameWithoutExtension(nomeFile);
                                System.Diagnostics.Debug.WriteLine("[UPLOAD INCARICO] File .xml.p7m → corretto come P7M");
                            }

                            // 🧩 2️⃣ Sanificazione nome file da accenti, apostrofi o simboli strani
                            nomeFile = System.Text.RegularExpressions.Regex.Replace(
                                nomeFile.Normalize(System.Text.NormalizationForm.FormC),
                                @"[^\w\.\- ]",
                                "_"
                            );

                            // 🧩 3️⃣ Fallback sicuri per estensione e content-type
                            if (string.IsNullOrWhiteSpace(estensione))
                                estensione = ".pdf";

                            var tipoContenuto = model.IncaricoProfessionale.ContentType;
                            if (string.IsNullOrWhiteSpace(tipoContenuto) || tipoContenuto == "application/octet-stream")
                                tipoContenuto = "application/pdf";

                            // 🧩 4️⃣ Controllo se esiste già un incarico per la stessa pratica
                            var incaricoEsistente = db.DocumentiPratiche
                                .FirstOrDefault(d => d.ID_Pratiche == pratica.ID_Pratiche && d.Stato == "Firmato");

                            if (incaricoEsistente != null)
                            {
                                // 🔄 Aggiorna incarico esistente
                                incaricoEsistente.NomeFile = nomeFile;
                                incaricoEsistente.Estensione = estensione;
                                incaricoEsistente.TipoContenuto = tipoContenuto;
                                incaricoEsistente.Documento = ms.ToArray();
                                incaricoEsistente.DataCaricamento = now;
                                incaricoEsistente.ID_UtenteCaricamento = idUtente;
                                incaricoEsistente.Note = "Aggiornato incarico firmato (p7m/pdf)";
                                incaricoEsistente.Stato = "Firmato";

                                System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Aggiornato documento esistente: {nomeFile}");
                            }
                            else
                            {
                                // ➕ Crea nuovo incarico
                                var doc = new DocumentiPratiche
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    NomeFile = nomeFile,
                                    Estensione = estensione,
                                    TipoContenuto = tipoContenuto,
                                    Documento = ms.ToArray(),
                                    DataCaricamento = now,
                                    ID_UtenteCaricamento = idUtente,
                                    Stato = "Firmato",
                                    Note = "Incarico firmato (upload p7m/pdf)"
                                };

                                db.DocumentiPratiche.Add(doc);
                                System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Nuovo documento aggiunto: {nomeFile}");
                            }

                            // 🧩 5️⃣ Log finale di verifica
                            System.Diagnostics.Debug.WriteLine($"[UPLOAD INCARICO] Estensione={estensione}, Tipo={tipoContenuto}, Bytes={ms.Length}");

                            db.SaveChanges();
                            System.Diagnostics.Debug.WriteLine("📁 [UPLOAD INCARICO] SaveChanges completato ✅");
                        }

                        System.Diagnostics.Debug.WriteLine("[UPLOAD INCARICO] Fine caricamento incarico in ModificaPratica ✅");
                    }

                    // 🔄 Gestione Compensi Pratica Dettaglio (con versionamento)
                    var compensiEsistenti = db.CompensiPraticaDettaglio
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche)
                        .ToList();

                    // Archivio versioni precedenti
                    foreach (var c in compensiEsistenti)
                    {
                        db.CompensiPraticaDettaglio_a.Add(new CompensiPraticaDettaglio_a
                        {
                            ID_RigaCompensoOriginale = c.ID_RigaCompenso,
                            ID_Pratiche = c.ID_Pratiche,
                            TipoCompenso = c.TipoCompenso,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            Categoria = c.Categoria,
                            ValoreStimato = c.ValoreStimato,
                            Ordine = c.Ordine,
                            EstremiGiudizio = c.EstremiGiudizio,
                            OggettoIncarico = c.OggettoIncarico,
                            DataCreazione = c.DataCreazione,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            UltimaModifica = c.UltimaModifica,
                            ID_UtenteUltimaModifica = c.ID_UtenteUltimaModifica,
                            NumeroVersione = (db.CompensiPraticaDettaglio_a
                                .Where(x => x.ID_RigaCompensoOriginale == c.ID_RigaCompenso)
                                .Max(x => (int?)x.NumeroVersione) ?? 0) + 1,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            ID_ProfessionistaIntestatario = c.ID_ProfessionistaIntestatario,
                            Collaboratori = c.Collaboratori, // già JSON
                            ModificheTestuali = "Modifica Compenso"
                        });
                    }

                    db.CompensiPraticaDettaglio.RemoveRange(compensiEsistenti);

                    // 🔄 Inserisci nuovi compensi dal JSON
                    if (!string.IsNullOrEmpty(model.CompensiJSON))
                    {
                        var compensiRaw = Newtonsoft.Json.JsonConvert
                            .DeserializeObject<List<Dictionary<string, object>>>(model.CompensiJSON);

                        if (compensiRaw != null && compensiRaw.Any())
                        {
                            int ordine = 1;
                            foreach (var c in compensiRaw)
                            {
                                var nuovoCompenso = new CompensiPraticaDettaglio
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    TipoCompenso = c.ContainsKey("Metodo") ? c["Metodo"]?.ToString() : null,

                                    Descrizione =
                                        (c.ContainsKey("Descrizione") ? c["Descrizione"]?.ToString() :
                                        (c.ContainsKey("Descrizione_1") ? c["Descrizione_1"]?.ToString() : null))
                                        ?? (c.ContainsKey("Ruolo_1") ? c["Ruolo_1"]?.ToString() : null)
                                        ?? (c.ContainsKey("Ruolo") ? c["Ruolo"]?.ToString() : null),

                                    Importo =
                                        (c.ContainsKey("Importo") && decimal.TryParse(c["Importo"]?.ToString(), out var imp) ? imp :
                                        (c.ContainsKey("Importo_1") && decimal.TryParse(c["Importo_1"]?.ToString(), out var imp1) ? imp1 : (decimal?)null))
                                        ?? (c.ContainsKey("Tariffa_1") && decimal.TryParse(c["Tariffa_1"]?.ToString(), out var impT) ? impT : (decimal?)null),

                                    Categoria = c.ContainsKey("Tipologia") ? c["Tipologia"]?.ToString()
                                              : (c.ContainsKey("Tipologia_1") ? c["Tipologia_1"]?.ToString() : "Contrattuale"),

                                    ValoreStimato =
                                        (c.ContainsKey("ValoreStimato") && decimal.TryParse(c["ValoreStimato"]?.ToString(), out var val) ? val :
                                        (c.ContainsKey("ValoreStimato_1") && decimal.TryParse(c["ValoreStimato_1"]?.ToString(), out var val1) ? val1 : (decimal?)null)),

                                    Ordine = ordine++,
                                    EstremiGiudizio = c.ContainsKey("EstremiGiudizio") ? c["EstremiGiudizio"]?.ToString() : null,
                                    OggettoIncarico = c.ContainsKey("OggettoIncarico") ? c["OggettoIncarico"]?.ToString() : null,
                                    DataCreazione = now,
                                    ID_UtenteCreatore = idUtente,

                                    ID_ProfessionistaIntestatario =
                                        (c.ContainsKey("ID_ProfessionistaIntestatario") && int.TryParse(c["ID_ProfessionistaIntestatario"]?.ToString(), out var idProf))
                                        ? (int?)idProf : null,

                                    // 👇 Collaboratori salvati come stringa JSON
                                    Collaboratori = c.ContainsKey("Collaboratori") && c["Collaboratori"] != null
                                        ? Newtonsoft.Json.JsonConvert.SerializeObject(c["Collaboratori"])
                                        : null
                                };

                                db.CompensiPraticaDettaglio.Add(nuovoCompenso);
                            }
                        }
                    }


                    // ... (inizio del metodo già definito sopra)

                    // ======================================================
                    // 🔄 CLUSTER – Versionamento
                    // ======================================================
                    var clusterEsistenti = db.Cluster
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche)
                        .ToList();

                    foreach (var c in clusterEsistenti)
                    {
                        db.Cluster_a.Add(new Cluster_a
                        {
                            ID_Cluster_Originale = c.ID_Cluster,
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = c.ID_Utente,              // ✅ ID_UTENTE (così com’è)
                            TipoCluster = c.TipoCluster,
                            PercentualePrevisione = c.PercentualePrevisione,
                            DataAssegnazione = c.DataAssegnazione,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione =
                                (db.Cluster_a
                                    .Where(x => x.ID_Cluster_Originale == c.ID_Cluster)
                                    .Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                        });
                    }

                    // 🧹 Rimuove cluster correnti
                    db.Cluster.RemoveRange(clusterEsistenti);

                    // ======================================================
                    // 💰 OWNER – reinserimento (ID_UTENTE)
                    // ======================================================
                    var ownerFee = db.TipologieCosti
                        .FirstOrDefault(t => t.Nome == "Owner Fee" &&
                                             t.Stato == "Attivo" &&
                                             t.Tipo == "Percentuale");

                    decimal percentualeOwner = ownerFee?.ValorePercentuale ?? 5;

                    // 🔹 OWNER nel cluster → ID_UTENTE
                    db.Cluster.Add(new Cluster
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = operatore.ID_UtenteCollegato.Value, // ✅ ID_UTENTE
                        TipoCluster = "Owner",
                        PercentualePrevisione = percentualeOwner,
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    });

                    // ======================================================
                    // 👥 COLLABORATORI – ID_UTENTE
                    // ======================================================
                    if (model.UtentiAssociati != null)
                    {
                        foreach (var u in model.UtentiAssociati)
                        {
                            // ❌ evita duplicato owner
                            if (u.ID_Utente == operatore.ID_UtenteCollegato.Value)
                                continue;

                            // 📌 Normalizzazione percentuale
                            decimal percentuale = u.PercentualePrevisione;

                            if (percentuale == 0)
                            {
                                var raw = Request.Form[
                                    $"UtentiAssociati[{model.UtentiAssociati.IndexOf(u)}].PercentualePrevisione"
                                ];

                                if (!string.IsNullOrWhiteSpace(raw))
                                {
                                    raw = raw.Replace(',', '.')
                                             .Replace("%", "")
                                             .Trim();

                                    decimal.TryParse(
                                        raw,
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out percentuale
                                    );
                                }
                            }

                            // 💾 Inserimento collaboratore → ID_UTENTE
                            db.Cluster.Add(new Cluster
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = u.ID_Utente,             // ✅ ID_UTENTE
                                TipoCluster = string.IsNullOrEmpty(u.TipoCluster)
                                    ? "Collaboratore"
                                    : u.TipoCluster,
                                PercentualePrevisione = percentuale,
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }

                    // 💾 Salvataggio finale cluster
                    db.SaveChanges();



                    // ======================================================
                    // 📊 [PREVISIONALE] Rigenerazione automatica in ModificaPratica
                    // ======================================================
                    try
                    {
                        System.Diagnostics.Trace.WriteLine("🔄 [ModificaPratica] Avvio rigenerazione previsionale...");

                        int idPratica = pratica.ID_Pratiche;
                        int idUtenteCreatore = idUtente;
                        DateTime dataPrev = pratica.DataInizioAttivitaStimata ?? DateTime.Now;

                        decimal budget = pratica.Budget;
                        if (budget <= 0)
                        {
                            System.Diagnostics.Trace.WriteLine("⚠️ [ModificaPratica] Budget nullo, previsionale non aggiornato.");
                        }
                        else
                        {
                            // 🔄 Elimina eventuali righe previsionali precedenti della pratica
                            var previsioniVecchie = db.Previsione.Where(p => p.ID_Pratiche == idPratica).ToList();
                            if (previsioniVecchie.Any())
                            {
                                foreach (var p in previsioniVecchie)
                                {
                                    db.Previsione_a.Add(new Previsione_a
                                    {
                                        ID_PrevisioneOriginale = p.ID_Previsione,
                                        ID_Pratiche = p.ID_Pratiche,
                                        ID_Professionista = p.ID_Professionista,
                                        Percentuale = p.Percentuale,
                                        TipoOperazione = p.TipoOperazione,
                                        Descrizione = p.Descrizione,
                                        ImportoPrevisto = p.ImportoPrevisto,
                                        DataPrevisione = p.DataPrevisione,
                                        Stato = p.Stato,
                                        ID_UtenteCreatore = p.ID_UtenteCreatore,
                                        NumeroVersione = (db.Previsione_a
                                            .Where(x => x.ID_PrevisioneOriginale == p.ID_Previsione)
                                            .Max(x => (int?)x.NumeroVersione) ?? 0) + 1,
                                        DataArchiviazione = DateTime.Now,
                                        ID_UtenteArchiviazione = idUtenteCreatore,
                                        ModificheTestuali = "Archiviazione automatica per rigenerazione previsionale"
                                    });
                                }

                                db.Previsione.RemoveRange(previsioniVecchie);
                                db.SaveChanges();
                                System.Diagnostics.Trace.WriteLine($"🧹 [ModificaPratica] Rimosse {previsioniVecchie.Count} previsioni precedenti.");
                            }

                            // =====================================================
                            // 🏛️ Recupero parametri economici base
                            // =====================================================
                            decimal percOwnerFee = db.RicorrenzeCosti
                                .Where(r => r.Categoria == "Owner Fee" && r.Attivo && r.TipoValore == "Percentuale")
                                .OrderByDescending(r => r.DataInizio)
                                .Select(r => (decimal?)r.Valore)
                                .FirstOrDefault() ?? 0m;

                            decimal percTrattenuta = db.RicorrenzeCosti
                                .Where(r => r.Categoria == "Trattenuta Sinergia" && r.Attivo && r.TipoValore == "Percentuale")
                                .OrderByDescending(r => r.DataInizio)
                                .Select(r => (decimal?)r.Valore)
                                .FirstOrDefault() ?? 0m;

                            // =====================================================
                            // 👥 Cluster collegati alla pratica
                            // =====================================================
                            var clusterList = db.Cluster
                                .Where(c => c.ID_Pratiche == idPratica && c.TipoCluster == "Collaboratore")
                                .ToList();

                            decimal sommaCluster = clusterList.Sum(c => c.PercentualePrevisione);
                            decimal percResponsabile = Math.Max(0, 100 - percOwnerFee - sommaCluster);

                            System.Diagnostics.Trace.WriteLine($"📈 [ModificaPratica] Percentuali → Resp={percResponsabile}%, Owner={percOwnerFee}%, ClusterTot={sommaCluster}%");

                            // =====================================================
                            // 📌 Helper per inserire nuova previsione
                            // =====================================================
                            void AggiungiPrevisione(string tipo, int? idProfessionista, decimal percentuale, decimal importo, string descrizione)
                            {
                                if (idProfessionista == null || importo <= 0)
                                {
                                    System.Diagnostics.Trace.WriteLine($"⚠️ [Previsione] Scartata: {descrizione} (id={idProfessionista}, importo={importo})");
                                    return;
                                }

                                var prev = new Previsione
                                {
                                    ID_Pratiche = idPratica,
                                    ID_Professionista = idProfessionista,
                                    Percentuale = percentuale,
                                    TipoOperazione = tipo,
                                    Descrizione = descrizione,
                                    ImportoPrevisto = Math.Round(importo, 2),
                                    DataPrevisione = dataPrev,
                                    Stato = "Previsionale",
                                    ID_UtenteCreatore = idUtenteCreatore
                                };

                                db.Previsione.Add(prev);
                                db.SaveChanges();

                                db.Previsione_a.Add(new Previsione_a
                                {
                                    ID_PrevisioneOriginale = prev.ID_Previsione,
                                    ID_Pratiche = prev.ID_Pratiche,
                                    ID_Professionista = prev.ID_Professionista,
                                    Percentuale = prev.Percentuale,
                                    TipoOperazione = prev.TipoOperazione,
                                    Descrizione = prev.Descrizione,
                                    ImportoPrevisto = prev.ImportoPrevisto,
                                    DataPrevisione = prev.DataPrevisione,
                                    Stato = prev.Stato,
                                    ID_UtenteCreatore = prev.ID_UtenteCreatore,
                                    NumeroVersione = 1,
                                    DataArchiviazione = DateTime.Now,
                                    ID_UtenteArchiviazione = idUtenteCreatore,
                                    ModificheTestuali = "Rigenerazione automatica previsionale da ModificaPratica"
                                });

                                db.SaveChanges();
                                System.Diagnostics.Trace.WriteLine($"✅ [Previsione] Inserita → {descrizione}, {importo:N2} €, {percentuale:N2}% (tipo={tipo})");
                            }

                            // =====================================================
                            // 👤 1️⃣ RESPONSABILE – quota netta
                            // =====================================================
                            decimal quotaResp = budget * percResponsabile / 100m;
                            AggiungiPrevisione("Entrata", pratica.ID_UtenteResponsabile, percResponsabile, quotaResp, $"Ricavo previsto (Responsabile): {pratica.Titolo}");

                            // =====================================================
                            // 👑 2️⃣ OWNER FEE
                            // =====================================================
                            if (pratica.ID_Owner != null && percOwnerFee > 0)
                            {
                                decimal quotaOwner = budget * percOwnerFee / 100m;
                                AggiungiPrevisione("Entrata", pratica.ID_Owner, percOwnerFee, quotaOwner, $"Quota Owner Fee ({percOwnerFee:N2}%): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 💼 3️⃣ TRATTENUTA SINERGIA (uscita)
                            // =====================================================
                            if (percTrattenuta > 0)
                            {
                                decimal quotaTratt = budget * percTrattenuta / 100m;
                                AggiungiPrevisione("Uscita", pratica.ID_UtenteResponsabile, percTrattenuta, quotaTratt, $"Trattenuta Sinergia ({percTrattenuta:N2}%): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 👥 4️⃣ COLLABORATORI CLUSTER
                            // =====================================================
                            foreach (var c in clusterList)
                            {
                                decimal importoCluster = budget * c.PercentualePrevisione / 100m;
                                AggiungiPrevisione("Entrata", c.ID_Utente, c.PercentualePrevisione, importoCluster,
                                    $"Quota Collaboratore (Cluster): {pratica.Titolo}");
                            }

                            // =====================================================
                            // 🧾 5️⃣ COLLABORATORI DEI COMPENSI DETTAGLIO (Compatibile v6)
                            // =====================================================
                            var compensiDettaglio = db.CompensiPraticaDettaglio
                                .Where(cc => cc.ID_Pratiche == idPratica)
                                .ToList();

                            foreach (var comp in compensiDettaglio)
                            {
                                if (string.IsNullOrWhiteSpace(comp.Collaboratori))
                                    continue;

                                try
                                {
                                    var listaColl = Newtonsoft.Json.Linq.JArray.Parse(comp.Collaboratori);

                                    foreach (Newtonsoft.Json.Linq.JObject coll in listaColl)
                                    {
                                        // ✅ Percentuale
                                        decimal perc = 0m;
                                        decimal tmpPerc;
                                        if (coll["Percentuale"] != null && decimal.TryParse(coll["Percentuale"].ToString(), out tmpPerc))
                                            perc = tmpPerc;
                                        if (perc <= 0) continue;

                                        // ✅ Importo base
                                        decimal baseImporto = comp.Importo ?? 0m;
                                        if (baseImporto <= 0) continue;

                                        decimal quota = Math.Round(baseImporto * (perc / 100m), 2);

                                        // ✅ Recupero ID collaboratore
                                        int? idCollab = null;
                                        int tmpId;
                                        if (coll["ID_Collaboratore"] != null && int.TryParse(coll["ID_Collaboratore"].ToString(), out tmpId))
                                            idCollab = tmpId;
                                        else if (coll["ID_Utente"] != null && int.TryParse(coll["ID_Utente"].ToString(), out tmpId))
                                            idCollab = tmpId;

                                        // ✅ Nome collaboratore (solo log)
                                        string nomeCollab = "-";
                                        if (coll["NomeCollaboratore"] != null)
                                            nomeCollab = coll["NomeCollaboratore"].ToString();

                                        if (idCollab == null)
                                        {
                                            System.Diagnostics.Trace.WriteLine(
                                                string.Format("⚠️ [Previsione] Scartata riga Quota Collaboratore Compenso: {0} (id=null, importo={1:N2})",
                                                comp.Descrizione, quota));
                                            continue;
                                        }

                                        // ✅ Controllo duplicati → stessa pratica, collaboratore e compenso
                                        bool esisteGia = db.Previsione.Any(prev =>
                                            prev.ID_Pratiche == idPratica &&
                                            prev.ID_Professionista == idCollab &&
                                            prev.Descrizione.Contains(comp.Descrizione));

                                        if (esisteGia)
                                        {
                                            System.Diagnostics.Trace.WriteLine(
                                                string.Format("⚠️ [Previsione] Saltata (duplicato) Quota Collaboratore Compenso: {0} (coll={1})",
                                                comp.Descrizione, nomeCollab));
                                            continue;
                                        }

                                        // ✅ Crea previsione legata al compenso specifico
                                        AggiungiPrevisione(
                                            "Entrata",
                                            idCollab,
                                            perc,
                                            quota,
                                            string.Format("Quota Collaboratore Compenso: {0} (ID_Compenso={1})",
                                            comp.Descrizione, comp.ID_RigaCompenso)
                                        );

                                        System.Diagnostics.Trace.WriteLine(
                                            string.Format("✅ [Previsione] Aggiunta → Quota Collaboratore Compenso: {0}, {1:N2} €, {2:N2}% (ID_Compenso={3}, ID_Collab={4}, Nome={5})",
                                            comp.Descrizione, quota, perc, comp.ID_RigaCompenso, idCollab, nomeCollab));
                                    }
                                }
                                catch (Exception exJson)
                                {
                                    System.Diagnostics.Trace.WriteLine(
                                        string.Format("⚠️ [Previsione] Errore JSON per compenso '{0}': {1}", comp.Descrizione, exJson.Message));
                                }
                            }


                            System.Diagnostics.Trace.WriteLine("✅ [ModificaPratica] Rigenerazione previsionale completata con successo.");
                        }
                    }
                    catch (Exception exPrev)
                    {
                        System.Diagnostics.Trace.WriteLine($"❌ [ModificaPratica] Errore rigenerazione previsionale: {exPrev}");
                    }


                    // 🔄 Relazioni
                    var relazioniEsistenti = db.RelazionePraticheUtenti.Where(r => r.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var r in relazioniEsistenti)
                    {
                        db.RelazionePraticheUtenti_a.Add(new RelazionePraticheUtenti_a
                        {
                            ID_Relazione_Originale = r.ID_Relazione,
                            ID_Pratiche = r.ID_Pratiche,
                            ID_Utente = r.ID_Utente,
                            Ruolo = r.Ruolo,
                            DataAssegnazione = r.DataAssegnazione,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = (db.RelazionePraticheUtenti_a.Where(x => x.ID_Relazione_Originale == r.ID_Relazione).Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                        });
                    }
                    db.RelazionePraticheUtenti.RemoveRange(relazioniEsistenti);

                    // Inserisci owner e collaboratori
                    var relOwner = new RelazionePraticheUtenti
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = idOwner,
                        Ruolo = "Owner",
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    };
                    db.RelazionePraticheUtenti.Add(relOwner);

                    if (model.UtentiAssociati != null)
                    {
                        foreach (var u in model.UtentiAssociati)
                        {
                            db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = u.ID_Utente,
                                Ruolo = "Collaboratore",
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }
                    db.SaveChanges();

                    // 🔄 Rimborsi
                    var rimborsiEsistenti = db.RimborsiPratica.Where(r => r.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var r in rimborsiEsistenti)
                    {
                        db.RimborsiPratica_a.Add(new RimborsiPratica_a
                        {
                            ID_RimborsoOriginale = r.ID_Rimborso,
                            ID_Pratiche = r.ID_Pratiche,
                            Descrizione = r.Descrizione,
                            Importo = r.Importo,
                            DataInserimento = r.DataInserimento,
                            ID_UtenteCreatore = r.ID_UtenteCreatore,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = (db.RimborsiPratica_a.Where(x => x.ID_RimborsoOriginale == r.ID_Rimborso).Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                        });
                    }
                    db.RimborsiPratica.RemoveRange(rimborsiEsistenti);

                    if (model.Rimborsi != null)
                    {
                        foreach (var r in model.Rimborsi)
                        {
                            db.RimborsiPratica.Add(new RimborsiPratica
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                Descrizione = r.Descrizione,
                                Importo = r.Importo,
                                DataInserimento = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }
                    db.SaveChanges();

                    // 🔄 Costi Pratica
                    var costiPraticaEsistenti = db.CostiPratica
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche)
                        .ToList();

                    // ============================================================
                    // 📚 1️⃣ ARCHIVIAZIONE VERSIONE PRECEDENTE (CostiPratica_a)
                    // ============================================================
                    foreach (var c in costiPraticaEsistenti)
                    {
                        db.CostiPratica_a.Add(new CostiPratica_a
                        {
                            ID_CostoPratica_Originale = c.ID_CostoPratica,
                            ID_Pratiche = c.ID_Pratiche,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                            ID_ClienteAssociato = c.ID_ClienteAssociato,
                            ID_Fornitore = c.ID_Fornitore, // ✅ nuovo campo
                            DataCompetenzaEconomica = c.DataCompetenzaEconomica, // ✅ nuovo campo
                            DataInserimento = c.DataInserimento,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = (db.CostiPratica_a
                                .Where(x => x.ID_CostoPratica_Originale == c.ID_CostoPratica)
                                .Max(x => (int?)x.NumeroVersione) ?? 0) + 1,
                            ModificheTestuali = "Modifica Costi Pratica"
                        });
                    }

                    // 🧹 Rimuove i vecchi costi prima di reinserirli
                    db.CostiPratica.RemoveRange(costiPraticaEsistenti);

                    // ============================================================
                    // 🆕 2️⃣ INSERIMENTO NUOVI COSTI
                    // ============================================================
                    if (model.CostiPratica != null && model.CostiPratica.Any())
                    {
                        foreach (var costo in model.CostiPratica)
                        {
                            if (costo.ID_AnagraficaCosto <= 0)
                                continue;

                            var voceAnagrafica = db.AnagraficaCostiPratica
                                .FirstOrDefault(a => a.ID_AnagraficaCosto == costo.ID_AnagraficaCosto);

                            // 🔹 Normalizza date
                            DateTime dataInserimento = costo.DataInserimento != default(DateTime)
                                ? costo.DataInserimento
                                : DateTime.Now;

                            DateTime? dataCompetenza = costo.DataCompetenzaEconomica != default(DateTime)
                                ? costo.DataCompetenzaEconomica
                                : (DateTime?)null;

                            if (voceAnagrafica != null)
                            {
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    Descrizione = $"{voceAnagrafica.Nome} - {voceAnagrafica.Descrizione}",
                                    ID_AnagraficaCosto = voceAnagrafica.ID_AnagraficaCosto,
                                    Importo = costo.Importo,
                                    ID_ClienteAssociato = costo.ID_ClienteAssociato,
                                    ID_Fornitore = costo.ID_Fornitore, // ✅ nuovo campo
                                    DataCompetenzaEconomica = dataCompetenza, // ✅ nuovo campo
                                    DataInserimento = dataInserimento,
                                    ID_UtenteCreatore = idUtente
                                });
                            }
                            else
                            {
                                // 🔹 fallback se non c’è anagrafica
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    Descrizione = costo.Descrizione ?? "Voce non trovata",
                                    Importo = costo.Importo,
                                    ID_ClienteAssociato = costo.ID_ClienteAssociato,
                                    ID_Fornitore = costo.ID_Fornitore,
                                    DataCompetenzaEconomica = dataCompetenza,
                                    DataInserimento = dataInserimento,
                                    ID_UtenteCreatore = idUtente
                                });
                            }
                        }
                    }

                    db.SaveChanges();


                    // 🔔 Notifiche
                    if (model.InviaNotificheAutomatiche)
                    {
                        string messaggio = !string.IsNullOrWhiteSpace(model.MessaggioNotificaPersonalizzato)
                            ? model.MessaggioNotificaPersonalizzato
                            : $"La pratica \"{model.Titolo}\" è stata modificata.";

                        var utentiNotificati = new List<int> { idOwner };
                        if (model.UtentiAssociati != null)
                            utentiNotificati.AddRange(model.UtentiAssociati.Select(u => u.ID_Utente));

                        foreach (var id in utentiNotificati.Distinct())
                        {
                            db.Notifiche.Add(new Notifiche
                            {
                                Titolo = "Modifica Pratica",
                                Descrizione = messaggio,
                                DataCreazione = now,
                                ID_Utente = id,
                                Tipo = "Pratica",
                                Stato = "Non letto",
                                Contatore = 1
                            });
                        }
                        db.SaveChanges();
                    }

                    // 🔍 Controllo incarico prima dello stato "In lavorazione"
                    if (model.Stato == "In lavorazione")
                    {
                        bool filePDFPresente = db.DocumentiPratiche.Any(d =>
                            d.ID_Pratiche == pratica.ID_Pratiche &&
                            d.Documento != null &&
                            d.NomeFile.ToLower().EndsWith(".pdf"));

                        if (!filePDFPresente)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "⚠️ Devi caricare un incarico firmato prima di passare allo stato 'In lavorazione'."
                            });
                        }
                    }

                    // 🔐 Se la pratica è segnala come "Conclusa"
                    if (pratica.Stato == "Conclusa")
                    {
                        // 🔎 Salva SOLO versione storica (NO spostamento)
                        var ultimaVersione = db.Pratiche_a
                            .Where(a => a.ID_Pratica_Originale == pratica.ID_Pratiche)
                            .Max(a => (int?)a.NumeroVersione) ?? 0;

                        var nuovoArchivio = new Pratiche_a
                        {
                            ID_Pratica_Originale = pratica.ID_Pratiche,
                            Titolo = pratica.Titolo,
                            Descrizione = pratica.Descrizione,
                            DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                            DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                            Stato = pratica.Stato,
                            ID_Cliente = pratica.ID_Cliente,
                            ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                            ID_Owner = pratica.ID_Owner,
                            Budget = pratica.Budget,
                            Note = pratica.Note,
                            Tipologia = pratica.Tipologia,
                            ImportoFisso = pratica.ImportoFisso,
                            TariffaOraria = pratica.TariffaOraria,
                            AccontoGiudiziale = pratica.AccontoGiudiziale,
                            GradoGiudizio = pratica.GradoGiudizio,
                            TerminiPagamento = pratica.TerminiPagamento,
                            OrePreviste = pratica.OrePreviste,
                            OreEffettive = pratica.OreEffettive,
                            TrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = ultimaVersione + 1,
                            ModificheTestuali = "Pratica segnata come conclusa"
                        };

                        db.Pratiche_a.Add(nuovoArchivio);
                        db.SaveChanges();

                        transaction.Commit();
                        return Json(new
                        {
                            success = true,
                            message = "✅ Pratica conclusa (non rimossa dal sistema)."
                        });
                    }

                    // 🟢 Caso normale
                    transaction.Commit();
                    return Json(new
                    {
                        success = true,
                        message = "Pratica aggiornata correttamente."
                    });

                }
            }
            catch (DbEntityValidationException ex)
            {
                // ===============================
                // 🔍 GESTIONE ERRORI DI VALIDAZIONE EF
                // ===============================
                var errors = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"Campo: {e.PropertyName} - Errore: {e.ErrorMessage}")
                    .ToList();

                System.Diagnostics.Debug.WriteLine("❌ [ModificaPratica] ERRORE DI VALIDAZIONE");
                foreach (var errore in errors)
                    System.Diagnostics.Debug.WriteLine("   • " + errore);

                Response.StatusCode = 400;
                Response.TrySkipIisCustomErrors = true;

                return Json(new
                {
                    success = false,
                    message = "⚠️ Errore di validazione dei dati.",
                    dettagli = errors
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // ===============================
                // 🔍 GESTIONE ERRORI GENERICI (con inner exception profonda)
                // ===============================
                Exception innerMost = ex;
                while (innerMost.InnerException != null)
                    innerMost = innerMost.InnerException;

                string innerMessage = innerMost?.Message ?? "(nessuna inner exception)";
                string innerStack = innerMost?.StackTrace ?? "";

                System.Diagnostics.Debug.WriteLine("❌ [ModificaPratica] ERRORE GENERICO");
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                System.Diagnostics.Debug.WriteLine($"🔍 Inner Exception (profonda): {innerMessage}");

                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;

                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante l'aggiornamento della pratica.",
                    dettaglio = innerMessage,
                    stack = ex.StackTrace
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult EliminaPratica(int id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == id && p.Stato != "Eliminato");
                    if (pratica == null)
                        return Json(new { success = false, message = "Pratica non trovata o già eliminata." });

                    int userId = UserManager.GetIDUtenteCollegato();
                    DateTime now = DateTime.Now;

                    // 🔢 Calcola numero versione
                    int ultimaVersione = db.Pratiche_a
                        .Where(p => p.ID_Pratica_Originale == pratica.ID_Pratiche)
                        .Select(p => (int?)p.NumeroVersione).Max() ?? 0;

                    // 🔁 Archiviazione principale
                    db.Pratiche_a.Add(new Pratiche_a
                    {
                        ID_Pratica_Originale = pratica.ID_Pratiche,
                        Titolo = pratica.Titolo,
                        Descrizione = pratica.Descrizione,
                        DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                        DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                        Stato = pratica.Stato,
                        ID_Cliente = pratica.ID_Cliente,
                        ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                        ID_UtenteCreatore = pratica.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = userId,
                        ID_Owner = pratica.ID_Owner,   // ✅ aggiunto
                        Budget = pratica.Budget,
                        Note = pratica.Note,
                        Tipologia = pratica.Tipologia,
                        OggettoPratica = pratica.OggettoPratica,
                        ImportoFisso = pratica.ImportoFisso,
                        TariffaOraria = pratica.TariffaOraria,
                        AccontoGiudiziale = pratica.AccontoGiudiziale,
                        GradoGiudizio = pratica.GradoGiudizio,
                        TerminiPagamento = pratica.TerminiPagamento,
                        OrePreviste = pratica.OrePreviste,
                        OreEffettive = pratica.OreEffettive,
                        DataCreazione = pratica.DataCreazione,
                        UltimaModifica = pratica.UltimaModifica,
                        TrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = ultimaVersione + 1,
                        ModificheTestuali = $"Eliminazione effettuata da ID_Utente = {userId} in data {now:g}"
                    });

                    // 🔁 Archivia tabelle correlate
                    void Archivia<T, A>(IQueryable<T> query, Func<T, A> factory) where A : class
                    {
                        foreach (var item in query.ToList())
                            db.Set<A>().Add(factory(item));
                    }

                    Archivia(db.RelazionePraticheUtenti.Where(r => r.ID_Pratiche == id), r => new RelazionePraticheUtenti_a
                    {
                        ID_Relazione_Originale = r.ID_Relazione,
                        ID_Pratiche = r.ID_Pratiche,
                        ID_Utente = r.ID_Utente,
                        Ruolo = r.Ruolo,
                        DataAssegnazione = r.DataAssegnazione,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = 1
                    });

                    Archivia(db.RimborsiPratica.Where(r => r.ID_Pratiche == id), r => new RimborsiPratica_a
                    {
                        ID_RimborsoOriginale = r.ID_Rimborso,
                        ID_Pratiche = r.ID_Pratiche,
                        Descrizione = r.Descrizione,
                        Importo = r.Importo,
                        DataInserimento = r.DataInserimento,
                        ID_UtenteCreatore = r.ID_UtenteCreatore,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = 1
                    });

                    // ============================================================
                    // 🗑️ ARCHIVIAZIONE COSTI PRATICA PRIMA DELL’ELIMINAZIONE
                    // ============================================================
                    Archivia(
                        db.CostiPratica.Where(c => c.ID_Pratiche == id),
                        c => new CostiPratica_a
                        {
                            ID_CostoPratica_Originale = c.ID_CostoPratica,
                            ID_Pratiche = c.ID_Pratiche,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                            ID_ClienteAssociato = c.ID_ClienteAssociato,
                            ID_Fornitore = c.ID_Fornitore, // ✅ nuovo campo
                            DataCompetenzaEconomica = c.DataCompetenzaEconomica, // ✅ nuovo campo
                            DataInserimento = c.DataInserimento,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = userId,
                            NumeroVersione = (db.CostiPratica_a
                                .Where(x => x.ID_CostoPratica_Originale == c.ID_CostoPratica)
                                .Max(x => (int?)x.NumeroVersione) ?? 0) + 1,
                            ModificheTestuali = "Eliminazione Costo Pratica"
                        }
                    );


                    Archivia(db.DocumentiPratiche.Where(d => d.ID_Pratiche == id), d => new DocumentiPratiche_a
                    {
                        ID_Documento_a = d.ID_Documento,
                        ID_Pratiche = d.ID_Pratiche,
                        NomeFile = d.NomeFile,
                        Estensione = d.Estensione,
                        TipoContenuto = d.TipoContenuto,
                        Documento = d.Documento,
                        Stato = d.Stato,
                        DataCaricamento = d.DataCaricamento,
                        ID_UtenteCaricamento = d.ID_UtenteCaricamento,
                        Note = d.Note,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = 1
                    });

                    // ======================================================
                    // ======================================================
                    // 📦 Archivia e rimuovi CLUSTER (supporto dati legacy)
                    // ======================================================
                    var clusters = db.Cluster
                        .Where(c => c.ID_Pratiche == id)
                        .ToList();

                    foreach (var c in clusters)
                    {
                        int idUtenteCorretto = c.ID_Utente;

                        // 🔎 CASO LEGACY:
                        // se ID_Utente corrisponde a un ID_OPERATORE,
                        // recuperiamo l'ID_UTENTE collegato
                        var opLegacy = db.OperatoriSinergia
                            .FirstOrDefault(o => o.ID_Operatore == c.ID_Utente);

                        if (opLegacy != null && opLegacy.ID_UtenteCollegato.HasValue)
                        {
                            idUtenteCorretto = opLegacy.ID_UtenteCollegato.Value;
                        }

                        // 🔢 Versionamento
                        int ver = db.Cluster_a
                            .Where(x => x.ID_Cluster_Originale == c.ID_Cluster)
                            .Max(x => (int?)x.NumeroVersione) ?? 0;

                        // 📦 Archivio (ID_UTENTE SEMPRE)
                        db.Cluster_a.Add(new Cluster_a
                        {
                            ID_Cluster_Originale = c.ID_Cluster,
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = idUtenteCorretto,          // ✅ SEMPRE ID_UTENTE
                            TipoCluster = c.TipoCluster,
                            PercentualePrevisione = c.PercentualePrevisione,
                            DataAssegnazione = c.DataAssegnazione,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = userId,
                            NumeroVersione = ver + 1,
                            ModificheTestuali = "Eliminazione pratica (supporto dati legacy)"
                        });
                    }

                    // 🗑️ Rimozione finale
                    if (clusters.Any())
                    {
                        db.Cluster.RemoveRange(clusters);
                        System.Diagnostics.Debug.WriteLine(
                            $"📉 [EliminaPratica] Cluster rimossi: {clusters.Count}"
                        );
                    }


                    // ======================================================
                    // 🧾 Archivia e rimuovi COMPENSI DETTAGLIO
                    // ======================================================
                    var compensi = db.CompensiPraticaDettaglio.Where(c => c.ID_Pratiche == id).ToList();
                    foreach (var c in compensi)
                    {
                        int ver = db.CompensiPraticaDettaglio_a
                            .Where(x => x.ID_RigaCompensoOriginale == c.ID_RigaCompenso)
                            .Max(x => (int?)x.NumeroVersione) ?? 0;

                        db.CompensiPraticaDettaglio_a.Add(new CompensiPraticaDettaglio_a
                        {
                            ID_RigaCompensoOriginale = c.ID_RigaCompenso,
                            ID_Pratiche = c.ID_Pratiche,
                            TipoCompenso = c.TipoCompenso,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
                            Categoria = c.Categoria,
                            ValoreStimato = c.ValoreStimato,
                            Ordine = c.Ordine,
                            EstremiGiudizio = c.EstremiGiudizio,
                            OggettoIncarico = c.OggettoIncarico,
                            DataCreazione = c.DataCreazione,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            UltimaModifica = c.UltimaModifica,
                            ID_UtenteUltimaModifica = c.ID_UtenteUltimaModifica,
                            Collaboratori = c.Collaboratori,
                            ID_ProfessionistaIntestatario = c.ID_ProfessionistaIntestatario,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = userId,
                            NumeroVersione = ver + 1,
                            ModificheTestuali = $"Archiviazione compenso eliminato (Pratica ID={id})"
                        });
                    }

                    if (compensi.Any())
                    {
                        db.CompensiPraticaDettaglio.RemoveRange(compensi);
                        System.Diagnostics.Debug.WriteLine($"📉 [EliminaPratica] Compensi rimossi: {compensi.Count}");
                    }


                    db.SaveChanges();

                    // ======================================================
                    // 📊 ARCHIVIAZIONE E RIMOZIONE PREVISIONALE
                    // ======================================================
                    var previsioni = db.Previsione.Where(p => p.ID_Pratiche == id).ToList();
                    if (previsioni.Any())
                    {
                        foreach (var p in previsioni)
                        {
                            db.Previsione_a.Add(new Previsione_a
                            {
                                ID_PrevisioneOriginale = p.ID_Previsione,
                                ID_Pratiche = p.ID_Pratiche,
                                ID_Professionista = p.ID_Professionista,
                                Percentuale = p.Percentuale,
                                TipoOperazione = p.TipoOperazione,
                                Descrizione = p.Descrizione,
                                ImportoPrevisto = p.ImportoPrevisto,
                                DataPrevisione = p.DataPrevisione,
                                Stato = p.Stato,
                                ID_UtenteCreatore = p.ID_UtenteCreatore,
                                NumeroVersione = (db.Previsione_a
                                    .Where(x => x.ID_PrevisioneOriginale == p.ID_Previsione)
                                    .Max(x => (int?)x.NumeroVersione) ?? 0) + 1,
                                DataArchiviazione = now,
                                ID_UtenteArchiviazione = userId,
                                ModificheTestuali = $"Archiviazione automatica in fase di eliminazione pratica (ID={id})"
                            });
                        }

                        db.Previsione.RemoveRange(previsioni);
                        System.Diagnostics.Debug.WriteLine($"📉 [EliminaPratica] Previsioni archiviate e rimosse: {previsioni.Count}");
                    }

                    db.SaveChanges();


                    // 🔁 Eliminazione logica
                    pratica.Stato = "Eliminato";
                    pratica.ID_UtenteUltimaModifica = userId;
                    pratica.UltimaModifica = now;
                    db.SaveChanges();

                    // 🔔 Notifiche agli utenti coinvolti
                    var destinatari = db.Cluster
                        .Where(c => c.ID_Pratiche == id)
                        .Select(c => c.ID_Utente)
                        .ToList();

                    // Include il responsabile
                    destinatari.Add(pratica.ID_UtenteResponsabile);

                    // Include sempre l’owner
                    if (pratica.ID_Owner.HasValue)
                        destinatari.Add(pratica.ID_Owner.Value);

                    foreach (var u in destinatari.Distinct())
                    {
                        db.Notifiche.Add(new Notifiche
                        {
                            Titolo = "Eliminazione Pratica",
                            Descrizione = $"La pratica \"{pratica.Titolo}\" è stata eliminata.",
                            DataCreazione = now,
                            ID_Utente = u,
                            Tipo = "Pratica",
                            Stato = "Non letto",
                            Contatore = 1
                        });
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "✅ Pratica eliminata correttamente e archiviata." });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"❌ Errore durante l'eliminazione: {ex.Message}" });
                }
            }
        }

        [HttpGet]
        public ActionResult GetPratica(int id)
        {
            var pratica = db.Pratiche
                .Where(p => p.ID_Pratiche == id && p.Stato != "Eliminato")
                .Select(p => new PraticaViewModel
                {
                    ID_Pratiche = p.ID_Pratiche,
                    Titolo = p.Titolo,
                    Descrizione = p.Descrizione,
                    DataInizioAttivitaStimata = p.DataInizioAttivitaStimata,
                    DataFineAttivitaStimata = p.DataFineAttivitaStimata,
                    Stato = p.Stato,
                    ID_Cliente = p.ID_Cliente,
                    ID_UtenteResponsabile = p.ID_UtenteResponsabile,
                    ID_UtenteUltimaModifica = p.ID_UtenteUltimaModifica,
                    ID_Owner = p.ID_Owner,
                    TrattenutaPersonalizzata = p.TrattenutaPersonalizzata,
                    Budget = p.Budget,
                    DataCreazione = p.DataCreazione,
                    UltimaModifica = p.UltimaModifica,
                    Note = p.Note,
                    Tipologia = p.Tipologia,
                    OggettoPratica = p.OggettoPratica,
                    ImportoFisso = p.ImportoFisso,
                    TariffaOraria = p.TariffaOraria,
                    AccontoGiudiziale = p.AccontoGiudiziale,
                    GradoGiudizio = p.GradoGiudizio,
                    TerminiPagamento = p.TerminiPagamento,
                    OrePreviste = p.OrePreviste,
                    OreEffettive = p.OreEffettive
                })
                .FirstOrDefault();

            if (pratica == null)
                return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

            // ======================================================
            // 👤 NOME RESPONSABILE
            // ======================================================
            var nomeResponsabile = db.Utenti
                .Where(u => u.ID_Utente == pratica.ID_UtenteResponsabile)
                .Select(u => u.Nome + " " + u.Cognome)
                .FirstOrDefault();

            // ======================================================
            // 🟦 RECUPERO OWNER — LOGICA DEFINITIVA (ID_Owner = ID_Operatore)
            // ======================================================
            string nomeOwner = "";

            if (pratica.ID_Owner.HasValue)
            {
                int idOperatore = pratica.ID_Owner.Value;

                var ownerData =
                    (from o in db.OperatoriSinergia
                     join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente into joined
                     from u in joined.DefaultIfEmpty()
                     where o.ID_Operatore == idOperatore
                     select new
                     {
                         Nome = (u != null ? u.Nome : o.Nome),
                         Cognome = (u != null ? u.Cognome : o.Cognome)
                     })
                    .FirstOrDefault();

                if (ownerData != null)
                    nomeOwner = ownerData.Nome + " " + ownerData.Cognome;
            }


            // ======================================================
            // 🏢 CLIENTE
            // ======================================================
            var nomeCliente = (from cli in db.Clienti
                               where cli.ID_Cliente == pratica.ID_Cliente
                               select string.IsNullOrEmpty(cli.RagioneSociale)
                                      ? (cli.Nome + " " + cli.Cognome)
                                      : cli.RagioneSociale)
                              .FirstOrDefault();

            // ======================================================
            // 👥 COLLABORATORI (CLUSTER)
            // ======================================================
            var utentiAssociati = (
                from c in db.Cluster
                where c.ID_Pratiche == id && c.TipoCluster == "Collaboratore"
                let op = db.OperatoriSinergia
                           .FirstOrDefault(o => o.ID_Operatore == c.ID_Utente)
                let opByUtente = db.OperatoriSinergia
                           .FirstOrDefault(o => o.ID_UtenteCollegato == c.ID_Utente)
                let operatoreFinale = op ?? opByUtente   // 1️⃣ operatore corretto
                let nomeOperatore = operatoreFinale != null
                    ? ((from u in db.Utenti
                        where u.ID_Utente == operatoreFinale.ID_UtenteCollegato
                        select u.Nome + " " + u.Cognome).FirstOrDefault()
                        ?? (operatoreFinale.Nome + " " + operatoreFinale.Cognome))
                    : "—"
                select new
                {
                    ID_Utente = operatoreFinale != null ? operatoreFinale.ID_Operatore : c.ID_Utente,
                    Nome = nomeOperatore,
                    c.PercentualePrevisione
                }
            ).ToList();


            // ======================================================
            // 📋 COSTI PRATICA
            // ======================================================
            var costiPratica = (
                from c in db.CostiPratica
                join a in db.AnagraficaCostiPratica on c.ID_AnagraficaCosto equals a.ID_AnagraficaCosto into joined
                from a in joined.DefaultIfEmpty()
                join f in db.OperatoriSinergia on c.ID_Fornitore equals f.ID_Operatore into joinedFornitori
                from f in joinedFornitori.DefaultIfEmpty()
                where c.ID_Pratiche == id
                select new
                {
                    c.ID_CostoPratica,
                    c.ID_AnagraficaCosto,
                    Descrizione = (c.Descrizione ?? ((a.Nome ?? "") + " - " + (a.Descrizione ?? ""))).Trim(),
                    c.Importo,
                    c.DataCompetenzaEconomica,
                    c.ID_Fornitore,
                    NomeFornitore = f != null
                        ? (f.Nome + " " + f.Cognome + (string.IsNullOrEmpty(f.PIVA) ? "" : " (" + f.PIVA + ")")).Trim()
                        : null,
                    c.DataInserimento
                }).ToList();

            // ======================================================
            // 💰 RIMBORSI
            // ======================================================
            var rimborsi = db.RimborsiPratica
                .Where(r => r.ID_Pratiche == id)
                .Select(r => new
                {
                    r.Descrizione,
                    r.Importo
                })
                .ToList();

            // ======================================================
            // 📄 DOCUMENTO INCARICO
            // ======================================================
            var documentoIncarico = db.DocumentiPratiche
                .Where(d => d.ID_Pratiche == id &&
                            (d.Estensione == ".html" || d.Estensione == ".pdf"))
                .OrderByDescending(d => d.DataCaricamento)
                .Select(d => new
                {
                    d.ID_Documento,
                    d.NomeFile,
                    d.Estensione,
                    d.TipoContenuto,
                    d.DataCaricamento
                })
                .FirstOrDefault();

            // ======================================================
            // 📤 RISPOSTA JSON
            // ======================================================
            return Json(new
            {
                success = true,
                data = pratica,
                responsabile = nomeResponsabile,
                nomeOwner = nomeOwner,
                cliente = nomeCliente,
                utentiAssociati,
                costi = costiPratica,
                rimborsi,
                incarico = documentoIncarico
            }, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public JsonResult GetCompensiPratica(int idPratica)
        {
            Debug.WriteLine($"[GetCompensiPratica] idPratica={idPratica}");

            // 🟢 Caso NUOVA PRATICA → compensi vuoti
            if (idPratica <= 0)
            {
                Debug.WriteLine("[GetCompensiPratica] Nuova pratica → ritorno JSON vuoto");
                return Json(new
                {
                    success = true,
                    dettaglio = new List<object>(),
                    raw = new List<object>(),
                    intestatari = new List<object>()
                }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                if (pratica == null)
                {
                    return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);
                }

                // 1️⃣ Compensi già salvati
                var compensiDettaglio = db.CompensiPraticaDettaglio
                    .Where(c => c.ID_Pratiche == idPratica)
                    .AsEnumerable()
                    .Select(c =>
                    {
                        Debug.WriteLine($"-- RigaCompenso {c.ID_RigaCompenso} --");
                        Debug.WriteLine($"   TipoCompenso={c.TipoCompenso}, Categoria={c.Categoria}, Importo={c.Importo}, ID_Intestatario={c.ID_ProfessionistaIntestatario}");

                        var nomeIntestatario = db.Utenti
                            .Where(u => u.ID_Utente == c.ID_ProfessionistaIntestatario)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault();

                        var listaCollab = new List<object>();
                        if (!string.IsNullOrEmpty(c.Collaboratori))
                        {
                            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CollaboratoreCompensoDTO>>(c.Collaboratori);
                            foreach (var col in parsed)
                            {
                                var nomeCollab = db.Utenti
                                    .Where(u => u.ID_Utente == col.ID_Collaboratore)
                                    .Select(u => u.Nome + " " + u.Cognome)
                                    .FirstOrDefault();

                                listaCollab.Add(new
                                {
                                    col.ID_Collaboratore,
                                    col.Percentuale,
                                    NomeCollaboratore = nomeCollab
                                });
                            }
                        }

                        return new
                        {
                            c.ID_RigaCompenso,
                            Metodo = c.TipoCompenso,
                            Tipologia = string.IsNullOrEmpty(c.Categoria) ? c.TipoCompenso : c.Categoria,
                            c.Descrizione,
                            c.Importo,
                            c.ValoreStimato,
                            c.Ordine,
                            c.EstremiGiudizio,
                            c.OggettoIncarico,
                            c.DataCreazione,
                            Creatore = db.Utenti.Where(u => u.ID_Utente == c.ID_UtenteCreatore)
                                .Select(u => u.Nome + " " + u.Cognome).FirstOrDefault(),
                            UltimaModifica = c.UltimaModifica,
                            UltimoModificatore = db.Utenti.Where(u => u.ID_Utente == c.ID_UtenteUltimaModifica)
                                .Select(u => u.Nome + " " + u.Cognome).FirstOrDefault(),
                            ID_ProfessionistaIntestatario = c.ID_ProfessionistaIntestatario,
                            NomeProfessionistaIntestatario = nomeIntestatario,
                            Collaboratori = listaCollab
                        };
                    })
                    .OrderBy(c => c.Ordine)
                    .ToList();

                // 2️⃣ Recupero intestatari possibili
                var intestatariPossibili = new List<dynamic>();

                // Owner
                if (pratica.ID_Owner.HasValue)
                {
                    var owner = (from o in db.OperatoriSinergia
                                 join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                                 where o.ID_Operatore == pratica.ID_Owner
                                 select new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                                .FirstOrDefault();
                    if (owner != null) intestatariPossibili.Add(owner);
                }

                // Responsabile
                if (pratica.ID_UtenteResponsabile > 0)
                {
                    var resp = db.Utenti
                        .Where(u => u.ID_Utente == pratica.ID_UtenteResponsabile)
                        .Select(u => new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                        .FirstOrDefault();
                    if (resp != null) intestatariPossibili.Add(resp);
                }

                // Professionisti collegati al cliente
                var collegati = (from cp in db.ClientiProfessionisti
                                 join o in db.OperatoriSinergia on cp.ID_Professionista equals o.ID_Operatore
                                 join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                                 where cp.ID_Cliente == pratica.ID_Cliente
                                 select new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                                 .ToList();

                intestatariPossibili.AddRange(collegati);

                var intestatariFinali = intestatariPossibili
                    .GroupBy(x => x.ID)
                    .Select(g => g.First())
                    .ToList();

                return Json(new
                {
                    success = true,
                    dettaglio = compensiDettaglio,
                    raw = compensiDettaglio,
                    intestatari = intestatariFinali
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetCompensiPratica][ERRORE] {ex}");
                return Json(new { success = false, message = "Errore server: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetIntestatariPratica(int? idPratica, int? idCliente)
        {
            Debug.WriteLine($"[GetIntestatariPratica] idPratica={idPratica}, idCliente={idCliente}");

            var intestatariPossibili = new List<dynamic>();

            try
            {
                if (idPratica.HasValue && idPratica > 0)
                {
                    // 🔹 Pratica esistente → recupero dalla tabella
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica.Value);
                    if (pratica == null)
                        return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

                    // Owner
                    if (pratica.ID_Owner.HasValue)
                    {
                        var owner = (from o in db.OperatoriSinergia
                                     join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                                     where o.ID_Operatore == pratica.ID_Owner
                                     select new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                                     .FirstOrDefault();
                        if (owner != null) intestatariPossibili.Add(owner);
                    }

                    // Responsabile
                    if (pratica.ID_UtenteResponsabile > 0)
                    {
                        var resp = db.Utenti
                            .Where(u => u.ID_Utente == pratica.ID_UtenteResponsabile)
                            .Select(u => new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                            .FirstOrDefault();
                        if (resp != null) intestatariPossibili.Add(resp);
                    }

                    // Collegati al cliente
                    idCliente = pratica.ID_Cliente;
                }

                // 🔹 Caso nuova pratica → uso direttamente idCliente
                if (idCliente.HasValue && idCliente > 0)
                {
                    // Recupero anche l'owner del cliente selezionato
                    var idOperatore = db.Clienti
                        .Where(c => c.ID_Cliente == idCliente.Value)
                        .Select(c => c.ID_Operatore)
                        .FirstOrDefault();

                    if (idOperatore > 0)
                    {
                        var owner = (from o in db.OperatoriSinergia
                                     join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                                     where o.ID_Operatore == idOperatore
                                     select new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                                     .FirstOrDefault();

                        if (owner != null) intestatariPossibili.Add(owner);
                    }

                    // Tutti i professionisti collegati
                    var collegati = (from cp in db.ClientiProfessionisti
                                     join o in db.OperatoriSinergia on cp.ID_Professionista equals o.ID_Operatore
                                     join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                                     where cp.ID_Cliente == idCliente.Value
                                     select new { ID = u.ID_Utente, Nome = u.Nome + " " + u.Cognome })
                                     .ToList();

                    intestatariPossibili.AddRange(collegati);
                }

                var intestatariFinali = intestatariPossibili
                    .GroupBy(x => x.ID)
                    .Select(g => g.First())
                    .ToList();

                return Json(new
                {
                    success = true,
                    intestatari = intestatariFinali
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetIntestatariPratica][ERRORE] {ex}");
                return Json(new { success = false, message = "Errore server: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult RiattivaPratica(int id)
        {
            try
            {
                var praticaArchiviata = db.Pratiche_a.FirstOrDefault(p => p.ID_Pratica_Originale == id);
                if (praticaArchiviata == null)
                    return Json(new { success = false, message = "Pratica archiviata non trovata." });

                var esisteGia = db.Pratiche.Any(p =>
                    p.Titolo == praticaArchiviata.Titolo &&
                    p.ID_Cliente == praticaArchiviata.ID_Cliente &&
                    p.Stato != "Eliminato");

                if (esisteGia)
                    return Json(new { success = false, message = "Questa pratica è già stata riattivata." });

                int idUtente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;

                var nuovaPratica = new Pratiche
                {
                    Titolo = praticaArchiviata.Titolo,
                    Descrizione = praticaArchiviata.Descrizione,
                    DataInizioAttivitaStimata = praticaArchiviata.DataInizioAttivitaStimata,
                    DataFineAttivitaStimata = praticaArchiviata.DataFineAttivitaStimata,
                    Stato = "Attiva",
                    ID_Cliente = praticaArchiviata.ID_Cliente,
                    ID_UtenteResponsabile = praticaArchiviata.ID_UtenteResponsabile,
                    ID_UtenteUltimaModifica = idUtente,
                    Budget = praticaArchiviata.Budget,
                    DataCreazione = now,
                    UltimaModifica = now,
                    Note = praticaArchiviata.Note,
                    //ImportoIncassato = praticaArchiviata.ImportoIncassato,
                    ID_Pratica_Originale = id
                };

                db.Pratiche.Add(nuovaPratica);
                db.SaveChanges();

                // 🔁 Relazioni
                var relazioni = db.RelazionePraticheUtenti_a.Where(r => r.ID_Pratiche == id).ToList();
                foreach (var r in relazioni)
                {
                    db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        ID_Utente = r.ID_Utente,
                        Ruolo = r.Ruolo,
                        DataAssegnazione = now
                    });
                }

                // 🔁 Cluster
                var clusterArch = db.Cluster_a.Where(c => c.ID_Pratiche == id).ToList();
                foreach (var c in clusterArch)
                {
                    db.Cluster.Add(new Cluster
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        ID_Utente = c.ID_Utente,
                        TipoCluster = c.TipoCluster,
                        PercentualePrevisione = c.PercentualePrevisione,
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Compensi
                var compensiArch = db.CompensiPratica_a.Where(c => c.ID_Pratiche == id).ToList();
                foreach (var c in compensiArch)
                {
                    db.CompensiPratica.Add(new CompensiPratica
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        Tipo = c.Tipo,
                        Descrizione = c.Descrizione,
                        Importo = c.Importo,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Rimborsi
                var rimborsiArch = db.RimborsiPratica_a.Where(r => r.ID_Pratiche == id).ToList();
                foreach (var r in rimborsiArch)
                {
                    db.RimborsiPratica.Add(new RimborsiPratica
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        Descrizione = r.Descrizione,
                        Importo = r.Importo,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Costi Pratica
                var costiPraticaArch = db.CostiPratica_a.Where(c => c.ID_Pratiche == id).ToList();
                foreach (var c in costiPraticaArch)
                {
                    db.CostiPratica.Add(new CostiPratica
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        Descrizione = c.Descrizione,
                        Importo = c.Importo,
                        ID_ClienteAssociato = c.ID_ClienteAssociato,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }


                // 🔁 Documenti Pratica (compresi incarichi HTML generati)
                var documentiArch = db.DocumentiPratiche_a.Where(d => d.ID_Pratiche == id).ToList();
                foreach (var doc in documentiArch)
                {
                    db.DocumentiPratiche.Add(new DocumentiPratiche
                    {
                        ID_Pratiche = nuovaPratica.ID_Pratiche,
                        NomeFile = doc.NomeFile,
                        Estensione = doc.Estensione,
                        TipoContenuto = doc.TipoContenuto,
                        Documento = doc.Documento,
                        Stato = doc.Stato,
                        DataCaricamento = now,
                        ID_UtenteCaricamento = idUtente,
                        Note = doc.Note
                    });
                }

                // 🔔 Notifiche
                var utenti = relazioni.Select(r => r.ID_Utente).ToList();
                utenti.Add(nuovaPratica.ID_UtenteResponsabile);

                foreach (var idDest in utenti.Distinct())
                {
                    db.Notifiche.Add(new Notifiche
                    {
                        Titolo = "Pratica riattivata",
                        Descrizione = $"La pratica \"{nuovaPratica.Titolo}\" è stata riattivata.",
                        DataCreazione = now,
                        Stato = "Non letto",
                        Tipo = "Pratica",
                        Contatore = 1,
                        ID_Utente = idDest
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Pratica riattivata con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la riattivazione: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult RecuperaPratica(int id)
        {
            try
            {
                var praticaArchivio = db.Pratiche_a.FirstOrDefault(p => p.ID_Pratica_Originale == id);
                if (praticaArchivio == null)
                    return Json(new { success = false, message = "Pratica archiviata non trovata." });

                var esisteGia = db.Pratiche.Any(p =>
                    p.Titolo == praticaArchivio.Titolo &&
                    p.ID_Cliente == praticaArchivio.ID_Cliente &&
                    p.Stato != "Eliminato");

                if (esisteGia)
                    return Json(new { success = false, message = "Questa pratica è già stata recuperata." });

                int idUtente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;

                var cliente = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == praticaArchivio.ID_Cliente);
                int? idOwner = cliente?.ID_Owner;

                var nuovaPratica = new Pratiche
                {
                    Titolo = praticaArchivio.Titolo,
                    Descrizione = praticaArchivio.Descrizione,
                    DataInizioAttivitaStimata = praticaArchivio.DataInizioAttivitaStimata,
                    DataFineAttivitaStimata = praticaArchivio.DataFineAttivitaStimata,
                    Stato = "Attiva",
                    ID_Cliente = praticaArchivio.ID_Cliente,
                    ID_UtenteResponsabile = praticaArchivio.ID_UtenteResponsabile,
                    ID_UtenteCreatore = praticaArchivio.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = idUtente,
                    Budget = praticaArchivio.Budget,
                    DataCreazione = praticaArchivio.DataCreazione ?? now,
                    UltimaModifica = now,
                    Note = praticaArchivio.Note,
                    //ImportoIncassato = praticaArchivio.ImportoIncassato ?? 0,
                    ID_Pratica_Originale = id
                };
                db.Pratiche.Add(nuovaPratica);
                db.SaveChanges();
                int nuovoId = nuovaPratica.ID_Pratiche;

                // 🔁 Cluster
                foreach (var cluster in db.Cluster_a.Where(c => c.ID_Pratiche == id))
                {
                    db.Cluster.Add(new Cluster
                    {
                        ID_Pratiche = nuovoId,
                        ID_Utente = cluster.ID_Utente,
                        TipoCluster = cluster.TipoCluster,
                        PercentualePrevisione = cluster.PercentualePrevisione,
                        DataAssegnazione = cluster.DataAssegnazione,
                        ID_UtenteCreatore = cluster.ID_UtenteArchiviazione ?? idUtente,
                        UltimaModifica = now,
                        ID_UtenteUltimaModifica = idUtente
                    });
                }


                // 🔁 Utenti associati
                var relazioniArchivio = db.RelazionePraticheUtenti_a.Where(r => r.ID_Pratiche == id).ToList();
                foreach (var rel in relazioniArchivio)
                {
                    db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                    {
                        ID_Pratiche = nuovoId,
                        ID_Utente = rel.ID_Utente,
                        Ruolo = "Professionista",
                        DataAssegnazione = rel.DataAssegnazione
                    });
                }


                // 🔁 Compensi
                foreach (var c in db.CompensiPratica_a.Where(c => c.ID_Pratiche == id))
                {
                    db.CompensiPratica.Add(new CompensiPratica
                    {
                        ID_Pratiche = nuovoId,
                        Tipo = c.Tipo,
                        Descrizione = c.Descrizione,
                        Importo = c.Importo,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Rimborsi
                foreach (var r in db.RimborsiPratica_a.Where(r => r.ID_Pratiche == id))
                {
                    db.RimborsiPratica.Add(new RimborsiPratica
                    {
                        ID_Pratiche = nuovoId,
                        Descrizione = r.Descrizione,
                        Importo = r.Importo,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Costi Pratica
                foreach (var c in db.CostiPratica_a.Where(c => c.ID_Pratiche == id))
                {
                    db.CostiPratica.Add(new CostiPratica
                    {
                        ID_Pratiche = nuovoId,
                        Descrizione = c.Descrizione,
                        Importo = c.Importo,
                        ID_ClienteAssociato = c.ID_ClienteAssociato,
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtente
                    });
                }

                // 🔁 Documenti Pratica (es. incarico HTML salvato o altri documenti)
                var documentiArch = db.DocumentiPratiche_a.Where(d => d.ID_Pratiche == id).ToList();
                foreach (var doc in documentiArch)
                {
                    db.DocumentiPratiche.Add(new DocumentiPratiche
                    {
                        ID_Pratiche = nuovoId,
                        NomeFile = doc.NomeFile,
                        Estensione = doc.Estensione,
                        TipoContenuto = doc.TipoContenuto,
                        Documento = doc.Documento,
                        Stato = doc.Stato,
                        DataCaricamento = now,
                        ID_UtenteCaricamento = idUtente,
                        Note = doc.Note
                    });
                }

                // 🔔 Notifiche
                string messaggio = $"La pratica \"{nuovaPratica.Titolo}\" è stata ripristinata.";

                var utenti = relazioniArchivio.Select(r => r.ID_Utente).ToList();
                utenti.Add(nuovaPratica.ID_UtenteResponsabile);
                if (idOwner.HasValue) utenti.Add(idOwner.Value);

                foreach (var idNotificato in utenti.Distinct().Where(u => u > 0))
                {
                    db.Notifiche.Add(new Notifiche
                    {
                        Titolo = "Ripristino Pratica",
                        Descrizione = messaggio,
                        DataCreazione = now,
                        ID_Utente = idNotificato,
                        Tipo = "Pratica",
                        Stato = "Non letto",
                        Contatore = 1
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Pratica recuperata con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il recupero: " + ex.Message });
            }
        }

        //[HttpGet]

        //public ActionResult GetTemplateByProfessionista(int idProfessionista)
        //{
        //    try
        //    {
        //        using (var db = new SinergiaDB())
        //        {
        //            // 🔍 Recupera la professione associata all'operatore (professionista)
        //            var idProfessione = db.OperatoriSinergia
        //                .Where(o => o.ID_Cliente == idProfessionista && o.TipoCliente == "Professionista")
        //                .Select(o => o.ID_Professione)
        //                .FirstOrDefault();

        //            if (idProfessione == 0 || idProfessione == null)
        //            {
        //                return Json(new { success = false, message = "Professione non associata a questo professionista." }, JsonRequestBehavior.AllowGet);
        //            }

        //            // 🔍 Recupera il template attivo per quella professione
        //            var template = db.TemplateIncarichi
        //                .Where(t => t.ID_Professione == idProfessione && t.Stato == "Attivo")
        //                .Select(t => new
        //                {
        //                    t.IDTemplateIncarichi,
        //                    t.NomeTemplate,
        //                    t.ContenutoHtml
        //                })
        //                .FirstOrDefault();

        //            if (template == null)
        //            {
        //                return Json(new { success = false, message = "Nessun template attivo trovato per questa professione." }, JsonRequestBehavior.AllowGet);
        //            }

        //            return Json(new { success = true, data = template }, JsonRequestBehavior.AllowGet);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new
        //        {
        //            success = false,
        //            message = "Errore durante il recupero del template: " + ex.Message
        //        }, JsonRequestBehavior.AllowGet);
        //    }
        //} 

        /* Gestione Template*/

        [HttpGet]
        public ActionResult GeneraFoglioIncarico(int idPratica)
        {
            try
            {
                // 🔍 Recupero la pratica + cliente collegato
                var praticaJoin = (from p in db.Pratiche
                                   join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                                   where p.ID_Pratiche == idPratica
                                   select new { Pratica = p, Cliente = c }).FirstOrDefault();

                if (praticaJoin == null)
                    return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

                var pratica = praticaJoin.Pratica;
                var cliente = praticaJoin.Cliente;

                // 🔍 Recupero professionista responsabile
                var professionista = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_UtenteCollegato == pratica.ID_UtenteResponsabile
                                      && o.TipoCliente == "Professionista");

                if (professionista == null)
                    return Json(new { success = false, message = "Professionista non trovato." }, JsonRequestBehavior.AllowGet);

                // 🔍 Recupero clienti associati al professionista
                var clientiAssociati = (from cp in db.ClientiProfessionisti
                                        join cl in db.Clienti on cp.ID_Cliente equals cl.ID_Cliente
                                        where cp.ID_Professionista == professionista.ID_Operatore
                                        select cl).ToList();

                // 🔍 Responsabile (utente collegato al responsabile pratica)
                var responsabile = db.Utenti.FirstOrDefault(u => u.ID_Utente == pratica.ID_UtenteResponsabile);

                // ==========================================================
                // 📌 Costruzione segnaposti
                // ==========================================================
                string nomeCliente = cliente.RagioneSociale
                    ?? (!string.IsNullOrEmpty(cliente.Cognome) || !string.IsNullOrEmpty(cliente.Nome)
                        ? $"{cliente.Cognome} {cliente.Nome}"
                        : "Cliente");

                string luogo = "";
                if (cliente.ID_Citta.HasValue)
                {
                    var citta = db.Citta.FirstOrDefault(c => c.ID_BPCitta == cliente.ID_Citta.Value);
                    if (citta != null) luogo = citta.NameLocalita;
                }
                var nomeCompletoCliente = !string.IsNullOrEmpty(cliente.RagioneSociale)
                     ? cliente.RagioneSociale   // Se è azienda → "Studio Rossi SRL"
                     : $"{cliente.Cognome} {cliente.Nome}".Trim(); // Se è persona fisica → "De Cola Andrea"

                // ==========================================================
                // 📌 Costruzione segnaposti
                // ==========================================================
                // ✅ Percorso assoluto compatibile con Rotativa
                string logoPath = "file:///" + Server.MapPath("~/Content/img/Icons/Logo Nuovo.png").Replace("\\", "/");

                var placeholdersBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // === CLIENTE ===
                    ["[CLIENTE]"] = !string.IsNullOrEmpty(cliente.RagioneSociale)
                        ? cliente.RagioneSociale
                        : (!string.IsNullOrEmpty(cliente.Nome) || !string.IsNullOrEmpty(cliente.Cognome)
                            ? $"{cliente.Cognome} {cliente.Nome}".Trim()
                            : "__________"),

                    ["[NOME CLIENTE]"] = !string.IsNullOrEmpty(cliente.Nome) ? cliente.Nome : "__________",
                    ["[COGNOME CLIENTE]"] = !string.IsNullOrEmpty(cliente.Cognome) ? cliente.Cognome : "__________",

                    // 👇 aggiunto placeholder corto [NOME COGNOME]
                    ["[NOME COGNOME]"] = (!string.IsNullOrEmpty(cliente.Nome) || !string.IsNullOrEmpty(cliente.Cognome))
                        ? $"{cliente.Cognome} {cliente.Nome}".Trim()
                        : "__________",

                    ["[RAGIONE SOCIALE]"] = !string.IsNullOrEmpty(cliente.RagioneSociale) ? cliente.RagioneSociale : "__________",
                    ["[CF CLIENTE]"] = !string.IsNullOrEmpty(cliente.CodiceFiscale) ? cliente.CodiceFiscale : "__________",
                    ["[P.IVA CLIENTE]"] = !string.IsNullOrEmpty(cliente.PIVA) ? cliente.PIVA : "__________",
                    ["[INDIRIZZO CLIENTE]"] = !string.IsNullOrEmpty(cliente.Indirizzo) ? cliente.Indirizzo : "__________",
                    ["[INDIRIZZO ]"] = !string.IsNullOrEmpty(cliente.Indirizzo) ? cliente.Indirizzo : "__________",
                    ["[SEDE LEGALE CLIENTE]"] = !string.IsNullOrEmpty(cliente.Indirizzo) ? cliente.Indirizzo : "__________",
                    ["[CITTA CLIENTE]"] = !string.IsNullOrEmpty(luogo) ? luogo : "__________",
                    ["[PROVINCIA CLIENTE]"] = "__________", // di solito vuota
                    ["[PROVINCIA ]"] = "__________",

                    // 🔹 AGGIUNTI - opzionali (non tutti i clienti li hanno)
                    ["[CITTA_NASCITA]"] = cliente.GetType().GetProperty("CittaNascita")?.GetValue(cliente)?.ToString() ?? "__________",
                    ["[DATA_NASCITA]"] = cliente.GetType().GetProperty("DataNascita")?.GetValue(cliente) is DateTime dn
                            ? dn.ToString("dd/MM/yyyy")
                            : "__________",

                    // === PROFESSIONISTA ===
                    ["[PROFESSIONISTA RESPONSABILE]"] = !string.IsNullOrWhiteSpace($"{professionista.Nome} {professionista.Cognome}".Trim())
                        ? $"{professionista.Nome} {professionista.Cognome}".Trim()
                        : "__________",
                    ["[CF PROFESSIONISTA]"] = !string.IsNullOrEmpty(professionista.CodiceFiscale) ? professionista.CodiceFiscale : "__________",
                    ["[INDIRIZZO PROFESSIONISTA]"] = !string.IsNullOrEmpty(professionista.Indirizzo) ? professionista.Indirizzo : "__________",
                    ["[P.IVA PROFESSIONISTA]"] = !string.IsNullOrEmpty(professionista.PIVA) ? professionista.PIVA : "__________",

                    ["[INDIRIZZO RESPONSABILE]"] = !string.IsNullOrEmpty(professionista.Indirizzo) ? professionista.Indirizzo : "__________",
                    ["[LOGO_RESPONSABILE]"] = $@"
                            <div style='width:100%; text-align:left; margin:0; padding:0;'>
                                <img src='{Url.Content("~/Content/img/Icons/Logo Nuovo.png")}'
                                     alt='Logo Sinergia'
                                     style='display:block; height:120px; width:auto;
                                            margin:0; padding:0;
                                            border:none; outline:none;
                                            background:none; line-height:0;
                                            -webkit-print-color-adjust:exact;'
                                />
                            </div>",

                    // === RESPONSABILE ===
                    ["[NOME RESPONSABILE]"] = responsabile != null ? $"{responsabile.Nome} {responsabile.Cognome}" : "__________",
                    ["[RUOLO RESPONSABILE]"] = !string.IsNullOrEmpty(responsabile?.Ruolo) ? responsabile.Ruolo : "__________",

                    // === PRATICA ===
                    ["[TITOLO PRATICA]"] = !string.IsNullOrEmpty(pratica.Titolo) ? pratica.Titolo : "__________",
                    ["[DESCRIZIONE PRATICA]"] = !string.IsNullOrEmpty(pratica.Descrizione) ? pratica.Descrizione : "__________",
                    ["[DATA INIZIO]"] = pratica.DataInizioAttivitaStimata?.ToString("dd/MM/yyyy") ?? "__________",
                    ["[DATA FINE]"] = pratica.DataFineAttivitaStimata?.ToString("dd/MM/yyyy") ?? "__________",
                    ["[BUDGET]"] = pratica.Budget > 0 ? pratica.Budget.ToString("N2") + " €" : "__________",

                    // === ALTRI CAMPI ===
                    ["[LUOGO]"] = !string.IsNullOrEmpty(luogo) ? luogo : "__________",
                    ["[DATA_GENERAZIONE]"] = DateTime.Now.ToString("dd/MM/yyyy"),
                    ["[DATA GENERAZIONE]"] = DateTime.Now.ToString("dd/MM/yyyy"),
                    ["DATA GENERAZIONE"] = DateTime.Now.ToString("dd/MM/yyyy"),

                    // campi a ore 
                    ["[OGGETTO_INCARICO]"] = "__________",
                    ["[DESCRIZIONE_RUOLO]"] = "__________",
                    ["[IMPORTO_ORARIO]"] = "__________",
                    //["[NUMERO_PROGRESSIVO]"] = "__________",
                    ["[PARTITA IVA CLIENTE]"] = !string.IsNullOrEmpty(cliente.PIVA) ? cliente.PIVA : "__________",
                };

                if (clientiAssociati.Any())
                {
                    placeholdersBase["[CLIENTI ASSOCIATI]"] = string.Join(", ",
                        clientiAssociati.Select(c =>
                            !string.IsNullOrEmpty(c.RagioneSociale)
                                ? c.RagioneSociale
                                : $"{c.Nome} {c.Cognome}".Trim()));
                }

                // 🔍 Debug per il nome e cognome
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Placeholder [NOME COGNOME] → '{placeholdersBase["[NOME COGNOME]"]}'");


                // ==========================================================
                // 📌 Recupero compensi e template
                // ==========================================================
                var compensi = db.CompensiPraticaDettaglio
                    .Where(cd => cd.ID_Pratiche == idPratica)
                    .ToList();

                // 🔍 Debug compensi
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Compensi trovati per pratica {idPratica}: {compensi.Count}");
                foreach (var c in compensi)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"   Ordine={c.Ordine}, Tipo={c.TipoCompenso}, Desc={c.Descrizione}, Importo={c.Importo}");
                }

                var tipiCompenso = compensi
                    .Select(c => c.TipoCompenso)
                    .Distinct()
                    .ToList();

                var templates = db.TemplateIncarichi
                    .Where(t => t.Stato == "Attivo")
                    .ToList();

                var risultati = new List<object>();
                foreach (var tipo in tipiCompenso)
                {
                    var template = templates.FirstOrDefault(t => t.TipoCompenso == tipo);
                    if (template == null) continue;

                    // 🔁 Normalizza prima i segnaposti spezzati
                    string contenuto = NormalizzaSegnaposti(template.ContenutoHtml);

                    // 🔍 DEBUG
                    LogSegnaposti(contenuto);

                    // ==============================================
                    // 🔍 DEBUG: Verifica tabella originale nel template
                    // ==============================================
                    System.Diagnostics.Debug.WriteLine("===== DEBUG TEMPLATE ORIGINALE GIUDIZIALE =====");
                    var debugMatch = Regex.Match(contenuto,
                        @"<table[\s\S]*?</table>",
                        RegexOptions.IgnoreCase);

                    if (debugMatch.Success)
                    {
                        System.Diagnostics.Debug.WriteLine(debugMatch.Value);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Nessuna tabella trovata nel template!");
                    }
                    System.Diagnostics.Debug.WriteLine("===============================================");


                    // 🔁 Compensi per tipo corrente
                    var compensiTipo = compensi
                        .Where(c => c.TipoCompenso == tipo)
                        .OrderBy(c => c.Ordine)
                        .ToList();

                    // ==========================================================
                    // 🔹 GESTIONE SPECIFICA PER TEMPLATE "A ORE"
                    // ==========================================================
                    if (tipo.Equals("A ore", StringComparison.OrdinalIgnoreCase))
                    {
                        // 1️⃣ Oggetto incarico
                        string oggetto = compensiTipo
                            .Where(c => !string.IsNullOrWhiteSpace(c.OggettoIncarico))
                            .Select(c => c.OggettoIncarico)
                            .FirstOrDefault() ?? "__________";
                        contenuto = contenuto.Replace("[OGGETTO_INCARICO]", oggetto);

                        // 2️⃣ Descrizione ruolo (campo A)
                        string descrizioneRuolo = compensiTipo
                            .Where(c => !string.IsNullOrWhiteSpace(c.Descrizione))
                            .Select(c => c.Descrizione)
                            .FirstOrDefault() ?? "__________";
                        contenuto = contenuto.Replace("[DESCRIZIONE_RUOLO]", descrizioneRuolo);

                        // 3️⃣ Importo orario (campo B)
                        string importoOrario = compensiTipo
                            .Where(c => c.Importo.HasValue)
                            .Select(c => $"{c.Importo.Value:N2} €")
                            .FirstOrDefault() ?? "__________";
                        contenuto = contenuto.Replace("[IMPORTO_ORARIO]", importoOrario);

                        // 4️⃣ Numero progressivo (campo C)
                        contenuto = contenuto.Replace("[NUMERO_PROGRESSIVO]", "1");

                        // 5️⃣ Costruzione blocco compensi (per eventuale tabella)
                        var righeCompensi = compensiTipo.Select((c, i) =>
                        {
                            string desc = !string.IsNullOrWhiteSpace(c.Descrizione) ? c.Descrizione.Trim() : "__________";
                            string imp = c.Importo.HasValue ? $"{c.Importo.Value:N2} €" : "__________";
                            return $"{i + 1}) {desc} - {imp}";
                        });
                        string bloccoCompensi = string.Join("<br/>", righeCompensi);
                        contenuto = contenuto.Replace("[TABELLA_COMPENSI]", bloccoCompensi);

                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Oggetto incarico (A ORE): {oggetto}");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Descrizione ruolo (A ORE): {descrizioneRuolo}");
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Importo orario (A ORE): {importoOrario}");
                    }

                    // 🔁 Sostituzione placeholder base
                    foreach (var kv in placeholdersBase)
                    {
                        contenuto = contenuto.Replace(kv.Key, kv.Value ?? "");
                    }

                    // ==========================================================
                    // 🔹 LOGICA STANDARD COMUNE (TUTTI I TIPI)
                    // ==========================================================
                    if (compensiTipo.Any())
                    {
                        // ✅ Controlla se tutte le descrizioni sono identiche
                        bool tutteUguali = compensiTipo
                            .Select(c => c.Descrizione?.Trim().ToLowerInvariant())
                            .Distinct()
                            .Count() == 1;

                        string listaDescrizioni;
                        string listaDescrizioniConNumero;
                        string listaProgressivi;

                        // ==========================================================
                        // 🔸 SEZIONE SPECIFICA PER "GIUDIZIALE"
                        // ==========================================================
                        if (tipo.Equals("Giudiziale", StringComparison.OrdinalIgnoreCase))
                        {
                            // 🔹 Se ci sono più descrizioni, le concateniamo in un'unica riga (per testo)
                            string unioneDescrizioni = string.Join(", ",
                                compensiTipo.Select(c => System.Net.WebUtility.HtmlEncode(c.Descrizione?.Trim() ?? "__________")));

                            // ❌ Tolto il "1)" per evitare duplicazione con [NUMERO_PROGRESSIVO]
                            listaDescrizioni = unioneDescrizioni;
                            listaDescrizioniConNumero = unioneDescrizioni;
                            listaProgressivi = "1";
                            contenuto = Regex.Replace(contenuto, @"\[\s*NUMERO_PROGRESSIVO\s*\]", listaProgressivi, RegexOptions.IgnoreCase);

                        }
                        else if (tutteUguali)
                        {
                            string unica = compensiTipo.First().Descrizione;
                            listaDescrizioni = unica;
                            listaDescrizioniConNumero = "1 " + unica;
                            listaProgressivi = "1";
                        }

                        else
                        {
                            // 🔹 Logica standard per tutti gli altri tipi
                            listaDescrizioni = string.Join(", ",
                                compensiTipo.Select(c => c.Descrizione));

                            listaDescrizioniConNumero = string.Join(", ",
                                compensiTipo.Select((c, i) =>
                                    $"<span style='white-space:nowrap;'>{i + 1}: {System.Net.WebUtility.HtmlEncode(c.Descrizione ?? "")}</span>"));

                            listaProgressivi = string.Join(", ",
                                compensiTipo.Select((c, i) => (i + 1).ToString()));
                        }

                        // ==========================================================
                        // 🔸 SEZIONE CALCOLI E IMPORTI
                        // ==========================================================
                        string listaImporti = string.Join("<br/>",
                            compensiTipo.Select(c => $"<div style='text-align:right;'>{c.Importo?.ToString("N2")} €</div>"));

                        decimal imponibile = compensiTipo.Sum(c => c.Importo ?? 0m);
                        decimal cpa = Math.Round(imponibile * 0.04m, 2);
                        decimal imponibileIva = imponibile + cpa;
                        decimal iva = Math.Round(imponibileIva * 0.22m, 2);
                        decimal totale = imponibileIva + iva;

                        string listaCategorie = string.Join(", ", compensiTipo.Select(c => c.Categoria));

                        // 🔁 Sostituzioni base
                        contenuto = Regex.Replace(
                            contenuto,
                            @"\[PROGRESSIVO\]\s*:\s*\[IMPORTO\]\s*\[CATEGORIA\]",
                            string.Join(", ", compensiTipo.Select((c, i) =>
                                $"<span style='white-space:nowrap;'>{i + 1}: {c.Importo?.ToString("N2")} € {c.Categoria}</span>")),
                            RegexOptions.IgnoreCase
                        );

                        contenuto = contenuto.Replace("[PROGRESSIVO]", listaProgressivi);
                        contenuto = contenuto.Replace("[DESCRIZIONE_ATTIVITA]", listaDescrizioni);
                        contenuto = contenuto.Replace("[IMPORTO]", listaImporti);
                        contenuto = contenuto.Replace("[CATEGORIA]", listaCategorie);

                        // 🔹 Valori calcolati
                        contenuto = Regex.Replace(contenuto, @"\[\s*CPA\s*\]", $"<div style='text-align:right;'>{cpa:N2} €</div>", RegexOptions.IgnoreCase);
                        contenuto = Regex.Replace(contenuto, @"\[\s*IMPONIBILE\s*\]", $"<div style='text-align:right;'>{imponibileIva:N2} €</div>", RegexOptions.IgnoreCase);
                        contenuto = Regex.Replace(contenuto, @"\[\s*IVA\s*\]", $"<div style='text-align:right;'>{iva:N2} €</div>", RegexOptions.IgnoreCase);
                        contenuto = Regex.Replace(contenuto, @"\[\s*TOTALE\s*\](?!_AVERE)", $"<div style='text-align:right;'>{totale:N2} €</div>", RegexOptions.IgnoreCase);
                        contenuto = Regex.Replace(contenuto, @"\[\s*TOTALE_AVERE\s*\]", $"<div style='text-align:right;'>{totale:N2} €</div>", RegexOptions.IgnoreCase);

                        if (tipo.Equals("Giudiziale", StringComparison.OrdinalIgnoreCase))
                        {
                            // ==========================================
                            // 🔹 Gestione speciale per GIUDIZIALE (tabella 30%, righe più alte)
                            // ==========================================

                            string estremi = compensiTipo
                                .Where(c => !string.IsNullOrWhiteSpace(c.EstremiGiudizio))
                                .Select(c => c.EstremiGiudizio)
                                .FirstOrDefault() ?? "__________";
                            contenuto = contenuto.Replace("[ESTREMI_GIUDIZIO]", estremi);

                            var matchTabella = Regex.Match(contenuto, @"(<table[^>]*>)([\s\S]*?)(</table>)", RegexOptions.IgnoreCase);

                            if (matchTabella.Success)
                            {
                                string tabellaOriginale = matchTabella.Value;

                                // 🔹 Righe con maggiore altezza e spaziatura verticale
                                var righeHtml = compensiTipo.Select((c, i) =>
                                {
                                    string numero = (i + 1).ToString();
                                    string descrizione = !string.IsNullOrWhiteSpace(c.Descrizione)
                                        ? System.Net.WebUtility.HtmlEncode(c.Descrizione.Trim())
                                        : "__________";
                                    string importo = c.Importo.HasValue
                                        ? $"{c.Importo.Value:N2} €"
                                        : "__________";

                                    return $@"
                                <tr style='border-bottom:0.5px solid #000; line-height:1.4; height:20px;'>
                                    <td style='border:0.5px solid #000; padding:4px 3px; font-size:8pt; width:70%; vertical-align:middle;'>
                                        {numero}) {descrizione}
                                    </td>
                                    <td style='border:0.5px solid #000; padding:4px 3px; font-size:10pt; width:30%; text-align:right; white-space:nowrap; vertical-align:middle;'>
                                        {importo}
                                    </td>
                                </tr>";
                                });

                                // 🔹 Tabella centrata (30% larghezza) con righe più alte
                                string nuovaTabella = $@"
                                <table style='width:30%; margin:auto; border-collapse:collapse; border:0.5px solid #000; font-size:10pt; line-height:1.4;'>
                                    <tbody>
                                        {string.Join("", righeHtml)}
                                    </tbody>
                                </table>";

                                contenuto = Regex.Replace(
                                    contenuto,
                                    @"<table[\s\S]*?</table>",
                                    nuovaTabella,
                                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                                );
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("⚠️ Nessuna tabella trovata nel template GIUDIZIALE!");
                            }

                            System.Diagnostics.Debug.WriteLine($"[DEBUG GIUDIZIALE] Tabella GIUDIZIALE (30%) con righe alte generata con {compensiTipo.Count} righe.");
                        }


                        // 🔍 Debug riepilogo
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Sostituito [DESCRIZIONE_ATTIVITA] → " + listaDescrizioni);
                        System.Diagnostics.Debug.WriteLine("[DEBUG] Calcolato Totale → " + totale.ToString("N2"));
                    }
                    else
                    {
                        // ==========================================================
                        // 🔹 CASO NESSUN COMPENSO
                        // ==========================================================
                        contenuto = contenuto
                            .Replace("[DESCRIZIONE_ATTIVITA]", "<em>Nessuna attività</em>")
                            .Replace("[IMPORTO]", "-")
                            .Replace("[IMPONIBILE]", "<div style='text-align:right;'>0,00 €</div>")
                            .Replace("[CPA]", "<div style='text-align:right;'>0,00 €</div>")
                            .Replace("[IVA]", "<div style='text-align:right;'>0,00 €</div>")
                            .Replace("[TOTALE]", "<div style='text-align:right;'>0,00 €</div>")
                            .Replace("[TOTALE_AVERE]", "<div style='text-align:right;'>0,00 €</div>");
                    }

                    // ==========================================================
                    // 🔹 GESTIONE SPECIFICA PER TEMPLATE "A ORE"
                    // ==========================================================
                    if (tipo == "A ore")
                    {
                        var righeCompensi = compensiTipo.Select((c, i) =>
                        {
                            string descrizione = !string.IsNullOrWhiteSpace(c.Descrizione)
                                ? c.Descrizione.Trim()
                                : "__________";
                            string importo = c.Importo.HasValue
                                ? $"{c.Importo.Value:N2} €"
                                : "__________";
                            return $"{i + 1}) {descrizione} - {importo}";
                        });

                        string bloccoCompensi = string.Join("<br/>", righeCompensi);

                        // 3️⃣ Sostituzioni placeholder
                        contenuto = Regex.Replace(
                            contenuto,
                            @"\[\s*TABELLA_COMPENSI\s*\]",
                            bloccoCompensi,
                            RegexOptions.IgnoreCase
                        );

                        // 4️⃣ Pulizia eventuali residui
                        contenuto = Regex.Replace(
                            contenuto,
                            @"\[(OGGETTO_INCARICO|DESCRIZIONE_RUOLO|IMPORTO_ORARIO|NUMERO_PROGRESSIVO|TABELLA_COMPENSI)\]",
                            "__________",
                            RegexOptions.IgnoreCase
                        );
                    }

                    // ⬇️ SOLO ALLA FINE rimuovo quadre residue rimaste
                    contenuto = Regex.Replace(
                        contenuto,
                        @"\[(?:[A-Z0-9_ ]+)\]",
                        m => m.Value.Trim('[', ']'),
                        RegexOptions.IgnoreCase
                    );

                    // 🔹 Aggiungi separatore tra i template per evitare che si attacchino
                    contenuto += "<div style='margin:40px 0; border-top:1px solid #ccc;'></div>";

                    risultati.Add(new
                    {
                        TipoCompenso = tipo,
                        Html = contenuto
                    });
                }

                // ==========================================================
                // 🧩 DEBUG: Log riepilogativo dei template generati
                // ==========================================================
                foreach (var t in risultati)
                {
                    var tipo = t.GetType().GetProperty("TipoCompenso")?.GetValue(t, null);
                    var html = t.GetType().GetProperty("Html")?.GetValue(t, null) as string;
                    System.Diagnostics.Debug.WriteLine(
                        $"[DEBUG ADD TEMPLATE] TipoCompenso={tipo}, Lunghezza HTML={html?.Length ?? 0}"
                    );
                }

                // ==========================================================
                // 🧩 DEBUG: Log JSON generato nel backend (solo struttura)
                // ==========================================================
                try
                {
                    string jsonDebug = Newtonsoft.Json.JsonConvert.SerializeObject(
                        new
                        {
                            success = true,
                            templates = risultati.Select(r => new
                            {
                                TipoCompenso = r.GetType().GetProperty("TipoCompenso")?.GetValue(r, null),
                                LunghezzaHtml = ((string)r.GetType().GetProperty("Html")?.GetValue(r, null))?.Length ?? 0
                            })
                        },
                        Newtonsoft.Json.Formatting.Indented
                    );

                    System.Diagnostics.Debug.WriteLine("=== DEBUG: JSON TEMPLATES ===");
                    System.Diagnostics.Debug.WriteLine(jsonDebug);
                    System.Diagnostics.Debug.WriteLine("==============================");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine("[ERRORE DEBUG JSON]: " + logEx.Message);
                }



                return Json(new { success = true, templates = risultati }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                // 🔍 Costruisci messaggio dettagliato
                string dettagli = ex.Message;

                if (ex.InnerException != null)
                    dettagli += " | INNER: " + ex.InnerException.Message;

                if (ex.InnerException?.InnerException != null)
                    dettagli += " | INNER2: " + ex.InnerException.InnerException.Message;

                // 🔁 Ritorna al client il messaggio completo
                return Json(new
                {
                    success = false,
                    message = $"❌ Errore durante la generazione foglio incarico: {dettagli}"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Logga a console tutti i segnaposti presenti nel contenuto (con quadre o senza).
        /// </summary>
        private void LogSegnaposti(string contenuto)
        {
            System.Diagnostics.Debug.WriteLine("===== SEGNAPOSTI TROVATI =====");

            // Cattura con quadre [QUALCOSA]
            var matchesQuadre = Regex.Matches(contenuto, @"\[[^\]]+\]");
            foreach (Match m in matchesQuadre)
            {
                System.Diagnostics.Debug.WriteLine("Con quadre: " + m.Value);
            }

            // Cattura parole chiave senza quadre tipo PROGRESSIVO, IMPORTO, CATEGORIA
            var matchesPlain = Regex.Matches(contenuto, @"\b(PROGRESSIVO|IMPORTO|CATEGORIA)\b", RegexOptions.IgnoreCase);
            foreach (Match m in matchesPlain)
            {
                System.Diagnostics.Debug.WriteLine("Senza quadre: " + m.Value);
            }

            System.Diagnostics.Debug.WriteLine("================================");
        }

        private string NormalizzaSegnaposti(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return html;

            // 1. Normalizza quadre sporche generate da Word
            html = Regex.Replace(html, @"\[\s*", "[");   // "[  NOME" → "[NOME"
            html = Regex.Replace(html, @"\s*\]", "]");   // "NOME   ]" → "NOME]"

            // 2. Correggi segnaposti doppi [[NOME COGNOME]]
            html = Regex.Replace(html, @"\[{2,}", "[");  // [[ → [
            html = Regex.Replace(html, @"\]{2,}", "]");  // ]] → ]

            // 3. Segnaposti generici [ ... ] con tag/spazi in mezzo
            html = Regex.Replace(html,
                @"\[\s*([A-Z0-9_. ]+?)\s*\]",   // accetta solo lettere maiuscole/numeri/underscore/punto/spazi
                m => $"[{m.Groups[1].Value.Trim()}]",
                RegexOptions.IgnoreCase);


            // 4. Normalizza underscore
            html = Regex.Replace(html,
                @"(<span[^>]*>)?_{3,}(<\/span>)?",
                "[VUOTO]",
                RegexOptions.IgnoreCase);

            // 5. Segnaposti speciali spezzati da Word
            // NOME COGNOME CLIENTE
            html = Regex.Replace(html,
                @"N\s*O\s*M\s*E\s*(</?\w+[^>]*>\s*)*C\s*O\s*G\s*N\s*O\s*M\s*E\s*(</?\w+[^>]*>\s*)*C\s*L\s*I\s*E\s*N\s*T\s*E",
                "[NOME COGNOME CLIENTE]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // NOME COGNOME semplice (NON già racchiuso tra [])
            html = Regex.Replace(html,
                @"(?<!\[)N\s*O\s*M\s*E\s*(</?\w+[^>]*>\s*)*C\s*O\s*G\s*N\s*O\s*M\s*E(?!\s*C\s*L\s*I\s*E\s*N\s*T\s*E)(?!\])",
                "[NOME COGNOME]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // 6. Blocchi multipli PROGRESSIVO + IMPORTO + CATEGORIA
            html = Regex.Replace(html,
                @"\[\s*PROGRESSIVO\s*\](?:\s|<[^>]+>)*:(?:\s|<[^>]+>)*\[\s*IMPORTO\s*\](?:\s|<[^>]+>)*\[\s*CATEGORIA\s*\]",
                "[PROGRESSIVO]: [IMPORTO] [CATEGORIA]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            html = Regex.Replace(html,
                @"PROGRESSIVO(?:\s|<[^>]+>)*:(?:\s|<[^>]+>)*\[\s*IMPORTO\s*\](?:\s|<[^>]+>)*\[\s*CATEGORIA\s*\]",
                "[PROGRESSIVO]: [IMPORTO] [CATEGORIA]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            html = Regex.Replace(html,
                @"PROGRESSIVO(?:\s|<[^>]+>)*:(?:\s|<[^>]+>)*IMPORTO(?:\s|<[^>]+>)*CATEGORIA",
                "[PROGRESSIVO]: [IMPORTO] [CATEGORIA]",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return html;
        }


        private string CostruisciListaCompensiLineare(List<CompensiPraticaDettaglio> compensi)
        {
            if (compensi == null || !compensi.Any())
                return "<p><em>Nessun compenso inserito</em></p>";

            var sb = new StringBuilder();
            int i = 1;
            foreach (var c in compensi.OrderBy(c => c.Ordine))
            {
                sb.Append($"{i}) {c.Descrizione} - {c.Importo:N2} €<br/>");
                i++;
            }
            return sb.ToString();
        }

        // caricato dall esterno
        [HttpPost]
        public ActionResult SalvaFoglioIncaricoFirmato(int idPratica, HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, message = "Nessun file selezionato." });

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Il file deve essere in formato PDF." });

            try
            {
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                {
                    byte[] fileData = binaryReader.ReadBytes(file.ContentLength);

                    var documento = new DocumentiPratiche
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        NomeFile = System.IO.Path.GetFileName(file.FileName),
                        Estensione = ".pdf",
                        TipoContenuto = "application/pdf",
                        Documento = fileData,
                        Stato = "Firmato",
                        DataCaricamento = DateTime.Now,
                        ID_UtenteCaricamento = UserManager.GetIDUtenteCollegato(),
                        Note = "Incarico firmato caricato manualmente"
                    };

                    db.DocumentiPratiche.Add(documento);
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Incarico firmato caricato con successo." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetDocumentiIncarico(int idPratica)
        {
            try
            {
                var documenti = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == idPratica &&
                                (d.NomeFile.Contains("Incarico") || d.Note.Contains("Incarico") ||
                                 d.Stato == "Firmato" || d.Stato == "Da firmare"))
                    .Select(d => new
                    {
                        d.ID_Documento,
                        d.NomeFile,
                        d.Estensione,
                        d.Stato,
                        DataCaricamento = d.DataCaricamento,
                        d.Note
                    })
                    .OrderByDescending(d => d.DataCaricamento)
                    .ToList();

                return Json(new
                {
                    success = true,
                    documenti
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore durante il caricamento dei documenti incarico: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        [ValidateInput(false)]
        public ActionResult GeneraPDFIncaricoDaHtml()
        {
            try
            {
                if (!int.TryParse(Request.Form["idPratica"], out int idPratica))
                    return Json(new { success = false, message = "ID pratica non valido." });

                // ✅ Lettura HTML non validato
                string html = Request.Unvalidated["html"];

                // 👇 qui viene definito tipoCompenso (nuovo parametro dal frontend)
                string tipoCompenso = Request.Form["tipoCompenso"];
                if (string.IsNullOrWhiteSpace(tipoCompenso))
                    tipoCompenso = "Generico";  // fallback

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                // ======================================================
                // 🧹 PULIZIA HTML (rimozione elementi inutili e rettangolini Word)
                // ======================================================

                // 🔸 1. Rimuovi background color e stili inutili
                html = Regex.Replace(html,
                    @"background(-color)?:\s*[^;""']+;?",
                    "",
                    RegexOptions.IgnoreCase);

                // 🔸 2. Rimuovi <div> vuoti con bordi o dimensioni
                html = Regex.Replace(html,
                    @"<div[^>]*(border|width|height)[^>]*>\s*</div>",
                    "",
                    RegexOptions.IgnoreCase);

                // 🔸 3. Rimuovi <span> vuoti con width/height (causa rettangolini)
                html = Regex.Replace(html,
                    @"<span[^>]*(width|height)\s*:\s*\d+(\.\d+)?(pt|in|cm|mm)[^>]*>\s*</span>",
                    "",
                    RegexOptions.IgnoreCase);

                // 🔸 4. Rimuovi paragrafi contenenti solo <span> vuoti
                html = Regex.Replace(html,
                    @"<p[^>]*>\s*(<span[^>]*(width|height)\s*:\s*\d+(\.\d+)?(pt|in|cm|mm)[^>]*>\s*</span>)+\s*</p>",
                    "",
                    RegexOptions.IgnoreCase);

                // 🔸 5. Rimuovi paragrafi o div completamente vuoti
                html = Regex.Replace(html, @"<p[^>]*>(\s|&nbsp;)*</p>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<div[^>]*>(\s|&nbsp;)*</div>", "", RegexOptions.IgnoreCase);
                html = html.Trim();

                // 🔥 Rimuove immagini senza src (quelle che creano il quadratino vuoto)
                html = Regex.Replace(html, @"<img[^>]*(src\s*=\s*['""]\s*['""][^>]*)?>", "", RegexOptions.IgnoreCase);
                // ======================================================
                // 📄 Sostituzione marker di separazione tra template
                // (gestisce anche i casi HTML-encodati da Word o browser)
                // ======================================================
                html = html.Replace("<!-- TEMPLATE_SEPARATOR -->", "<div class='page-break'></div>");
                html = html.Replace("&lt;!-- TEMPLATE_SEPARATOR --&gt;", "<div class='page-break'></div>");
                html = html.Replace("<!--TEMPLATE_SEPARATOR-->", "<div class='page-break'></div>");
                html = html.Replace("&lt;!--TEMPLATE_SEPARATOR--&gt;", "<div class='page-break'></div>");

                // 🔍 Log diagnostico (opzionale)
                int countSeparators = Regex.Matches(html, "page-break", RegexOptions.IgnoreCase).Count;
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Trovati {countSeparators} separatori di pagina nel template HTML");

                // ======================================================
                // 🧱 Evita che le tabelle (es. compensi) vengano spezzate tra due pagine
                //     e ingloba anche il testo o span immediatamente precedenti
                // ======================================================
                if (!html.Contains("class='avoid-break'"))
                {
                    var lines = html.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None).ToList();
                    for (int i = 1; i < lines.Count; i++)
                    {
                        if (lines[i].Contains("<table") && !lines[i].Contains("avoid-break"))
                        {
                            string prev = lines[i - 1].Trim();
                            if (!string.IsNullOrEmpty(prev) && (prev.Contains("<span") || prev.Contains("€")))
                            {
                                lines[i - 1] = $"<div class='avoid-break'>{lines[i - 1]}";
                                lines[i] = lines[i] + "</div>";
                            }
                        }
                    }
                    html = string.Join("\n", lines);
                }


                // ======================================================
                // 💄 CSS globale per evitare la divisione delle tabelle
                // ======================================================
                string cssNoBreak = @"
                <style>
                .avoid-break {
                    page-break-before: auto !important;
                    page-break-after: auto !important;
                    page-break-inside: avoid !important;
                    break-inside: avoid !important;
                    display: block !important;
                    margin: 10px 0 !important;
                }
                table {
                    border-collapse: collapse !important;
                    page-break-inside: avoid !important;
                    break-inside: avoid !important;
                    margin: auto !important;
                    margin-bottom: 10px !important;
                }
                tr, td, th {
                    page-break-inside: avoid !important;
                    break-inside: avoid !important;
                    border: 1px solid #000 !important;
                    vertical-align: middle !important;
                }
                td, th {
                    padding: 3px 6px !important;
                    font-size: 10pt !important;
                }

                /* ✅ Imposta numerazione di pagina (per browser o PDF compatibili) */
        @page {
                    @bottom-center {
                        content: 'Pagina ' counter(page) ' di ' counter(pages);
                        font-size: 9pt;
                        color: #666;
                    }
                }

                /* ✅ Footer visivo compatibile anche con Rotativa */
                .footer-page {
                    text-align: center;
                    font-size: 9pt;
                    color: #666;
                    margin-top: 20px;
                }
                </style>";


                html = cssNoBreak + html;

                // ======================================================
                // 🧾 DEBUG: salva HTML pulito (facoltativo)
                // ======================================================
                string debugPath = Server.MapPath("~/Content/test_html.txt");
                System.IO.File.WriteAllText(debugPath, html);
                System.Diagnostics.Debug.WriteLine($"🧩 HTML pulito salvato in: {debugPath}");

                // ======================================================
                // 📄 Nome file PDF
                // ======================================================
                string titoloSanificato = pratica.Titolo;
                if (!string.IsNullOrEmpty(titoloSanificato))
                {
                    foreach (var c in Path.GetInvalidFileNameChars())
                        titoloSanificato = titoloSanificato.Replace(c, '_');
                }
                else
                {
                    titoloSanificato = pratica.ID_Pratiche.ToString();
                }

                string nomeFile = $"Incarico_{titoloSanificato}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                // ======================================================
                // 🔍 DEBUG VISIVO per identificare il quadratino misterioso
                // ======================================================
                try
                {
                    string debugHtml = html;

                    // 🔴 Evidenzia immagini senza src
                    debugHtml = Regex.Replace(debugHtml,
                        @"<img([^>]*)(src\s*=\s*['""]\s*['""])?([^>]*)>",
                        "<div style='border:2px solid red; background:#ffeeee; padding:5px; margin:5px;'>IMG VUOTO TROVATO</div>",
                        RegexOptions.IgnoreCase);

                    // 🟡 Evidenzia eventuali v:shape di Word
                    debugHtml = Regex.Replace(debugHtml,
                        @"<v:shape[^>]*>.*?</v:shape>",
                        "<div style='border:2px dashed orange; background:#fff4cc; padding:5px; margin:5px;'>V:SHAPE TROVATO</div>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    // 🟡 Evidenzia eventuali object
                    debugHtml = Regex.Replace(debugHtml,
                        @"<object[^>]*>.*?</object>",
                        "<div style='border:2px dashed orange; background:#fff4cc; padding:5px; margin:5px;'>OBJECT TROVATO</div>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    // 📝 Salva file HTML per analisi visiva
                    string debugFilePath = Server.MapPath("~/Content/debug_html_logo.html");
                    System.IO.File.WriteAllText(debugFilePath, debugHtml);
                    System.Diagnostics.Debug.WriteLine("🔍 File debug HTML salvato in: " + debugFilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Errore durante il debug HTML: " + ex.Message);
                }

                // ======================================================
                // 🖨️ Generazione PDF con Rotativa
                // ======================================================
                var pdf = new Rotativa.ViewAsPdf("~/Views/TemplateIncarichi/TemplateCompilato.cshtml", (object)html)
                {
                    FileName = nomeFile,
                    PageSize = Rotativa.Options.Size.A4,
                    PageMargins = new Rotativa.Options.Margins(15, 15, 25, 15),
                    CustomSwitches = "--disable-forms --disable-javascript --print-media-type --disable-smart-shrinking"
                };

                byte[] pdfBytes = pdf.BuildPdf(ControllerContext);


                // ======================================================
                // 💾 Salvataggio in DocumentiPratiche
                // ======================================================
                var documento = new DocumentiPratiche
                {
                    ID_Pratiche = pratica.ID_Pratiche,
                    NomeFile = nomeFile,
                    Documento = pdfBytes,
                    Estensione = ".pdf",
                    TipoContenuto = "application/pdf",
                    Stato = "Da firmare",
                    DataCaricamento = DateTime.Now,
                    ID_UtenteCaricamento = UserManager.GetIDUtenteCollegato(),
                    CategoriaDocumento = $"Incarico {tipoCompenso}",
                    Note = "Incarico generato da template"
                };

                db.DocumentiPratiche.Add(documento);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ PDF generato e salvato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore generazione PDF: " + ex.Message });
            }
        }



        /*Fine Gestione Template */

        [HttpGet]
        public ActionResult DettaglioPratica(int id, string tipoCliente)
        {
            System.Diagnostics.Trace.WriteLine("══════════════════════════════════════════════════════");
            System.Diagnostics.Trace.WriteLine($"📄 [DettaglioPratica] ID Pratica = {id}, TipoCliente = {tipoCliente}");

            try
            {
                int idUtenteCollegato = UserManager.GetIDUtenteCollegato();
                System.Diagnostics.Trace.WriteLine($"👤 Utente collegato: {idUtenteCollegato}");

                // =====================================================
                // 🔍 PRATICA BASE
                // =====================================================
                var praticaEntity = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == id && p.Stato != "Eliminato");
                if (praticaEntity == null)
                {
                    return View("~/Views/Shared/Error.cshtml", model: "Pratica non trovata o eliminata.");
                }

                // =====================================================
                // 🧾 CLIENTE + OWNER
                // =====================================================
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == praticaEntity.ID_Cliente);
                string nomeCliente = cliente != null ? $"{cliente.Nome} {cliente.Cognome}".Trim() : "Cliente sconosciuto";

                string nomeOwnerCliente = "-";
                if (cliente?.ID_Operatore != null)
                {
                    var ownerCliente = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == cliente.ID_Operatore);
                    if (ownerCliente != null)
                        nomeOwnerCliente = $"{ownerCliente.Nome} {ownerCliente.Cognome}".Trim();
                }

                // =====================================================
                // 👨‍💼 RESPONSABILE (LETTURA CORRETTA DA UTENTI)
                // =====================================================
                string nomeResponsabile = db.Utenti
                    .Where(u => u.ID_Utente == praticaEntity.ID_UtenteResponsabile)
                    .Select(u => u.Nome + " " + u.Cognome)
                    .FirstOrDefault() ?? "Responsabile sconosciuto";

                System.Diagnostics.Trace.WriteLine($"👨‍💼 Responsabile: {nomeResponsabile}");

                // =====================================================
                // 👥 CLUSTER ASSOCIATI (lettura corretta SOLO da OperatoriSinergia)
                // =====================================================
                var clusterRaw = (
                    from c in db.Cluster
                    where c.ID_Pratiche == id
                    select new
                    {
                        c.ID_Pratiche,
                        c.ID_Utente,   // questo è SEMPRE ID_Operatore
                        c.TipoCluster,
                        c.PercentualePrevisione,
                        c.DataAssegnazione,

                        Operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == c.ID_Utente)
                    }
                ).ToList();

                var clusterList = clusterRaw.Select(c => new ClusterViewModel
                {
                    ID_Pratiche = c.ID_Pratiche,
                    ID_Utente = c.ID_Utente,
                    TipoCluster = c.TipoCluster,
                    PercentualePrevisione = c.PercentualePrevisione,
                    DataAssegnazione = c.DataAssegnazione,

                    NomeUtente = c.Operatore != null
                        ? $"{c.Operatore.Nome} {c.Operatore.Cognome}".Trim()
                        : "—",

                    ImportoCalcolato = praticaEntity.Budget * (c.PercentualePrevisione / 100)
                }).ToList();

                // =====================================================
                // 💼 COLLABORATORI DA COMPENSI DETTAGLIO
                // =====================================================
                var listaCollaboratoriDettaglio = new List<CollaboratoreDettaglioViewModel>();
                var compensiDettaglio = db.CompensiPraticaDettaglio
                    .Where(cd => cd.ID_Pratiche == id)
                    .ToList();

                foreach (var dett in compensiDettaglio)
                {
                    if (!string.IsNullOrEmpty(dett.Collaboratori))
                    {
                        try
                        {
                            var collabs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(dett.Collaboratori);

                            foreach (var c in collabs)
                            {
                                decimal perc = (decimal?)(c.Percentuale ?? 0) ?? 0;
                                decimal importoRiga = dett.Importo ?? 0;
                                decimal quota = Math.Round(importoRiga * (perc / 100m), 2);

                                listaCollaboratoriDettaglio.Add(new CollaboratoreDettaglioViewModel
                                {
                                    Nome = (string)(c.NomeCollaboratore ?? "-"),
                                    Percentuale = perc,
                                    Importo = quota,
                                    NomeCompenso = $"{(dett.TipoCompenso ?? "-")} - {(dett.Descrizione ?? "-")}"
                                });
                            }
                        }
                        catch (Exception jsonEx)
                        {
                            System.Diagnostics.Trace.WriteLine($"⚠️ Errore parse JSON collaboratori: {jsonEx.Message}");
                        }
                    }
                }

                // =====================================================
                // 🧾 AVVISI PARCELLA
                // =====================================================
                var avvisiParcella = db.AvvisiParcella
                    .Where(a => a.ID_Pratiche == id && a.Stato != "Annullato")
                    .Select(a => new AvvisoParcellaViewModel
                    {
                        ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                        TitoloAvviso = a.TitoloAvviso,
                        DataAvviso = a.DataAvviso,
                        Importo = a.Importo,
                        MetodoPagamento = a.MetodoPagamento,
                        Stato = a.Stato,
                        ImportoIVA = a.ImportoIVA,
                        AliquotaIVA = a.AliquotaIVA,
                        ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                        ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale
                    })
                    .ToList();

                // =====================================================
                // 💰 TOTALE RIEPILOGO
                // =====================================================
                decimal totaleCompensi = compensiDettaglio.Sum(c => (decimal?)c.Importo) ?? 0;
                decimal totaleRimborsi = db.RimborsiPratica.Where(r => r.ID_Pratiche == id).Sum(r => (decimal?)r.Importo) ?? 0;
                decimal totaleCosti = db.CostiPratica.Where(c => c.ID_Pratiche == id).Sum(c => (decimal?)c.Importo) ?? 0;

                // =====================================================
                // 📦 MODEL COMPLETO
                // =====================================================
                var pratica = new PraticaViewModel
                {
                    ID_Pratiche = praticaEntity.ID_Pratiche,
                    Titolo = praticaEntity.Titolo,
                    Stato = praticaEntity.Stato,
                    Budget = praticaEntity.Budget,
                    NomeCliente = nomeCliente,
                    NomeUtenteResponsabile = nomeResponsabile,
                    Note = praticaEntity.Note,
                    DataCreazione = praticaEntity.DataCreazione
                };

                var model = new VisualizzaDettaglioPraticaViewModel
                {
                    Pratica = pratica,
                    Cluster = clusterList,
                    TotaleCompensi = totaleCompensi,
                    TotaleRimborsi = totaleRimborsi,
                    TotaleCosti = totaleCosti,
                    AvvisiParcella = avvisiParcella,
                    CollaboratoriDettaglio = listaCollaboratoriDettaglio
                };

                return View("~/Views/Pratiche/VisualizzaDettaglio.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("~/Views/Shared/Error.cshtml",
                    model: $"Errore durante il caricamento del dettaglio pratica: {ex.Message}");
            }
        }



        [HttpGet]
        public JsonResult GetUtentiDisponibiliPerPratica(int idCliente)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var lista = DashboardHelper.GetUtentiDisponibiliPerPratica(idCliente, idUtente);
            return Json(lista, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult GetDocumentiPratica(int idPratica)
        {
            if (idPratica == 0)
                return PartialView("~/Views/Pratiche/_VisualizzaDocumentiPratica.cshtml", new List<DocumentiPratiche>());

            var documenti = db.DocumentiPratiche
                .Where(d => d.ID_Pratiche == idPratica && d.Stato == "Attivo")
                .OrderByDescending(d => d.DataCaricamento)
                .ToList();

            ViewBag.IDPratica = idPratica;

            return PartialView("~/Views/Pratiche/_VisualizzaDocumentiPratica.cshtml", documenti);
        }


        [HttpPost]
        public ActionResult CaricaDocumentoPratica(int idPratica, List<HttpPostedFileBase> files)
        {
            if (files == null || !files.Any())
            {
                return Json(new { success = false, message = "Nessun file selezionato." });
            }

            var estensioniConsentite = new List<string>
                {
                    ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" ,".txt"
                };

            int idUtente = UserManager.GetIDUtenteCollegato();
            DateTime now = DateTime.Now;

            foreach (var file in files)
            {
                if (file == null || file.ContentLength == 0)
                    continue;

                string estensione = Path.GetExtension(file.FileName).ToLower();

                if (!estensioniConsentite.Contains(estensione))
                {
                    return Json(new { success = false, message = $"Formato file non consentito: {file.FileName}" });
                }

                using (var binaryReader = new BinaryReader(file.InputStream))
                {
                    var documentoBytes = binaryReader.ReadBytes(file.ContentLength);

                    var documento = new DocumentiPratiche
                    {
                        ID_Pratiche = idPratica,
                        NomeFile = Path.GetFileName(file.FileName),
                        Estensione = estensione,
                        TipoContenuto = file.ContentType,
                        Documento = documentoBytes,
                        DataCaricamento = now,
                        ID_UtenteCaricamento = idUtente,
                        Stato = "Attivo"
                    };

                    db.DocumentiPratiche.Add(documento);
                }
            }

            db.SaveChanges();

            return Json(new { success = true, message = "Documenti caricati correttamente." });
        }


        // Funzione di supporto per db 
        private byte[] ConvertToBytes(HttpPostedFileBase file)
        {
            using (var inputStream = file.InputStream)
            {
                var memoryStream = new System.IO.MemoryStream();
                inputStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        [HttpGet]
        public ActionResult DownloadDocumentoPratica(int idDocumento)
        {
            try
            {
                var documento = db.DocumentiPratiche.FirstOrDefault(d => d.ID_Documento == idDocumento);
                if (documento == null)
                    return HttpNotFound("Documento non trovato.");

                var fileBytes = documento.Documento;
                if (fileBytes == null || fileBytes.Length == 0)
                    return HttpNotFound("Contenuto del file non disponibile.");

                // 🧩 Normalizza nome, estensione e tipo
                string nomeFile = documento.NomeFile;
                string estensione = documento.Estensione?.ToLower() ?? "";
                string contentType = documento.TipoContenuto ?? "application/octet-stream";

                if (estensione == ".p7m" || nomeFile.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase))
                    contentType = "application/pkcs7-mime";

                if (estensione == ".pdf" || nomeFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    contentType = "application/pdf";

                if (!nomeFile.EndsWith(estensione, StringComparison.OrdinalIgnoreCase))
                    nomeFile += estensione;

                // 💾 Restituisci file scaricabile
                Response.AddHeader("Content-Disposition", $"attachment; filename=\"{nomeFile}\"");
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRORE DownloadDocumentoPratica] {ex.Message}");
                return new HttpStatusCodeResult(500, "Errore durante il download del documento.");
            }
        }



        [HttpPost]
        public ActionResult EliminaDocumentoPratica(int id)
        {
            try
            {
                var documento = db.DocumentiPratiche.FirstOrDefault(d => d.ID_Documento == id);
                if (documento == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                db.DocumentiPratiche.Remove(documento);
                db.SaveChanges();

                return Json(new { success = true, message = "Documento eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }


        // questo metodo mi serve per selezionare gli utenti da inserire in una pratica
        [HttpGet]
        public JsonResult GetUtentiAttivi(int? idCliente)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[GetUtentiAttivi] INIZIO - idCliente={idCliente}");

                int? idOwner = null;

                if (idCliente.HasValue && idCliente > 0)
                {
                    // 🔹 1. Cerco come Cliente
                    int? idOperatore = db.Clienti
                        .Where(c => c.ID_Cliente == idCliente.Value)
                        .Select(c => c.ID_Operatore)
                        .FirstOrDefault();

                    if (idOperatore > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Step1A] Interpretato come Cliente {idCliente} → ID_Operatore={idOperatore}");
                    }
                    else
                    {
                        idOperatore = idCliente.Value;
                        System.Diagnostics.Debug.WriteLine($"[Step1B] Interpretato come Operatore → {idOperatore}");
                    }

                    // 🔹 2. Traduco in ID_UtenteOwner
                    if (idOperatore > 0)
                    {
                        idOwner = db.OperatoriSinergia
                            .Where(o => o.ID_Operatore == idOperatore)
                            .Select(o => o.ID_UtenteCollegato)
                            .FirstOrDefault();

                        System.Diagnostics.Debug.WriteLine($"[Step2] Operatore={idOperatore} → UtenteOwner={idOwner}");
                    }
                }
                else
                {
                    // 🔹 Caso nuova pratica → prendo owner dalla sessione
                    int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
                    idOwner = idUtenteLoggato;
                    System.Diagnostics.Debug.WriteLine($"[StepNuovaPratica] Nuova pratica → escludo sempre Owner corrente {idOwner}");
                }

                // 🔹 Recupero professionisti attivi
                var utentiQuery = db.Utenti
                    .Where(u => u.Stato == "Attivo" && u.TipoUtente == "Professionista");

                var utentiPrimaDelFiltro = utentiQuery.ToList();
                System.Diagnostics.Debug.WriteLine($"[Step3] Professionisti attivi trovati={utentiPrimaDelFiltro.Count}");

                // 🔹 Escludo l'owner (sia in nuova che in modifica pratica)
                var utentiAttivi = utentiPrimaDelFiltro
                    .Where(u => !idOwner.HasValue || u.ID_Utente != idOwner.Value)
                    .Select(u => new
                    {
                        u.ID_Utente,
                        NomeCompleto = u.Nome + " " + u.Cognome,
                        Ruolo = u.TipoUtente
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[Step4] Lista finale dopo esclusione → Count={utentiAttivi.Count}");
                foreach (var u in utentiAttivi)
                {
                    System.Diagnostics.Debug.WriteLine($"[Step4] → {u.ID_Utente} {u.NomeCompleto}");
                }

                System.Diagnostics.Debug.WriteLine("[GetUtentiAttivi] FINE");

                return Json(utentiAttivi, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetUtentiAttivi][ERRORE] {ex}");
                return Json(new { success = false, message = "Errore server: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult CreaUtenteDaPratica(UtenteViewModel model)
        {
            var utenteId = (int?)Session["ID_Utente"];
            if (utenteId == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dati utente non validi." });

            // Verifica se utente esiste già
            var esiste = db.Utenti.Any(u =>
                u.Nome.ToLower() == model.Nome.ToLower() &&
                u.Cognome.ToLower() == model.Cognome.ToLower());

            if (esiste)
                return Json(new { success = false, message = "Utente già esistente." });

            // 🔥 Genera codice password temporanea (esempio: 1234)
            var codiceTemp = new Random().Next(1000, 9999).ToString();

            // 🔥 Crea l'utente
            var nuovoUtente = new Utenti
            {
                Nome = model.Nome,
                Cognome = model.Cognome,
                MAIL1 = model.MAIL1, // Usando MAIL1 perché non hai Email
                TipoUtente = model.TipoUtente,
                Stato = "Attivo",
                DataCreazione = DateTime.Now,
                ID_UtenteCreatore = utenteId,
                PasswordTemporanea = codiceTemp,
                PasswordHash = "TEMP",
                Salt = null,
                NomeAccount = $"{model.Nome.Trim().ToLower()[0]}.{model.Cognome.Trim().ToLower().Replace(" ", "")}"
            };

            db.Utenti.Add(nuovoUtente);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                idUtente = nuovoUtente.ID_Utente,
                nomeCompleto = nuovoUtente.Nome + " " + nuovoUtente.Cognome
            });
        }
        // questo metodo si attiva gia se ha quel cliente e stato gia assegnato un responsabile e mostra una lista di quali sono e da li e possibile selezionarli 
        [HttpGet]
        public JsonResult GetUtentiAssociatiCliente(int idCliente)
        {
            // 🔍 Recupera cliente
            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == idCliente);
            if (cliente == null)
            {
                return Json(new { success = false, message = "Cliente non trovato." }, JsonRequestBehavior.AllowGet);
            }

            // 1️⃣ Owner
            var owner = (from o in db.OperatoriSinergia
                         join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                         where o.ID_Operatore == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore
                         select new
                         {
                             ID_Utente = u.ID_Utente,
                             NomeCompleto = u.Nome + " " + u.Cognome + " (Owner)"
                         }).FirstOrDefault();

            // 2️⃣ Associati
            var associati = (from cp in db.ClientiProfessionisti
                             join o in db.OperatoriSinergia on cp.ID_Professionista equals o.ID_Operatore
                             join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                             where cp.ID_Cliente == idCliente
                             select new
                             {
                                 ID_Utente = u.ID_Utente,
                                 NomeCompleto = u.Nome + " " + u.Cognome
                             }).ToList();

            // 3️⃣ Combina risultati
            var utenti = new List<object>();
            if (owner != null) utenti.Add(owner);
            utenti.AddRange(associati);

            // 🔽 Ordino subito sulla proprietà anonima
            var ordinati = utenti.OrderBy(u => u.GetType().GetProperty("NomeCompleto").GetValue(u, null)).ToList();

            return Json(ordinati, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public JsonResult GetClientiAttivi()
        {
            // 👤 Usa l'utente ATTIVO (impersonificato o loggato normale)
            int idUtente = UserManager.GetIDUtenteAttivo();

            // 🔍 Trova l'operatore (professionista) collegato a quell'utente
            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente && o.TipoCliente == "Professionista");

            if (operatore == null)
            {
                return Json(new { success = false, message = "Operatore non trovato." }, JsonRequestBehavior.AllowGet);
            }

            int idProfessionista = operatore.ID_Operatore;

            // 🔹 Prende tutti i clienti in cui:
            //   - è l'owner (campo legacy ID_Operatore)
            //   - oppure è associato nella nuova tabella ClientiProfessionisti
            var clienti = db.Clienti
                .Where(c => c.Stato == "Attivo" &&
                           (c.ID_Operatore == idProfessionista ||
                            db.ClientiProfessionisti.Any(cp => cp.ID_Cliente == c.ID_Cliente &&
                                                               cp.ID_Professionista == idProfessionista)))
              .Select(c => new
              {
                  c.ID_Cliente,
                  Nome = string.IsNullOrEmpty(c.RagioneSociale)
            ? (c.Cognome + " " + c.Nome) // ✅ Cognome prima del Nome
            : c.RagioneSociale
              })

                .OrderBy(c => c.Nome)
                .ToList();

            return Json(clienti, JsonRequestBehavior.AllowGet);
        }

        // questo metodo parte nel caso di creazione pratica l'azienda e il professionista non ha la partita iva registrata in anagrafica 
        [HttpPost]
        public JsonResult CompletaDatiCliente(OperatoriSinergia model)
        {
            var cliente = db.OperatoriSinergia.Find(model.ID_Operatore);
            if (cliente == null)
                return Json(new { success = false, message = "Cliente non trovato." });

            cliente.PIVA = model.PIVA;
            cliente.CodiceFiscale = model.CodiceFiscale;
            cliente.MAIL1 = model.MAIL1;
            cliente.UltimaModifica = DateTime.Now;
            db.SaveChanges();

            return Json(new { success = true, message = "Dati cliente aggiornati." });
        }

        [HttpGet]
        public ActionResult GetAnagraficaCostiPratica()
        {
            try
            {
                var lista = db.AnagraficaCostiPratica
                    .Where(c => c.Attivo)
                    .OrderBy(c => c.Nome)
                    .Select(c => new AnagraficaCostiPraticaViewModel
                    {
                        ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                        Nome = c.Nome,
                        Descrizione = c.Descrizione,
                        Attivo = c.Attivo,
                        DataCreazione = (DateTime)c.DataCreazione,
                        ID_UtenteCreatore = c.ID_UtenteCreatore,
                        TipoCreatore = c.TipoCreatore
                    })
                    .ToList();

                return Json(new { success = true, data = lista }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetFornitoriAttivi()
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    // ======================================================
                    // 🏢 FORNITORI: Operatori di tipo "Azienda" e attivi
                    // ======================================================
                    var fornitori = db.OperatoriSinergia
                        .Where(o => o.TipoCliente == "Azienda" && o.Stato == "Attivo")
                        .OrderBy(o => o.Nome)
                        .ToList() // forza esecuzione SQL
                        .Select(o => new
                        {
                            o.ID_Operatore,
                            Nome = !string.IsNullOrEmpty(o.PIVA)
                                ? o.Nome + " (" + o.PIVA + ")"
                                : o.Nome
                        })
                        .ToList();

                    // ======================================================
                    // ✅ Risposta JSON
                    // ======================================================
                    return Json(new
                    {
                        success = true,
                        data = fornitori
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ [GetFornitoriAttivi] Errore: " + ex.Message);
                if (ex.InnerException != null)
                    System.Diagnostics.Trace.WriteLine("🔹 Inner: " + ex.InnerException.Message);

                return Json(new
                {
                    success = false,
                    message = "Errore durante il caricamento fornitori: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// 🔍 Recupera la categoria associata a un costo dell’anagrafica costi pratica.
        /// Usato per filtrare i fornitori compatibili.
        /// </summary>
        [HttpGet]
        public ActionResult GetCategoriaByCosto(int idCosto)
        {
            try
            {
                if (idCosto <= 0)
                    return Json(new { success = false, message = "ID costo non valido." }, JsonRequestBehavior.AllowGet);

                // Recupera ID categoria collegata all'anagrafica del costo
                var idCategoria = db.AnagraficaCostiPratica
                    .Where(c => c.ID_AnagraficaCosto == idCosto)
                    .Select(c => c.ID_Categoria)
                    .FirstOrDefault();

                if (idCategoria == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Categoria non trovata per questo costo."
                    }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    idCategoria
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ [GetCategoriaByCosto] Errore: " + ex.Message);
                return Json(new
                {
                    success = false,
                    message = "Errore durante il recupero categoria: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// 🏢 Restituisce la lista dei fornitori attivi appartenenti a una determinata categoria di servizi.
        /// </summary>
        [HttpGet]
        public ActionResult GetFornitoriByCategoria(int idCategoria)
        {
            try
            {
                // Se non c’è categoria → ritorna tutti i fornitori attivi (fallback)
                var query = db.OperatoriSinergia
                    .Where(f => f.TipoCliente == "Azienda" && f.EFornitore == true && f.Stato == "Attivo");

                if (idCategoria > 0)
                    query = query.Where(f => f.ID_CategoriaServizi == idCategoria);

                var fornitori = query
                    .OrderBy(f => f.Nome)
                    .Select(f => new
                    {
                        f.ID_Operatore,
                        Nome = !string.IsNullOrEmpty(f.PIVA)
                            ? f.Nome + " (" + f.PIVA + ")"
                            : f.Nome
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    data = fornitori
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ [GetFornitoriByCategoria] Errore: " + ex.Message);
                return Json(new
                {
                    success = false,
                    message = "Errore durante il recupero fornitori: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public JsonResult GetOwnerCliente(int idCliente)
        {
            try
            {
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == idCliente && c.Stato != "Eliminato");
                if (cliente == null)
                    return Json(new { success = false, message = "Cliente non trovato." }, JsonRequestBehavior.AllowGet);

                if (cliente.ID_Operatore == 0)
                    return Json(new { success = false, message = "Cliente senza operatore." }, JsonRequestBehavior.AllowGet);

                // 🔍 Recupera record OperatoriSinergia (professionista owner collegato al cliente)
                var owner = db.OperatoriSinergia.FirstOrDefault(o =>
                    o.ID_Operatore == cliente.ID_Operatore &&
                    o.TipoCliente == "Professionista" &&
                    o.Stato != "Eliminato");

                if (owner == null)
                    return Json(new { success = false, message = "Owner non trovato." }, JsonRequestBehavior.AllowGet);

                // 🔍 Utente collegato
                var utenteOwner = db.Utenti.FirstOrDefault(u => u.ID_Utente == owner.ID_UtenteCollegato);

                string nomeCompleto = string.IsNullOrWhiteSpace(owner.Cognome)
                    ? owner.Nome
                    : $"{owner.Nome} {owner.Cognome}";

                return Json(new
                {
                    success = true,
                    nomeOwner = nomeCompleto,
                    idOperatore = owner.ID_Operatore,                // ID Operatore (tabella OperatoriSinergia)
                    idOwner = utenteOwner?.ID_Utente,              // ✅ ID corretto dell’utente collegato
                    idProfessione = owner.ID_Professione
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore durante il recupero dell'owner.",
                    dettaglio = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public ActionResult GetCategoriePratiche()
        {
            var list = db.AnagraficaCategoriePratiche
                .Where(c => c.Attivo)
                .OrderBy(c => c.Tipo)
                .Select(c => new
                {
                    value = c.Tipo,
                    text = c.Tipo
                }).ToList();

            return Json(list, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult EsportaPraticheCsv(DateTime? da, DateTime? a)
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            bool isAdmin = utenteCorrente.TipoUtente == "Admin";

            // =====================================================
            // 🔥 RECUPERO PROFESSIONISTA SELEZIONATO DALLA NAVBAR
            // =====================================================

            int idProfessionistaFiltro = 0;

            if (isAdmin)
            {
                // 👉 Admin filtra in base al professionista selezionato nella navbar
                idProfessionistaFiltro = Session["IDClienteProfessionistaCorrente"] != null
                    ? Convert.ToInt32(Session["IDClienteProfessionistaCorrente"])
                    : 0;
            }
            else
            {
                // 👉 Utente normale → filtra per il proprio operatore collegato
                idProfessionistaFiltro = UserManager.GetOperatoreDaUtente(idUtenteLoggato);
            }

            System.Diagnostics.Trace.WriteLine($"EXPORT CSV → Filtro Professionista = {idProfessionistaFiltro}");

            // =====================================================

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Conclusa" };

            var query = db.Pratiche.Where(p =>
                p.Stato != "Eliminato" &&
                statiValidi.Contains(p.Stato) &&
                p.DataCreazione >= inizio &&
                p.DataCreazione <= fine
            );

            // 🔥 Applico il filtro solo se valido
            if (idProfessionistaFiltro > 0)
                query = query.Where(p => p.ID_Owner == idProfessionistaFiltro);

            var pratiche = query.OrderBy(p => p.DataCreazione).ToList();

            // =====================================================
            // CSV BUILDER
            // =====================================================

            var sb = new StringBuilder();
            sb.AppendLine("ID Pratica;Titolo;Cliente;Owner;Responsabile;Data Creazione;Stato;Budget;Totale Compensi");

            foreach (var p in pratiche)
            {
                string cliente = db.Clienti
                    .Where(c => c.ID_Cliente == p.ID_Cliente)
                    .Select(c => c.Nome + " " + c.Cognome)
                    .FirstOrDefault() ?? "-";

                string owner = db.OperatoriSinergia
                    .Where(o => o.ID_Operatore == p.ID_Owner)
                    .Select(o => o.Nome + " " + o.Cognome)
                    .FirstOrDefault() ?? "-";

                string responsabile = db.Utenti
                    .Where(u => u.ID_Utente == p.ID_UtenteResponsabile)
                    .Select(u => u.Nome + " " + u.Cognome)
                    .FirstOrDefault() ?? "-";

                decimal totaleCompensi = db.CompensiPraticaDettaglio
                    .Where(c => c.ID_Pratiche == p.ID_Pratiche)
                    .Sum(c => (decimal?)c.Importo) ?? 0m;

                sb.AppendLine($"{p.ID_Pratiche};{p.Titolo};{cliente};{owner};{responsabile};{p.DataCreazione:dd/MM/yyyy};{p.Stato};{p.Budget:N2};{totaleCompensi:N2}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Pratiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }


        [HttpGet]
        public ActionResult EsportaPratichePdf(DateTime? da, DateTime? a)
        {
            System.Diagnostics.Trace.WriteLine("==== PDF EXPORT START ====");

            try
            {
                int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
                System.Diagnostics.Trace.WriteLine("Utente loggato = " + idUtenteLoggato);

                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);
                if (utenteCorrente == null)
                {
                    System.Diagnostics.Trace.WriteLine("ERRORE: utente NON autenticato");
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
                }

                bool isAdmin = utenteCorrente.TipoUtente == "Admin";
                System.Diagnostics.Trace.WriteLine("IsAdmin = " + isAdmin);

                int idProfessionistaFiltro = 0;

                if (isAdmin)
                {
                    if (Session["IDClienteProfessionistaCorrente"] != null)
                    {
                        idProfessionistaFiltro = Convert.ToInt32(Session["IDClienteProfessionistaCorrente"]);
                        System.Diagnostics.Trace.WriteLine("Filtro da navbar = " + idProfessionistaFiltro);
                    }
                    else
                        System.Diagnostics.Trace.WriteLine("Nessun professionista selezionato → export totale");
                }
                else
                {
                    idProfessionistaFiltro = UserManager.GetOperatoreDaUtente(idUtenteLoggato);
                    System.Diagnostics.Trace.WriteLine("Filtro operatore collegato = " + idProfessionistaFiltro);
                }

                DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

                System.Diagnostics.Trace.WriteLine("Range date = " + inizio + " → " + fine);

                var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Conclusa" };

                var query = db.Pratiche.Where(p =>
                    p.Stato != "Eliminato" &&
                    statiValidi.Contains(p.Stato) &&
                    p.DataCreazione >= inizio &&
                    p.DataCreazione <= fine
                );

                if (idProfessionistaFiltro > 0)
                {
                    System.Diagnostics.Trace.WriteLine("APPLICO filtro owner = " + idProfessionistaFiltro);
                    query = query.Where(p => p.ID_Owner == idProfessionistaFiltro);
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("NESSUN filtro applicato");
                }

                // 🔥 LOG numero pratiche trovate PRIMA del Select
                var praticheList = query.ToList();
                System.Diagnostics.Trace.WriteLine("Pratiche trovate = " + praticheList.Count);

                var lista = new List<PraticaViewModel>();

                foreach (var p in praticheList)
                {
                    System.Diagnostics.Trace.WriteLine("--- Pratica ID = " + p.ID_Pratiche + " ---");

                    // CLIENTE
                    string nomeCliente = "-";
                    try
                    {
                        nomeCliente = db.Clienti
                            .Where(c => c.ID_Cliente == p.ID_Cliente)
                            .Select(c => c.Nome + " " + c.Cognome)
                            .FirstOrDefault() ?? "-";

                        System.Diagnostics.Trace.WriteLine("Cliente: " + nomeCliente);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("ERRORE CLIENTE: " + ex.Message);
                    }

                    // OWNER
                    string nomeOwner = "-";
                    try
                    {
                        if (p.ID_Owner.HasValue)
                        {
                            nomeOwner = db.OperatoriSinergia
                                .Where(o => o.ID_Operatore == p.ID_Owner.Value)
                                .Select(o => o.Nome + " " + o.Cognome)
                                .FirstOrDefault() ?? "-";
                        }
                        System.Diagnostics.Trace.WriteLine("Owner: " + nomeOwner);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("ERRORE OWNER: " + ex.Message);
                    }

                    // COMPENSI
                    decimal totaleCompensi = 0;
                    try
                    {
                        totaleCompensi = (decimal)db.CompensiPraticaDettaglio
                            .Where(c => c.ID_Pratiche == p.ID_Pratiche)
                            .ToList()
                            .Sum(c => c.Importo);

                        System.Diagnostics.Trace.WriteLine("Totale Compensi = " + totaleCompensi);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("ERRORE COMPENSI: " + ex.Message);
                    }

                    lista.Add(new PraticaViewModel
                    {
                        ID_Pratiche = p.ID_Pratiche,
                        Titolo = p.Titolo ?? "",
                        NomeCliente = nomeCliente,
                        NomeOwner = nomeOwner,
                        DataCreazione = p.DataCreazione,
                        Stato = p.Stato ?? "",
                        Budget = p.Budget,
                        TotaleCompensi = totaleCompensi
                    });
                }

                if (!lista.Any())
                {
                    System.Diagnostics.Trace.WriteLine("NESSUNA PRATICA → ritorno errore.");
                    return View("~/Views/Shared/Error.cshtml",
                        model: "Nessuna pratica trovata per i filtri selezionati.");
                }

                System.Diagnostics.Trace.WriteLine("==== START ROTATIVA ====");

                return View("~/Views/Pratiche/ReportPratichePdf.cshtml", lista);

                //{
                //    FileName = $"Pratiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                //    PageSize = Rotativa.Options.Size.A4,
                //    PageOrientation = Rotativa.Options.Orientation.Landscape
                //};
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("ERRORE GENERALE EXPORT PDF: " + ex.ToString());
                return View("~/Views/Shared/Error.cshtml", model: ex.ToString());
            }
        }




        #endregion

        #region  COSTI GENERALI 
        public ActionResult GestioneSpeseGenerali()
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return RedirectToAction("Login", "Account");

                // 🔽 Caricamento lista dei costi
                var costiList = db.TipologieCosti
                    .Where(t => t.Stato != "Eliminato" && t.TipoCostoApplicazione == "Generale")
                    .OrderBy(t => t.ID_TipoCosto)
                    .ToList()
                    .Select(t =>
                    {
                        string nomeCreatore = "Sconosciuto";

                        if (t.ID_UtenteCreatore.HasValue)
                        {
                            var creatore = db.Utenti.FirstOrDefault(u => u.ID_Utente == t.ID_UtenteCreatore.Value);
                            if (creatore != null)
                                nomeCreatore = $"{creatore.Nome} {creatore.Cognome}";
                        }

                        int numeroAssegnati = db.RicorrenzeCosti.Count(r =>
                            r.ID_TipoCostoGenerale == t.ID_TipoCosto &&
                            r.ID_Professionista != null &&
                            r.Attivo);

                        var ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r =>
                            r.ID_TipoCostoGenerale == t.ID_TipoCosto &&
                            r.Attivo == true &&
                            r.ID_Professionista == null); // ✅ solo quella generale

                        return new TipologieCostiViewModel
                        {
                            ID_TipoCosto = t.ID_TipoCosto,
                            Nome = t.Nome,
                            ValorePercentuale = t.ValorePercentuale,
                            ValoreFisso = t.ValoreFisso,
                            Tipo = t.Tipo,
                            Stato = t.Stato,
                            DataInizio = t.DataInizio,
                            DataFine = t.DataFine,
                            ID_UtenteCreatore = t.ID_UtenteCreatore,
                            ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
                            DataUltimaModifica = t.DataUltimaModifica,
                            NomeCreatore = nomeCreatore,
                            NumeroAssegnati = numeroAssegnati,

                            // 🔁 Dati ricorrenza
                            RicorrenzaAttiva = ricorrenza != null,
                            Periodicita = ricorrenza?.Periodicita,
                            TipoValore = ricorrenza?.TipoValore,
                            ValoreRicorrenza = ricorrenza?.Valore,
                            DataInizioRicorrenza = ricorrenza?.DataInizio,
                            DataFineRicorrenza = ricorrenza?.DataFine,
                            Categoria = ricorrenza?.Categoria
                        };
                    })

                    .ToList();

                // 🔐 Permessi utente
                var permessiUtente = new PermessiViewModel
                {
                    ID_Utente = utenteCorrente.ID_Utente,
                    NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
                    Permessi = new List<PermessoSingoloViewModel>()
                };

                if (utenteCorrente.TipoUtente == "Admin")
                {
                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = true,
                        Modifica = true,
                        Elimina = true
                    });
                }
                else
                {
                    var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();

                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = permessiDb.Any(p => p.Aggiungi == true),
                        Modifica = permessiDb.Any(p => p.Modifica == true),
                        Elimina = permessiDb.Any(p => p.Elimina == true)
                    });
                }

                ViewBag.Permessi = permessiUtente;

                // 🔽 Professionisti attivi
                ViewBag.Professionisti = db.OperatoriSinergia
                    .Where(p => p.TipoCliente == "Professionista" && p.Stato == "Attivo" && p.ID_UtenteCollegato != null)
                    .Select(p => new SelectListItem
                    {
                        Value = p.ID_UtenteCollegato.Value.ToString(),
                        Text = p.Nome + " " + p.Cognome
                    })
                    .OrderBy(p => p.Text)
                    .ToList();

                // ✅ Ritorna la view con model = costiList
                return View("~/Views/CostiGenerali/GestioneCostiGenerali.cshtml", costiList);
            }
            catch (Exception ex)
            {
                ViewBag.MessaggioErrore = $"❌ Errore nella vista Costi Generali: {ex.Message} - {(ex.InnerException?.Message ?? "")}";
                return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
            }
        }


        //[HttpGet]
        //public ActionResult GestioneSpeseGeneraliList()
        //{
        //    try
        //    {
        //        int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
        //        var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

        //        if (utenteCorrente == null)
        //        {
        //            ViewBag.MessaggioErrore = "Utente non autenticato o sessione scaduta.";
        //            return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
        //        }

        //        var query = db.TipologieCosti
        //            .Where(t =>
        //                t.Stato != "Eliminato" &&
        //                t.TipoCostoApplicazione == "Generale");

        //        var costiList = query
        //            .OrderBy(t => t.Nome)
        //            .ToList()
        //            .Select(t =>
        //            {
        //                string nomeCreatore = "Sconosciuto";

        //                if (t.ID_UtenteCreatore.HasValue)
        //                {
        //                    int idCreatore = t.ID_UtenteCreatore.Value;
        //                    var creatore = db.Utenti.FirstOrDefault(u => u.ID_Utente == idCreatore);
        //                    if (creatore != null)
        //                        nomeCreatore = $"{creatore.Nome} {creatore.Cognome}";
        //                }

        //                int numeroAssegnati = db.RicorrenzeCosti.Count(r =>
        //                    r.ID_TipoCostoGenerale == t.ID_TipoCosto &&
        //                    r.ID_Professionista != null &&
        //                    r.Attivo);

        //                return new TipologieCostiViewModel
        //                {
        //                    ID_TipoCosto = t.ID_TipoCosto,
        //                    Nome = t.Nome,
        //                    ValorePercentuale = t.ValorePercentuale,
        //                    ValoreFisso = t.ValoreFisso,
        //                    Tipo = t.Tipo,
        //                    Stato = t.Stato,
        //                    DataInizio = t.DataInizio,
        //                    DataFine = t.DataFine,
        //                    ID_UtenteCreatore = t.ID_UtenteCreatore,
        //                    ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
        //                    DataUltimaModifica = t.DataUltimaModifica,
        //                    NomeCreatore = nomeCreatore,
        //                    NumeroAssegnati = numeroAssegnati
        //                };
        //            })
        //            .ToList();

        //        // 🔐 Gestione permessi
        //        var permessiUtente = new PermessiViewModel
        //        {
        //            ID_Utente = utenteCorrente.ID_Utente,
        //            NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
        //            Permessi = new List<PermessoSingoloViewModel>()
        //        };

        //        if (utenteCorrente.TipoUtente == "Admin")
        //        {
        //            permessiUtente.Permessi.Add(new PermessoSingoloViewModel
        //            {
        //                Aggiungi = true,
        //                Modifica = true,
        //                Elimina = true
        //            });
        //        }
        //        else
        //        {
        //            var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();

        //            permessiUtente.Permessi.Add(new PermessoSingoloViewModel
        //            {
        //                Aggiungi = permessiDb.Any(p => p.Aggiungi == true),
        //                Modifica = permessiDb.Any(p => p.Modifica == true),
        //                Elimina = permessiDb.Any(p => p.Elimina == true)
        //            });
        //        }

        //        ViewBag.Permessi = permessiUtente;

        //        // 🔽 Lista professionisti attivi
        //        ViewBag.Professionisti = db.OperatoriSinergia
        //            .Where(p => p.TipoCliente == "Professionista" && p.Stato == "Attivo" && p.ID_UtenteCollegato != null)
        //            .Select(p => new SelectListItem
        //            {
        //                Value = p.ID_UtenteCollegato.Value.ToString(),
        //                Text = p.Nome + " " + p.Cognome
        //            })
        //            .OrderBy(p => p.Text)
        //            .ToList();

        //        return PartialView("~/Views/CostiGenerali/_GestioneCostiGeneraliList.cshtml", costiList);
        //    }
        //    catch (Exception ex)
        //    {
        //        ViewBag.MessaggioErrore = $"❌ Errore nella vista Costi Generali: {ex.Message} - {(ex.InnerException != null ? ex.InnerException.Message : "")}";
        //        return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
        //    }
        //}



        [HttpPost]
        public ActionResult CreaTipologiaCosto(TipologieCostiViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi obbligatori." });

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                               db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Aggiungi == true);

            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per creare una nuova tipologia." });

            try
            {
                var nuova = new TipologieCosti
                {
                    Nome = model.Nome?.Trim(),
                    Tipo = "Generale",
                    Stato = model.Stato,
                    DataInizio = model.DataInizio != DateTime.MinValue ? model.DataInizio : DateTime.Now,
                    DataFine = model.DataFine,
                    ID_UtenteCreatore = idUtenteCorrente,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    TipoCostoApplicazione = model.TipoCostoApplicazione,

                    // ❗ Usa solo il campo corretto in base al tipo valore
                    ValorePercentuale = model.Ricorrenza?.TipoValore == "Percentuale" ? model.Ricorrenza.Valore : null,
                    ValoreFisso = model.Ricorrenza?.TipoValore == "Fisso" ? model.Ricorrenza.Valore : null
                };


                db.TipologieCosti.Add(nuova);
                db.SaveChanges();

                // 🔁 Archivio Tipologia
                db.TipologieCosti_a.Add(new TipologieCosti_a
                {
                    ID_TipoCosto = nuova.ID_TipoCosto,
                    Nome = nuova.Nome,
                    ValorePercentuale = nuova.ValorePercentuale,
                    ValoreFisso = nuova.ValoreFisso,
                    Tipo = nuova.Tipo,
                    Stato = nuova.Stato,
                    DataInizio = nuova.DataInizio,
                    DataFine = nuova.DataFine,
                    TipoCostoApplicazione = nuova.TipoCostoApplicazione,
                    ID_UtenteCreatore = nuova.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = nuova.ID_UtenteUltimaModifica,
                    DataUltimaModifica = nuova.DataUltimaModifica,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Inserimento effettuato da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}",
                    Operazione = "Inserimento",

                });

                // ✅ Ricorrenza Costi (opzionale)
                if (model.Ricorrenza != null && !string.IsNullOrEmpty(model.Ricorrenza.Periodicita))
                {
                    var ricorrenza = new RicorrenzeCosti
                    {
                        ID_TipoCostoGenerale = nuova.ID_TipoCosto,
                        ID_Professione = model.Ricorrenza.ID_Professione,
                        ID_Professionista = model.Ricorrenza.ID_Professionista,
                        Categoria = model.Ricorrenza.Categoria,
                        Periodicita = model.Ricorrenza.Periodicita,
                        TipoValore = model.Ricorrenza.TipoValore,
                        Valore = (decimal)model.Ricorrenza.Valore,
                        DataInizio = model.Ricorrenza.DataInizio,
                        DataFine = model.Ricorrenza.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataCreazione = DateTime.Now,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = DateTime.Now
                    };

                    db.RicorrenzeCosti.Add(ricorrenza);
                    db.SaveChanges();

                    // 🗂️ Archivio Ricorrenza
                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_TipoCostoGenerale = ricorrenza.ID_TipoCostoGenerale,
                        ID_Professione = ricorrenza.ID_Professione,
                        ID_Professionista = ricorrenza.ID_Professionista,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio,
                        DataFine = ricorrenza.DataFine,
                        Attivo = ricorrenza.Attivo,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataCreazione = ricorrenza.DataCreazione,
                        ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                        DataUltimaModifica = ricorrenza.DataUltimaModifica,
                        NumeroVersione = 1,
                        ModificheTestuali = $"✅ Inserita ricorrenza per costo ID = {ricorrenza.ID_TipoCostoGenerale} da utente ID = {idUtenteCorrente} il {DateTime.Now:g}"
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Tipologia creata correttamente." });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException valEx)
            {
                var erroriValidazione = valEx.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                    .ToList();

                string messaggio = "❌ Errore di validazione dei campi:\n" + string.Join("\n", erroriValidazione);

                return Json(new
                {
                    success = false,
                    message = messaggio
                });
            }
        }

        [HttpPost]
        public ActionResult ModificaTipologiaCosto(TipologieCostiViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi obbligatori." });

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Modifica == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per modificare questa tipologia." });

            try
            {
                var esistente = db.TipologieCosti.FirstOrDefault(t => t.ID_TipoCosto == model.ID_TipoCosto);
                if (esistente == null)
                    return Json(new { success = false, message = "Tipologia non trovata." });

                int ultimaVersione = db.TipologieCosti_a
                    .Where(s => s.ID_TipoCosto == esistente.ID_TipoCosto)
                    .OrderByDescending(s => s.NumeroVersione)
                    .Select(s => s.NumeroVersione)
                    .FirstOrDefault();

                List<string> modifiche = new List<string>();
                void CheckModifica(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                // 🔄 Confronto tipologia
                CheckModifica("Nome", esistente.Nome, model.Nome?.Trim());
                CheckModifica("Tipo", esistente.Tipo, model.Tipo);
                CheckModifica("TipoCostoApplicazione", esistente.TipoCostoApplicazione, model.TipoCostoApplicazione);
                CheckModifica("Stato", esistente.Stato, model.Stato);
                CheckModifica("DataInizio", esistente.DataInizio, model.DataInizio);
                CheckModifica("DataFine", esistente.DataFine, model.DataFine);

                // 🔁 Valore corretto in base al TipoValore
                if (model.TipoValore == "Percentuale")
                {
                    CheckModifica("ValorePercentuale", esistente.ValorePercentuale, model.ValoreRicorrenza);
                    CheckModifica("ValoreFisso", esistente.ValoreFisso, null);
                    esistente.ValorePercentuale = model.ValoreRicorrenza;
                    esistente.ValoreFisso = null;
                }
                else
                {
                    CheckModifica("ValorePercentuale", esistente.ValorePercentuale, null);
                    CheckModifica("ValoreFisso", esistente.ValoreFisso, model.ValoreRicorrenza);
                    esistente.ValoreFisso = model.ValoreRicorrenza;
                    esistente.ValorePercentuale = null;
                }

                // ✍️ Aggiorna tipologia
                esistente.Nome = model.Nome?.Trim();
                esistente.Tipo = "Generale";
                esistente.TipoCostoApplicazione = model.TipoCostoApplicazione;
                esistente.Stato = model.Stato;
                esistente.DataInizio = model.DataInizio != DateTime.MinValue ? model.DataInizio : esistente.DataInizio;
                esistente.DataFine = model.DataFine;
                esistente.ID_UtenteUltimaModifica = idUtenteCorrente;
                esistente.DataUltimaModifica = DateTime.Now;

                // 🧾 Archivia tipologia
                db.TipologieCosti_a.Add(new TipologieCosti_a
                {
                    ID_TipoCosto = esistente.ID_TipoCosto,
                    Nome = esistente.Nome,
                    ValorePercentuale = esistente.ValorePercentuale,
                    ValoreFisso = esistente.ValoreFisso,
                    Tipo = esistente.Tipo,
                    TipoCostoApplicazione = esistente.TipoCostoApplicazione,
                    Stato = esistente.Stato,
                    DataInizio = esistente.DataInizio,
                    DataFine = esistente.DataFine,
                    ID_UtenteCreatore = esistente.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    Operazione = "Modifica",
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = modifiche.Count > 0
                        ? $"Modifica effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica salvata senza cambiamenti rilevanti"
                });

                // 🔁 GESTIONE RICORRENZA
                var ric = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_TipoCostoGenerale == esistente.ID_TipoCosto);
                int ricorrenzaVersione = db.RicorrenzeCosti_a
                    .Where(r => r.ID_TipoCostoGenerale == esistente.ID_TipoCosto)
                    .OrderByDescending(r => r.NumeroVersione)
                    .Select(r => (int?)r.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                List<string> modificheRic = new List<string>();
                void CheckRic(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modificheRic.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                if (!string.IsNullOrWhiteSpace(model.Periodicita) || model.ValoreRicorrenza.HasValue)
                {
                    if (ric == null)
                    {
                        ric = new RicorrenzeCosti
                        {
                            ID_TipoCostoGenerale = esistente.ID_TipoCosto,
                            ID_UtenteCreatore = idUtenteCorrente,
                            DataCreazione = DateTime.Now
                        };
                        db.RicorrenzeCosti.Add(ric);
                        modificheRic.Add("➕ Ricorrenza creata.");
                    }
                    else
                    {
                        CheckRic("Periodicita", ric.Periodicita, model.Periodicita);
                        CheckRic("TipoValore", ric.TipoValore, model.TipoValore);
                        CheckRic("Valore", ric.Valore, model.ValoreRicorrenza);
                        CheckRic("Categoria", ric.Categoria, model.Categoria);
                        CheckRic("DataInizio", ric.DataInizio, model.DataInizioRicorrenza);
                        CheckRic("DataFine", ric.DataFine, model.DataFineRicorrenza);
                        CheckRic("Attivo", ric.Attivo, model.RicorrenzaAttiva);
                    }

                    ric.Periodicita = model.Periodicita;
                    ric.TipoValore = model.TipoValore;
                    ric.Valore = (decimal)model.ValoreRicorrenza;
                    ric.Categoria = model.Categoria;
                    ric.DataInizio = model.DataInizioRicorrenza;
                    ric.DataFine = model.DataFineRicorrenza;
                    ric.Attivo = model.RicorrenzaAttiva ?? true;
                    ric.ID_UtenteUltimaModifica = idUtenteCorrente;
                    ric.DataUltimaModifica = DateTime.Now;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_TipoCostoGenerale = ric.ID_TipoCostoGenerale,
                        Periodicita = ric.Periodicita,
                        TipoValore = ric.TipoValore,
                        Valore = ric.Valore,
                        Categoria = ric.Categoria,
                        DataInizio = ric.DataInizio,
                        DataFine = ric.DataFine,
                        Attivo = ric.Attivo,
                        ID_UtenteCreatore = ric.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = ric.DataCreazione,
                        DataUltimaModifica = ric.DataUltimaModifica,
                        NumeroVersione = ricorrenzaVersione + 1,
                        ModificheTestuali = modificheRic.Any()
                            ? $"Ricorrenza modificata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}:\n{string.Join("\n", modificheRic)}"
                            : "Nessuna modifica rilevante alla ricorrenza"
                    });
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Tipologia modificata correttamente." });
            }
            catch (DbEntityValidationException dbEx)
            {
                var errorMessages = dbEx.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"- {e.PropertyName}: {e.ErrorMessage}");

                string fullError = string.Join("\n", errorMessages);
                return Json(new { success = false, message = "Errore di validazione:\n" + fullError });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetTipologieCosti(int id)
        {
            var tipologia = db.TipologieCosti
                .Where(t => t.ID_TipoCosto == id && t.Stato != "Eliminato")
                .Select(t => new TipologieCostiViewModel
                {
                    ID_TipoCosto = t.ID_TipoCosto,
                    Nome = t.Nome,
                    ValorePercentuale = t.ValorePercentuale,
                    ValoreFisso = t.ValoreFisso,
                    Tipo = t.Tipo,
                    Stato = t.Stato,
                    DataInizio = t.DataInizio,
                    DataFine = t.DataFine,
                    TipoCostoApplicazione = t.TipoCostoApplicazione,
                    ID_UtenteCreatore = t.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
                    DataUltimaModifica = t.DataUltimaModifica,
                    Ricorrenza = db.RicorrenzeCosti
                        .Where(r => r.ID_TipoCostoGenerale == t.ID_TipoCosto && r.Attivo == true)
                        .OrderByDescending(r => r.DataUltimaModifica)
                        .Select(r => new RicorrenzaCostoViewModel
                        {
                            ID_Ricorrenza = r.ID_Ricorrenza,
                            ID_AnagraficaCosto = (int)r.ID_TipoCostoGenerale,
                            ID_Professione = r.ID_Professione,
                            ID_Professionista = r.ID_Professionista,
                            Categoria = r.Categoria,
                            Periodicita = r.Periodicita,
                            TipoValore = r.TipoValore,
                            Valore = r.Valore,
                            DataInizio = r.DataInizio,
                            DataFine = r.DataFine,
                            Attivo = r.Attivo
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefault();

            if (tipologia == null)
                return Json(new { success = false, message = "Tipologia non trovata." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, tipologia }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaTipologiaCosto(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                // 🔐 Verifica permessi
                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare la tipologia." });

                // 🗑️ Recupera la tipologia
                var tipologia = db.TipologieCosti.FirstOrDefault(t => t.ID_TipoCosto == id);
                if (tipologia == null)
                    return Json(new { success = false, message = "Tipologia non trovata." });

                // 🔁 Numero versione
                int ultimaVersione = db.TipologieCosti_a
                    .Where(s => s.ID_TipoCosto == tipologia.ID_TipoCosto)
                    .OrderByDescending(s => s.NumeroVersione)
                    .Select(s => (int?)s.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 💾 Archivia tipologia eliminata
                db.TipologieCosti_a.Add(new TipologieCosti_a
                {
                    ID_TipoCosto = tipologia.ID_TipoCosto,
                    Nome = tipologia.Nome,
                    ValorePercentuale = tipologia.ValorePercentuale,
                    ValoreFisso = tipologia.ValoreFisso,
                    Tipo = tipologia.Tipo,
                    TipoCostoApplicazione = tipologia.TipoCostoApplicazione,
                    Stato = "Eliminato",
                    DataInizio = tipologia.DataInizio,
                    DataFine = tipologia.DataFine,
                    ID_UtenteCreatore = tipologia.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    Operazione = "Eliminazione",
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"Eliminazione definitiva effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                // 🔁 Elimina eventuali ricorrenze collegate
                var ricorrenze = db.RicorrenzeCosti
                    .Where(r => r.ID_TipoCostoGenerale == tipologia.ID_TipoCosto)
                    .ToList();

                foreach (var ric in ricorrenze)
                {
                    int versioneRicorrenza = db.RicorrenzeCosti_a
                        .Where(r => r.ID_Ricorrenza == ric.ID_Ricorrenza)
                        .OrderByDescending(r => r.NumeroVersione)
                        .Select(r => (int?)r.NumeroVersione)
                        .FirstOrDefault() ?? 0;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        IDVersioneRicorrenza = ric.ID_Ricorrenza,
                        ID_Ricorrenza = ric.ID_Ricorrenza,
                        ID_TipoCostoGenerale = ric.ID_TipoCostoGenerale,
                        ID_Professione = ric.ID_Professione,
                        ID_Professionista = ric.ID_Professionista,
                        Categoria = ric.Categoria,
                        Periodicita = ric.Periodicita,
                        TipoValore = ric.TipoValore,
                        Valore = ric.TipoValore == "Percentuale"
                            ? ric.Valore
                            : ric.Valore,
                        DataInizio = ric.DataInizio,
                        DataFine = ric.DataFine,
                        Attivo = false,
                        ID_UtenteCreatore = ric.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = ric.DataCreazione,
                        DataUltimaModifica = DateTime.Now,
                        NumeroVersione = versioneRicorrenza + 1,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        ModificheTestuali = $"✅ Ricorrenza eliminata insieme alla tipologia il {DateTime.Now:g}"
                    });

                    db.RicorrenzeCosti.Remove(ric);
                }

                // ❌ Eliminazione definitiva
                db.TipologieCosti.Remove(tipologia);

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Tipologia eliminata definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetRicorrenzaCostoGenerale(int idAnagraficaCosto)
        {
            try
            {
                var ricorrenza = db.RicorrenzeCosti
                    .Where(r => r.ID_TipoCostoGenerale == idAnagraficaCosto && r.Attivo == true)
                    .OrderByDescending(r => r.DataUltimaModifica)
                    .FirstOrDefault();

                // 🔍 Recupero nome del costo (serve SEMPRE)
                var nomeCosto = db.TipologieCosti
                    .Where(t => t.ID_TipoCosto == idAnagraficaCosto)
                    .Select(t => t.Nome)
                    .FirstOrDefault() ?? "-";

                // ================================================================
                // 🚨 SE NON ESISTE RICORRENZA → RESTITUISCO COMUNQUE LA CATEGORIA
                // ================================================================
                if (ricorrenza == null)
                {
                    string categoriaDefault;

                    switch (nomeCosto.Trim())
                    {
                        case "Owner Fee":
                            categoriaDefault = "Owner Fee";
                            break;

                        case "Trattenuta Sinergia 20%":
                        case "Trattenuta Sinergia":
                            categoriaDefault = "Trattenuta Sinergia";
                            break;

                        case "Costo fisso Resident":
                        case "Costo Fisso Resident":
                            categoriaDefault = "Costo Fisso Resident";
                            break;

                        default:
                            categoriaDefault = "Costo Generale";
                            break;
                    }

                    return Json(new
                    {
                        success = false,
                        categoria = categoriaDefault,
                        nomeCosto = nomeCosto
                    }, JsonRequestBehavior.AllowGet);
                }

                // ================================================================
                // 🎯 SE ESISTE UNA RICORRENZA → CALCOLO LA CATEGORIA FINALE
                // ================================================================
                string categoriaFinale = ricorrenza.Categoria;

                if (string.IsNullOrEmpty(categoriaFinale) || categoriaFinale == "Costo Generale")
                {
                    switch (nomeCosto.Trim())
                    {
                        case "Owner Fee":
                            categoriaFinale = "Owner Fee";
                            break;

                        case "Trattenuta Sinergia 20%":
                        case "Trattenuta Sinergia":
                            categoriaFinale = "Trattenuta Sinergia";
                            break;

                        case "Costo fisso Resident":
                        case "Costo Fisso Resident":
                            categoriaFinale = "Costo Fisso Resident";
                            break;

                        default:
                            categoriaFinale = "Costo Generale";
                            break;
                    }
                }

                return Json(new
                {
                    success = true,
                    ricorrenza = new
                    {
                        ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                        ID_TipoCosto = ricorrenza.ID_TipoCostoGenerale,
                        ID_Professione = ricorrenza.ID_Professione,
                        ID_Professionista = ricorrenza.ID_Professionista,
                        Categoria = categoriaFinale,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio?.ToString("yyyy-MM-dd"),
                        DataFine = ricorrenza.DataFine?.ToString("yyyy-MM-dd"),
                        Attivo = ricorrenza.Attivo,
                        NomeCosto = nomeCosto,
                        IsUnaTantum =
                        (ricorrenza.Periodicita == null &&
                         ricorrenza.DataInizio.HasValue &&
                         ricorrenza.DataFine.HasValue &&
                         ricorrenza.DataInizio.Value.Date == ricorrenza.DataFine.Value.Date)

                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public ActionResult SalvaRicorrenzaCostoGenerale(RicorrenzaCostoViewModel model)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                int idProfessionistaAttivo = UserManager.GetIDUtenteAttivo();
                DateTime now = DateTime.Now;
                RicorrenzeCosti ricorrenza;
                bool isModifica = model.ID_Ricorrenza.HasValue;

                // ====================================================
                // 🔍 DEBUG – Log iniziale
                // ====================================================
                System.Diagnostics.Trace.WriteLine("=== DEBUG SalvaRicorrenzaCostoGenerale (INIZIO) ===");
                System.Diagnostics.Trace.WriteLine($"ID_AnagraficaCosto: '{model.ID_AnagraficaCosto}'");
                System.Diagnostics.Trace.WriteLine($"Categoria (raw): '{model.Categoria}'");
                System.Diagnostics.Trace.WriteLine($"TipoValore: '{model.TipoValore}'");
                System.Diagnostics.Trace.WriteLine($"Valore: '{model.Valore}'");
                System.Diagnostics.Trace.WriteLine($"Periodicita (raw): '{model.Periodicita}'");
                System.Diagnostics.Trace.WriteLine($"DataInizio (raw): '{model.DataInizio}'");
                System.Diagnostics.Trace.WriteLine($"DataFine (raw): '{model.DataFine}'");
                System.Diagnostics.Trace.WriteLine($"ID_Professionista: '{model.ID_Professionista}'");
                System.Diagnostics.Trace.WriteLine("====================================================");

                // ====================================================
                // 🔍 Validazione base
                // ====================================================
                if (model.ID_AnagraficaCosto <= 0 || string.IsNullOrEmpty(model.Categoria) ||
                    string.IsNullOrEmpty(model.TipoValore) || model.Valore == null)
                {
                    System.Diagnostics.Trace.WriteLine("❌ Validazione base fallita");
                    return Json(new { success = false, message = "Compilare tutti i campi obbligatori." });
                }

                // ====================================================
                // ⚙️ Costi speciali
                // ====================================================
                string cat = (model.Categoria ?? "").Trim().ToLowerInvariant();

                bool èSpeciale =
                    cat == "trattenuta sinergia" ||
                    cat == "owner fee" ;

                System.Diagnostics.Trace.WriteLine($"Categoria normalizzata: '{cat}'");
                System.Diagnostics.Trace.WriteLine($"È speciale? {èSpeciale}");

                if (!èSpeciale)
                {
                    System.Diagnostics.Trace.WriteLine("🔍 Validazione costo NON speciale");

                    if (string.IsNullOrEmpty(model.Periodicita))
                        System.Diagnostics.Trace.WriteLine("❌ Periodicità mancante!");

                    if (!model.DataInizio.HasValue)
                        System.Diagnostics.Trace.WriteLine("❌ Data Inizio mancante!");

                    if (string.IsNullOrEmpty(model.Periodicita) || !model.DataInizio.HasValue)
                    {
                        return Json(new { success = false, message = "Compilare Periodicità e Data Inizio." });
                    }
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("✔ Costo speciale (reset date e periodicità)");

                    model.Periodicita = null;
                    model.DataInizio = null;
                    model.DataFine = null;
                }

                // ====================================================
                // 🧠 Una Tantum
                // ====================================================
                // ⚠️ Regola chiara: UNA TANTUM solo se Periodicità è vuota
                // e DataInizio == DataFine
                if (!èSpeciale && string.IsNullOrEmpty(model.Periodicita))
                {
                    if (model.DataInizio.HasValue && model.DataFine.HasValue &&
                        model.DataInizio.Value.Date == model.DataFine.Value.Date)
                    {
                        System.Diagnostics.Trace.WriteLine("ℹ Una Tantum attivata");
                        // Non forziamo Periodicità, resta null
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("ℹ NON è una una tantum (date diverse)");
                        // Se le date non coincidono → non è una tantum → DataFine può essere null
                    }
                }

                // ====================================================
                // 🎯 Assegna professionista se mancante
                // ====================================================
                if (model.Categoria == "Costo Generale" &&
                    (model.ID_Professionista == null || model.ID_Professionista == 0))
                {
                    System.Diagnostics.Trace.WriteLine("ℹ Recupero professionista attivo...");

                    var professionista = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_UtenteCollegato == idProfessionistaAttivo
                                          && o.TipoCliente == "Professionista");

                    if (professionista != null)
                    {
                        model.ID_Professionista = professionista.ID_Operatore;
                        model.ID_Professione = professionista.ID_Professione;

                        System.Diagnostics.Trace.WriteLine($"✔ Assegnato professionista: {model.ID_Professionista}");
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("⚠ Nessun professionista trovato!");
                    }
                }

                // ====================================================
                // ✏️ MODIFICA o NUOVO
                // ====================================================
                if (isModifica)
                {
                    System.Diagnostics.Trace.WriteLine("✏ MODIFICA ricorrenza");

                    ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_Ricorrenza == model.ID_Ricorrenza);
                    if (ricorrenza == null)
                    {
                        System.Diagnostics.Trace.WriteLine("❌ Ricorrenza non trovata");
                        return Json(new { success = false, message = "Ricorrenza non trovata." });
                    }

                    ricorrenza.ID_Professionista = model.ID_Professionista;
                    ricorrenza.ID_Professione = model.ID_Professione;
                    ricorrenza.Categoria = model.Categoria;
                    ricorrenza.TipoValore = model.TipoValore;
                    ricorrenza.Valore = (decimal)model.Valore;
                    ricorrenza.Periodicita = model.Periodicita;
                    ricorrenza.DataInizio = model.DataInizio;
                    ricorrenza.DataFine = model.DataFine;
                    ricorrenza.ID_UtenteUltimaModifica = idUtenteCorrente;
                    ricorrenza.DataUltimaModifica = now;
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("➕ CREAZIONE nuova ricorrenza");

                    ricorrenza = new RicorrenzeCosti
                    {
                        ID_TipoCostoGenerale = model.ID_AnagraficaCosto,
                        ID_Professione = model.ID_Professione,
                        ID_Professionista = model.ID_Professionista,
                        Categoria = model.Categoria,
                        TipoValore = model.TipoValore,
                        Valore = (decimal)model.Valore,
                        Periodicita = model.Periodicita,
                        DataInizio = model.DataInizio,
                        DataFine = model.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = now,
                        DataUltimaModifica = now
                    };
                    db.RicorrenzeCosti.Add(ricorrenza);
                }

                // ====================================================
                // 🔍 DEBUG PRIMA DEL SALVATAGGIO
                // ====================================================
                System.Diagnostics.Trace.WriteLine("--- PRIMA DEL SALVATAGGIO ---");
                System.Diagnostics.Trace.WriteLine($"Categoria salvata: '{ricorrenza.Categoria}'");
                System.Diagnostics.Trace.WriteLine($"Periodicità salvata: '{ricorrenza.Periodicita}'");
                System.Diagnostics.Trace.WriteLine($"DataInizio salvata: '{ricorrenza.DataInizio}'");
                System.Diagnostics.Trace.WriteLine($"DataFine salvata: '{ricorrenza.DataFine}'");
                System.Diagnostics.Trace.WriteLine("--------------------------------");

                db.SaveChanges();

                // ====================================================
                // 🗂️ Versionamento automatico
                // ====================================================
                int numeroVersione = db.RicorrenzeCosti_a
                    .Count(a => a.IDVersioneRicorrenza == ricorrenza.ID_Ricorrenza) + 1;

                db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                {
                    IDVersioneRicorrenza = ricorrenza.ID_Ricorrenza,
                    ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                    ID_TipoCostoGenerale = ricorrenza.ID_TipoCostoGenerale,
                    ID_Professione = ricorrenza.ID_Professione,
                    ID_Professionista = ricorrenza.ID_Professionista,
                    Categoria = ricorrenza.Categoria,
                    TipoValore = ricorrenza.TipoValore,
                    Valore = ricorrenza.Valore,
                    Periodicita = ricorrenza.Periodicita,
                    DataInizio = ricorrenza.DataInizio,
                    DataFine = ricorrenza.DataFine,
                    Attivo = ricorrenza.Attivo,
                    ID_UtenteCreatore = ricorrenza.ID_UtenteCreatore,
                    DataCreazione = ricorrenza.DataCreazione,
                    ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                    DataUltimaModifica = ricorrenza.DataUltimaModifica,
                    NumeroVersione = numeroVersione,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"💾 {(isModifica ? "Modificata" : "Creata")} ricorrenza (TipoCosto #{ricorrenza.ID_TipoCostoGenerale}) da utente {idUtenteCorrente}"
                });

                db.SaveChanges();

                System.Diagnostics.Trace.WriteLine("✔ SALVATAGGIO COMPLETATO");
                System.Diagnostics.Trace.WriteLine("=== DEBUG SalvaRicorrenzaCostoGenerale (FINE) ===");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ ERRORE: " + ex);
                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante il salvataggio: " + ex.Message
                });
            }
        }



        [HttpPost]
        public ActionResult AssegnaRicorrenzaCostoGenerale(int ID_TipoCostoGenerale, List<int> ID_UtentiSelezionati)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato(); // o GetIDUtenteAttivo()

            if (idUtenteCorrente <= 0)
            {
                return Json(new { success = false, message = "Utente non autenticato o sessione scaduta." });
            }

            if (ID_UtentiSelezionati == null || !ID_UtentiSelezionati.Any())
            {
                return Json(new { success = false, message = "Nessun professionista selezionato." });
            }

            try
            {
                DateTime now = DateTime.Now;

                var ricorrenzaBase = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.ID_TipoCostoGenerale == ID_TipoCostoGenerale && r.ID_Professionista == null);

                if (ricorrenzaBase == null)
                    return Json(new { success = false, message = "Ricorrenza base non trovata." });

                int countNuove = 0;

                foreach (int idProfessionista in ID_UtentiSelezionati)
                {
                    bool giàEsiste = db.RicorrenzeCosti.Any(r =>
                        r.ID_TipoCostoGenerale == ID_TipoCostoGenerale &&
                        r.Categoria == ricorrenzaBase.Categoria &&
                        r.ID_Professionista == idProfessionista &&
                        r.Attivo);

                    if (giàEsiste)
                        continue;

                    // 🔁 Nuova ricorrenza
                    var nuovaRicorrenza = new RicorrenzeCosti
                    {
                        ID_TipoCostoGenerale = ricorrenzaBase.ID_TipoCostoGenerale,
                        Categoria = ricorrenzaBase.Categoria,
                        ID_Professione = ricorrenzaBase.ID_Professione,
                        ID_Professionista = idProfessionista,
                        Periodicita = ricorrenzaBase.Periodicita,
                        TipoValore = ricorrenzaBase.TipoValore,
                        Valore = ricorrenzaBase.Valore,
                        DataInizio = ricorrenzaBase.DataInizio,
                        DataFine = ricorrenzaBase.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = now,
                        DataUltimaModifica = now
                    };
                    db.RicorrenzeCosti.Add(nuovaRicorrenza);
                    db.SaveChanges();

                    // 🔁 Versionamento Ricorrenza
                    int numeroVersioneRic = db.RicorrenzeCosti_a
                        .Count(r => r.IDVersioneRicorrenza == nuovaRicorrenza.ID_Ricorrenza) + 1;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        IDVersioneRicorrenza = nuovaRicorrenza.ID_Ricorrenza,
                        ID_Ricorrenza = nuovaRicorrenza.ID_Ricorrenza,
                        ID_TipoCostoGenerale = nuovaRicorrenza.ID_TipoCostoGenerale,
                        Categoria = nuovaRicorrenza.Categoria,
                        ID_Professione = nuovaRicorrenza.ID_Professione,
                        ID_Professionista = nuovaRicorrenza.ID_Professionista,
                        Periodicita = nuovaRicorrenza.Periodicita,
                        TipoValore = nuovaRicorrenza.TipoValore,
                        Valore = nuovaRicorrenza.Valore,
                        DataInizio = nuovaRicorrenza.DataInizio,
                        DataFine = nuovaRicorrenza.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = now,
                        DataUltimaModifica = now,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        NumeroVersione = numeroVersioneRic,
                        ModificheTestuali = $"🏷️ Ricorrenza duplicata e assegnata a professionista {idProfessionista} da utente {idUtenteCorrente}"
                    });

                    // ✅ Salva anche su CostiGeneraliUtente
                    var costoGenerale = new CostiGeneraliUtente
                    {
                        ID_Utente = idProfessionista,
                        Descrizione = ricorrenzaBase.Categoria,
                        Importo = ricorrenzaBase.Valore,
                        DataInserimento = now.Date,
                        Approvato = false,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = now,
                        ID_TipoCosto = ricorrenzaBase.ID_TipoCostoGenerale
                    };
                    db.CostiGeneraliUtente.Add(costoGenerale);
                    db.SaveChanges();

                    // 🔁 Versionamento CostiGeneraliUtente
                    int numeroVersioneCosto = db.CostiGeneraliUtente_a
                        .Count(v => v.ID_CostoGenerale == costoGenerale.ID_CostoGenerale) + 1;

                    db.CostiGeneraliUtente_a.Add(new CostiGeneraliUtente_a
                    {
                        ID_CostoGenerale = costoGenerale.ID_CostoGenerale,
                        ID_Utente = costoGenerale.ID_Utente,
                        Descrizione = costoGenerale.Descrizione,
                        Importo = costoGenerale.Importo,
                        DataInserimento = costoGenerale.DataInserimento,
                        Approvato = costoGenerale.Approvato,
                        ID_UtenteCreatore = (int)costoGenerale.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = costoGenerale.ID_UtenteUltimaModifica,
                        DataUltimaModifica = costoGenerale.DataUltimaModifica,
                        NumeroVersione = numeroVersioneCosto,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        ModificheTestuali = $"📝 Assegnato costo generale a professionista {idProfessionista} da utente {idUtenteCorrente}",
                        ID_TipoCosto = costoGenerale.ID_TipoCosto
                    });

                    db.SaveChanges();
                    countNuove++;
                }

                return Json(new
                {
                    success = true,
                    message = $"✅ Assegnazione completata ({countNuove} nuove ricorrenze e costi utente)."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }



        [HttpGet]
        public ActionResult VisualizzaAssegnazioniRicorrenza(int idTipoCosto)
        {
            try
            {
                var ricorrenzaBase = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.ID_TipoCostoGenerale == idTipoCosto && r.ID_Professionista == null);

                if (ricorrenzaBase == null)
                    return Json(new { success = false, message = "Ricorrenza base non trovata." }, JsonRequestBehavior.AllowGet);

                string categoria = ricorrenzaBase.Categoria;

                var assegnazioni = (from r in db.RicorrenzeCosti
                                    join u in db.Utenti on r.ID_Professionista equals u.ID_Utente
                                    where r.ID_TipoCostoGenerale == idTipoCosto
                                       && r.Categoria == categoria
                                       && r.ID_Professionista != null
                                       && r.Attivo
                                    select new
                                    {
                                        ID_Ricorrenza = r.ID_Ricorrenza,
                                        ID_Utente = r.ID_Professionista,
                                        NomeProfessionista = u.Nome + " " + u.Cognome,
                                        DataAssegnazione = r.DataCreazione,
                                        Importo = r.TipoValore == "Percentuale"
                                                  ? r.Valore + "%"
                                                  : r.Valore + " €"
                                    }).ToList()
                                    .Select(r => new
                                    {
                                        r.ID_Ricorrenza,
                                        r.ID_Utente,
                                        r.NomeProfessionista,
                                        DataAssegnazione = r.DataAssegnazione?.ToString("dd/MM/yyyy") ?? "",
                                        r.Importo
                                    })
                                    .ToList();

                return Json(new { success = true, assegnazioni }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public ActionResult GetListaProfessionistiAssegnabiliRicorrenza(int idCosto)
        {
            try
            {
                // Prendo la prima ricorrenza attiva con quel tipo costo (anche se già assegnata)
                var ricorrenzaBase = db.RicorrenzeCosti
                    .Where(r => r.ID_TipoCostoGenerale == idCosto && r.Attivo)
                    .OrderBy(r => r.DataCreazione)
                    .FirstOrDefault();

                if (ricorrenzaBase == null)
                    return Json(new { success = false, message = "Ricorrenza base non trovata." }, JsonRequestBehavior.AllowGet);

                string categoria = ricorrenzaBase.Categoria;

                // Prendo tutti gli ID professionisti a cui è già assegnata
                var idAssegnati = db.RicorrenzeCosti
                    .Where(r => r.ID_TipoCostoGenerale == idCosto &&
                                r.Categoria == categoria &&
                                r.ID_Professionista != null &&
                                r.Attivo)
                    .Select(r => r.ID_Professionista)
                    .Distinct()
                    .ToList();

                // Prendo i professionisti attivi NON già assegnati
                var disponibili = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista" && o.Stato == "Attivo")
                    .Where(p => !idAssegnati.Contains(p.ID_UtenteCollegato))
                    .Select(p => new
                    {
                        ID = p.ID_UtenteCollegato,
                        Nome = p.Nome + " " + p.Cognome
                    })
                    .OrderBy(p => p.Nome)
                    .ToList();

                return Json(new { success = true, professionisti = disponibili }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult EliminaAssegnazioneCostoGenerale(int idRicorrenza)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteAttivo();
                DateTime now = DateTime.Now;

                // ✅ Recupero ricorrenza assegnata (non la base)
                var ricorrenza = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.ID_Ricorrenza == idRicorrenza &&
                                         r.ID_Professionista != null &&
                                         r.Categoria == "Costo Generale");

                if (ricorrenza == null)
                    return Json(new { success = false, message = "❌ Ricorrenza assegnata non trovata." });

                int idUtente = ricorrenza.ID_Professionista ?? 0;
                int? idTipoCosto = ricorrenza.ID_TipoCostoGenerale;

                // ✅ Rimuovi anche dalla tabella CostiGeneraliUtente
                var costoUtente = db.CostiGeneraliUtente
                    .FirstOrDefault(c => c.ID_Utente == idUtente && c.ID_TipoCosto == idTipoCosto);

                if (costoUtente != null)
                {
                    int numeroVersione = db.CostiGeneraliUtente_a
                        .Count(a => a.ID_CostoGenerale == costoUtente.ID_CostoGenerale) + 1;

                    db.CostiGeneraliUtente_a.Add(new CostiGeneraliUtente_a
                    {
                        IDVersioneCostoGenerale = costoUtente.ID_CostoGenerale,
                        ID_CostoGenerale = costoUtente.ID_CostoGenerale,
                        ID_TipoCosto = costoUtente.ID_TipoCosto,
                        ID_Utente = costoUtente.ID_Utente,
                        Importo = costoUtente.Importo,
                        DataInserimento = costoUtente.DataInserimento,
                        NumeroVersione = numeroVersione,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        ModificheTestuali = $"❌ Assegnazione eliminata (costo utente) da utente {idUtenteCorrente} il {now:g}"
                    });

                    db.CostiGeneraliUtente.Remove(costoUtente);
                }

                // 🗂️ Archivia la ricorrenza assegnata
                db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                {
                    ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                    Categoria = ricorrenza.Categoria,
                    ID_TipoCostoGenerale = ricorrenza.ID_TipoCostoGenerale,
                    ID_Professionista = ricorrenza.ID_Professionista,
                    Valore = ricorrenza.Valore,
                    TipoValore = ricorrenza.TipoValore,
                    Periodicita = ricorrenza.Periodicita,
                    DataInizio = ricorrenza.DataInizio,
                    DataFine = ricorrenza.DataFine,
                    Attivo = ricorrenza.Attivo,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"❌ Ricorrenza assegnata eliminata da utente {idUtenteCorrente} il {now:g}"
                });

                db.RicorrenzeCosti.Remove(ricorrenza);

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Assegnazione eliminata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l’eliminazione: " + ex.Message });
            }
        }

        #endregion

        #region Costi Professionista

        public ActionResult GestioneSpeseProfessionista()
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return RedirectToAction("Login", "Account");

                // 🔽 Caricamento lista dei costi del professionista
                var costiList = db.AnagraficaCostiProfessionista
                    //.Where(t => t.Attivo) // <-- opzionale se vuoi mostrare solo i costi attivi
                    .OrderBy(t => t.ID_AnagraficaCostoProfessionista)
                    .ToList()
                    .Select(t =>
                    {
                        string nomeCreatore = "Sconosciuto";

                        if (t.ID_UtenteCreatore.HasValue)
                        {
                            var creatore = db.Utenti.FirstOrDefault(u => u.ID_Utente == t.ID_UtenteCreatore.Value);
                            if (creatore != null)
                                nomeCreatore = $"{creatore.Nome} {creatore.Cognome}";
                        }

                        var ricorrenzeAttive = db.RicorrenzeCosti
                          .Where(r => r.ID_CostoProfessionista == t.ID_AnagraficaCostoProfessionista && r.Attivo)
                          .ToList();

                        int numeroAssegnati = ricorrenzeAttive.Count(r => r.ID_Professionista != null);

                        // 🔄 Verifica se c’è almeno una ricorrenza attiva
                        bool haRicorrenzaAttiva = ricorrenzeAttive.Any();


                        return new CostoProfessionistaCompletoViewModel
                        {
                            ID_AnagraficaCostoProfessionista = t.ID_AnagraficaCostoProfessionista,
                            Descrizione = t.Descrizione,
                            ModalitaRipartizione = t.ModalitaRipartizione,
                            TipoPeriodicita = t.TipoPeriodicita,
                            ImportoBase = t.ImportoBase,
                            Attivo = (bool)t.Attivo,
                            ID_UtenteCreatore = t.ID_UtenteCreatore,
                            ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
                            DataUltimaModifica = t.DataUltimaModifica,
                            NomeCreatore = nomeCreatore,
                            NumeroAssegnati = numeroAssegnati,
                            RicorrenzaAttiva = haRicorrenzaAttiva
                        };
                    })
                    .ToList();

                // 🔐 Permessi utente
                var permessiUtente = new PermessiViewModel
                {
                    ID_Utente = utenteCorrente.ID_Utente,
                    NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
                    Permessi = new List<PermessoSingoloViewModel>()
                };

                if (utenteCorrente.TipoUtente == "Admin")
                {
                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = true,
                        Modifica = true,
                        Elimina = true
                    });
                }
                else
                {
                    var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();

                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = permessiDb.Any(p => p.Aggiungi == true),
                        Modifica = permessiDb.Any(p => p.Modifica == true),
                        Elimina = permessiDb.Any(p => p.Elimina == true)
                    });
                }

                ViewBag.Permessi = permessiUtente;

                // 🔽 Professionisti attivi
                ViewBag.Professionisti = db.OperatoriSinergia
                    .Where(p => p.TipoCliente == "Professionista" && p.Stato == "Attivo" && p.ID_UtenteCollegato != null)
                    .Select(p => new SelectListItem
                    {
                        Value = p.ID_UtenteCollegato.Value.ToString(),
                        Text = p.Nome + " " + p.Cognome
                    })
                    .OrderBy(p => p.Text)
                    .ToList();

                // ✅ Ritorna la view con model = costiList
                return View("~/Views/CostiProfessionista/GestioneCostiProfessionista.cshtml", costiList);
            }
            catch (Exception ex)
            {
                ViewBag.MessaggioErrore = $"❌ Errore nella vista Costi Professionista: {ex.Message} - {(ex.InnerException?.Message ?? "")}";
                return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
            }
        }




        //public ActionResult GestioneSpeseProfessionistaList()
        //{
        //    int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
        //    var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

        //    if (utenteCorrente == null)
        //    {
        //        ViewBag.MessaggioErrore = "Utente non autenticato o sessione scaduta.";
        //        return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
        //    }

        //    IQueryable<AnagraficaCostiProfessionista> query = db.AnagraficaCostiProfessionista
        //        .Where(t => t.Attivo == true);

        //    // 🔐 Gestione Permessi
        //    bool puoAggiungere = false;
        //    bool puoModificare = false;
        //    bool puoEliminare = false;

        //    if (utenteCorrente.TipoUtente == "Admin")
        //    {
        //        puoAggiungere = puoModificare = puoEliminare = true;
        //    }
        //    else if (utenteCorrente.TipoUtente == "Professionista" || utenteCorrente.TipoUtente == "Collaboratore")
        //    {
        //        var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
        //        puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
        //        puoModificare = permessiDb.Any(p => p.Modifica == true);
        //        puoEliminare = permessiDb.Any(p => p.Elimina == true);
        //    }

        //    var lista = query
        //        .OrderBy(t => t.Descrizione)
        //        .ToList()
        //       .Select(t => new CostoProfessionistaCompletoViewModel
        //       {
        //           ID_AnagraficaCostoProfessionista = t.ID_AnagraficaCostoProfessionista,
        //           Descrizione = t.Descrizione,
        //           ModalitaRipartizione = t.ModalitaRipartizione,
        //           TipoPeriodicita = t.TipoPeriodicita,
        //           ImportoBase = t.ImportoBase,
        //           Attivo = (bool)t.Attivo,
        //           ID_UtenteCreatore = t.ID_UtenteCreatore,
        //           ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
        //           DataUltimaModifica = t.DataUltimaModifica,
        //           NomeCreatore = t.ID_UtenteCreatore != null ? db.Utenti.Where(u => u.ID_Utente == t.ID_UtenteCreatore)
        //          .Select(u => u.Nome + " " + u.Cognome).FirstOrDefault() : null,

        //           // 👇 Aggiunto
        //           NumeroAssegnati = db.CostiPersonaliUtente.Count(c => c.ID_AnagraficaCostoProfessionista == t.ID_AnagraficaCostoProfessionista)
        //       })
        //        .ToList();

        //    ViewBag.PuoAggiungere = puoAggiungere;
        //    ViewBag.Permessi = new PermessiViewModel
        //    {
        //        ID_Utente = utenteCorrente.ID_Utente,
        //        NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
        //        Permessi = new List<PermessoSingoloViewModel>
        //{
        //    new PermessoSingoloViewModel
        //    {
        //        Aggiungi = puoAggiungere,
        //        Modifica = puoModificare,
        //        Elimina = puoEliminare
        //    }
        //}
        //    };

        //    // ✅ ViewBag per ricorrenze
        //    ViewBag.IDProfessionistaCorrente = utenteCorrente.ID_Utente;

        //    var professione = db.OperatoriSinergia
        //        .FirstOrDefault(o => o.ID_UtenteCollegato == utenteCorrente.ID_Utente && o.TipoCliente == "Professionista");

        //    ViewBag.IDProfessioneCorrente = professione?.ID_Professione ?? 0;

        //    // ✅ ViewBag per dropdown professionisti (modale assegnazione)
        //    var professionistiAttivi = db.OperatoriSinergia
        //        .Where(o => o.TipoCliente == "Professionista" && o.Stato == "Attivo")
        //        .OrderBy(o => o.Nome)
        //        .ToList();

        //    ViewBag.Professionisti = professionistiAttivi.Select(o => new SelectListItem
        //    {
        //        Value = o.ID_UtenteCollegato.ToString(),
        //        Text = o.Nome + " " + o.Cognome
        //    }).ToList();

        //    return PartialView("~/Views/CostiProfessionista/_GestioneCostiProfessionistaList.cshtml", lista);
        //}

        [HttpPost]
        public ActionResult CreaAnagraficaCostoProfessionista(CostoProfessionistaCompletoViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare tutti i campi obbligatori." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var nuovo = new AnagraficaCostiProfessionista
                {
                    Descrizione = model.Descrizione?.Trim(),
                    ModalitaRipartizione = model.ModalitaRipartizione,
                    TipoPeriodicita = model.TipoPeriodicita,
                    ImportoBase = model.ImportoBase,
                    Attivo = true,
                    ID_UtenteCreatore = idUtente,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now
                };

                db.AnagraficaCostiProfessionista.Add(nuovo);
                db.SaveChanges();

                // 🔁 Archivio
                db.AnagraficaCostiProfessionista_a.Add(new AnagraficaCostiProfessionista_a
                {
                    ID_AnagraficaCostoProfessionista = nuovo.ID_AnagraficaCostoProfessionista,
                    Descrizione = nuovo.Descrizione,
                    ModalitaRipartizione = nuovo.ModalitaRipartizione,
                    TipoPeriodicita = nuovo.TipoPeriodicita,
                    ImportoBase = nuovo.ImportoBase,
                    Attivo = nuovo.Attivo,
                    ID_UtenteCreatore = idUtente,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = 1,
                    Operazione = "Inserimento",
                    ModificheTestuali = $"✅ Inserito da utente {idUtente} il {DateTime.Now:g}"
                });

                // ✅ Eventuale ricorrenza
                if (model.TipoPeriodicita != null && model.TipoPeriodicita != "Una Tantum")
                {
                    var ricorrenza = new RicorrenzeCosti
                    {
                        ID_CostoProfessionista = nuovo.ID_AnagraficaCostoProfessionista,
                        Categoria = "Costo Professionista",
                        Periodicita = model.TipoPeriodicita,
                        TipoValore = model.ModalitaRipartizione,
                        Valore = model.ModalitaRipartizione == "Percentuale" ? (model.ImportoBase ?? 0) / 100m  : (model.ImportoBase ?? 0),
                        DataInizio = DateTime.Today,
                        DataFine = null, // opzionale
                        Attivo = true,
                        ID_UtenteCreatore = idUtente,
                        DataCreazione = DateTime.Now,
                        ID_UtenteUltimaModifica = idUtente,
                        DataUltimaModifica = DateTime.Now
                    };

                    db.RicorrenzeCosti.Add(ricorrenza);
                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_CostoProfessionista = ricorrenza.ID_CostoProfessionista,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio,
                        DataFine = ricorrenza.DataFine,
                        Attivo = ricorrenza.Attivo,
                        ID_UtenteCreatore = idUtente,
                        DataCreazione = ricorrenza.DataCreazione,
                        ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                        DataUltimaModifica = ricorrenza.DataUltimaModifica,
                        NumeroVersione = 1,
                        ModificheTestuali = $"✅ Inserita ricorrenza automatica per costo professionista ID = {ricorrenza.ID_CostoProfessionista}"
                    });
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo creato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ModificaAnagraficaCostoProfessionista(CostoProfessionistaCompletoViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var esistente = db.AnagraficaCostiProfessionista
                    .FirstOrDefault(c => c.ID_AnagraficaCostoProfessionista == model.ID_AnagraficaCostoProfessionista);
                if (esistente == null)
                    return Json(new { success = false, message = "Costo non trovato." });

                int ultimaVersione = db.AnagraficaCostiProfessionista_a
                    .Where(a => a.ID_AnagraficaCostoProfessionista == esistente.ID_AnagraficaCostoProfessionista)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                List<string> modifiche = new List<string>();
                void Check(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                Check("Descrizione", esistente.Descrizione, model.Descrizione);
                Check("Modalità", esistente.ModalitaRipartizione, model.ModalitaRipartizione);
                Check("Periodicità", esistente.TipoPeriodicita, model.TipoPeriodicita);
                Check("Importo Base", esistente.ImportoBase, model.ImportoBase);

                esistente.Descrizione = model.Descrizione?.Trim();
                esistente.ModalitaRipartizione = model.ModalitaRipartizione;
                esistente.TipoPeriodicita = model.TipoPeriodicita;
                esistente.ImportoBase = model.ImportoBase;
                esistente.ID_UtenteUltimaModifica = idUtente;
                esistente.DataUltimaModifica = DateTime.Now;

                db.AnagraficaCostiProfessionista_a.Add(new AnagraficaCostiProfessionista_a
                {
                    ID_AnagraficaCostoProfessionista = esistente.ID_AnagraficaCostoProfessionista,
                    Descrizione = esistente.Descrizione,
                    ModalitaRipartizione = esistente.ModalitaRipartizione,
                    TipoPeriodicita = esistente.TipoPeriodicita,
                    ImportoBase = esistente.ImportoBase,
                    Attivo = esistente.Attivo,
                    ID_UtenteCreatore = esistente.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = esistente.DataUltimaModifica,
                    NumeroVersione = ultimaVersione + 1,
                    Operazione = "Modifica",
                    ModificheTestuali = modifiche.Any()
                        ? $"Modifica effettuata da ID_Utente = {idUtente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica senza variazioni rilevanti"
                });

                // 🔁 Gestione Ricorrenza
                var ric = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.ID_CostoProfessionista == esistente.ID_AnagraficaCostoProfessionista &&
                                         r.Categoria == "Costo Professionista");

                int ricVer = db.RicorrenzeCosti_a
                    .Where(r => r.ID_CostoProfessionista == esistente.ID_AnagraficaCostoProfessionista &&
                                r.Categoria == "Costo Professionista")
                    .OrderByDescending(r => r.NumeroVersione)
                    .Select(r => (int?)r.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                List<string> modificheRic = new List<string>();
                void CheckRic(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modificheRic.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                if (!string.IsNullOrWhiteSpace(model.TipoPeriodicita) && model.TipoPeriodicita != "Una Tantum")
                {
                    var valoreDaSalvare = model.ModalitaRipartizione == "Percentuale"
                        ? (model.ImportoBase ?? 0) / 100m
                        : (model.ImportoBase ?? 0);

                    if (ric == null)
                    {
                        ric = new RicorrenzeCosti
                        {
                            ID_CostoProfessionista = esistente.ID_AnagraficaCostoProfessionista,
                            Categoria = "Costo Professionista",
                            ID_UtenteCreatore = idUtente,
                            DataCreazione = DateTime.Now
                        };
                        db.RicorrenzeCosti.Add(ric);
                        modificheRic.Add("➕ Ricorrenza creata.");
                    }
                    else
                    {
                        CheckRic("Periodicita", ric.Periodicita, model.TipoPeriodicita);
                        CheckRic("TipoValore", ric.TipoValore, model.ModalitaRipartizione);
                        CheckRic("Valore", ric.Valore, valoreDaSalvare);
                    }

                    ric.Periodicita = model.TipoPeriodicita;
                    ric.TipoValore = model.ModalitaRipartizione;
                    ric.Valore = valoreDaSalvare;
                    ric.DataInizio = DateTime.Today;
                    ric.DataFine = null;
                    ric.Attivo = true;
                    ric.ID_UtenteUltimaModifica = idUtente;
                    ric.DataUltimaModifica = DateTime.Now;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_CostoProfessionista = ric.ID_CostoProfessionista,
                        Categoria = ric.Categoria,
                        Periodicita = ric.Periodicita,
                        TipoValore = ric.TipoValore,
                        Valore = ric.Valore,
                        DataInizio = ric.DataInizio,
                        DataFine = ric.DataFine,
                        Attivo = ric.Attivo,
                        ID_UtenteCreatore = ric.ID_UtenteCreatore,
                        DataCreazione = ric.DataCreazione,
                        ID_UtenteUltimaModifica = ric.ID_UtenteUltimaModifica,
                        DataUltimaModifica = ric.DataUltimaModifica,
                        NumeroVersione = ricVer + 1,
                        ModificheTestuali = modificheRic.Any()
                            ? $"Modifiche ricorrenza da utente {idUtente} il {DateTime.Now:g}:\n{string.Join("\n", modificheRic)}"
                            : "Nessuna modifica rilevante alla ricorrenza"
                    });
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo modificato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante la modifica: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetCostoProfessionista(int id)
        {
            var costo = db.AnagraficaCostiProfessionista
                .Where(c => c.ID_AnagraficaCostoProfessionista == id && c.Attivo == true)
                .Select(c => new CostoProfessionistaCompletoViewModel
                {
                    ID_AnagraficaCostoProfessionista = c.ID_AnagraficaCostoProfessionista,
                    Descrizione = c.Descrizione,
                    ModalitaRipartizione = c.ModalitaRipartizione,
                    TipoPeriodicita = c.TipoPeriodicita,
                    ImportoBase = c.ImportoBase,
                    Attivo = (bool)c.Attivo,

                    // Utenti a cui è stato assegnato questo costo
                    CostiAssegnati = db.CostiPersonaliUtente
                        .Where(p => p.ID_AnagraficaCostoProfessionista == c.ID_AnagraficaCostoProfessionista)
                        .Select(p => new CostiPersonaliUtenteViewModel
                        {
                            ID_CostoPersonale = p.ID_CostoPersonale,
                            ID_Utente = p.ID_Utente,
                            Importo = (decimal)p.Importo,
                            DataInserimento = (DateTime)p.DataInserimento,
                            NomeProfessionista = db.Utenti
                                .Where(u => u.ID_Utente == p.ID_Utente)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                        })
                        .ToList()
                })
                .FirstOrDefault();

            if (costo == null)
                return Json(new { success = false, message = "Costo non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, costo }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EliminaCostoProfessionista(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare il costo." });

                var costo = db.AnagraficaCostiProfessionista.FirstOrDefault(c => c.ID_AnagraficaCostoProfessionista == id);
                if (costo == null)
                    return Json(new { success = false, message = "Costo non trovato." });

                int ultimaVersione = db.AnagraficaCostiProfessionista_a
                    .Where(a => a.ID_AnagraficaCostoProfessionista == id)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                db.AnagraficaCostiProfessionista_a.Add(new AnagraficaCostiProfessionista_a
                {
                    ID_AnagraficaCostoProfessionista = costo.ID_AnagraficaCostoProfessionista,
                    Descrizione = costo.Descrizione,
                    ID_UtenteCreatore = costo.ID_UtenteCreatore,
                    DataCreazione = costo.DataCreazione,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"Eliminazione definitiva effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}",
                    Operazione = "Eliminazione"
                });

                db.AnagraficaCostiProfessionista.Remove(costo);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Costo eliminato definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetRicorrenzaCostoProfessionista(int idAnagraficaCostoProfessionista)
        {
            try
            {
                var ricorrenza = db.RicorrenzeCosti
                    .Where(r =>
                        r.ID_CostoProfessionista == idAnagraficaCostoProfessionista &&
                        r.Categoria == "Costo Professionista" &&
                        r.Attivo == true)
                    .OrderByDescending(r => r.DataUltimaModifica)
                    .FirstOrDefault();

                if (ricorrenza == null)
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    ricorrenza = new
                    {
                        ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                        ID_AnagraficaCosto = ricorrenza.ID_CostoProfessionista,
                        ID_Professione = ricorrenza.ID_Professione,
                        ID_Professionista = ricorrenza.ID_Professionista,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio?.ToString("yyyy-MM-dd"),
                        DataFine = ricorrenza.DataFine?.ToString("yyyy-MM-dd"),
                        Attivo = ricorrenza.Attivo
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public ActionResult SalvaRicorrenzaCostoProfessionista(RicorrenzaCostoViewModel model)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;
                RicorrenzeCosti ricorrenza;
                bool isModifica = model.ID_Ricorrenza.HasValue;

                // 🔍 Validazione obbligatoria
                if (model.ID_AnagraficaCosto <= 0 || string.IsNullOrEmpty(model.Categoria) ||
                    string.IsNullOrEmpty(model.TipoValore) || model.Valore == null)
                {
                    return Json(new { success = false, message = "Compilare tutti i campi obbligatori." });
                }

                // ❗Categorie speciali che non richiedono periodicità/date
                bool èSpeciale = model.Categoria == "Trattenuta Sinergia" || model.Categoria == "Owner Fee";
                if (!èSpeciale)
                {
                    if (string.IsNullOrEmpty(model.Periodicita) || !model.DataInizio.HasValue)
                    {
                        return Json(new { success = false, message = "Compilare Periodicità e Data Inizio." });
                    }
                }
                else
                {
                    model.Periodicita = null;
                    model.DataInizio = null;
                    model.DataFine = null;
                }

                // ℹ️ Una Tantum → Periodicità automatica se necessario
                if (model.DataInizio.HasValue && model.DataFine.HasValue && model.DataInizio == model.DataFine)
                {
                    if (string.IsNullOrEmpty(model.Periodicita))
                        model.Periodicita = "Giornaliero";
                }

                if (isModifica)
                {
                    ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_Ricorrenza == model.ID_Ricorrenza);
                    if (ricorrenza == null)
                        return Json(new { success = false, message = "Ricorrenza non trovata." });

                    ricorrenza.ID_Professionista = model.ID_Professionista;
                    ricorrenza.ID_Professione = model.ID_Professione;
                    ricorrenza.Categoria = model.Categoria;
                    ricorrenza.Periodicita = model.Periodicita;
                    ricorrenza.TipoValore = model.TipoValore;
                    ricorrenza.Valore = (decimal)model.Valore;
                    ricorrenza.DataInizio = model.DataInizio;
                    ricorrenza.DataFine = model.DataFine;
                    ricorrenza.ID_UtenteUltimaModifica = idUtenteCorrente;
                    ricorrenza.DataUltimaModifica = now;
                }
                else
                {
                    ricorrenza = new RicorrenzeCosti
                    {
                        ID_CostoProfessionista = model.ID_AnagraficaCosto,
                        Categoria = model.Categoria,
                        Periodicita = model.Periodicita,
                        TipoValore = model.TipoValore,
                        Valore = (decimal)model.Valore,
                        DataInizio = model.DataInizio,
                        DataFine = model.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataCreazione = now,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = now
                    };
                    db.RicorrenzeCosti.Add(ricorrenza);
                }

                db.SaveChanges();

                // 🗃️ Versionamento
                int numeroVersione = db.RicorrenzeCosti_a
                    .Count(a => a.IDVersioneRicorrenza == ricorrenza.ID_Ricorrenza) + 1;

                db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                {
                    IDVersioneRicorrenza = ricorrenza.ID_Ricorrenza,
                    ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                    ID_CostoProfessionista = ricorrenza.ID_CostoProfessionista,
                    ID_Professione = ricorrenza.ID_Professione,
                    ID_Professionista = ricorrenza.ID_Professionista,
                    Categoria = ricorrenza.Categoria,
                    Periodicita = ricorrenza.Periodicita,
                    TipoValore = ricorrenza.TipoValore,
                    Valore = ricorrenza.Valore,
                    DataInizio = ricorrenza.DataInizio,
                    DataFine = ricorrenza.DataFine,
                    Attivo = ricorrenza.Attivo,
                    ID_UtenteCreatore = ricorrenza.ID_UtenteCreatore,
                    DataCreazione = ricorrenza.DataCreazione,
                    ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                    DataUltimaModifica = ricorrenza.DataUltimaModifica,
                    NumeroVersione = numeroVersione,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"💾 {(isModifica ? "Modificata" : "Creata")} ricorrenza (CostoProfessionista #{ricorrenza.ID_CostoProfessionista}) da utente {idUtenteCorrente}"
                });

                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante il salvataggio: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult AssegnaCostoProfessionista(int ID_AnagraficaCostoProfessionista, List<int> ID_UtentiSelezionati)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();

            if (idUtenteCorrente <= 0)
                return Json(new { success = false, message = "Utente non autenticato o sessione scaduta." });

            if (ID_UtentiSelezionati == null || !ID_UtentiSelezionati.Any())
                return Json(new { success = false, message = "Nessun professionista selezionato." });

            try
            {
                DateTime now = DateTime.Now;

                var ricorrenzaBase = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.ID_CostoProfessionista == ID_AnagraficaCostoProfessionista && r.ID_Professionista == null);

                if (ricorrenzaBase == null)
                    return Json(new { success = false, message = "Ricorrenza base non trovata." });

                int countNuove = 0;

                foreach (int idProfessionista in ID_UtentiSelezionati)
                {
                    bool giàEsiste = db.RicorrenzeCosti.Any(r =>
                        r.ID_CostoProfessionista == ID_AnagraficaCostoProfessionista &&
                        r.Categoria == ricorrenzaBase.Categoria &&
                        r.ID_Professionista == idProfessionista &&
                        r.Attivo);

                    if (giàEsiste)
                        continue;

                    // 🔁 Nuova ricorrenza
                    var nuovaRicorrenza = new RicorrenzeCosti
                    {
                        ID_CostoProfessionista = ricorrenzaBase.ID_CostoProfessionista,
                        Categoria = ricorrenzaBase.Categoria,
                        ID_Professione = ricorrenzaBase.ID_Professione,
                        ID_Professionista = idProfessionista,
                        Periodicita = ricorrenzaBase.Periodicita,
                        TipoValore = ricorrenzaBase.TipoValore,
                        Valore = ricorrenzaBase.Valore,
                        DataInizio = ricorrenzaBase.DataInizio,
                        DataFine = ricorrenzaBase.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = now,
                        DataUltimaModifica = now
                    };
                    db.RicorrenzeCosti.Add(nuovaRicorrenza);
                    db.SaveChanges();

                    int numeroVersioneRic = db.RicorrenzeCosti_a
                        .Count(r => r.IDVersioneRicorrenza == nuovaRicorrenza.ID_Ricorrenza) + 1;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        IDVersioneRicorrenza = nuovaRicorrenza.ID_Ricorrenza,
                        ID_Ricorrenza = nuovaRicorrenza.ID_Ricorrenza,
                       
                        Categoria = nuovaRicorrenza.Categoria,
                        ID_Professione = nuovaRicorrenza.ID_Professione,
                        ID_Professionista = nuovaRicorrenza.ID_Professionista,
                        Periodicita = nuovaRicorrenza.Periodicita,
                        TipoValore = nuovaRicorrenza.TipoValore,
                        Valore = nuovaRicorrenza.Valore,
                        DataInizio = nuovaRicorrenza.DataInizio,
                        DataFine = nuovaRicorrenza.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataCreazione = now,
                        DataUltimaModifica = now,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        NumeroVersione = numeroVersioneRic,
                        ModificheTestuali = $"📌 Ricorrenza assegnata al professionista {idProfessionista} da utente {idUtenteCorrente}"
                    });

                    // ✅ Salvataggio CostiPersonaliUtente
                    var costoUtente = new CostiPersonaliUtente
                    {
                        ID_Utente = idProfessionista,
                        Descrizione = ricorrenzaBase.Categoria,
                        Importo = ricorrenzaBase.Valore,
                        DataInserimento = now.Date,
                        Approvato = false,
                        ID_UtenteCreatore = idUtenteCorrente,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = now,
                        ID_AnagraficaCostoProfessionista = ricorrenzaBase.ID_CostoProfessionista
                    };
                    db.CostiPersonaliUtente.Add(costoUtente);
                    db.SaveChanges();

                    int numeroVersioneCosto = db.CostiPersonaliUtente_a
                        .Count(v => v.ID_CostoPersonale == costoUtente.ID_CostoPersonale) + 1;

                    db.CostiPersonaliUtente_a.Add(new CostiPersonaliUtente_a
                    {
                        ID_CostoPersonale = costoUtente.ID_CostoPersonale,
                        ID_Utente = costoUtente.ID_Utente,
                        Descrizione = costoUtente.Descrizione,
                        Importo = costoUtente.Importo,
                        DataInserimento = costoUtente.DataInserimento,
                        Approvato = costoUtente.Approvato,
                        ID_UtenteCreatore = (int)costoUtente.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = costoUtente.ID_UtenteUltimaModifica,
                        DataUltimaModifica = costoUtente.DataUltimaModifica,
                        NumeroVersione = numeroVersioneCosto,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        ModificheTestuali = $"📝 Assegnato costo personale a professionista {idProfessionista} da utente {idUtenteCorrente}",
                        ID_AnagraficaCostoProfessionista = costoUtente.ID_AnagraficaCostoProfessionista
                    });

                    db.SaveChanges();
                    countNuove++;
                }

                return Json(new
                {
                    success = true,
                    message = $"✅ Assegnazione completata ({countNuove} nuove ricorrenze e costi utente)."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }





        [HttpGet]
        public ActionResult VisualizzaAssegnazioniCosto(int idAnagraficaCosto)
        {
            try
            {
                var assegnazioni = db.CostiPersonaliUtente
                    .Where(c => c.ID_AnagraficaCostoProfessionista == idAnagraficaCosto)
                    .ToList()
                    .Select(c =>
                    {
                        var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == c.ID_Utente);

                        // ✅ Ricava importo da ricorrenza attiva
                        var ricorrenza = db.RicorrenzeCosti
                        .Where(r =>
                            r.ID_CostoProfessionista == c.ID_AnagraficaCostoProfessionista &&
                            r.Categoria == "Costo Professionista" &&
                            r.Attivo == true)
                        .OrderByDescending(r => r.DataUltimaModifica)
                        .FirstOrDefault();


                        return new
                        {
                            c.ID_CostoPersonale,
                            c.ID_Utente,
                            NomeProfessionista = utente != null ? utente.Nome + " " + utente.Cognome : "N/A",
                            DataAssegnazione = c.DataInserimento.HasValue ? c.DataInserimento.Value.ToString("dd/MM/yyyy") : "",

                            Importo = ricorrenza?.Valore ?? 0
                        };
                    });

                return Json(new { success = true, assegnazioni }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il recupero delle assegnazioni: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult EliminaAssegnazioneCosto(int idCostoPersonaleUtente)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;

                var costo = db.CostiPersonaliUtente.FirstOrDefault(c => c.ID_CostoPersonale == idCostoPersonaleUtente);
                if (costo == null)
                    return Json(new { success = false, message = "Costo non trovato." });

                // 🔁 Calcolo numero versione archivio
                int numeroVersione = db.CostiPersonaliUtente_a
                    .Count(a => a.ID_CostoPersonale == idCostoPersonaleUtente) + 1;

                // 💾 Archiviazione prima dell’eliminazione
                db.CostiPersonaliUtente_a.Add(new CostiPersonaliUtente_a
                {
                    IDVersioneCostoPersonale = costo.ID_CostoPersonale,
                    ID_AnagraficaCostoProfessionista = costo.ID_AnagraficaCostoProfessionista,
                    ID_Utente = costo.ID_Utente,
                    DataInserimento = costo.DataInserimento,
                    DataArchiviazione = now,
                    NumeroVersione = numeroVersione,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"❌ Assegnazione eliminata da utente {idUtenteCorrente} il {now:g}"
                });

                // ❌ Rimozione effettiva
                db.CostiPersonaliUtente.Remove(costo);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Assegnazione eliminata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l’eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetListaProfessionistiAssegnabili(int idCosto)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();

                // 🔍 Professionisti attivi da OperatoriSinergia
                var professionisti = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista" && o.Stato == "Attivo")
                    .ToList();

                // ❌ ID già assegnati
                var idAssegnati = db.CostiPersonaliUtente
                    .Where(c => c.ID_AnagraficaCostoProfessionista == idCosto)
                    .Select(c => c.ID_Utente)
                    .ToList();

                // 🔍 Filtro SENZA Contains → usando !Any()
                var disponibili = professionisti
                    .Where(p => !idAssegnati.Any(a => a == p.ID_UtenteCollegato))
                    .Select(p => new
                    {
                        ID = p.ID_UtenteCollegato,
                        Nome = p.Nome + " " + p.Cognome
                    })
                    .OrderBy(p => p.Nome)
                    .ToList();

                return Json(new { success = true, professionisti = disponibili }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il recupero: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region COSTI DI TEAM 

        [HttpGet]
        public ActionResult GestioneSpeseTeam()
        {
            try
            {
                ViewBag.Title = "Gestione Spese Team";

                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                {
                    ViewBag.MessaggioErrore = "Utente non autenticato o sessione scaduta.";
                    return View("~/Views/Shared/_MessaggioErrore.cshtml");
                }

                // 🔐 Gestione permessi
                bool puoAggiungere = false, puoModificare = false, puoEliminare = false;

                if (utenteCorrente.TipoUtente == "Admin")
                {
                    puoAggiungere = puoModificare = puoEliminare = true;
                }
                else if (utenteCorrente.TipoUtente == "Professionista" || utenteCorrente.TipoUtente == "Collaboratore")
                {
                    var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
                    puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
                    puoModificare = permessiDb.Any(p => p.Modifica == true);
                    puoEliminare = permessiDb.Any(p => p.Elimina == true);
                }

                var lista = db.AnagraficaCostiTeam
                    .Where(t => t.Stato == "Attivo")
                    .OrderBy(t => t.ID_AnagraficaCostoTeam)
                    .ToList()
                    .Select(t =>
                    {
                        var ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r =>
                            r.Categoria == "Costo Team" &&
                            r.ID_CostoTeam == t.ID_AnagraficaCostoTeam);

                        return new CostoTeamViewModel
                        {
                            ID_AnagraficaCostoTeam = t.ID_AnagraficaCostoTeam,
                            Descrizione = t.Descrizione,
                            Importo = t.Importo ?? 0,
                            Stato = t.Stato,
                            ID_UtenteCreatore = t.ID_UtenteCreatore,
                            ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
                            DataUltimaModifica = t.DataUltimaModifica,
                            NomeCreatore = db.OperatoriSinergia
                                .Where(o => o.ID_UtenteCollegato == t.ID_UtenteCreatore && o.TipoCliente == "Professionista")
                                .Select(o => o.Nome + " " + o.Cognome)
                                .FirstOrDefault() ?? "-",
                            NumeroDistribuzioni = db.DistribuzioneCostiTeam
                                .Count(c => c.ID_AnagraficaCostoTeam == t.ID_AnagraficaCostoTeam),

                            // ✅ Campi ricorrenza
                            RicorrenzaAttiva = ricorrenza?.Attivo == true,
                            DataInizioRicorrenza = ricorrenza?.DataInizio,
                            DataFineRicorrenza = ricorrenza?.DataFine
                        };
                    })
                    .ToList();

                ViewBag.PuoAggiungere = puoAggiungere;
                ViewBag.Permessi = new PermessiViewModel
                {
                    ID_Utente = utenteCorrente.ID_Utente,
                    NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
                    Permessi = new List<PermessoSingoloViewModel>
            {
                new PermessoSingoloViewModel
                {
                    Aggiungi = puoAggiungere,
                    Modifica = puoModificare,
                    Elimina = puoEliminare
                }
            }
                };

                ViewBag.IDProfessionistaCorrente = utenteCorrente.ID_Utente;

                return View("~/Views/CostiTeam/GestioneCostiTeam.cshtml", lista);
            }
            catch (Exception ex)
            {
                ViewBag.MessaggioErrore = $"❌ Errore nella vista Costi Team: {ex.Message}";
                return View("~/Views/Shared/_MessaggioErrore.cshtml");
            }
        }


        //public ActionResult GestioneSpeseTeamList()
        //{
        //    int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
        //    var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

        //    if (utenteCorrente == null)
        //    {
        //        ViewBag.MessaggioErrore = "Utente non autenticato o sessione scaduta.";
        //        return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
        //    }

        //    IQueryable<AnagraficaCostiTeam> query = db.AnagraficaCostiTeam
        //        .Where(t => t.Stato == "Attivo");

        //    // 🔐 Gestione Permessi
        //    bool puoAggiungere = false;
        //    bool puoModificare = false;
        //    bool puoEliminare = false;

        //    if (utenteCorrente.TipoUtente == "Admin")
        //    {
        //        puoAggiungere = puoModificare = puoEliminare = true;
        //    }
        //    else if (utenteCorrente.TipoUtente == "Professionista" || utenteCorrente.TipoUtente == "Collaboratore")
        //    {
        //        var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
        //        puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
        //        puoModificare = permessiDb.Any(p => p.Modifica == true);
        //        puoEliminare = permessiDb.Any(p => p.Elimina == true);
        //    }

        //    var lista = query
        //        .OrderBy(t => t.Descrizione)
        //        .ToList()
        //        .Select(t => new CostoTeamViewModel
        //        {
        //            ID_AnagraficaCostoTeam = t.ID_AnagraficaCostoTeam,
        //            Descrizione = t.Descrizione,
        //            Importo = (decimal)t.Importo,
        //            Stato = t.Stato, // 👈 AGGIUNTO QUI
        //            ID_UtenteCreatore = t.ID_UtenteCreatore,
        //            ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
        //            DataUltimaModifica = t.DataUltimaModifica,
        //            NomeCreatore = db.OperatoriSinergia
        //            .Where(o => o.ID_UtenteCollegato == t.ID_UtenteCreatore && o.TipoCliente == "Professionista")
        //            .Select(o => o.Nome + " " + o.Cognome)
        //            .FirstOrDefault(),
        //            NumeroDistribuzioni = db.DistribuzioneCostiTeam
        //                .Count(c => c.ID_AnagraficaCostoTeam == t.ID_AnagraficaCostoTeam)
        //        })
        //        .ToList();

        //    ViewBag.PuoAggiungere = puoAggiungere;
        //    ViewBag.Permessi = new PermessiViewModel
        //    {
        //        ID_Utente = utenteCorrente.ID_Utente,
        //        NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
        //        Permessi = new List<PermessoSingoloViewModel>
        //{
        //    new PermessoSingoloViewModel
        //    {
        //        Aggiungi = puoAggiungere,
        //        Modifica = puoModificare,
        //        Elimina = puoEliminare
        //    }
        //}
        //    };

        //    ViewBag.IDProfessionistaCorrente = utenteCorrente.ID_Utente;

        //    return PartialView("~/Views/CostiTeam/_GestioneCostiTeamList.cshtml", lista);
        //}


        [HttpPost]
        public ActionResult CreaAnagraficaCostoTeam(CostoTeamViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare tutti i campi obbligatori." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var nuovo = new AnagraficaCostiTeam
                {
                    Descrizione = model.Descrizione?.Trim(),
                    ID_Professione = model.ID_Professione, // opzionale
                    Importo = model.Importo,
                    Ricorrente = model.Ricorrente,
                    Stato = string.IsNullOrWhiteSpace(model.Stato) ? "Attivo" : model.Stato.Trim(), // ✅ AGGIUNTO
                    ID_UtenteCreatore = idUtente,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now
                };

                db.AnagraficaCostiTeam.Add(nuovo);
                db.SaveChanges();

                // 🔁 Versione storica
                db.AnagraficaCostiTeam_a.Add(new AnagraficaCostiTeam_a
                {
                    ID_AnagraficaCostoTeam = nuovo.ID_AnagraficaCostoTeam,
                    Descrizione = nuovo.Descrizione,
                    ID_Professione = nuovo.ID_Professione,
                    Importo = nuovo.Importo,
                    Ricorrente = nuovo.Ricorrente,
                    Stato = nuovo.Stato, // ✅ AGGIUNTO ANCHE QUI
                    ID_UtenteArchiviazione = idUtente,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Inserito da utente {idUtente} il {DateTime.Now:g}"
                });

                // ✅ Ricorrenza automatica se prevista
                if (model.Ricorrente && !string.IsNullOrWhiteSpace(model.Periodicita) && model.Periodicita != "Una Tantum")
                {
                    var ricorrenza = new RicorrenzeCosti
                    {
                        ID_CostoTeam = nuovo.ID_AnagraficaCostoTeam,
                        Categoria = "Costo Team",
                        Periodicita = model.Periodicita,
                        TipoValore = model.TipoValore,
                        Valore = model.Importo,
                        DataInizio = model.DataInizio,
                        DataFine = model.DataFine,
                        ID_Team = null,
                        Attivo = true,
                        ID_UtenteCreatore = idUtente,
                        DataCreazione = DateTime.Now,
                        ID_UtenteUltimaModifica = idUtente,
                        DataUltimaModifica = DateTime.Now
                    };

                    db.RicorrenzeCosti.Add(ricorrenza);

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_CostoTeam = ricorrenza.ID_CostoTeam,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio,
                        DataFine = ricorrenza.DataFine,
                        Attivo = ricorrenza.Attivo,
                        ID_Team = ricorrenza.ID_Team,
                        ID_UtenteCreatore = ricorrenza.ID_UtenteCreatore,
                        DataCreazione = ricorrenza.DataCreazione,
                        ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                        DataUltimaModifica = ricorrenza.DataUltimaModifica,
                        NumeroVersione = 1,
                        ModificheTestuali = $"✅ Inserita ricorrenza automatica per costo team ID = {ricorrenza.ID_CostoTeam}"
                    });
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo team creato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult ModificaAnagraficaCostoTeam(CostoTeamViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var esistente = db.AnagraficaCostiTeam.FirstOrDefault(c => c.ID_AnagraficaCostoTeam == model.ID_AnagraficaCostoTeam);
                if (esistente == null)
                    return Json(new { success = false, message = "Costo team non trovato." });

                int ultimaVersione = db.AnagraficaCostiTeam_a
                    .Where(a => a.ID_AnagraficaCostoTeam == esistente.ID_AnagraficaCostoTeam)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                DateTime minSqlDate = new DateTime(1753, 1, 1);

                List<string> modifiche = new List<string>();
                void Check(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                Check("Descrizione", esistente.Descrizione, model.Descrizione);
                Check("ID Professione", esistente.ID_Professione, model.ID_Professione);
                Check("Importo", esistente.Importo, model.Importo);
                Check("Stato", esistente.Stato, model.Stato);

                esistente.Descrizione = model.Descrizione?.Trim();
                esistente.ID_Professione = model.ID_Professione;
                esistente.Importo = model.Importo;
                esistente.Ricorrente = model.Ricorrente;
                esistente.Stato = model.Stato?.Trim();
                esistente.ID_UtenteUltimaModifica = idUtente;
                esistente.DataUltimaModifica = DateTime.Now;

                db.AnagraficaCostiTeam_a.Add(new AnagraficaCostiTeam_a
                {
                    ID_AnagraficaCostoTeam = esistente.ID_AnagraficaCostoTeam,
                    Descrizione = esistente.Descrizione,
                    ID_Professione = esistente.ID_Professione,
                    Importo = esistente.Importo,
                    Ricorrente = esistente.Ricorrente,
                    Stato = esistente.Stato,
                    ID_UtenteArchiviazione = idUtente,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = modifiche.Any()
                        ? $"Modifica effettuata da ID_Utente = {idUtente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica senza variazioni rilevanti"
                });

                // 🔁 Ricorrenza (solo se Ricorrente == true e Periodicita valida)
                if (model.Ricorrente && !string.IsNullOrWhiteSpace(model.Periodicita) && model.Periodicita != "Una Tantum")
                {
                    var ric = db.RicorrenzeCosti
                        .FirstOrDefault(r => r.ID_CostoTeam == esistente.ID_AnagraficaCostoTeam && r.Categoria == "Costo Team");

                    int ricVer = db.RicorrenzeCosti_a
                        .Where(r => r.ID_CostoTeam == esistente.ID_AnagraficaCostoTeam && r.Categoria == "Costo Team")
                        .OrderByDescending(r => r.NumeroVersione)
                        .Select(r => (int?)r.NumeroVersione)
                        .FirstOrDefault() ?? 0;

                    List<string> modificheRic = new List<string>();
                    void CheckRic(string campo, object oldVal, object newVal)
                    {
                        if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                            modificheRic.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                    }

                    if (ric == null)
                    {
                        ric = new RicorrenzeCosti
                        {
                            ID_CostoTeam = esistente.ID_AnagraficaCostoTeam,
                            Categoria = "Costo Team",
                            ID_UtenteCreatore = idUtente,
                            DataCreazione = DateTime.Now
                        };
                        db.RicorrenzeCosti.Add(ric);
                        modificheRic.Add("➕ Ricorrenza creata.");
                    }
                    else
                    {
                        CheckRic("Periodicità", ric.Periodicita, model.Periodicita);
                        CheckRic("TipoValore", ric.TipoValore, model.TipoValore);
                        CheckRic("Valore", ric.Valore, model.Importo);
                        CheckRic("Data Inizio", ric.DataInizio, model.DataInizio);
                        CheckRic("Data Fine", ric.DataFine, model.DataFine);
                    }

                    ric.Periodicita = model.Periodicita;
                    ric.TipoValore = model.TipoValore;
                    ric.Valore = model.Importo;
                    ric.DataInizio = model.DataInizio >= minSqlDate ? (DateTime?)model.DataInizio : null;
                    ric.DataFine = model.DataFine >= minSqlDate ? (DateTime?)model.DataFine : null;
                    ric.Attivo = true;
                    ric.ID_Team = null;
                    ric.ID_UtenteUltimaModifica = idUtente;
                    ric.DataUltimaModifica = DateTime.Now;

                    db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                    {
                        ID_CostoTeam = ric.ID_CostoTeam,
                        Categoria = ric.Categoria,
                        Periodicita = ric.Periodicita,
                        TipoValore = ric.TipoValore,
                        Valore = ric.Valore,
                        DataInizio = ric.DataInizio,
                        DataFine = ric.DataFine,
                        Attivo = ric.Attivo,
                        ID_Team = ric.ID_Team,
                        ID_UtenteCreatore = ric.ID_UtenteCreatore,
                        DataCreazione = ric.DataCreazione,
                        ID_UtenteUltimaModifica = ric.ID_UtenteUltimaModifica,
                        DataUltimaModifica = ric.DataUltimaModifica,
                        NumeroVersione = ricVer + 1,
                        ModificheTestuali = modificheRic.Any()
                            ? $"Modifiche ricorrenza da utente {idUtente} il {DateTime.Now:g}:\n{string.Join("\n", modificheRic)}"
                            : "Nessuna modifica rilevante alla ricorrenza"
                    });
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo team modificato correttamente." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("🔥 Errore SaveChanges: " + ex.Message);

                // 🔍 Scansione di tutte le entità in tracking per trovare DateTime non valide
                foreach (var entry in db.ChangeTracker.Entries())
                {
                    foreach (var prop in entry.CurrentValues.PropertyNames)
                    {
                        var value = entry.CurrentValues[prop];

                        if (value is DateTime dt && dt < new DateTime(1753, 1, 1))
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ ERRORE DateTime troppo vecchia → {entry.Entity.GetType().Name}.{prop} = {dt}");
                        }
                    }
                }

                throw; // ❗ Rilancia l’eccezione per vederla nel debugger
            }

        }



        [HttpGet]
        public ActionResult GetCostoTeam(int id)
        {
            var costo = db.AnagraficaCostiTeam
                .Where(c => c.ID_AnagraficaCostoTeam == id)
                .Select(c => new CostoTeamViewModel
                {
                    ID_AnagraficaCostoTeam = c.ID_AnagraficaCostoTeam,
                    Descrizione = c.Descrizione,
                    ID_Professione = c.ID_Professione,
                    Importo = (decimal)c.Importo,
                    Ricorrente = c.Ricorrente,
                    Stato = c.Stato,

                    // Team a cui è stato assegnato
                    TeamAssegnati = db.DistribuzioneCostiTeam
                        .Where(d => d.ID_AnagraficaCostoTeam == c.ID_AnagraficaCostoTeam)
                        .Select(d => new TeamAssegnatoViewModel
                        {
                            ID_Distribuzione = d.ID_Distribuzione,
                            ID_Team = d.ID_Team,
                            Percentuale = d.Percentuale,
                            NomeTeam = db.TeamProfessionisti
                                .Where(t => t.ID_Team == d.ID_Team)
                                .Select(t => t.Nome)
                                .FirstOrDefault()
                        })
                        .ToList()
                })
                .FirstOrDefault();

            if (costo == null)
                return Json(new { success = false, message = "Costo team non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, costo }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EliminaCostoTeam(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare il costo." });

                var costo = db.AnagraficaCostiTeam.FirstOrDefault(c => c.ID_AnagraficaCostoTeam == id);
                if (costo == null)
                    return Json(new { success = false, message = "Costo team non trovato." });

                int ultimaVersione = db.AnagraficaCostiTeam_a
                    .Where(a => a.ID_AnagraficaCostoTeam == id)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                db.AnagraficaCostiTeam_a.Add(new AnagraficaCostiTeam_a
                {
                    ID_AnagraficaCostoTeam = costo.ID_AnagraficaCostoTeam,
                    Descrizione = costo.Descrizione,
                    ID_Professione = costo.ID_Professione,
                    Importo = costo.Importo,
                    Stato = costo.Stato,
                    Ricorrente = costo.Ricorrente,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑 Eliminazione definitiva effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                db.AnagraficaCostiTeam.Remove(costo);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Costo team eliminato definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetRicorrenzaCostoTeam(int idAnagraficaCostoTeam)
        {
            try
            {
                var ricorrenza = db.RicorrenzeCosti
                    .Where(r =>
                        r.ID_CostoTeam == idAnagraficaCostoTeam &&
                        r.Categoria == "Costo Team" &&
                        r.Attivo == true)
                    .OrderByDescending(r => r.DataUltimaModifica)
                    .FirstOrDefault();

                if (ricorrenza == null)
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    ricorrenza = new
                    {
                        ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                        ID_AnagraficaCosto = ricorrenza.ID_CostoTeam,
                        ID_Professione = ricorrenza.ID_Professione,
                        ID_Professionista = ricorrenza.ID_Professionista,
                        ID_Team = ricorrenza.ID_Team,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio?.ToString("yyyy-MM-dd"),
                        DataFine = ricorrenza.DataFine?.ToString("yyyy-MM-dd"),
                        Attivo = ricorrenza.Attivo
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult SalvaRicorrenzaCostoTeam(RicorrenzaCostoViewModel model)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;
                RicorrenzeCosti ricorrenza;
                bool isModifica = model.ID_Ricorrenza.HasValue;

                // 🔍 Validazione obbligatoria
                if (model.ID_AnagraficaCosto <= 0 || string.IsNullOrEmpty(model.Categoria) ||
                    string.IsNullOrEmpty(model.TipoValore) || model.Valore == null ||
                    string.IsNullOrEmpty(model.Periodicita) || !model.DataInizio.HasValue)
                {
                    return Json(new { success = false, message = "Compilare tutti i campi obbligatori, incluse Periodicità e Data Inizio." });
                }

                // ℹ️ Gestione automatica "Una Tantum"
                if (model.DataInizio.HasValue && model.DataFine.HasValue && model.DataInizio == model.DataFine)
                {
                    if (string.IsNullOrEmpty(model.Periodicita) || model.Periodicita == "Una Tantum")
                        model.Periodicita = "Giornaliero";
                }

                if (isModifica)
                {
                    ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_Ricorrenza == model.ID_Ricorrenza);
                    if (ricorrenza == null)
                        return Json(new { success = false, message = "Ricorrenza non trovata." });

                    ricorrenza.ID_Team = model.ID_Team;
                    ricorrenza.ID_Professione = model.ID_Professione;
                    ricorrenza.Categoria = model.Categoria;
                    ricorrenza.Periodicita = model.Periodicita;
                    ricorrenza.TipoValore = model.TipoValore;
                    ricorrenza.Valore = (decimal)model.Valore;
                    ricorrenza.DataInizio = model.DataInizio;
                    ricorrenza.DataFine = model.DataFine;
                    ricorrenza.ID_UtenteUltimaModifica = idUtenteCorrente;
                    ricorrenza.DataUltimaModifica = now;
                }
                else
                {
                    ricorrenza = new RicorrenzeCosti
                    {
                        ID_CostoTeam = model.ID_AnagraficaCosto,
                        ID_Team = model.ID_Team,
                        ID_Professione = model.ID_Professione,
                        Categoria = model.Categoria,
                        Periodicita = model.Periodicita,
                        TipoValore = model.TipoValore,
                        Valore = (decimal)model.Valore,
                        DataInizio = model.DataInizio,
                        DataFine = model.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataCreazione = now,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = now
                    };
                    db.RicorrenzeCosti.Add(ricorrenza);
                }

                db.SaveChanges();

                // 🗃️ Versionamento archivio
                int numeroVersione = db.RicorrenzeCosti_a
                    .Count(a => a.IDVersioneRicorrenza == ricorrenza.ID_Ricorrenza) + 1;

                db.RicorrenzeCosti_a.Add(new RicorrenzeCosti_a
                {
                    IDVersioneRicorrenza = ricorrenza.ID_Ricorrenza,
                    ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                    ID_CostoTeam = ricorrenza.ID_CostoTeam,
                    ID_Team = ricorrenza.ID_Team,
                    ID_Professione = ricorrenza.ID_Professione,
                    Categoria = ricorrenza.Categoria,
                    Periodicita = ricorrenza.Periodicita,
                    TipoValore = ricorrenza.TipoValore,
                    Valore = ricorrenza.Valore,
                    DataInizio = ricorrenza.DataInizio,
                    DataFine = ricorrenza.DataFine,
                    Attivo = ricorrenza.Attivo,
                    ID_UtenteCreatore = ricorrenza.ID_UtenteCreatore,
                    DataCreazione = ricorrenza.DataCreazione,
                    ID_UtenteUltimaModifica = ricorrenza.ID_UtenteUltimaModifica,
                    DataUltimaModifica = ricorrenza.DataUltimaModifica,
                    NumeroVersione = numeroVersione,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"💾 {(isModifica ? "Modificata" : "Creata")} ricorrenza (CostoTeam #{ricorrenza.ID_CostoTeam}) da utente {idUtenteCorrente}"
                });

                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante il salvataggio: " + ex.Message });
            }
        }

        [HttpPost]
        // [ValidateAntiForgeryToken] ← RIMOSSO COME RICHIESTO
        public ActionResult AssegnaCostoTeam(CostoTeamViewModel model)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;

                if (model.ID_AnagraficaCostoTeam <= 0 || model.ID_TeamSelezionati == null || !model.ID_TeamSelezionati.Any())
                    return Json(new { success = false, message = "Seleziona almeno un team." });

                var anagrafica = db.AnagraficaCostiTeam
                    .FirstOrDefault(a => a.ID_AnagraficaCostoTeam == model.ID_AnagraficaCostoTeam);

                if (anagrafica == null)
                    return Json(new { success = false, message = "Costo team non trovato." });

                foreach (int idTeam in model.ID_TeamSelezionati)
                {
                    string chiavePercentuale = $"Percentuale_{idTeam}";
                    decimal percentuale = 0;

                    if (!string.IsNullOrWhiteSpace(Request.Form[chiavePercentuale]) &&
                        decimal.TryParse(Request.Form[chiavePercentuale].Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                    {
                        percentuale = parsed;
                    }

                    var assegnazione = new DistribuzioneCostiTeam
                    {
                        ID_Team = idTeam,
                        ID_AnagraficaCostoTeam = model.ID_AnagraficaCostoTeam,
                        Percentuale = percentuale,
                        DataCreazione = now,
                        ID_UtenteCreatore = idUtenteCorrente
                    };

                    db.DistribuzioneCostiTeam.Add(assegnazione);
                    db.SaveChanges();

                    db.DistribuzioneCostiTeam_a.Add(new DistribuzioneCostiTeam_a
                    {
                        ID_DistribuzioneArchivio = assegnazione.ID_Distribuzione,
                        ID_Team = assegnazione.ID_Team,
                        ID_AnagraficaCostoTeam = assegnazione.ID_AnagraficaCostoTeam,
                        Percentuale = assegnazione.Percentuale,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        NumeroVersione = 1,
                        ModificheTestuali = $"🏷️ Assegnato costo team {model.ID_AnagraficaCostoTeam} al team {idTeam} da utente {idUtenteCorrente}"
                    });

                    db.SaveChanges();
                }

                var distribuzioni = db.DistribuzioneCostiTeam
                    .Where(d => d.ID_AnagraficaCostoTeam == model.ID_AnagraficaCostoTeam)
                    .ToList();

                var ricorrenzaBase = db.RicorrenzeCosti
                    .Where(r => r.ID_CostoTeam == model.ID_AnagraficaCostoTeam && r.ID_Team == null && r.Categoria == "Costo Team")
                    .OrderByDescending(r => r.DataCreazione)
                    .FirstOrDefault();

                if (ricorrenzaBase == null)
                    return Json(new { success = false, message = "⚠️ Nessuna ricorrenza base trovata per il costo team." });

                foreach (var dist in distribuzioni)
                {
                    bool esisteGia = db.RicorrenzeCosti.Any(r =>
                        r.ID_CostoTeam == dist.ID_AnagraficaCostoTeam &&
                        r.ID_Team == dist.ID_Team &&
                        r.Categoria == "Costo Team" &&
                        r.Attivo);

                    if (esisteGia)
                        continue;

                    decimal valoreProQuota = Math.Round(ricorrenzaBase.Valore * dist.Percentuale / 100, 2);

                    db.RicorrenzeCosti.Add(new RicorrenzeCosti
                    {
                        ID_CostoTeam = dist.ID_AnagraficaCostoTeam,
                        ID_Team = dist.ID_Team,
                        Categoria = "Costo Team",
                        Periodicita = ricorrenzaBase.Periodicita,
                        TipoValore = ricorrenzaBase.TipoValore,
                        Valore = valoreProQuota,
                        DataInizio = ricorrenzaBase.DataInizio,
                        DataFine = ricorrenzaBase.DataFine,
                        Attivo = true,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataCreazione = now
                    });

                    db.SaveChanges();
                }

                return Json(new { success = true, message = "✅ Costo assegnato ai team selezionati." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante l'assegnazione: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult VisualizzaAssegnazioniCostoTeam(int idAnagraficaCostoTeam)
        {
            try
            {
                var assegnazioni = db.DistribuzioneCostiTeam
                    .Where(c => c.ID_AnagraficaCostoTeam == idAnagraficaCostoTeam)
                    .ToList()
                    .Select(c =>
                    {
                        var team = db.TeamProfessionisti.FirstOrDefault(t => t.ID_Team == c.ID_Team);

                        // 📦 Recupera i membri associati al team
                        var membri = db.MembriTeam
                          .Where(mt => mt.ID_Team == c.ID_Team)
                          .Join(db.Utenti,
                                mt => mt.ID_Professionista,
                                u => u.ID_Utente,
                                (mt, u) => new
                                {
                                    u.Nome,
                                    u.Cognome
                                })
                          .ToList() // 🔁 EF fa la query SQL qui. Da questo punto in poi sei in memoria.
                          .Select(m => $"{m.Nome} {m.Cognome}") // ✅ Ora puoi usare stringhe interpolate
                          .ToList();

                        return new
                        {
                            c.ID_Distribuzione,
                            c.ID_Team,
                            NomeTeam = team != null ? team.Nome : "N/A",
                            Percentuale = c.Percentuale,
                            DataAssegnazione = c.DataCreazione.ToString("dd/MM/yyyy"),
                            Membri = membri
                        };
                    });

                return Json(new { success = true, assegnazioni }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il recupero delle assegnazioni: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpPost]
        public ActionResult EliminaAssegnazioneCostoTeam(int idDistribuzione)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                DateTime now = DateTime.Now;

                var costo = db.DistribuzioneCostiTeam.FirstOrDefault(c => c.ID_Distribuzione == idDistribuzione);
                if (costo == null)
                    return Json(new { success = false, message = "Assegnazione non trovata." });

                // 🔁 Calcolo numero versione archivio
                int numeroVersione = db.DistribuzioneCostiTeam_a
                    .Count(a => a.ID_Distribuzione == idDistribuzione) + 1;

                // 💾 Archiviazione prima dell’eliminazione
                db.DistribuzioneCostiTeam_a.Add(new DistribuzioneCostiTeam_a
                {
                    ID_Distribuzione = costo.ID_Distribuzione,
                    ID_AnagraficaCostoTeam = costo.ID_AnagraficaCostoTeam,
                    ID_Team = costo.ID_Team,
                    Percentuale = costo.Percentuale,

                    DataArchiviazione = now,
                    NumeroVersione = numeroVersione,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"❌ Assegnazione costo team eliminata da utente {idUtenteCorrente} il {now:g}"
                });

                // ❌ Rimozione effettiva
                db.DistribuzioneCostiTeam.Remove(costo);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Assegnazione eliminata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l’eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetListaTeamAssegnabili(int idCostoTeam)
        {
            try
            {
                // 🔍 Recupera ID_Team già assegnati a questo costo
                var teamAssegnati = db.DistribuzioneCostiTeam
                    .Where(d => d.ID_AnagraficaCostoTeam == idCostoTeam)
                    .Select(d => d.ID_Team)
                    .ToList();

                // 🔁 Recupera solo team attivi e non ancora assegnati
                var risultato = db.TeamProfessionisti
                    .Where(t => t.Attivo && !teamAssegnati.Contains(t.ID_Team))
                    .Select(t => new
                    {
                        ID = t.ID_Team,
                        Nome = t.Nome,
                        Percentuale = 0m,
                        Assegnato = false
                    })
                    .OrderBy(t => t.Nome)
                    .ToList();

                return Json(new { success = true, team = risultato }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il recupero: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region GENERAZIONE COSTI E ECCEZIONI 

        [HttpGet]
        public ActionResult GestioneSpeseGenerazioneCosti()
        {
            ViewBag.Title = "Generazione Costi";
            return View("~/Views/GenerazioneCosti/GenerazioneCosti.cshtml");
        }

        [HttpGet]
        public ActionResult GenerazioneCostiList(DateTime? dataDa, DateTime? dataA, int? idProfessionista, int? idTeam, string categoria, string ricorrenza, string stato, bool mostraEccezioni = false)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            var query = db.GenerazioneCosti.AsQueryable();

            // Filtro date
            if (dataDa.HasValue)
                query = query.Where(c => c.DataRegistrazione >= dataDa.Value);
            if (dataA.HasValue)
                query = query.Where(c => c.DataRegistrazione <= dataA.Value);

            // Filtro professionista / team / categoria
            if (idProfessionista.HasValue)
                query = query.Where(c => c.ID_Utente == idProfessionista.Value);
            if (idTeam.HasValue)
                query = query.Where(c => c.ID_Team == idTeam.Value);
            if (!string.IsNullOrEmpty(categoria))
                query = query.Where(c => c.Categoria == categoria);

            // Filtro ricorrenza
            if (!string.IsNullOrEmpty(ricorrenza) && ricorrenza != "Tutti")
            {
                if (ricorrenza == "Ricorrenti")
                    query = query.Where(c => c.Periodicita != "Una Tantum");
                else if (ricorrenza == "Una Tantum")
                    query = query.Where(c => c.Periodicita == "Una Tantum");
            }

            // Filtro stato
            if (!string.IsNullOrEmpty(stato))
            {
                if (stato == "Da Generare")
                    query = query.Where(c => c.Approvato == null);
                else if (stato == "Generati")
                    query = query.Where(c => c.Approvato == true);
                else if (stato == "Pagati")
                    query = query.Where(c => c.Approvato == false);
            }

            // Mostra bottone "Esegui pagamento costi mensili" solo se ci sono costi previsionali non pagati
            var inizioMese = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var fineMese = inizioMese.AddMonths(1);

            bool mostraPagamentoManuale = db.GenerazioneCosti.Any(c =>
                c.Approvato == false &&
                c.Stato == "Previsionale" &&
                c.DataRegistrazione >= inizioMese &&
                c.DataRegistrazione < fineMese
            );

            ViewBag.MostraPagamentoManuale = mostraPagamentoManuale;



            var lista = (from c in query
                         join p in db.Pratiche on c.ID_Pratiche equals p.ID_Pratiche into praticheJoin
                         from pratica in praticheJoin.DefaultIfEmpty()
                         select new GenerazioneCostiViewModel
                         {
                             ID_GenerazioneCosto = c.ID_GenerazioneCosto,
                             ID_Riferimento = c.ID_Riferimento,
                             Categoria = c.Categoria,
                             Descrizione = c.Descrizione,
                             Importo = c.Importo ?? 0,
                             Periodicita = c.Periodicita,
                             DataRegistrazione = c.DataRegistrazione,
                             Origine = c.Origine,
                             Stato = c.Approvato == true ? "Pagato" : (c.Approvato == false ? "Previsionale" : "Da generare"),
                             NomeProfessionista = c.Categoria == "Costo Generale"
                                 ? (from os in db.OperatoriSinergia
                                    where os.ID_UtenteCollegato == c.ID_Utente && os.TipoCliente == "Professionista"
                                    select os.Nome + " " + os.Cognome).FirstOrDefault()
                                 ?? "-"
                                 : (from os in db.OperatoriSinergia
                                    where os.ID_Operatore == c.ID_Utente && os.TipoCliente == "Professionista"
                                    select os.Nome + " " + os.Cognome).FirstOrDefault()
                                 ?? db.Utenti.Where(u => u.ID_Utente == c.ID_Utente)
                                    .Select(u => u.Nome + " " + u.Cognome)
                                    .FirstOrDefault()
                                 ?? "-",
                             Team = c.ID_Team.HasValue
                                 ? db.TeamProfessionisti.Where(t => t.ID_Team == c.ID_Team.Value).Select(t => t.Nome).FirstOrDefault()
                                 : null,
                             TitoloPratica = pratica != null ? pratica.Titolo : "-",
                             ID_Professionista = c.ID_Team.HasValue ? (int?)null : c.ID_Utente,
                             ID_Team = c.ID_Team,

                             HaEccezione = db.EccezioniRicorrenzeCosti.Any(e =>
                                     e.Categoria == c.Categoria &&
                                     (
                                         (e.ID_Professionista.HasValue && e.ID_Professionista == c.ID_Utente) ||
                                         (e.ID_Team.HasValue && e.ID_Team == c.ID_Team)
                                     )
                                )

                         }).ToList();

            // Inserisci righe per le eccezioni se richiesto
            if (mostraEccezioni)
            {
                var eccezioni = db.EccezioniRicorrenzeCosti
                    .Where(e =>
                        (!idProfessionista.HasValue || e.ID_Professionista == idProfessionista) &&
                        (!idTeam.HasValue || e.ID_Team == idTeam) &&
                        (dataDa == null || e.DataFine >= dataDa) &&
                        (dataA == null || e.DataInizio <= dataA))
                    .ToList();

                foreach (var e in eccezioni)
                {
                    var nomeProfessionista = e.ID_Professionista.HasValue
                        ? db.OperatoriSinergia
                            .Where(o => o.ID_UtenteCollegato == e.ID_Professionista && o.TipoCliente == "Professionista")
                            .Select(o => o.Nome + " " + o.Cognome)
                            .FirstOrDefault()
                        : null;

                    var nomeTeam = e.ID_Team.HasValue
                        ? db.TeamProfessionisti
                            .Where(t => t.ID_Team == e.ID_Team)
                            .Select(t => t.Nome)
                            .FirstOrDefault()
                        : null;

                    lista.Add(new GenerazioneCostiViewModel
                    {
                        Categoria = "Eccezione",
                        Descrizione = "Eccezione attiva: " + e.Motivazione,
                        Importo = 0,
                        DataRegistrazione = e.DataInizio,
                        Stato = "Bloccato",
                        Utente_Professionista = nomeProfessionista,
                        Team = nomeTeam,
                    });
                }
            }

            ViewBag.ListaProfessionisti = db.Utenti
                .Where(u => u.TipoUtente == "Professionista" && u.Stato == "Attivo")
                .OrderBy(u => u.Nome)
                .ToList();


            return PartialView("~/Views/GenerazioneCosti/_GenerazioneCostiList.cshtml", lista);
        }


        [HttpGet]
        public JsonResult GetDettaglioCosto(int? id)
        {
            try
            {
                var costo = db.GenerazioneCosti.FirstOrDefault(x => x.ID_GenerazioneCosto == id);
                if (costo == null)
                {
                    return Json(new { success = false, message = "Costo non trovato." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    costo = new
                    {
                        costo.ID_GenerazioneCosto,
                        costo.Categoria,
                        ID_Utente = costo.ID_Utente,
                        ID_Team = costo.ID_Team
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore interno: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public JsonResult GetDettaglioJoinCostoRicorrenza(int idGenerazioneCosto)
        {
            using (var db = new SinergiaDB())
            {
                var costo = db.GenerazioneCosti.FirstOrDefault(c => c.ID_GenerazioneCosto == idGenerazioneCosto);

                if (costo == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Costo generato non trovato."
                    }, JsonRequestBehavior.AllowGet);
                }

                var creatore = db.Utenti.FirstOrDefault(u => u.ID_Utente == costo.ID_UtenteCreatore);
                var modificatore = db.Utenti.FirstOrDefault(u => u.ID_Utente == costo.ID_UtenteUltimaModifica);

                var generazioneCosto = new
                {
                    ID_GenerazioneCosto = costo.ID_GenerazioneCosto,
                    Categoria = costo.Categoria,
                    Descrizione = costo.Descrizione,
                    Importo = costo.Importo,
                    Periodicita = costo.Periodicita,
                    DataRegistrazione = costo.DataRegistrazione,
                    ID_Riferimento = costo.ID_Riferimento,
                    DataCreazione = costo.DataCreazione,
                    DataUltimaModifica = costo.DataUltimaModifica,
                    NomeCreatore = creatore != null ? creatore.Nome + " " + creatore.Cognome : "-",
                    NomeModificatore = modificatore != null ? modificatore.Nome + " " + modificatore.Cognome : "-",
                    TitoloPratica = costo.ID_Pratiche.HasValue
                        ? db.Pratiche.Where(p => p.ID_Pratiche == costo.ID_Pratiche.Value).Select(p => p.Titolo).FirstOrDefault()
                        : "-"
                };

                object ricorrenzaCosto = null;

                if (costo.Origine == "Ricorrenza" && costo.ID_Riferimento.HasValue)
                {
                    var ric = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_Ricorrenza == costo.ID_Riferimento.Value);

                    if (ric != null)
                    {
                        ricorrenzaCosto = new
                        {
                            ric.Valore,
                            ric.DataInizio,
                            ric.DataFine
                        };
                    }
                }

                return Json(new
                {
                    success = true,
                    generazioneCosto,
                    ricorrenzaCosto
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult EseguiPagamentoCostiAutomatico(int idProfessionista)
        {
            try
            {
                if (idProfessionista <= 0)
                {
                    return Json(new { success = false, messaggio = "ID professionista non valido." });
                }

                // 🔁 Ora il metodo restituisce un RisultatoPagamento
                var risultato = CostiHelper.VerificaPagamentoConPlafondSingolo(idProfessionista);

                return Json(new
                {
                    success = risultato.Successo,
                    messaggio = risultato.Messaggio
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore in EseguiPagamentoCostiAutomatico: {ex}");

                return Json(new
                {
                    success = false,
                    messaggio = "Errore durante il pagamento: " + ex.Message
                });
            }
        }






        // GESTIONE ECCEZIONI 

        [HttpPost]
        public ActionResult CreaEccezione(EccezioneRicorrenzaCostoViewModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Dati mancanti." });

            if (string.IsNullOrEmpty(model.Categoria))
                return Json(new { success = false, message = "Categoria obbligatoria." });

            if (model.ID_Professionista == null && model.ID_Team == null)
                return Json(new { success = false, message = "Selezionare un professionista o un team." });

            if (model.DataInizio > model.DataFine)
                return Json(new { success = false, message = "La data di inizio non può essere successiva alla data di fine." });

            if (string.IsNullOrWhiteSpace(model.Motivazione))
                return Json(new { success = false, message = "Motivazione obbligatoria." });

            // 🔎 Controllo che non siano state attivate entrambe le opzioni
            bool isModificaImporto = model.Motivazione.ToUpper().StartsWith("IMPORTO=");
            bool isSaltaCosto = model.Motivazione.ToUpper().StartsWith("SKIP");

            if (isModificaImporto && isSaltaCosto)
                return Json(new { success = false, message = "Non puoi inserire sia un nuovo importo che bloccare il costo. Scegli una sola opzione." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            DateTime now = DateTime.Now;

            using (var db = new SinergiaDB())
            {
                // 📝 Creazione nuova eccezione
                var nuova = new EccezioniRicorrenzeCosti
                {
                    ID_Professionista = model.ID_Professionista,
                    ID_Team = model.ID_Team,
                    ID_RicorrenzaCosto = model.ID_RicorrenzaCosto,
                    Categoria = model.Categoria,
                    SaltaCosto =model.SaltaCosto,
                    NuovoImporto = model.NuovoImporto,
                    DataInizio = model.DataInizio,
                    DataFine = model.DataFine,
                    Motivazione = model.Motivazione,
                    ID_UtenteCreatore = idUtente,
                    DataCreazione = now,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = now
                };

                db.EccezioniRicorrenzeCosti.Add(nuova);
                db.SaveChanges();

                // 🗂️ Salva anche nella tabella di archivio
                db.EccezioniRicorrenzeCosti_a.Add(new EccezioniRicorrenzeCosti_a
                {
                    ID_Eccezione = nuova.ID_Eccezione,
                    ID_Professionista = nuova.ID_Professionista,
                    ID_Team = nuova.ID_Team,
                    ID_RicorrenzaCosto = nuova.ID_RicorrenzaCosto,
                    Categoria = nuova.Categoria,
                    DataInizio = nuova.DataInizio,
                    SaltaCosto = nuova.SaltaCosto,
                    NuovoImporto = nuova.NuovoImporto,
                    DataFine = nuova.DataFine,
                    Motivazione = nuova.Motivazione,
                    ID_UtenteCreatore = nuova.ID_UtenteCreatore,
                    DataCreazione = nuova.DataCreazione,
                    ID_UtenteUltimaModifica = nuova.ID_UtenteUltimaModifica,
                    DataUltimaModifica = nuova.DataUltimaModifica,
                    NumeroVersione = 1,
                    ModificheTestuali = "➕ Creazione eccezione"
                });

                // 🔁 Aggiorna righe già presenti in GenerazioneCosti (opzionale)
                var costiDaAggiornare = db.GenerazioneCosti.Where(c =>
                    c.Categoria == nuova.Categoria &&
                    (
                        (nuova.ID_Professionista.HasValue && c.ID_Utente == nuova.ID_Professionista) ||
                        (nuova.ID_Team.HasValue && c.ID_Team == nuova.ID_Team)
                    ) &&
                    c.DataRegistrazione >= nuova.DataInizio &&
                    c.DataRegistrazione <= nuova.DataFine
                ).ToList();

                foreach (var costo in costiDaAggiornare)
                {
                    costo.HaEccezione = true;
                    costo.ID_UtenteUltimaModifica = idUtente;
                    costo.DataUltimaModifica = now;
                }

                db.SaveChanges();
            }

            return Json(new { success = true, message = "✅ Eccezione salvata correttamente." });
        }

        [HttpGet]
        public ActionResult GetListaEccezioni(string categoria = null, int? idProfessionista = null, int? idTeam = null, DateTime? da = null, DateTime? a = null, string motivazione = null, int? idRicorrenzaCosto = null)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    var query = db.EccezioniRicorrenzeCosti.AsQueryable();

                    if (!string.IsNullOrEmpty(categoria))
                        query = query.Where(e => e.Categoria == categoria);

                    if (idProfessionista.HasValue)
                        query = query.Where(e => e.ID_Professionista == idProfessionista.Value);

                    if (idTeam.HasValue)
                        query = query.Where(e => e.ID_Team == idTeam.Value);

                    if (idRicorrenzaCosto.HasValue)
                        query = query.Where(e => e.ID_RicorrenzaCosto == idRicorrenzaCosto.Value);

                    if (da.HasValue)
                        query = query.Where(e => e.DataInizio >= da.Value);

                    if (a.HasValue)
                        query = query.Where(e => e.DataFine <= a.Value);

                    if (!string.IsNullOrEmpty(motivazione))
                        query = query.Where(e => e.Motivazione.Contains(motivazione));

                    var lista = query
                        .OrderByDescending(e => e.DataInizio)
                        .ToList()
                        .Select(e => new
                        {
                            e.ID_Eccezione,
                            e.ID_RicorrenzaCosto,
                            e.Categoria,
                            e.ID_Professionista,
                            NomeProfessionista = e.ID_Professionista.HasValue
                                ? db.OperatoriSinergia
                                    .Where(o => o.ID_UtenteCollegato == e.ID_Professionista && o.TipoCliente == "Professionista")
                                    .Select(o => o.Nome + " " + o.Cognome)
                                    .FirstOrDefault()
                                : null,
                            e.ID_Team,
                            NomeTeam = e.ID_Team.HasValue
                                ? db.TeamProfessionisti
                                    .Where(t => t.ID_Team == e.ID_Team)
                                    .Select(t => t.Nome)
                                    .FirstOrDefault()
                                : null,
                            e.DataInizio,
                            e.DataFine,
                            e.Motivazione,
                            e.NuovoImporto
                        })
                        .ToList();

                    return Json(new { success = true, eccezioni = lista }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public ActionResult GetDettaglioEccezione(int idCosto)
        {
            using (var db = new SinergiaDB())
            {
                var costo = db.GenerazioneCosti.FirstOrDefault(c => c.ID_GenerazioneCosto == idCosto);
                if (costo == null)
                {
                    return Json(new { success = false, message = "Costo non trovato." }, JsonRequestBehavior.AllowGet);
                }

                var eccezione = db.EccezioniRicorrenzeCosti
                    .Where(e =>
                        e.Categoria == costo.Categoria &&
                        (
                            (e.ID_Professionista.HasValue && e.ID_Professionista == costo.ID_Utente) ||
                            (e.ID_Team.HasValue && e.ID_Team == costo.ID_Team)
                        )
                    )
                    .OrderByDescending(e => e.DataInizio)
                    .FirstOrDefault();

                if (eccezione == null)
                {
                    return Json(new { success = false, message = "Nessuna eccezione trovata per questo costo." }, JsonRequestBehavior.AllowGet);
                }

                var nomeProfessionista = eccezione.ID_Professionista.HasValue
                    ? db.OperatoriSinergia
                        .Where(o => o.ID_UtenteCollegato == eccezione.ID_Professionista && o.TipoCliente == "Professionista")
                        .Select(o => o.Nome + " " + o.Cognome)
                        .FirstOrDefault()
                    : null;

                var nomeTeam = eccezione.ID_Team.HasValue
                    ? db.TeamProfessionisti
                        .Where(t => t.ID_Team == eccezione.ID_Team)
                        .Select(t => t.Nome)
                        .FirstOrDefault()
                    : null;

                // 🔹 Aggiunte minime: ImportoOriginale (dal costo) + Delta
                decimal? importoOriginale = costo.Importo;
                decimal? nuovoImporto = eccezione.NuovoImporto;
                decimal? delta = (importoOriginale.HasValue && nuovoImporto.HasValue)
                    ? (nuovoImporto - importoOriginale)
                    : (decimal?)null;

                return Json(new
                {
                    success = true,
                    eccezione = new
                    {
                        eccezione.ID_Eccezione,
                        eccezione.Categoria,
                        NomeSoggetto = nomeProfessionista ?? nomeTeam ?? "-",
                        DataInizio = eccezione.DataInizio?.ToString("yyyy-MM-dd"),
                        DataFine = eccezione.DataFine?.ToString("yyyy-MM-dd"),
                        eccezione.Motivazione,
                        ImportoOriginale = importoOriginale,
                        NuovoImporto = nuovoImporto,
                        Delta = delta
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }



        [HttpPost]
        public ActionResult EliminaEccezione(int idEccezione)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare l'eccezione." });

                var eccezione = db.EccezioniRicorrenzeCosti.FirstOrDefault(e => e.ID_Eccezione == idEccezione);
                if (eccezione == null)
                    return Json(new { success = false, message = "Eccezione non trovata." });

                int ultimaVersione = db.EccezioniRicorrenzeCosti_a
                    .Where(a => a.ID_Eccezione == idEccezione)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // Archivia
                db.EccezioniRicorrenzeCosti_a.Add(new EccezioniRicorrenzeCosti_a
                {
                    ID_Eccezione = eccezione.ID_Eccezione,
                    ID_Professionista = eccezione.ID_Professionista,
                    ID_Team = eccezione.ID_Team,
                    ID_RicorrenzaCosto = eccezione.ID_RicorrenzaCosto,
                    Categoria = eccezione.Categoria,
                    DataInizio = eccezione.DataInizio,
                    NuovoImporto = eccezione.NuovoImporto,
                    DataFine = eccezione.DataFine,
                    Motivazione = eccezione.Motivazione,
                    ID_UtenteCreatore = eccezione.ID_UtenteCreatore,
                    DataCreazione = eccezione.DataCreazione,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑 Eliminazione effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                db.EccezioniRicorrenzeCosti.Remove(eccezione);

                // Aggiorna i costi generati (solo se rientrano nel periodo dell’eccezione)
                var costiDaAggiornare = db.GenerazioneCosti.Where(c =>
                    c.Categoria == eccezione.Categoria &&
                    (
                        (eccezione.ID_Professionista.HasValue && c.ID_Utente == eccezione.ID_Professionista) ||
                        (eccezione.ID_Team.HasValue && c.ID_Team == eccezione.ID_Team)
                    ) &&
                    c.DataRegistrazione >= eccezione.DataInizio &&
                    c.DataRegistrazione <= eccezione.DataFine
                ).ToList();

                foreach (var costo in costiDaAggiornare)
                {
                    costo.HaEccezione = false;
                    costo.ID_UtenteUltimaModifica = idUtenteCorrente;
                    costo.DataUltimaModifica = DateTime.Now;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Eccezione eliminata e costi aggiornati." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult EsportaGenerazioneCostiCsv(DateTime? dataDa, DateTime? dataA, int? idProfessionista, int? idTeam, string categoria, string ricorrenza, string stato, bool mostraEccezioni = false)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // Recupero lista richiamando direttamente la logica di GenerazioneCostiList
            var listaResult = GenerazioneCostiList(dataDa, dataA, idProfessionista, idTeam, categoria, ricorrenza, stato, mostraEccezioni) as PartialViewResult;
            var lista = listaResult?.Model as List<GenerazioneCostiViewModel> ?? new List<GenerazioneCostiViewModel>();

            var sb = new StringBuilder();
            sb.AppendLine("ID;Categoria;Descrizione;Importo;Periodicità;Data Registrazione;Origine;Stato;Professionista;Team;Pratica");

            foreach (var c in lista)
            {
                sb.AppendLine($"{c.ID_GenerazioneCosto};" +
                              $"{(string.IsNullOrWhiteSpace(c.Categoria) ? "-" : c.Categoria)};" +
                              $"{(string.IsNullOrWhiteSpace(c.Descrizione) ? "-" : c.Descrizione)};" +
                              $"{(c.Importo.HasValue ? c.Importo.Value.ToString("N2") : "0,00")};" +
                              $"{(string.IsNullOrWhiteSpace(c.Periodicita) ? "-" : c.Periodicita)};" +
                              $"{(c.DataRegistrazione.HasValue ? c.DataRegistrazione.Value.ToString("dd/MM/yyyy") : "-")};" +
                              $"{(string.IsNullOrWhiteSpace(c.Origine) ? "-" : c.Origine)};" +
                              $"{(string.IsNullOrWhiteSpace(c.Stato) ? "-" : c.Stato)};" +
                              $"{(string.IsNullOrWhiteSpace(c.NomeProfessionista) ? "-" : c.NomeProfessionista)};" +
                              $"{(string.IsNullOrWhiteSpace(c.Team) ? "-" : c.Team)};" +
                              $"{(string.IsNullOrWhiteSpace(c.TitoloPratica) ? "-" : c.TitoloPratica)}");
            }

            byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(buffer, "text/csv", $"GenerazioneCosti_{(dataDa ?? DateTime.Today):yyyyMMdd}_{(dataA ?? DateTime.Today):yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaGenerazioneCostiPdf(DateTime? dataDa, DateTime? dataA, int? idProfessionista, int? idTeam, string categoria, string ricorrenza, string stato, bool mostraEccezioni = false)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            var listaResult = GenerazioneCostiList(dataDa, dataA, idProfessionista, idTeam, categoria, ricorrenza, stato, mostraEccezioni) as PartialViewResult;
            var lista = listaResult?.Model as List<GenerazioneCostiViewModel> ?? new List<GenerazioneCostiViewModel>();

            return new Rotativa.ViewAsPdf("~/Views/GenerazioneCosti/ReportGenerazioneCostiPdf.cshtml", lista)
            {
                FileName = $"GenerazioneCosti_{(dataDa ?? DateTime.Today):yyyyMMdd}_{(dataA ?? DateTime.Today):yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }



        // qua ce il modifca lo lasciamo ma credo che non lo utilizzerò per facilità
        //[HttpPost]
        //public ActionResult ModificaEccezione(EccezioneRicorrenzaCostoViewModel model)
        //{
        //    if (model == null)
        //        return Json(new { success = false, message = "Dati mancanti." });

        //    if (string.IsNullOrEmpty(model.Categoria))
        //        return Json(new { success = false, message = "Categoria obbligatoria." });

        //    if (model.ID_Professionista == null && model.ID_Team == null)
        //        return Json(new { success = false, message = "Selezionare un professionista o un team." });

        //    if (model.DataInizio > model.DataFine)
        //        return Json(new { success = false, message = "La data di inizio non può essere successiva alla data di fine." });

        //    if (string.IsNullOrEmpty(model.Motivazione))
        //        model.Motivazione = "";

        //    int idUtente = UserManager.GetIDUtenteCollegato();
        //    DateTime now = DateTime.Now;

        //    try
        //    {
        //        using (var db = new SinergiaDB())
        //        {
        //            var esistente = db.EccezioniRicorrenzeCosti.FirstOrDefault(e => e.ID_Eccezione == model.ID_Eccezione);
        //            if (esistente == null)
        //                return Json(new { success = false, message = "Eccezione non trovata." });

        //            int ultimaVersione = db.EccezioniRicorrenzeCosti_a
        //                .Where(a => a.ID_Eccezione == esistente.ID_Eccezione)
        //                .OrderByDescending(a => a.NumeroVersione)
        //                .Select(a => (int?)a.NumeroVersione)
        //                .FirstOrDefault() ?? 0;

        //            List<string> modifiche = new List<string>();
        //            void Check(string campo, object oldVal, object newVal)
        //            {
        //                if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
        //                    modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
        //            }

        //            Check("ID_Professionista", esistente.ID_Professionista, model.ID_Professionista);
        //            Check("ID_Team", esistente.ID_Team, model.ID_Team);
        //            Check("ID_RicorrenzaCosto", esistente.ID_RicorrenzaCosto, model.ID_RicorrenzaCosto);
        //            Check("Categoria", esistente.Categoria, model.Categoria);
        //            Check("DataInizio", esistente.DataInizio, model.DataInizio);
        //            Check("DataFine", esistente.DataFine, model.DataFine);
        //            Check("Motivazione", esistente.Motivazione, model.Motivazione);

        //            // 🔁 Aggiorna l'entità principale
        //            esistente.ID_Professionista = model.ID_Professionista;
        //            esistente.ID_Team = model.ID_Team;
        //            esistente.ID_RicorrenzaCosto = model.ID_RicorrenzaCosto;
        //            esistente.Categoria = model.Categoria;
        //            esistente.DataInizio = model.DataInizio;
        //            esistente.DataFine = model.DataFine;
        //            esistente.Motivazione = model.Motivazione;
        //            esistente.ID_UtenteUltimaModifica = idUtente;
        //            esistente.DataUltimaModifica = now;

        //            // 💾 Aggiungi nuova versione
        //            db.EccezioniRicorrenzeCosti_a.Add(new EccezioniRicorrenzeCosti_a
        //            {
        //                ID_Eccezione = esistente.ID_Eccezione,
        //                ID_Professionista = esistente.ID_Professionista,
        //                ID_Team = esistente.ID_Team,
        //                ID_RicorrenzaCosto = esistente.ID_RicorrenzaCosto,
        //                Categoria = esistente.Categoria,
        //                DataInizio = esistente.DataInizio,
        //                DataFine = esistente.DataFine,
        //                Motivazione = esistente.Motivazione,
        //                ID_UtenteCreatore = esistente.ID_UtenteCreatore,
        //                DataCreazione = esistente.DataCreazione,
        //                ID_UtenteUltimaModifica = idUtente,
        //                DataUltimaModifica = now,
        //                NumeroVersione = ultimaVersione + 1,
        //                ModificheTestuali = modifiche.Any()
        //                    ? $"✏️ Modifica da ID_Utente {idUtente} il {now:g}:\n{string.Join("\n", modifiche)}"
        //                    : "✏️ Modifica senza variazioni rilevanti"
        //            });

        //            db.SaveChanges();
        //            return Json(new { success = true, message = "✅ Eccezione modificata correttamente." });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = "Errore: " + ex.Message });
        //    }
        //}

        #endregion

        #region PLAFOND
        public ActionResult GestionePlafond()
        {
            ViewBag.Title = "Gestione Plafond";
            return View("~/Views/Plafond/GestionePlafond.cshtml");
        }

        [HttpGet]
        public ActionResult GestionePlafondList(bool mostraPagamenti = false)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
                if (utenteCorrente == null)
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

                // ====================================================
                // 🔐 Permessi (VERSIONE CORRETTA)
                // ====================================================
                var permessiUtente = new PermessiViewModel
                {
                    Permessi = new List<PermessoSingoloViewModel>()
                };

                if (utenteCorrente.TipoUtente == "Admin")
                {
                    // L’admin ha sempre tutti i permessi
                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = true,
                        Modifica = true,
                        Elimina = true
                    });
                }
                else
                {
                    var permessiDb = db.Permessi
                        .Where(p => p.ID_Utente == idUtenteCorrente)
                        .ToList();

                    foreach (var p in permessiDb)
                    {
                        permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                        {
                            Aggiungi = p.Aggiungi ?? false,
                            Modifica = p.Modifica ?? false,
                            Elimina = p.Elimina ?? false
                        });
                    }
                }

                ViewBag.PuoModificare = permessiUtente.Permessi.Any(p => p.Modifica);
                ViewBag.PuoEliminare = permessiUtente.Permessi.Any(p => p.Elimina);
                ViewBag.MostraAzioni = (ViewBag.PuoModificare || ViewBag.PuoEliminare);
                ViewBag.Permessi = permessiUtente;

                // ====================================================
                // 👑 Filtro Professionista / Collaboratore / Impersonificazione
                // ====================================================
                int? idFiltro = null;

                if (utenteCorrente.TipoUtente == "Professionista")
                {
                    idFiltro = idUtenteCorrente;
                }
                else if (utenteCorrente.TipoUtente == "Collaboratore")
                {
                    idFiltro = db.RelazioneUtenti
                        .Where(r => r.ID_UtenteAssociato == idUtenteCorrente && r.Stato == "Attivo")
                        .Select(r => r.ID_Utente)
                        .FirstOrDefault();
                }
                else if (utenteCorrente.TipoUtente == "Admin")
                {
                    int? impersonato = Session["ID_UtenteImpers"] as int?;
                    if (impersonato.HasValue && impersonato.Value > 0)
                        idFiltro = impersonato.Value;
                }

                // ====================================================
                // 📦 Carica tabelle
                // ====================================================
                var finanziamenti = db.FinanziamentiProfessionisti.ToList();
                var versamenti = db.PlafondUtente.ToList();
                var costi = db.CostiPersonaliUtente.ToList();
                var pagamenti = db.GenerazioneCosti
                    .Where(g => g.Approvato == true && g.Stato == "Pagato" && g.ID_Utente.HasValue)
                    .ToList();

                // ====================================================
                // 🔗 Applica filtro professionista
                // ====================================================
                if (idFiltro.HasValue)
                {
                    int filter = idFiltro.Value;

                    // Recupero ID_Operatore associato (ID alternativo)
                    int? idOperatore = db.OperatoriSinergia
                        .Where(o => o.ID_UtenteCollegato == filter && o.TipoCliente == "Professionista")
                        .Select(o => (int?)o.ID_Operatore)
                        .FirstOrDefault();

                    // Finanziamenti personali
                    finanziamenti = finanziamenti
                        .Where(f => f.ID_Professionista == filter)
                        .ToList();

                    // Versamenti → match su ID_Utente O ID_Operatore
                    versamenti = versamenti
                        .Where(v =>
                            v.ID_Utente == filter ||
                            (idOperatore.HasValue && v.ID_Utente == idOperatore.Value)
                        )
                        .ToList();

                    // Costi personali
                    costi = costi
                        .Where(c => c.ID_Utente == filter)
                        .ToList();

                    // Pagamenti costi
                    pagamenti = pagamenti
                        .Where(p => p.ID_Utente == filter)
                        .ToList();
                }

                // ====================================================
                // 💰 Mappo Finanziamenti
                // ====================================================
                var listaFin = finanziamenti.Select(f => new FinanziamentiProfessionistiViewModel
                {
                    ID_Finanziamento = f.ID_Finanziamento,
                    NomeProfessionista = GetNomeProfessionista(f.ID_Professionista),
                    TipoPlafond = "Finanziamento",
                    Importo = f.Importo,
                    DataVersamento = f.DataVersamento ?? DateTime.MinValue,
                    PuoEliminare = ViewBag.PuoEliminare,
                    PuoModificare = ViewBag.PuoModificare
                }).ToList();

                // ====================================================
                // 💶 Mappo Versamenti (PlafondUtente)
                // ====================================================
                var listaInc = versamenti.Select(v => new FinanziamentiProfessionistiViewModel
                {
                    ID_Plafond = v.ID_PlannedPlafond,
                    NomeProfessionista = GetNomeProfessionista(v.ID_Utente),
                    TipoPlafond = v.TipoPlafond ?? "Incasso",
                    Importo = v.Importo,
                    DataVersamento = v.DataVersamento ?? DateTime.MinValue,
                    PuoEliminare = ViewBag.PuoEliminare,
                    PuoModificare = ViewBag.PuoModificare
                }).ToList();

                // ====================================================
                // 💸 Mappo Costi Personali
                // ====================================================
                var listaCosti = costi.Select(c => new FinanziamentiProfessionistiViewModel
                {
                    ID_CostoPersonale = c.ID_CostoPersonale,
                    NomeProfessionista = GetNomeProfessionista(c.ID_Utente),
                    TipoPlafond = "Costo Personale",
                    Importo = -(c.Importo ?? 0),
                    DataVersamento = c.DataInserimento,
                    PuoEliminare = ViewBag.PuoEliminare,
                    PuoModificare = ViewBag.PuoModificare
                }).ToList();

                // ====================================================
                // 🧾 Mappo Pagamenti (se richiesto)
                // ====================================================
                var listaPagamenti = new List<FinanziamentiProfessionistiViewModel>();

                if (mostraPagamenti)
                {
                    listaPagamenti = pagamenti.Select(g => new FinanziamentiProfessionistiViewModel
                    {
                        ID_Plafond = g.ID_GenerazioneCosto,
                        NomeProfessionista = GetNomeProfessionista(g.ID_Utente.Value),
                        TipoPlafond = "Pagamento Costo",
                        Importo = -(g.Importo ?? 0),
                        DataVersamento = g.DataRegistrazione,
                        PuoEliminare = ViewBag.PuoEliminare,
                        PuoModificare = ViewBag.PuoModificare
                    }).ToList();
                }

                // ====================================================
                // 📊 Unisco tutto
                // ====================================================
                var lista = listaFin
                    .Concat(listaInc)
                    .Concat(listaCosti)
                    .Concat(listaPagamenti)
                    .OrderByDescending(x => x.DataVersamento)
                    .ToList();

                ViewBag.TotalePlafond = lista.Sum(x => x.Importo);
                ViewBag.MostraPagamenti = mostraPagamenti;

                return PartialView("~/Views/Plafond/_GestionePlafondList.cshtml", lista);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante il caricamento del plafond: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }



        private string GetNomeProfessionista(int id)
        {
            System.Diagnostics.Trace.WriteLine("══════════════════════════════");
            System.Diagnostics.Trace.WriteLine($"🔎 [GetNomeProfessionista] CHIAMATA con ID = {id}");

            // 1️⃣ PRIMA controllo se è un ID_Operatore
            var operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == id);
            if (operatore != null)
            {
                System.Diagnostics.Trace.WriteLine($"🟡 ID = {id} è un OperatoreSinergia → ID_UtenteCollegato = {operatore.ID_UtenteCollegato}");

                var prof = db.Utenti.FirstOrDefault(u => u.ID_Utente == operatore.ID_UtenteCollegato);
                if (prof != null)
                {
                    string nome = $"{prof.Cognome} {prof.Nome}";
                    System.Diagnostics.Trace.WriteLine($"🟢 Nome trovato da OperatoriSinergia → {nome}");
                    return nome;
                }

                System.Diagnostics.Trace.WriteLine("❌ ERRORE: Operatore trovato ma utente collegato NON esiste!");
            }

            System.Diagnostics.Trace.WriteLine("⚠️ Non è un operatore → controllo in Utenti...");

            // 2️⃣ SOLO SE NON È OPERATORE, lo cerco come ID_Utente
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == id);
            if (utente != null)
            {
                string nome = $"{utente.Cognome} {utente.Nome}";
                System.Diagnostics.Trace.WriteLine($"🟢 Nome trovato in Utenti → {nome}");
                return nome;
            }

            System.Diagnostics.Trace.WriteLine("🔴 Nessun nome trovato");
            return "—";
        }



        [HttpPost]
        public ActionResult CreaFinanziamento(FinanziamentiProfessionisti model)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            var permessi = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
            bool puoAggiungere = utente.TipoUtente == "Admin" || permessi.Any(p => p.Aggiungi == true);
            if (!puoAggiungere)
                return Json(new { success = false, message = "Non hai i permessi per aggiungere finanziamenti." });

            try
            {
                System.Diagnostics.Debug.WriteLine("📥 [CreaFinanziamento] Dati ricevuti:");
                System.Diagnostics.Debug.WriteLine($"- ID_Professionista: {model.ID_Professionista}");
                System.Diagnostics.Debug.WriteLine($"- Importo: {model.Importo}");
                System.Diagnostics.Debug.WriteLine($"- DataVersamento: {model.DataVersamento?.ToString("yyyy-MM-dd") ?? "null"}");

                // ✅ Controllo sicurezza su date "minime" non valide
                if (model.DataVersamento.HasValue && model.DataVersamento.Value < new DateTime(1753, 1, 1))
                    model.DataVersamento = null;

                // ➕ Crea finanziamento
                var nuovo = new FinanziamentiProfessionisti
                {
                    ID_Professionista = model.ID_Professionista,
                    Importo = model.Importo,
                    DataVersamento = model.DataVersamento,
                    ID_UtenteCreatore = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    ID_UtenteUltimaModifica = idUtenteCorrente
                };

                db.FinanziamentiProfessionisti.Add(nuovo);
                db.SaveChanges();

                // 🔁 Versionamento archivio
                db.FinanziamentiProfessionisti_a.Add(new FinanziamentiProfessionisti_a
                {
                    ID_Finanziamento_Originale = nuovo.ID_Finanziamento,
                    ID_Professionista = nuovo.ID_Professionista,
                    Importo = nuovo.Importo,
                    DataVersamento = nuovo.DataVersamento,
                    ID_UtenteCreatore = idUtenteCorrente,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = 1,
                    Operazione = "Inserimento",
                    ModificheTestuali = $"Inserito finanziamento di {nuovo.Importo:N2}" +
                                        (nuovo.DataVersamento.HasValue ? $" il {nuovo.DataVersamento.Value:dd/MM/yyyy}" : ""),
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtenteCorrente
                });

                db.SaveChanges();

                // 🔁 Calcolo totale finanziamenti
                var finanziamentiTotali = db.FinanziamentiProfessionisti
                    .Where(f => f.ID_Professionista == model.ID_Professionista)
                    .Sum(f => (decimal?)f.Importo) ?? 0;

                // 🔁 Aggiorna o crea Plafond
                var plafondEsistente = db.PlafondUtente
                    .FirstOrDefault(p => p.ID_Utente == model.ID_Professionista);

                if (plafondEsistente != null)
                {
                    int ultimaVersione = db.PlafondUtente_a
                        .Where(p => p.ID_Utente == plafondEsistente.ID_Utente)
                        .Select(p => p.NumeroVersione)
                        .DefaultIfEmpty(0)
                        .Max();

                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_Utente = plafondEsistente.ID_Utente,
                        ImportoTotale = plafondEsistente.ImportoTotale,
                        TipoPlafond = plafondEsistente.TipoPlafond,
                        DataInizio = plafondEsistente.DataInizio,
                        DataFine = plafondEsistente.DataFine,
                        ID_UtenteCreatore = plafondEsistente.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataUltimaModifica = DateTime.Now,
                        NumeroVersione = ultimaVersione + 1,
                        Operazione = "Aggiornamento dopo finanziamento",
                        ModificheTestuali = $"Plafond aggiornato a {finanziamentiTotali:N2}",
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtenteCorrente
                    });

                    plafondEsistente.ImportoTotale = finanziamentiTotali;
                    plafondEsistente.DataUltimaModifica = DateTime.Now;
                    plafondEsistente.ID_UtenteUltimaModifica = idUtenteCorrente;
                }
                else
                {
                    var nuovoPlafond = new PlafondUtente
                    {
                        ID_Utente = model.ID_Professionista,
                        ImportoTotale = finanziamentiTotali,
                        TipoPlafond = "Investimento",
                        DataInizio = model.DataVersamento,
                        DataFine = null,
                        ID_UtenteCreatore = idUtenteCorrente,
                        DataUltimaModifica = DateTime.Now,
                        ID_UtenteUltimaModifica = idUtenteCorrente,
                        DataVersamento = model.DataVersamento,
                        DataInserimento = DateTime.Now,
                        ID_UtenteInserimento = idUtenteCorrente,
                        Importo = model.Importo,
                        Note = null,
                        ID_Incasso = null,
                        ID_Pratiche = null
                    };

                    db.PlafondUtente.Add(nuovoPlafond);
                    db.SaveChanges();

                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_Utente = nuovoPlafond.ID_Utente,
                        ImportoTotale = nuovoPlafond.ImportoTotale,
                        TipoPlafond = nuovoPlafond.TipoPlafond,
                        DataInizio = nuovoPlafond.DataInizio,
                        DataFine = nuovoPlafond.DataFine,
                        ID_UtenteCreatore = nuovoPlafond.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = nuovoPlafond.ID_UtenteUltimaModifica,
                        DataUltimaModifica = nuovoPlafond.DataUltimaModifica,
                        NumeroVersione = 1,
                        Operazione = "Inserimento",
                        ModificheTestuali = $"Creato nuovo plafond con importo {nuovoPlafond.ImportoTotale:N2}",
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        DataVersamento = nuovoPlafond.DataVersamento,
                        DataInserimento = nuovoPlafond.DataInserimento,
                        ID_UtenteInserimento = nuovoPlafond.ID_UtenteInserimento,
                        Importo = nuovoPlafond.Importo,
                        Note = nuovoPlafond.Note,
                        ID_Incasso = nuovoPlafond.ID_Incasso,
                        ID_Pratiche = nuovoPlafond.ID_Pratiche,
                        ID_PlannedPlafond_Originale = nuovoPlafond.ID_PlannedPlafond
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Finanziamento registrato e Plafond aggiornato." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore nel salvataggio finanziamento:");
                System.Diagnostics.Debug.WriteLine(ex.ToString());

                return Json(new
                {
                    success = false,
                    message = "Errore durante la creazione del finanziamento: " + ex.GetBaseException().Message
                });
            }
        }




        [HttpPost]
        public ActionResult ModificaFinanziamento(FinanziamentiProfessionisti model)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            var finanziamento = db.FinanziamentiProfessionisti.FirstOrDefault(f => f.ID_Finanziamento == model.ID_Finanziamento);
            if (finanziamento == null)
                return Json(new { success = false, message = "Finanziamento non trovato." });

            bool isAdmin = utente.TipoUtente == "Admin";
            var permessi = db.Permessi.Where(p => p.ID_Utente == idUtente).ToList();
            bool puoModificare = isAdmin || permessi.Any(p => p.Modifica == true);

            if (!isAdmin)
            {
                if (utente.TipoUtente == "Professionista" && finanziamento.ID_Professionista != idUtente)
                    return Json(new { success = false, message = "Non puoi modificare finanziamenti di altri professionisti." });

                if (utente.TipoUtente == "Collaboratore")
                {
                    var professionistiAssegnati = db.RelazioneUtenti
                        .Where(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo")
                        .Select(r => r.ID_Utente)
                        .ToList();

                    if (!professionistiAssegnati.Contains(finanziamento.ID_Professionista))
                        return Json(new { success = false, message = "Non hai accesso a questo finanziamento." });
                }
            }

            if (!puoModificare)
                return Json(new { success = false, message = "Non hai i permessi per modificare finanziamenti." });

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    // 🔍 Confronto dati finanziamento
                    var modifiche = new List<string>();
                    if (finanziamento.Importo != model.Importo)
                        modifiche.Add($"Importo: {finanziamento.Importo:N2} → {model.Importo:N2}");
                    if (finanziamento.DataVersamento?.Date != model.DataVersamento?.Date)
                        modifiche.Add($"DataVersamento: {finanziamento.DataVersamento:dd/MM/yyyy} → {model.DataVersamento:dd/MM/yyyy}");

                    int ultimaVersione = db.FinanziamentiProfessionisti_a
                        .Where(a => a.ID_Finanziamento_Originale == finanziamento.ID_Finanziamento)
                        .Select(a => (int?)a.NumeroVersione).Max() ?? 0;

                    db.FinanziamentiProfessionisti_a.Add(new FinanziamentiProfessionisti_a
                    {
                        ID_Finanziamento_Originale = finanziamento.ID_Finanziamento,
                        ID_Professionista = finanziamento.ID_Professionista,
                        Importo = finanziamento.Importo,
                        DataVersamento = finanziamento.DataVersamento ?? DateTime.MinValue, // fallback per sicurezza
                        ID_UtenteCreatore = finanziamento.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = finanziamento.ID_UtenteUltimaModifica,
                        DataUltimaModifica = finanziamento.DataUltimaModifica,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = ultimaVersione + 1,
                        ModificheTestuali = string.Join(" | ", modifiche),
                        Operazione = "Modifica"
                    });

                    // ✏️ Modifica
                    finanziamento.Importo = model.Importo;
                    finanziamento.DataVersamento = model.DataVersamento;
                    finanziamento.ID_UtenteUltimaModifica = idUtente;
                    finanziamento.DataUltimaModifica = DateTime.Now;

                    // 🔁 Ricalcolo e aggiornamento Plafond
                    var totale = db.FinanziamentiProfessionisti
                        .Where(f => f.ID_Professionista == finanziamento.ID_Professionista)
                        .Sum(f => (decimal?)f.Importo) ?? 0;

                    var plafond = db.PlafondUtente.FirstOrDefault(p => p.ID_Utente == finanziamento.ID_Professionista);
                    if (plafond != null)
                    {
                        decimal importoPrecedente = plafond.ImportoTotale;

                        var modifichePlafond = new List<string>();
                        if (importoPrecedente != totale)
                            modifichePlafond.Add($"ImportoTotale: {importoPrecedente:N2} → {totale:N2}");

                        if (modifichePlafond.Any())
                        {
                            int ultimaVers = db.PlafondUtente_a
                                .Where(a => a.ID_Utente == plafond.ID_Utente)
                                .Select(a => (int?)a.NumeroVersione).Max() ?? 0;

                            db.PlafondUtente_a.Add(new PlafondUtente_a
                            {
                                ID_Utente = plafond.ID_Utente,
                                ImportoTotale = importoPrecedente,
                                TipoPlafond = plafond.TipoPlafond,
                                DataInizio = plafond.DataInizio,
                                DataFine = plafond.DataFine,
                                ID_UtenteCreatore = plafond.ID_UtenteCreatore,
                                ID_UtenteUltimaModifica = plafond.ID_UtenteUltimaModifica,
                                DataUltimaModifica = plafond.DataUltimaModifica,
                                DataArchiviazione = DateTime.Now,
                                ID_UtenteArchiviazione = idUtente,
                                NumeroVersione = ultimaVers + 1,
                                ModificheTestuali = string.Join(" | ", modifichePlafond),
                                Operazione = "Modifica"
                            });
                        }

                        plafond.ImportoTotale = totale;
                        plafond.DataUltimaModifica = DateTime.Now;
                        plafond.ID_UtenteUltimaModifica = idUtente;
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "✅ Finanziamento e Plafond aggiornati con versione." });
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"❌ {e.PropertyName}: {e.ErrorMessage}")
                    .ToList();

                var fullErrorMessage = string.Join(" | ", errorMessages);
                return Json(new { success = false, message = "Errore di validazione: " + fullErrorMessage });
            }
        }





        [HttpGet]
        public ActionResult GetFinanziamento(int? id)
        {
            if (!id.HasValue)
                return Json(new { success = false, message = "ID mancante." }, JsonRequestBehavior.AllowGet);

            var f = db.FinanziamentiProfessionisti.FirstOrDefault(x => x.ID_Finanziamento == id.Value);
            if (f == null)
                return Json(new { success = false, message = "Finanziamento non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                finanziamento = new
                {
                    f.ID_Finanziamento,
                    f.ID_Professionista,
                    f.Importo,
                    DataVersamento = f.DataVersamento.HasValue
                        ? f.DataVersamento.Value.ToString("yyyy-MM-dd")
                        : null
                }
            }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaFinanziamento(int id)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.Find(idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            var finanziamento = db.FinanziamentiProfessionisti.FirstOrDefault(f => f.ID_Finanziamento == id);
            if (finanziamento == null)
                return Json(new { success = false, message = "Finanziamento non trovato." });

            var permessi = db.Permessi.Where(p => p.ID_Utente == idUtente).ToList();
            bool puoEliminare = permessi.Any(p => p.Elimina == true);
            bool autorizzato = false;

            if (utente.TipoUtente == "Admin")
                autorizzato = true;
            else if (utente.TipoUtente == "Professionista" && finanziamento.ID_Professionista == idUtente && puoEliminare)
                autorizzato = true;
            else if (utente.TipoUtente == "Collaboratore")
            {
                var assegnati = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo")
                    .Select(r => r.ID_Utente)
                    .ToList();

                if (assegnati.Contains(finanziamento.ID_Professionista) && puoEliminare)
                    autorizzato = true;
            }

            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per eliminare questo finanziamento." });

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    // 🔢 Numero versione precedente del finanziamento
                    int ultimaVersione = db.FinanziamentiProfessionisti_a
                        .Where(a => a.ID_Finanziamento_Originale == finanziamento.ID_Finanziamento)
                        .Select(a => (int?)a.NumeroVersione).Max() ?? 0;

                    // 🗂️ Archivia finanziamento eliminato
                    db.FinanziamentiProfessionisti_a.Add(new FinanziamentiProfessionisti_a
                    {
                        ID_Professionista = finanziamento.ID_Professionista,
                        Importo = finanziamento.Importo,
                        DataVersamento = (DateTime)finanziamento.DataVersamento,
                        ID_UtenteCreatore = finanziamento.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtente,
                        DataUltimaModifica = DateTime.Now,
                        ID_Finanziamento_Originale = finanziamento.ID_Finanziamento,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = ultimaVersione + 1,
                        ModificheTestuali = "❌ Eliminazione finanziamento",
                        Operazione = "Eliminazione"
                    });

                    db.FinanziamentiProfessionisti.Remove(finanziamento);
                    db.SaveChanges(); // Stato consistente prima di aggiornare il plafond

                    // 🔁 Ricalcola Plafond
                    var nuovoTotale = db.FinanziamentiProfessionisti
                        .Where(f => f.ID_Professionista == finanziamento.ID_Professionista)
                        .Sum(f => (decimal?)f.Importo) ?? 0;

                    var plafond = db.PlafondUtente.FirstOrDefault(p => p.ID_Utente == finanziamento.ID_Professionista);
                    if (plafond != null)
                    {
                        // 🔢 Versionamento plafond
                        int ultimaVersPlafond = db.PlafondUtente_a
                            .Where(a => a.ID_Utente == plafond.ID_Utente)
                            .Select(a => (int?)a.NumeroVersione).Max() ?? 0;

                        db.PlafondUtente_a.Add(new PlafondUtente_a
                        {
                            ID_Utente = plafond.ID_Utente,
                            ImportoTotale = plafond.ImportoTotale,
                            TipoPlafond = plafond.TipoPlafond,
                            DataInizio = plafond.DataInizio,
                            DataFine = plafond.DataFine,
                            ID_UtenteCreatore = plafond.ID_UtenteCreatore,
                            ID_UtenteUltimaModifica = idUtente,
                            DataUltimaModifica = DateTime.Now,
                            NumeroVersione = ultimaVersPlafond + 1,
                            Operazione = "Aggiornamento dopo eliminazione",
                            ModificheTestuali = $"❌ Eliminazione finanziamento, nuovo totale = {nuovoTotale:N2}",
                            DataArchiviazione = DateTime.Now,
                            ID_UtenteArchiviazione = idUtente
                        });

                        // 🔁 Aggiorna plafond attuale
                        plafond.ImportoTotale = nuovoTotale;
                        plafond.DataUltimaModifica = DateTime.Now;
                        plafond.ID_UtenteUltimaModifica = idUtente;

                        db.SaveChanges();
                    }

                    transaction.Commit();
                    return Json(new { success = true, message = "✅ Finanziamento eliminato e Plafond aggiornato con versionamento." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }




        [HttpPost]
        public ActionResult InserisciCostoPersonale(CostiPersonaliUtenteViewModel model)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            int idProfessionista = 0;

            if (utente.TipoUtente == "Professionista")
            {
                idProfessionista = utente.ID_Utente;
            }
            else if (utente.TipoUtente == "Collaboratore")
            {
                var rel = db.RelazioneUtenti
                    .FirstOrDefault(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo");

                if (rel == null)
                    return Json(new { success = false, message = "Nessun professionista assegnato." });

                idProfessionista = (int)rel.ID_Utente;
            }

            var plafond = db.PlafondUtente
                .FirstOrDefault(p => p.ID_Utente == idProfessionista && p.TipoPlafond == "Investimento");

            if (plafond == null)
                return Json(new { success = false, message = "Plafond non trovato per questo professionista." });

            var totaleCostiPersonali = db.CostiPersonaliUtente
                .Where(c => c.ID_Utente == idProfessionista)
                .Sum(c => (decimal?)c.Importo) ?? 0;

            if (totaleCostiPersonali + model.Importo > plafond.ImportoTotale)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ Il costo personale supera il plafond disponibile."
                });
            }

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    // ➕ Inserisci costo personale
                    var nuovoCosto = new CostiPersonaliUtente
                    {
                        ID_Utente = idProfessionista,
                        Descrizione = model.Descrizione,
                        Importo = model.Importo,
                        DataInserimento = DateTime.Now,
                        ID_UtenteCreatore = idUtente,
                        Approvato = false
                    };

                    db.CostiPersonaliUtente.Add(nuovoCosto);
                    db.SaveChanges(); // serve per ottenere ID_CostoPersonale

                    // 🗂️ Archivia anche in _a
                    db.CostiPersonaliUtente_a.Add(new CostiPersonaliUtente_a
                    {
                        IDVersioneCostoPersonale = nuovoCosto.ID_CostoPersonale,
                        ID_Utente = nuovoCosto.ID_Utente,
                        Descrizione = nuovoCosto.Descrizione,
                        Approvato = nuovoCosto.Approvato,
                        Importo = nuovoCosto.Importo,
                        DataInserimento = nuovoCosto.DataInserimento,
                        ID_UtenteCreatore = (int)nuovoCosto.ID_UtenteCreatore,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = 1,
                        ModificheTestuali = "➕ Costo personale inserito"
                    });

                    // ➕ Inserisci voce collegata nel PlafondUtente
                    var nuovaVocePlafond = new PlafondUtente
                    {
                        ID_Utente = idProfessionista,
                        ImportoTotale = model.Importo,
                        TipoPlafond = "Costo Personale",
                        DataInizio = DateTime.Now,
                        ID_UtenteCreatore = idUtente,
                        ID_CostoPersonale = nuovoCosto.ID_CostoPersonale
                    };
                    db.PlafondUtente.Add(nuovaVocePlafond);
                    db.SaveChanges();

                    // 🔁 Archivia versione plafond collegata
                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_PlannedPlafond_Originale = nuovaVocePlafond.ID_PlannedPlafond,
                        ID_Utente = nuovaVocePlafond.ID_Utente,
                        ImportoTotale = nuovaVocePlafond.ImportoTotale,
                        TipoPlafond = nuovaVocePlafond.TipoPlafond,
                        DataInizio = nuovaVocePlafond.DataInizio,
                        DataFine = nuovaVocePlafond.DataFine,
                        ID_UtenteCreatore = nuovaVocePlafond.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtente,
                        DataUltimaModifica = DateTime.Now,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = 1,
                        Operazione = "Costo Personale",
                        ModificheTestuali = $"➕ Inserito costo personale '{model.Descrizione}' da {model.Importo:N2}."
                    });

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "✅ Costo personale inserito e versionato correttamente." });
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"{e.PropertyName}: {e.ErrorMessage}");

                var fullErrorMessage = string.Join("; ", errorMessages);
                return Json(new { success = false, message = "Errore durante l'inserimento: " + fullErrorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore generico: " + ex.Message });
            }
        }
        [HttpPost]
        public ActionResult EliminaCostoPersonale(int id)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.Find(idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            var costo = db.CostiPersonaliUtente.FirstOrDefault(c => c.ID_CostoPersonale == id);
            if (costo == null)
                return Json(new { success = false, message = "Costo personale non trovato." });

            var permessi = db.Permessi.Where(p => p.ID_Utente == idUtente).ToList();
            bool puoEliminare = permessi.Any(p => p.Elimina == true);
            bool autorizzato = false;

            if (utente.TipoUtente == "Admin")
                autorizzato = true;
            else if (utente.TipoUtente == "Professionista" && costo.ID_Utente == idUtente && puoEliminare)
                autorizzato = true;
            else if (utente.TipoUtente == "Collaboratore")
            {
                var assegnati = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo")
                    .Select(r => r.ID_Utente)
                    .ToList();

                if (assegnati.Contains(costo.ID_Utente) && puoEliminare)
                    autorizzato = true;
            }

            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per eliminare questo costo personale." });

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    // 📁 Versionamento costo personale
                    int ultimaVersioneCosto = db.CostiPersonaliUtente_a
                        .Where(a => a.ID_CostoPersonale == costo.ID_CostoPersonale)
                        .Select(a => (int?)a.NumeroVersione).Max() ?? 0;

                    db.CostiPersonaliUtente_a.Add(new CostiPersonaliUtente_a
                    {
                        ID_CostoPersonale = costo.ID_CostoPersonale,
                        ID_Utente = costo.ID_Utente,
                        Descrizione = costo.Descrizione,
                        Importo = costo.Importo,
                        DataInserimento = costo.DataInserimento,
                        Approvato = costo.Approvato,
                        ID_UtenteCreatore = (int)costo.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = idUtente,
                        DataUltimaModifica = DateTime.Now,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = ultimaVersioneCosto + 1,
                        ModificheTestuali = "❌ Eliminazione costo personale"
                    });

                    // 🔎 Cerca riga in PlafondUtente legata al costo personale
                    var vocePlafond = db.PlafondUtente.FirstOrDefault(p => p.ID_CostoPersonale == costo.ID_CostoPersonale);

                    if (vocePlafond != null)
                    {
                        // 🗂️ Versionamento riga plafond
                        int ultimaVersionePlafond = db.PlafondUtente_a
                            .Where(p => p.ID_PlannedPlafond_Originale == vocePlafond.ID_PlannedPlafond)
                            .Select(p => (int?)p.NumeroVersione).Max() ?? 0;

                        db.PlafondUtente_a.Add(new PlafondUtente_a
                        {
                            ID_PlannedPlafond_Originale = vocePlafond.ID_PlannedPlafond,
                            ID_Utente = vocePlafond.ID_Utente,
                            ImportoTotale = vocePlafond.ImportoTotale,
                            TipoPlafond = vocePlafond.TipoPlafond,
                            DataInizio = vocePlafond.DataInizio,
                            DataFine = vocePlafond.DataFine,
                            ID_UtenteCreatore = vocePlafond.ID_UtenteCreatore,
                            ID_UtenteUltimaModifica = idUtente,
                            DataUltimaModifica = DateTime.Now,
                            DataArchiviazione = DateTime.Now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = ultimaVersionePlafond + 1,
                            Operazione = "❌ Costo Personale Eliminato",
                            ModificheTestuali = $"❌ Eliminato collegamento al costo personale '{costo.Descrizione}' da {costo.Importo:N2}."
                        });

                        db.PlafondUtente.Remove(vocePlafond);
                    }

                    // 🗑️ Elimina costo personale
                    db.CostiPersonaliUtente.Remove(costo);
                    db.SaveChanges();

                    transaction.Commit();
                    return Json(new { success = true, message = "✅ Costo personale e voce plafond eliminati con versionamento." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }




        [HttpPost]
        public JsonResult EliminaVersamentoPlafond(int id)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    var versamento = db.PlafondUtente.FirstOrDefault(p => p.ID_PlannedPlafond == id);
                    if (versamento == null)
                        return Json(new { success = false, message = "Versamento non trovato." });

                    db.PlafondUtente.Remove(versamento);
                    db.SaveChanges();

                    return Json(new { success = true, message = "Versamento eliminato con successo." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione." });
            }
        }
        // usare in caso filtro totale plafond professionisti

        //[HttpGet]
        //public ActionResult GetTotalePlafond(int idProfessionista)
        //{
        //    using (var db = new SinergiaDB())
        //    {
        //        var finanziamenti = db.FinanziamentiProfessionisti
        //            .Where(f => f.ID_Professionista == idProfessionista)
        //            .Select(f => f.Importo).DefaultIfEmpty(0).Sum();

        //        var incassi = db.PlafondUtente
        //            .Where(p => p.ID_Utente == idProfessionista)
        //            .Select(p => p.Importo).DefaultIfEmpty(0).Sum();

        //        decimal totale = finanziamenti + incassi;

        //        return Json(new { success = true, totale = totale.ToString("N2") }, JsonRequestBehavior.AllowGet);
        //    }
        //}
        [HttpGet]
        public JsonResult GetPlafondDisponibile()
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();

                using (var db = new SinergiaDB())
                {
                    // 🔍 1. Verifica se l’utente ha anche un OperatoreSinergia associato
                    var operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_UtenteCollegato == idUtente);
                    int? idCliente = operatore?.ID_Operatore;

                    // 🔢 2. Recupera tutti i plafond associati sia come utente diretto che da OperatoreSinergia
                    var plafondTotale = db.PlafondUtente
                        .Where(p => p.ID_Utente == idUtente || (idCliente != null && p.ID_Utente == idCliente))
                        .Sum(p => (decimal?)p.Importo) ?? 0;

                    // 🔻 3. Sottrai eventuali Costi Personali registrati
                    var costiPersonali = db.CostiPersonaliUtente
                        .Where(c => c.ID_Utente == idUtente || (idCliente != null && c.ID_Utente == idCliente))
                        .Sum(c => (decimal?)c.Importo) ?? 0;

                    var disponibile = plafondTotale - costiPersonali;

                    return Json(new { success = true, plafond = disponibile.ToString("N2") + " €" }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }



        #endregion

        #region TEMPLATE INCARICHI 

        public ActionResult GestioneIncarichi()
        {
            return View("~/Views/TemplateIncarichi/GestioneTemplateIncarichi.cshtml");
        }

        public ActionResult GestioneTemplateIncarichiList()
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

                // 🔎 Query base: tutti i template non eliminati
                IQueryable<TemplateIncarichi> query = db.TemplateIncarichi
                    .Where(t => t.Stato != "Eliminato");

                // 🔐 Gestione Permessi
                bool puoAggiungere = false;
                bool puoModificare = false;
                bool puoEliminare = false;

                if (utenteCorrente.TipoUtente == "Admin")
                {
                    puoAggiungere = puoModificare = puoEliminare = true;
                }
                else if (utenteCorrente.TipoUtente == "Professionista" || utenteCorrente.TipoUtente == "Collaboratore")
                {
                    var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
                    puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
                    puoModificare = permessiDb.Any(p => p.Modifica == true);
                    puoEliminare = permessiDb.Any(p => p.Elimina == true);
                }

                // 🔄 Proiezione nel ViewModel
                var lista = query
                    .OrderBy(t => t.NomeTemplate)
                    .ToList()
                    .Select(t => new TemplateIncaricoViewModel
                    {
                        IDTemplateIncarichi = t.IDTemplateIncarichi,
                        NomeTemplate = t.NomeTemplate,
                        ContenutoHtml = t.ContenutoHtml,
                        Stato = t.Stato,
                        TipoCompenso = t.TipoCompenso, // 👈 ora mostriamo il tipo di compenso
                        PuoModificare = puoModificare,
                        PuoEliminare = puoEliminare
                    })
                    .ToList();

                ViewBag.PuoAggiungere = puoAggiungere;
                ViewBag.Permessi = new PermessiViewModel
                {
                    ID_Utente = utenteCorrente.ID_Utente,
                    NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
                    Permessi = new List<PermessoSingoloViewModel>
                    {
                        new PermessoSingoloViewModel
                        {
                            Aggiungi = puoAggiungere,
                            Modifica = puoModificare,
                            Elimina = puoEliminare
                        }
                    }
                };

                return PartialView("~/Views/TemplateIncarichi/_GestioneTemplateIncarichiList.cshtml", lista);
            }



        [HttpGet]
        public ActionResult CreaTemplateIncarico()
        {
            var professioni = db.Professioni
                .OrderBy(p => p.Descrizione)
                .Select(p => new SelectListItem
                {
                    Value = p.ProfessioniID.ToString(),
                    Text = (p.Codice ?? "") + " - " + p.Descrizione
                })
                .ToList();

            ViewBag.Professioni = professioni;

            ViewBag.TipiCompenso = new List<SelectListItem>
            {
                new SelectListItem { Value = "Fisso", Text = "Fisso" },
                new SelectListItem { Value = "A ore", Text = "A ore" },
                new SelectListItem { Value = "Giudiziale", Text = "Giudiziale" }
            };


            return View("~/Views/TemplateIncarichi/CreaTemplateIncarico.cshtml"); // Assicurati che la view abbia questo nome
        }


        [HttpPost]
        public ActionResult CreaTemplateIncarico(TemplateIncaricoViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi obbligatori." });

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                               db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Aggiungi == true);

            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per creare un nuovo template." });

            try
            {
                var nuovo = new TemplateIncarichi
                {
                    NomeTemplate = model.NomeTemplate?.Trim(),
                    ContenutoHtml = model.ContenutoHtml,
                    Stato = model.Stato?.Trim() ?? "Attivo",
                    ID_Professione = model.ID_Professione,
                    TipoCompenso = model.TipoCompenso
                };

                db.TemplateIncarichi.Add(nuovo);
                db.SaveChanges();

                // 🔁 Archivio
                db.TemplateIncarichi_a.Add(new TemplateIncarichi_a
                {
                    ID_Archivio = nuovo.IDTemplateIncarichi,
                    NomeTemplate = nuovo.NomeTemplate,
                    ContenutoHtml = nuovo.ContenutoHtml,
                    Stato = nuovo.Stato,
                    ID_Professione = nuovo.ID_Professione,
                    TipoCompenso = nuovo.TipoCompenso,
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"✅ Inserimento effettuato da utente ID = {idUtenteCorrente} il {DateTime.Now:g}",
                });

                db.SaveChanges();

                return RedirectToAction("GestioneIncarichi");

            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante la creazione del template: " + ex.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetTemplateIncarico(int id)
        {
            var template = db.TemplateIncarichi
                .FirstOrDefault(t => t.IDTemplateIncarichi == id && t.Stato == "Attivo");

            if (template == null)
                return Json(new { success = false, message = "Template non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                template = new
                {
                    IDTemplateIncarichi = template.IDTemplateIncarichi,
                    NomeTemplate = template.NomeTemplate,
                    TipoCompenso = template.TipoCompenso,
                    ContenutoHtml = template.ContenutoHtml
                }
            }, JsonRequestBehavior.AllowGet);
        }



        [HttpPost]
        public ActionResult ModificaTemplateIncarico(TemplateIncaricoViewModel model)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var template = db.TemplateIncarichi.FirstOrDefault(t => t.IDTemplateIncarichi == model.IDTemplateIncarichi);
            if (template == null)
                return Json(new { success = false, message = "Template non trovato." });

            template.NomeTemplate = model.NomeTemplate?.Trim();
            template.ContenutoHtml = model.ContenutoHtml;
            db.SaveChanges();

            // Archivio
            int ultimaVersione = db.TemplateIncarichi_a
                .Where(a => a.ID_Archivio == model.IDTemplateIncarichi)
                .Select(a => a.NumeroVersione)
                .DefaultIfEmpty(0)
                .Max();

            db.TemplateIncarichi_a.Add(new TemplateIncarichi_a
            {
                ID_Archivio = model.IDTemplateIncarichi,
                NomeTemplate = template.NomeTemplate,
                ContenutoHtml = template.ContenutoHtml,
                Stato = template.Stato,
                ID_Professione = template.ID_Professione,
                TipoCompenso = template.TipoCompenso,
                NumeroVersione = ultimaVersione + 1,
                DataArchiviazione = DateTime.Now,
                ID_UtenteArchiviazione = idUtente,
                ModificheTestuali = $"✏️ Modificato da utente {idUtente} il {DateTime.Now:g}"
            });
            db.SaveChanges();

            return Json(new { success = true, message = "✅ Template modificato correttamente!" });
        }



        [HttpPost]
        public ActionResult EliminaTemplateIncarico(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool autorizzato = utenteCorrente.TipoUtente == "Admin" ||
                                   db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!autorizzato)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare questo template." });

                var template = db.TemplateIncarichi.FirstOrDefault(t => t.IDTemplateIncarichi == id);
                if (template == null)
                    return Json(new { success = false, message = "Template incarico non trovato." });

                // 🔢 Recupera ultima versione
                int ultimaVersione = db.TemplateIncarichi_a
                    .Where(a => a.ID_Archivio == template.IDTemplateIncarichi)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 🗂️ Archivia lo stato eliminato
                db.TemplateIncarichi_a.Add(new TemplateIncarichi_a
                {
                    ID_Archivio = template.IDTemplateIncarichi,
                    NomeTemplate = template.NomeTemplate,
                    ContenutoHtml = template.ContenutoHtml,
                    Stato = "Eliminato",
                    ID_Professione = template.ID_Professione,
                    TipoCompenso= template.TipoCompenso,
                    NumeroVersione = ultimaVersione + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtenteCorrente,
                    ModificheTestuali = $"Eliminazione effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}"
                });

                // 🔄 Soft delete
                template.Stato = "Eliminato";

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Template incarico eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        public ActionResult GeneraPdfIncarico(int idPratica, int idTemplate)
        {
            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
            var template = db.TemplateIncarichi.FirstOrDefault(t => t.IDTemplateIncarichi == idTemplate && t.Stato == "Attivo");

            if (pratica == null || template == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Dati mancanti o non validi.");

            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
            var professionista = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == pratica.ID_UtenteResponsabile && o.TipoCliente == "Professionista");

            string htmlCompilato = template.ContenutoHtml
                .Replace("{{NomeCliente}}", (cliente?.Nome + " " + cliente?.Cognome)?.Trim() ?? "")
                .Replace("{{TitoloPratica}}", pratica.Titolo ?? "")
                .Replace("{{DataPratica}}", pratica.DataCreazione?.ToString("dd/MM/yyyy") ?? "")
                .Replace("{{Professionista}}", professionista?.Nome ?? "")
                .Replace("{{Budget}}", pratica.Budget.ToString("N2"))
                .Replace("{{Note}}", pratica.Note ?? "");

            return new Rotativa.ViewAsPdf("TemplateCompilato", (object)htmlCompilato)
            {
                FileName = "Incarico_" + pratica.Titolo.Replace(" ", "_") + ".pdf",
                PageSize = Rotativa.Options.Size.A4,
                CustomSwitches = "--disable-smart-shrinking"
            };
        }

        [HttpPost]
        public ActionResult UploadTemplateDocx(HttpPostedFileBase file, string tipoCompenso)
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, message = "❌ Nessun file caricato." });

            if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "❌ Caricare solo file in formato DOCX." });

            if (string.IsNullOrWhiteSpace(tipoCompenso))
                return Json(new { success = false, message = "❌ Devi selezionare un tipo di compenso." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            if (idUtente <= 0)
                return Json(new { success = false, message = "❌ Utente non autenticato." });

            try
            {
                string htmlContent;

                using (var ms = new MemoryStream())
                {
                    file.InputStream.CopyTo(ms);
                    ms.Position = 0;

                    using (WordprocessingDocument doc = WordprocessingDocument.Open(ms, true))
                    {
                        var settings = new HtmlConverterSettings()
                        {
                            PageTitle = "Template incarico importato",
                            FabricateCssClasses = true,
                            CssClassPrefix = "docx"
                        };

                        // === Corpo documento principale ===
                        XElement html = HtmlConverter.ConvertToHtml(doc, settings);

                        var styles = html.Descendants()
                                         .FirstOrDefault(x => x.Name.LocalName == "head")
                                         ?.ToString(SaveOptions.DisableFormatting) ?? "";

                        var body = html.Descendants()
                                       .FirstOrDefault(x => x.Name.LocalName == "body");

                        var bodyContent = body != null
                            ? string.Join("", body.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)))
                            : "";
                        // === HEADER e FOOTER ===
                        string headerHtml = "";
                        string footerHtml = "";

                        var mainPart = doc.MainDocumentPart;
                        if (mainPart != null)
                        {
                            // 🔹 HEADER
                            foreach (var headerPart in mainPart.HeaderParts)
                            {
                                try
                                {
                                    var headerParagraphs = headerPart.Header.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
                                    if (headerParagraphs != null && headerParagraphs.Any())
                                    {
                                        headerHtml += "<div class='docx-header' style='text-align:center; margin-bottom:20px;'>";
                                        foreach (var p in headerParagraphs)
                                        {
                                            var runs = p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>();
                                            string text = string.Join("", runs.Select(r => r.Text));
                                            if (!string.IsNullOrWhiteSpace(text))
                                                headerHtml += $"<p style='margin:0; line-height:1.2;'>{System.Net.WebUtility.HtmlEncode(text)}</p>";
                                        }
                                        headerHtml += "</div>";
                                    }
                                }
                                catch { /* ignora se non esiste */ }
                            }

                            // 🔹 FOOTER (solo il primo trovato)
                            var footerPart = mainPart.FooterParts.FirstOrDefault();
                            if (footerPart != null)
                            {
                                try
                                {
                                    var footerParagraphs = footerPart.Footer.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>();
                                    if (footerParagraphs != null && footerParagraphs.Any())
                                    {
                                        footerHtml += "<div class='docx-footer' style='text-align:center; margin-top:25px; font-size:11pt; line-height:1.3;'>";
                                        foreach (var p in footerParagraphs)
                                        {
                                            var runs = p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>();
                                            string text = string.Join("", runs.Select(r => r.Text));
                                            if (!string.IsNullOrWhiteSpace(text))
                                                footerHtml += $"<p style='margin:0;'>{System.Net.WebUtility.HtmlEncode(text)}</p>";
                                        }
                                        footerHtml += "</div>";
                                    }
                                }
                                catch { /* ignora se non esiste footer */ }
                            }
                        }

                        // === Componi tutto l’HTML ===
                        htmlContent = styles + headerHtml + bodyContent + footerHtml;

                        // === Normalizzazione elenchi numerati (post-conversione) ===
                        htmlContent = Regex.Replace(
                            htmlContent,
                            @"(<p[^>]*>\s*)(\d{1,2}\.)\s*(.*?)<\/p>",
                            "<ol style='margin-left:25px; padding-left:10px;'><li>$3</li></ol>",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline
                        );

                        // Rimuovi eventuali <ol> consecutivi duplicati
                        htmlContent = Regex.Replace(
                            htmlContent,
                            @"<\/ol>\s*<ol[^>]*>",
                            "",
                            RegexOptions.IgnoreCase
                        );
                    }
                }

                if (string.IsNullOrWhiteSpace(htmlContent))
                    return Json(new { success = false, message = "❌ Conversione fallita: HTML vuoto." });

                // ============================================
                // 🔁 Placeholder comuni
                // ============================================
                var placeholders = new List<KeyValuePair<string, string>>
        {
            // === CLIENTE ===
            new KeyValuePair<string, string>(@"\bN\s*O\s*M\s*E\s+C\s*O\s*G\s*N\s*O\s*M\s*E\b", "[NOME COGNOME]"),
            new KeyValuePair<string, string>(@"\bC\.?\s*F\.?\s*CLIENTE\b", "[CF CLIENTE]"),
            new KeyValuePair<string, string>(@"\bRAGIONE\s*SOCIALE\b", "[RAGIONE SOCIALE]"),
         // Sostituisce solo se "via" è preceduta da "residente in" o "alla"
            new KeyValuePair<string, string>(@"(?<=\bresidente\s+in\s+|alla\s+via\s+)\bvia\b", "[INDIRIZZO CLIENTE]"),
            new KeyValuePair<string, string>(@"\bProvincia\b|\(Provincia\)", "[PROVINCIA CLIENTE]"),

            
                // === DATI ANAGRAFICI CLIENTE ===
             // === CITTA: sostituzione contestuale e sicura ===

                // === CITTA (robusto per conversione Word in HTML) ===

                // 🔸 Città di nascita → "nato a CITTA"
                new KeyValuePair<string, string>(
                    @"(?<=\bnato\s+a\s+)(?:<[^>]+>)*C\s*I\s*T\s*T\s*A\b",
                    "[CITTA_NASCITA]"
                ),

                // 🔸 Città di residenza → "residente in CITTA"
                new KeyValuePair<string, string>(
                    @"(?<=\bresidente\s+in\s+)(?:<[^>]+>)*C\s*I\s*T\s*T\s*A\b",
                    "[CITTA CLIENTE]"
                ),

                // 🔸 Città sede legale → "con sede legale in CITTA"
                new KeyValuePair<string, string>(
                    @"(?<=\bcon\s+sede\s+legale\s+in\s+)(?:<[^>]+>)*C\s*I\s*T\s*T\s*A\b",
                    "[CITTA CLIENTE]"
                ),

                new KeyValuePair<string, string>(@"\bDATA\s*(DI\s*)?NASCITA\b", "[DATA_NASCITA]"),

                // === INDIRIZZO CLIENTE ===
                new KeyValuePair<string, string>(
                    @"(INDIRIZZO|VIA|V\.)(?:\s|&nbsp;|<[^>]*>)*CLIENTE",
                    "[INDIRIZZO CLIENTE]"
                ),

                new KeyValuePair<string, string>(
                    @"P(?:\s|<[^>]+>)*A(?:\s|<[^>]+>)*R(?:\s|<[^>]+>)*T(?:\s|<[^>]+>)*I(?:\s|<[^>]+>)*T(?:\s|<[^>]+>)*A(?:\s|<[^>]+>)*" +
                    @"(?:I(?:\s|<[^>]+>)*V(?:\s|<[^>]+>)*A)?(?:\s|<[^>]+>)*C(?:\s|<[^>]+>)*L(?:\s|<[^>]+>)*I(?:\s|<[^>]+>)*E(?:\s|<[^>]+>)*N(?:\s|<[^>]+>)*T(?:\s|<[^>]+>)*E",
                    "[PARTITA IVA CLIENTE]"
                ),


            // === PROFESSIONISTA RESPONSABILE ===
            new KeyValuePair<string, string>(@"\bProfessionista\s*Responsabile\b", "[PROFESSIONISTA RESPONSABILE]"),
            new KeyValuePair<string, string>(@"\bCodice\s*Fiscale\s*Responsabile\b", "[CF PROFESSIONISTA]"),
            new KeyValuePair<string, string>(@"\bPartita\s*IVA\s*Responsabile\b", "[P.IVA PROFESSIONISTA]"),
            new KeyValuePair<string, string>(@"\bIndirizzo\s*(Responsabile|Responsanile)\b", "[INDIRIZZO PROFESSIONISTA]"),

            // === LOGO ===
            new KeyValuePair<string, string>(@"\bLogo\s*Responsabile\b", "[LOGO_RESPONSABILE]"),

            // === GENERICI ===
            new KeyValuePair<string, string>(@"\[DATA\s*GENERAZIONE\]", "[DATA_GENERAZIONE]"),
            new KeyValuePair<string, string>(@"\bDATA_GENERAZIONE\b", "[DATA_GENERAZIONE]"),
            new KeyValuePair<string, string>(@"\[PROGRESSIVO\]", "[PROGRESSIVO]")
        };

                // ============================================
                // 🔁 Placeholder specifici per tipo compenso
                // ============================================
                if (tipoCompenso.Equals("A ore", StringComparison.OrdinalIgnoreCase))
                {
                    // ============================================
                    // 🔹 Placeholder specifici per il compenso "A ore"
                    // ============================================
                    var segnapostiAore = new List<KeyValuePair<string, string>>
                        {
//                      // === CITTA del cliente (riconosce anche 'Citt.' o 'Città' prima) ===
//// === CITTA del cliente (versione definitiva, gestisce tutti i casi reali da Word) ===
//new KeyValuePair<string, string>(
//    @"(?:Citt(?:[àa’']|\.)?(?:\s|&nbsp;|<[^>]+>|[\r\n])*)?(?:<[^>]+>)*C\s*I\s*T\s*T\s*A\b",
//    "[CITTA CLIENTE]"
//),


                         //// === Via / Indirizzo del cliente ===
                         //       new KeyValuePair<string, string>(
                         //           @"INDIRIZZO(?:\s|&nbsp;|<[^>]*>)*CLIENTE",
                         //           "[INDIRIZZO CLIENTE]"
                         //       ),

                            // === Provincia del cliente ===
                            new KeyValuePair<string, string>(
                                @"\bPROVINCIA\s+CLIENTE\b|\[\[?PROVINCIA[_\s]*CLIENTE\]?\]?",
                                "[PROVINCIA CLIENTE]"
                            ),

                            // === Codice Fiscale Cliente (mantiene "C.F.") ===
                            new KeyValuePair<string, string>(
                                @"(?<=C\.?\s*F\.?\s*)CLIENTE|\bCODICE\s*FISCALE\s*CLIENTE\b",
                                "[CF CLIENTE]"
                            ),

                            // === Partita IVA Cliente (mantiene "P.IVA" o sostituisce "PARTITA IVA") ===
                            new KeyValuePair<string, string>(
                                @"(?<=P\.?\s*IVA\s*)CLIENTE|\bPARTITA\s*IVA\s*CLIENTE\b",
                                "[PARTITA IVA CLIENTE]"
                            ),

                            // === Oggetto dell’incarico ===
                            new KeyValuePair<string, string>(
                                @"\bOGGETTO\s*(DELL[’']|DELL’|DI)\s*INCARICO\b|\[\[?OGGETTO[_\s]*INCARICO\]?\]?",
                                "[OGGETTO_INCARICO]"
                            ),

                            // === Descrizione ruolo (Campo A) ===
                            new KeyValuePair<string, string>(
                                @"\bCAMPO\s*A\b|\[\[?DESCRIZIONE[_\s]*RUOLO\]?\]?",
                                "[DESCRIZIONE_RUOLO]"
                            ),

                            // === Importo orario (Campo B) ===
                            new KeyValuePair<string, string>(
                                @"\bCAMPO\s*B\b|\bIMPORTO\s*ORARIO\b|\[\[?IMPORTO[_\s]*ORARIO\]?\]?",
                                "[IMPORTO_ORARIO]"
                            ),

                            // === Numero progressivo (Campo C) ===
                            new KeyValuePair<string, string>(
                                @"\bCAMPO\s*C\b|\[\[?NUMERO[_\s]*PROGRESSIVO\]?\]?",
                                "[NUMERO_PROGRESSIVO]"
                            ),

                            // === Data generazione ===
                            new KeyValuePair<string, string>(
                                @"DATA[_\s]*GENERAZIONE",
                                "[DATA_GENERAZIONE]"
                            )
                        };

                    // 🔁 Aggiungi solo i nuovi placeholder se non già presenti nei generali
                    foreach (var kv in segnapostiAore)
                    {
                        if (!placeholders.Any(p => p.Value == kv.Value))
                            placeholders.Add(kv);
                    }

                    // === Allineamento automatico dell'importo orario a destra ===
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"<td[^>]*>\s*\[IMPORTO_ORARIO\]\s*</td>",
                        "<td style='text-align:right;'>[IMPORTO_ORARIO]</td>",
                        RegexOptions.IgnoreCase
                    );
                    //// 🧩 Fix finale per casi come "INDIRIZZO <span>CLIENTE</span>"
                    //htmlContent = Regex.Replace(
                    //    htmlContent,
                    //    @"INDIRIZZO(?:\s|&nbsp;|<[^>]*>)*CLIENTE",
                    //    "[INDIRIZZO CLIENTE]",
                    //    RegexOptions.IgnoreCase
                    //);


                    // === Normalizzazione eventuali doppie parentesi ([[...]] → [...]) ===
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"\[\[([A-Z_ ]+)\]\]",
                        "[$1]",
                        RegexOptions.IgnoreCase
                    );
                }

                else if (tipoCompenso.Equals("Fisso", StringComparison.OrdinalIgnoreCase))
                {
                    placeholders.Add(new KeyValuePair<string, string>(@"\bCampo\s*A\b", "[DESCRIZIONE_ATTIVITA]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\bCampo\s*B\b", "[IMPORTO]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\bCampo\s*C\b", "[CATEGORIA]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\bCampo\s*D\b", "[PROGRESSIVO]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\[(CPA)\]", "[CPA]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\[(IMPONIBILE)\]", "[IMPONIBILE]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\[(IVA)\]", "[IVA]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\[(TOTALE)\](?!_AVERE)", "[TOTALE]"));
                    placeholders.Add(new KeyValuePair<string, string>(@"\[(TOTALE_AVERE)\]", "[TOTALE_AVERE]"));

                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"<td[^>]*>\s*\[IMPORTO\]\s*</td>",
                        "<td style='text-align:right;'>[IMPORTO]</td>",
                        RegexOptions.IgnoreCase
                    );

                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"<td[^>]*>\s*\[(CPA|IMPONIBILE|IVA|TOTALE|TOTALE_AVERE)\]\s*</td>",
                        "<td style='text-align:right;'>[$1]</td>",
                        RegexOptions.IgnoreCase
                    );
                }
                else if (tipoCompenso.Equals("Giudiziale", StringComparison.OrdinalIgnoreCase))
                {
                    // ======================================================
                    // 🔹 SEGNAPOSTI per GIUDIZIALE
                    // ======================================================
                    var segnapostiGiudiziale = new List<KeyValuePair<string, string>>
                        {
                         // Campo libero “Estremi giudizio oggetto dell’incarico” o solo “Estremi giudizio”
                            new KeyValuePair<string, string>(
                                @"E\s*(?:<[^>]+>)*\s*s\s*(?:<[^>]+>)*\s*t\s*(?:<[^>]+>)*\s*r\s*(?:<[^>]+>)*\s*e\s*(?:<[^>]+>)*\s*m\s*(?:<[^>]+>)*\s*i\s*(?:<[^>]+>)*\s*[\s>]*g\s*(?:<[^>]+>)*\s*i\s*(?:<[^>]+>)*\s*u\s*(?:<[^>]+>)*\s*d\s*(?:<[^>]+>)*\s*i\s*(?:<[^>]+>)*\s*z\s*(?:<[^>]+>)*\s*i\s*(?:<[^>]+>)*\s*o",
                                "[ESTREMI_GIUDIZIO]"
                            ),


                            // Campo A = Descrizione attività o fase del giudizio
                            new KeyValuePair<string, string>(
                                @"Campo\s*A|\[DESCRIZIONE[_\s]*ATTIVITA\]",
                                "[DESCRIZIONE_ATTIVITA]"
                            ),

                            // Campo B = Importo
                            new KeyValuePair<string, string>(
                                @"Campo\s*B|\[IMPORTO\]",
                                "[IMPORTO]"
                            ),

                            // Campo C = Numero progressivo (automatico)
                            new KeyValuePair<string, string>(
                                @"Campo\s*C|\[NUMERO[_\s]*PROGRESSIVO\]",
                                "[NUMERO_PROGRESSIVO]"
                            )
                        };
                   




                    // 🔁 Aggiungi solo i nuovi segnaposti se non già presenti
                    foreach (var kv in segnapostiGiudiziale)
                    {
                        if (!placeholders.Any(p => p.Value == kv.Value))
                            placeholders.Add(kv);
                    }

                    // ======================================================
                    // 💄 Allineamento Importo (Campo B) a destra
                    // ======================================================
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"<td[^>]*>\s*\[IMPORTO\]\s*</td>",
                        "<td style='text-align:right;'>[IMPORTO]</td>",
                        RegexOptions.IgnoreCase
                    );

                    // ======================================================
                    // 🧩 Normalizzazione eventuali parentesi doppie [[...]] → [...]
                    // ======================================================
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"\[\[([A-Z_ ]+)\]\]",
                        "[$1]",
                        RegexOptions.IgnoreCase
                    );
                }
                else if (tipoCompenso.Equals("Avviso Parcella", StringComparison.OrdinalIgnoreCase))
                {
                    // ======================================================
                    // 📑 SEGNAPOSTI SPECIFICI PER TEMPLATE "AVVISO PARCELLA"
                    // ======================================================
                    var segnapostiAvviso = new List<KeyValuePair<string, string>>
    {
        // === 📄 DATI GENERALI ===
        new KeyValuePair<string, string>(@"DATA\s*CREAZIONE\s*AVVISO", "[DATA_CREAZIONE_AVVISO]"),
        new KeyValuePair<string, string>(@"PROGRESSIVO\s*PRATICA", "[PROGRESSIVO_PRATICA]"),
        new KeyValuePair<string, string>(@"TITOLO\s+AVVISO\s+PARCELLA", "[TITOLO_AVVISO_PARCELLA]"),

        // === 👤 PROFESSIONISTA / RESPONSABILE PRATICA ===
        new KeyValuePair<string, string>(
            @"(?<!Email(?:<[^>]+>|\s)*[:]*\s*)Responsabile(?:<[^>]+>|\s)*Pratica(?![^<]*@)",
            "[NOME_PROFESSIONISTA_RESPONSABILE]"
        ),
        new KeyValuePair<string, string>(
            @"Email(?:<[^>]+>|\s)*[:]*\s*(?:email|e\-mail)(?:<[^>]+>|\s)*responsabile(?:<[^>]+>|\s)*pratica",
            "Email : [EMAIL_RESPONSABILE_PRATICA]"
        ),

        // === 🧾 CLIENTE ===
        new KeyValuePair<string, string>(
            @"NOME(?:<[^>]+>|\s)*COGNOME(?:<[^>]+>|\s)*CLIENTE",
            "[NOME_COGNOME_CLIENTE]"
        ),
        new KeyValuePair<string, string>(
            @"RAGIONE(?:<[^>]+>|\s)*SOCIALE(?:<[^>]+>|\s)*CLIENTE",
            "[RAGIONE_SOCIALE_CLIENTE]"
        ),
        new KeyValuePair<string, string>(
            @"Residenza(?:<[^>]+>|\s)*\/?(?:<[^>]+>|\s)*sede(?:<[^>]+>|\s)*legale(?:<[^>]+>|\s)*cliente",
            "[INDIRIZZO_CLIENTE]"
        ),
        new KeyValuePair<string, string>(
            @"CF(?:<[^>]+>|\s)*CLIENTE",
            "[CF_CLIENTE]"
        ),
        new KeyValuePair<string, string>(
            @"Email(?:<[^>]+>|\s)*[:]*\s*(?:email|e\-mail)(?:<[^>]+>|\s)*cliente",
            "Email : [EMAIL_CLIENTE]"
        ),

        // === 👥 PROFESSIONISTA / OWNER CLIENTE ===
        new KeyValuePair<string, string>(
            @"NOME(?:<[^>]+>|\s)*COGNOME(?:<[^>]+>|\s)*Professionista",
            "[NOME_COGNOME_PROFESSIONISTA]"
        ),
        new KeyValuePair<string, string>(
            @"Owner(?:<[^>]+>|\s)*Cliente",
            "[OWNER_CLIENTE]"
        ),
        new KeyValuePair<string, string>(
            @"Email(?:<[^>]+>|\s)*[:]*\s*(?:email|e\-mail)(?:<[^>]+>|\s)*professionista",
            "Email : [EMAIL_PROFESSIONISTA]"
        ),

        // === 💶 IMPORTI ===
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*Sorte\s*(?=</td>)", "[IMPORTO_SORTE]"),
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*Cassa\s+avv\.ti\s*4%\s*(?=</td>)", "[IMPORTO_CASSA_4]"),
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*Spese\s+Generali\s*15%\s*(?=</td>)", "[IMPORTO_SPESE_GENERALI]"),
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*Totale\s+imponibile\s+IVA\s*(?=</td>)", "[TOTALE_IMPONIBILE]"),
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*IVA\s*22%\s*(?=</td>)", "[IMPORTO_IVA]"),
        new KeyValuePair<string, string>(@"(?<=</td>\s*<td[^>]*>)\s*Totale\s*(?=</td>)", "[TOTALE_COMPLESSIVO]"),

        // === ✍️ FIRME ===
        new KeyValuePair<string, string>(@"\[FIRMA_FRATINI\]", "[FIRMA_FRATINI]"),
        new KeyValuePair<string, string>(@"\[FIRMA_DAMICO\]", "[FIRMA_DAMICO]")
    };

                    // ======================================================
                    // 🔁 Applica sostituzioni testuali
                    // ======================================================
                    foreach (var kv in segnapostiAvviso)
                    {
                        htmlContent = Regex.Replace(htmlContent, kv.Key, kv.Value, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    }

                    // ======================================================
                    // 💄 Allineamento numerico importi
                    // ======================================================
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"(<tr[^>]*>\s*<td[^>]*>.*?</td>\s*<td[^>]*>)\s*\[(IMPORTO_SORTE|IMPORTO_CASSA_4|IMPORTO_SPESE_GENERALI|IMPORTO_IVA|TOTALE_IMPONIBILE|TOTALE_COMPLESSIVO)\]\s*(</td>)",
                        "$1<span style='display:block; text-align:right; padding-right:8px; font-size:10pt;'>[$2]</span>$3",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );

                    // ======================================================
                    // 🧩 Normalizzazione doppie parentesi ([[...]] → [...])
                    // ======================================================
                    htmlContent = Regex.Replace(htmlContent, @"\[\[([A-Z_ ]+)\]\]", "[$1]", RegexOptions.IgnoreCase);

                    // ======================================================
                    // ✉️ Ridimensiona email cliente
                    // ======================================================
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"(<td[^>]*>\s*Email\s*:\s*email\s*cliente.*?</td>)",
                        "<td style='font-size:9pt; vertical-align:top;'>$1</td>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );

                    // ======================================================
                    // 🎨 Margini e font generali (stile Word)
                    // ======================================================
                    htmlContent = "<div style='margin:1cm; font-family:Calibri, sans-serif; font-size:11pt; line-height:1.4;'>" +
                                  htmlContent + "</div>";

                    // ======================================================
                    // 🧩 Disposizione orizzontale firme (Word/Rotativa stabile)
                    // ======================================================
                    htmlContent = Regex.Replace(
                        htmlContent,
                        @"(\[FIRMA_FRATINI\].*?\[FIRMA_DAMICO\])",
                        @"<table style='width:100%; border:none; border-collapse:collapse; margin-top:40px; font-family:Calibri, sans-serif; font-size:11pt;'>
                                <tr>
                                    <td style='width:50%; text-align:left; vertical-align:top; white-space:nowrap; letter-spacing:0;'>
                                        Avv.&nbsp;Riccardo&nbsp;Fratini&nbsp;–&nbsp;Presidente&nbsp;CdA<br/>
                                        [FIRMA_FRATINI]
                                    </td>
                                    <td style='width:50%; text-align:right; vertical-align:top; white-space:nowrap; letter-spacing:0;'>
                                        Avv.&nbsp;Dario&nbsp;D’Amico&nbsp;–&nbsp;Vice-Presidente&nbsp;CdA<br/>
                                        [FIRMA_DAMICO]
                                    </td>
                                </tr>
                            </table>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );

                    // ======================================================
                    // 🧾 DEBUG: salva HTML generato per verifica (facoltativo)
                    // ======================================================
                    try
                    {
                        string debugPath = Server.MapPath("~/Content/debug_upload_template.html");
                        System.IO.File.WriteAllText(debugPath, htmlContent, System.Text.Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Errore scrittura debug HTML: " + ex.Message);
                    }
                }

                // ============================================
                // 🔁 Applica le sostituzioni
                // ============================================
                foreach (var kv in placeholders)
                {
                    htmlContent = Regex.Replace(
                        htmlContent,
                        kv.Key,
                        kv.Value,
                        RegexOptions.IgnoreCase | RegexOptions.Singleline
                    );
                }

                // ============================================
                // 🔁 Gestione LOGO
                // ============================================
                string logoPath = Url.Content("~/Content/img/Icons/Logo Nuovo.png");
                string logoTag = $@"
                    <div style='display:flex; align-items:center; margin-bottom:30px;'>
                        <img src='{logoPath}' alt='Logo Sinergia'
                             style='max-height:180px; width:auto; margin-right:20px; display:block; object-fit:contain;' />
                    </div>";

                htmlContent = htmlContent.Replace("[LOGO_RESPONSABILE]", logoTag);
                // 🔥 Fix per immagini vuote residue
                htmlContent = Regex.Replace(
                    htmlContent,
                    @"<img[^>]*(src\s*=\s*['""]\s*['""][^>]*>|src\s*=\s*['""]data:image/[^'""]*;base64,\s*['""])?[^>]*>",
                    "",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                );
                if (!htmlContent.Contains(logoTag))
                    htmlContent = logoTag + htmlContent;

                // ============================================
                // 🔁 Pulizia HTML
                // ============================================
                htmlContent = Regex.Replace(htmlContent, @"\|+|={2,}", "");
                htmlContent = Regex.Replace(htmlContent, @"<td[^>]*>(\s|&nbsp;)*</td>", "<td></td>", RegexOptions.IgnoreCase);
                htmlContent = Regex.Replace(htmlContent, @"<tr>(\s*<td[^>]*>\s*</td>)+\s*</tr>", "", RegexOptions.IgnoreCase);
                htmlContent = Regex.Replace(htmlContent, @"<p[^>]*>(\s|&nbsp;)*<\/p>", "", RegexOptions.IgnoreCase);
                htmlContent = Regex.Replace(htmlContent, @"<span[^>]*>(\s|&nbsp;)*<\/span>", "", RegexOptions.IgnoreCase);

                // ============================================
                // 🔁 Salvataggio nel DB
                // ============================================
                string nomeTemplate = Path.GetFileNameWithoutExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(nomeTemplate))
                    nomeTemplate = "Template_" + DateTime.Now.ToString("yyyyMMdd_HHmm");

                var nuovoTemplate = new TemplateIncarichi
                {
                    NomeTemplate = nomeTemplate,
                    ContenutoHtml = htmlContent,
                    Stato = "Attivo",
                    ID_Professione = 0,
                    TipoCompenso = tipoCompenso
                };

                db.TemplateIncarichi.Add(nuovoTemplate);
                db.SaveChanges();

                db.TemplateIncarichi_a.Add(new TemplateIncarichi_a
                {
                    ID_Archivio = nuovoTemplate.IDTemplateIncarichi,
                    NomeTemplate = nuovoTemplate.NomeTemplate,
                    ContenutoHtml = nuovoTemplate.ContenutoHtml,
                    Stato = nuovoTemplate.Stato,
                    ID_Professione = 0,
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = $"📄 Importato da file DOCX ({file.FileName}) con TipoCompenso={tipoCompenso} da utente {idUtente} il {DateTime.Now:g}"
                });
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"✅ Template '{nuovoTemplate.NomeTemplate}' ({tipoCompenso}) importato correttamente!"
                });
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                if (ex.InnerException != null) err += " → " + ex.InnerException.Message;
                return Json(new { success = false, message = "❌ Errore durante l'importazione: " + err });
            }
        }



        #endregion

        #region AVVISI PARCELLA
        public ActionResult GestioneParcelle()
        {
            return View("~/Views/AvvisiParcella/GestioneAvvisiParcella.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneAvvisiParcellaList(int? idPratica = null)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();

            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            System.Diagnostics.Trace.WriteLine("═══════════════ AVVISI PARCELLA LIST ═══════════════");
            System.Diagnostics.Trace.WriteLine($"👤 ID Utente Collegato = {idUtenteCorrente}");
            System.Diagnostics.Trace.WriteLine($"👤 Tipo Utente = {utenteCorrente?.TipoUtente}");

            // --- Recupero professionista selezionato da navbar ---
            string idClienteSelezionato = Session["ID_ClienteSelezionato"] as string;

            int idProfessionistaFiltro = 0;

            if (!string.IsNullOrEmpty(idClienteSelezionato) && idClienteSelezionato.Length > 2)
            {
                string tipo = idClienteSelezionato.Substring(0, 2);  // A_, P_, C_
                string idNumeric = idClienteSelezionato.Substring(2); // parte numerica

                if (int.TryParse(idNumeric, out int idParsed))
                {
                    if (tipo == "A_" || tipo == "P_")
                    {
                        idProfessionistaFiltro = db.OperatoriSinergia
                            .Where(o => o.ID_Operatore == idParsed && o.TipoCliente == "Professionista")
                            .Select(o => o.ID_UtenteCollegato ?? 0)
                            .FirstOrDefault();
                    }
                    else if (tipo == "C_")
                    {
                        // Collaboratore → l’ID_Parsed è direttamente ID_Utente
                        idProfessionistaFiltro = idParsed;
                    }

                    System.Diagnostics.Trace.WriteLine($"🔵 PROFESSIONISTA FILTRO = {idProfessionistaFiltro}");
                }
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("👉 Nessun professionista selezionato nella navbar.");
            }

            // ----------------------------------------------------------
            // 🔍 QUERY BASE
            // ----------------------------------------------------------
            IQueryable<AvvisiParcella> query = db.AvvisiParcella;

            if (idPratica.HasValue)
                query = query.Where(a => a.ID_Pratiche == idPratica.Value);

            string tipoUtente = utenteCorrente.TipoUtente?.Trim().ToLowerInvariant() ?? "";

            if (tipoUtente == "professionista")
            {
                query = query.Where(a =>
                    a.ID_ResponsabilePratica == idUtenteCorrente);
            }
            else if (tipoUtente == "collaboratore")
            {
                var idProfessionistiCollegati = (
                    from r in db.RelazioneUtenti
                    join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                    where r.ID_UtenteAssociato == idUtenteCorrente
                          && r.Stato == "Attivo"
                          && o.TipoCliente == "Professionista"
                          && o.ID_UtenteCollegato.HasValue
                    select o.ID_UtenteCollegato.Value
                ).Distinct().ToList();

                if (!idProfessionistiCollegati.Any())
                    query = query.Where(a => false);
                else
                    query = query.Where(a =>
                        idProfessionistiCollegati.Contains(a.ID_ResponsabilePratica.Value));
            }
            // Admin → nessun filtro

            // ----------------------------------------------------------
            // 🔍 APPLICAZIONE FILTRO NAVBAR
            // ----------------------------------------------------------
            if (idProfessionistaFiltro > 0)
            {
                query = query.Where(a =>
                    a.ID_ResponsabilePratica == idProfessionistaFiltro
                );

                System.Diagnostics.Trace.WriteLine("👉 FILTRO NAVBAR APPLICATO (Responsabile Pratica).");
            }

            else
            {
                System.Diagnostics.Trace.WriteLine("👉 Nessun filtro applicato: MOSTRO TUTTI.");
            }

            // ----------------------------------------------------------
            // 🔐 PERMESSI
            // ----------------------------------------------------------
            bool puoAggiungere = false;
            bool puoModificare = false;
            bool puoEliminare = false;

            if (utenteCorrente.TipoUtente == "Admin")
            {
                puoAggiungere = puoModificare = puoEliminare = true;
            }
            else
            {
                var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
                puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
                puoModificare = permessiDb.Any(p => p.Modifica == true);
                puoEliminare = permessiDb.Any(p => p.Elimina == true);
            }

            // ----------------------------------------------------------
            // 🔄 PROIEZIONE VIEWMODEL
            // ----------------------------------------------------------
            var lista = query
                .OrderByDescending(a => a.DataAvviso)
                .ToList()
                .Select(a =>
                {
                    var compenso = (a.ID_CompensoOrigine.HasValue)
                        ? db.CompensiPraticaDettaglio.FirstOrDefault(c => c.ID_RigaCompenso == a.ID_CompensoOrigine.Value)
                        : null;

                    decimal importoBase = a.Importo ?? 0;
                    decimal contributo = a.ContributoIntegrativoImporto ?? 0;
                    decimal iva = a.ImportoIVA ?? 0;
                    decimal rimborso = a.ImportoRimborsoSpese ?? 0;

                    decimal totaleAvviso = a.TotaleAvvisiParcella ?? (importoBase + contributo + iva + rimborso);

                    bool haDocumento = db.DocumentiPratiche.Any(d =>
                        d.ID_RiferimentoAvvisoParcella == a.ID_AvvisoParcelle &&
                        d.CategoriaDocumento == "Avviso Parcella");

                    return new AvvisoParcellaViewModel
                    {
                        ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                        TitoloAvviso = a.TitoloAvviso,
                        ID_Pratiche = a.ID_Pratiche ?? 0,
                        DataAvviso = a.DataAvviso,
                        Importo = importoBase,
                        Note = a.Note,
                        Stato = a.Stato,
                        MetodoPagamento = a.MetodoPagamento,

                        PuoEliminare = puoEliminare,
                        PuoModificare = puoModificare,
                        HaDocumentoCaricato = haDocumento,

                        NomePratica = db.Pratiche
                            .Where(p => p.ID_Pratiche == a.ID_Pratiche)
                            .Select(p => p.Titolo)
                            .FirstOrDefault() ?? "(N/D)",

                        ID_ResponsabilePratica = a.ID_ResponsabilePratica,
                        ID_OwnerCliente = a.ID_OwnerCliente,

                        NomeResponsabilePratica = a.ID_ResponsabilePratica.HasValue
                            ? db.Utenti
                                .Where(u => u.ID_Utente == a.ID_ResponsabilePratica.Value)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                            : "(N/D)",

                        NomeOwnerCliente = a.ID_OwnerCliente.HasValue
                            ? db.Utenti
                                .Where(u => u.ID_Utente == a.ID_OwnerCliente.Value)
                                .Select(u => u.Nome + " " + u.Cognome)
                                .FirstOrDefault()
                            : "(N/D)",

                        ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                        ContributoIntegrativoImporto = contributo,
                        AliquotaIVA = a.AliquotaIVA,
                        ImportoIVA = iva,
                        RimborsoSpesePercentuale = a.RimborsoSpesePercentuale,
                        ImportoRimborsoSpese = rimborso,
                        ImportoAcconto = a.ImportoAcconto ?? 0,

                        TipologiaAvviso = a.TipologiaAvviso,
                        FaseGiudiziale = a.FaseGiudiziale,

                        ID_CompensoOrigine = a.ID_CompensoOrigine,
                        DescrizioneCompenso = compenso?.Descrizione,

                        TariffaOraria = (compenso?.TipoCompenso?.ToLower() == "a ore") ? compenso?.Importo : null,
                        OreEffettive = (compenso?.TipoCompenso?.ToLower() == "a ore") ? compenso?.ValoreStimato : null,

                        TotaleAvvisoParcella = totaleAvviso,
                        DataInvio = a.DataInvio,
                        DataCompetenzaEconomica = a.DataCompetenzaEconomica,

                        StatoIncasso = db.Incassi
                            .Where(i => i.ID_AvvisoParcella == a.ID_AvvisoParcelle)
                            .Select(i => i.StatoIncasso)
                            .FirstOrDefault() ?? "—",
                    };
                })
                .ToList();

            // ----------------------------------------------------------
            // 🔐 PERMESSI VIEWBAG
            // ----------------------------------------------------------
            ViewBag.PuoAggiungere = puoAggiungere;
            ViewBag.Permessi = new PermessiViewModel
            {
                ID_Utente = utenteCorrente.ID_Utente,
                NomeUtente = utenteCorrente.Nome + " " + utenteCorrente.Cognome,
                Permessi = new List<PermessoSingoloViewModel>
        {
            new PermessoSingoloViewModel
            {
                Aggiungi = puoAggiungere,
                Modifica = puoModificare,
                Elimina = puoEliminare
            }
        }
            };

            ViewBag.IDPraticaCorrente = idPratica;

            return PartialView("~/Views/AvvisiParcella/_GestioneAvvisiParcellaList.cshtml", lista);
        }



        [HttpPost]
        public ActionResult CreaAvvisoParcella(AvvisoParcellaViewModel model)
        {
            System.Diagnostics.Debug.WriteLine("========== [CreaAvvisoParcella] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 Timestamp: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"🟡 Raw form Importo: {Request.Form["Importo"]}");
            System.Diagnostics.Debug.WriteLine($"🟡 Model.Importo: {model.Importo}");
            System.Diagnostics.Debug.WriteLine($"🟡 Model.ImportoAcconto: {model.ImportoAcconto}");
            System.Diagnostics.Debug.WriteLine($"🟡 Stato selezionato: {model.Stato}");
            System.Diagnostics.Debug.WriteLine($"🟡 ModelState valido: {ModelState.IsValid}");

            if (ModelState.ContainsKey("ID_AvvisoParcelle"))
                ModelState["ID_AvvisoParcelle"].Errors.Clear();

            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("it-IT");

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" || db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Aggiungi == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per creare un avviso parcella." });

            try
            {
                DateTime now = DateTime.Now;

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                int? idResponsabilePratica = pratica.ID_UtenteResponsabile;
                int? idOwnerCliente = db.Clienti
                    .Where(c => c.ID_Cliente == pratica.ID_Cliente)
                    .Select(c => c.ID_Operatore)
                    .FirstOrDefault();

                decimal importoBase = model.Importo ?? 0;
                decimal importoAcconto = model.ImportoAcconto ?? 0;

                // Se l’avviso è un acconto usa l’importo acconto come base
                if (importoBase <= 0 && importoAcconto > 0)
                    importoBase = importoAcconto;

                if (importoBase <= 0)
                    return Json(new { success = false, message = "L'importo non è valido (vuoto o nullo)." });

                // Recupero CI professionista
                var operatoreResp = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_UtenteCollegato == idResponsabilePratica && o.TipoCliente == "Professionista");

                decimal percentualeCI = 0;
                if (model.ContributoIntegrativoPercentuale.HasValue)
                    percentualeCI = model.ContributoIntegrativoPercentuale.Value;
                else if (operatoreResp?.ID_Professione != null)
                {
                    percentualeCI = db.Professioni
                        .Where(p => p.ProfessioniID == operatoreResp.ID_Professione)
                        .Select(p => p.PercentualeContributoIntegrativo)
                        .FirstOrDefault() ?? 0;
                }

                // Percentuali da model
                decimal aliquotaIVA = model.AliquotaIVA ?? 0;
                decimal rimborsoPerc = model.RimborsoSpesePercentuale ?? 0;      // ← Spese generali (%)

                // 1️⃣ Importo spese generali
                decimal importoRimborso = Math.Round(importoBase * rimborsoPerc / 100m, 2);

                // 2️⃣ Base per CI = imponibile + spese generali
                decimal baseCI = importoBase + importoRimborso;

                // 3️⃣ Contributo integrativo (su imponibile + spese generali)
                decimal contributo = Math.Round(baseCI * percentualeCI / 100m, 2);

                // 4️⃣ Base IVA = imponibile + spese generali + CI
                decimal imponibileIVA = importoBase + importoRimborso + contributo;

                // 5️⃣ IVA
                decimal importoIVA = Math.Round(imponibileIVA * aliquotaIVA / 100m, 2);

                // 6️⃣ Totale avviso
                decimal totaleAvviso = importoBase + importoRimborso + contributo + importoIVA;


                // Date
                DateTime dataAvviso = model.DataAvviso ?? now;
                DateTime dataCompetenza = dataAvviso;

                DateTime? dataInvio = null;
                if (model.Stato != null && model.Stato.Equals("Inviato", StringComparison.OrdinalIgnoreCase))
                    dataInvio = now;


                var nuovo = new AvvisiParcella
                {
                    ID_Pratiche = model.ID_Pratiche,
                    DataAvviso = dataAvviso,
                    TitoloAvviso = model.TitoloAvviso,
                    Importo = importoBase,
                    ImportoAcconto = importoAcconto,
                    Note = model.Note?.Trim(),
                    Stato = model.Stato?.Trim(),
                    MetodoPagamento = model.MetodoPagamento?.Trim(),
                    ContributoIntegrativoPercentuale = percentualeCI,
                    ContributoIntegrativoImporto = contributo,
                    AliquotaIVA = aliquotaIVA,
                    ImportoIVA = importoIVA,
                    TotaleAvvisiParcella = totaleAvviso,
                    RimborsoSpesePercentuale = rimborsoPerc,
                    ImportoRimborsoSpese = importoRimborso,
                    ID_ResponsabilePratica = idResponsabilePratica,
                    ID_OwnerCliente = idOwnerCliente,
                    TipologiaAvviso = model.TipologiaAvviso?.Trim(),
                    FaseGiudiziale = model.FaseGiudiziale?.Trim(),
                    ID_CompensoOrigine = model.ID_CompensoOrigine,
                    ID_UtenteCreatore = idUtenteCorrente,
                    ID_UtenteModifica = idUtenteCorrente,
                    DataModifica = now,
                    DataInvio = dataInvio,
                    DataCompetenzaEconomica = dataCompetenza
                };

                db.AvvisiParcella.Add(nuovo);
                db.SaveChanges();

                if (model.ID_CompensoOrigine.HasValue)
                {
                    var compenso = db.CompensiPraticaDettaglio.FirstOrDefault(c => c.ID_RigaCompenso == model.ID_CompensoOrigine.Value);
                    if (compenso != null)
                    {
                        compenso.ImportoInviatoAllaFatturazione = (compenso.ImportoInviatoAllaFatturazione ?? 0) + importoBase;
                        compenso.ID_AvvisoParcella = nuovo.ID_AvvisoParcelle;
                        db.Entry(compenso).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();
                        GeneraEconomicoDaCompenso(compenso, nuovo.ID_AvvisoParcelle);
                    }
                }
                else if (!string.IsNullOrEmpty(model.TipologiaAvviso))
                {
                    var compensi = db.CompensiPraticaDettaglio
                        .Where(c => c.ID_Pratiche == model.ID_Pratiche && c.TipoCompenso == model.TipologiaAvviso)
                        .ToList();

                    foreach (var comp in compensi)
                    {
                        comp.ID_AvvisoParcella = nuovo.ID_AvvisoParcelle;
                        GeneraEconomicoDaCompenso(comp, nuovo.ID_AvvisoParcelle);
                    }

                    db.SaveChanges();
                }

                db.AvvisiParcella_a.Add(new AvvisiParcella_a
                {
                    ID_Archivio = nuovo.ID_AvvisoParcelle,
                    ID_Pratiche = nuovo.ID_Pratiche,
                    TitoloAvviso = nuovo.TitoloAvviso,
                    DataAvviso = nuovo.DataAvviso,
                    Importo = nuovo.Importo,
                    Note = nuovo.Note,
                    Stato = nuovo.Stato,
                    MetodoPagamento = nuovo.MetodoPagamento,
                    ContributoIntegrativoPercentuale = nuovo.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = nuovo.ContributoIntegrativoImporto,
                    AliquotaIVA = nuovo.AliquotaIVA,
                    ImportoIVA = nuovo.ImportoIVA,
                    TotaleAvvisiParcella = nuovo.TotaleAvvisiParcella,
                    TipologiaAvviso = nuovo.TipologiaAvviso,
                    FaseGiudiziale = nuovo.FaseGiudiziale,
                    RimborsoSpesePercentuale = nuovo.RimborsoSpesePercentuale,
                    ImportoRimborsoSpese = nuovo.ImportoRimborsoSpese,
                    ImportoAcconto = nuovo.ImportoAcconto,
                    ID_CompensoOrigine = nuovo.ID_CompensoOrigine,
                    ID_ResponsabilePratica = nuovo.ID_ResponsabilePratica,
                    ID_OwnerCliente = nuovo.ID_OwnerCliente,
                    DataInvio = dataInvio,
                    DataCompetenzaEconomica = dataCompetenza,
                    DataModifica = now,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Avviso creato da utente ID {idUtenteCorrente} in data {now:g}"
                });

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Avviso parcella creato correttamente.", id = nuovo.ID_AvvisoParcelle });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore durante il salvataggio: " + ex);
                return Json(new { success = false, message = "Errore durante il salvataggio: " + ex.Message });
            }
        }

        private void GeneraEconomicoDaCompenso(CompensiPraticaDettaglio comp, int idAvviso)
        {
            if (comp == null) return;

            int idPratica = comp.ID_Pratiche;
            var pratica = db.Pratiche.Find(idPratica);
            if (pratica == null) return;

            DateTime dataCompetenza = DateTime.Now;
            int? idResponsabile = pratica.ID_UtenteResponsabile;

            decimal importoBase = comp.Importo ?? 0;
            if (importoBase <= 0) return;

            System.Diagnostics.Trace.WriteLine($"📊 [Economico Avviso] Calcolo quote da compenso ID {comp.ID_RigaCompenso}");

            decimal percOwnerFee = db.RicorrenzeCosti
                .Where(r => r.Categoria == "Owner Fee" && r.Attivo &&
                      r.TipoValore == "Percentuale")
                .OrderByDescending(r => r.DataInizio)
                .Select(r => (decimal?)r.Valore)
                .FirstOrDefault() ?? 0m;

            decimal percTrattenuta = db.RicorrenzeCosti
                .Where(r => r.Categoria == "Trattenuta Sinergia" && r.Attivo &&
                      r.TipoValore == "Percentuale")
                .OrderByDescending(r => r.DataInizio)
                .Select(r => (decimal?)r.Valore)
                .FirstOrDefault() ?? 0m;

            var clusterList = db.Cluster
                .Where(c => c.ID_Pratiche == idPratica &&
                            c.TipoCluster == "Collaboratore")
                .ToList();

            decimal sommaCluster = clusterList.Sum(c => c.PercentualePrevisione);
            decimal percResponsabile = Math.Max(0, 100 - percOwnerFee - percTrattenuta - sommaCluster);

            void AggiungiEconomico(string tipo, int? idProf, decimal perc, decimal importo, string desc)
            {
                if (idProf == null || importo <= 0) return;

                var now = DateTime.Now;
                int utente = UserManager.GetIDUtenteCollegato();

                // Salvo nel bilancio professionista
                db.BilancioProfessionista.Add(new BilancioProfessionista
                {
                    ID_Professionista = idProf.Value,
                    ID_Pratiche = idPratica,
                    DataRegistrazione = dataCompetenza,
                    TipoVoce = tipo,
                    Categoria = "Economico Avviso",
                    Descrizione = desc,
                    Importo = Math.Round(importo, 2),
                    Stato = "Economico",
                    Origine = "Avviso Parcella",
                    DataInserimento = now,
                    ID_UtenteInserimento = utente,
                    DataCompetenzaEconomica = dataCompetenza
                });

                db.SaveChanges();

                // Registro il movimento in Economico
                var eco = new Economico
                {
                    ID_Pratiche = idPratica,
                    ID_Professionista = idProf.Value,
                    Percentuale = perc,
                    TipoOperazione = tipo,
                    Descrizione = desc,
                    ImportoEconomico = Math.Round(importo, 2),
                    DataRegistrazione = dataCompetenza,
                    ID_AvvisoParcella = idAvviso,
                    Stato = "Economico",
                    Categoria = "Avviso Parcella",
                    DataCompetenzaEconomica = dataCompetenza,
                    ID_UtenteCreatore = utente,
                    DataArchiviazione = now
                };

                db.Economico.Add(eco);
                db.SaveChanges();

                // Archiviazione della versione 1
                db.Economico_a.Add(new Economico_a
                {
                    ID_EconomicoOriginale = eco.ID_Economico,
                    ID_Pratiche = eco.ID_Pratiche,
                    ID_Professionista = eco.ID_Professionista,
                    Percentuale = eco.Percentuale,
                    TipoOperazione = eco.TipoOperazione,
                    Descrizione = eco.Descrizione,
                    ImportoEconomico = eco.ImportoEconomico,
                    DataRegistrazione = eco.DataRegistrazione,
                    ID_AvvisoParcella = eco.ID_AvvisoParcella,
                    Stato = eco.Stato,
                    Categoria = eco.Categoria,
                    DataCompetenzaEconomica = eco.DataCompetenzaEconomica,
                    NumeroVersione = 1,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = utente,
                    ModificheTestuali = "Creazione automatica Economico da Avviso Parcella"
                });

                db.SaveChanges();
                System.Diagnostics.Trace.WriteLine($"✅ Movimento economico salvato per pratica {idPratica}: {desc}");
            }


            decimal quotaResp = importoBase * percResponsabile / 100m;
            AggiungiEconomico("Entrata", idResponsabile, percResponsabile,
                quotaResp, $"Ricavo Avviso Responsabile: {comp.Descrizione}");

            if (pratica.ID_Owner != null && percOwnerFee > 0)
            {
                decimal quotaOwner = importoBase * percOwnerFee / 100m;
                AggiungiEconomico("Entrata", pratica.ID_Owner, percOwnerFee,
                    quotaOwner, $"Owner Fee Avviso: {comp.Descrizione}");
            }

            if (percTrattenuta > 0)
            {
                decimal quotaTratt = importoBase * percTrattenuta / 100m;
                AggiungiEconomico("Uscita", idResponsabile, percTrattenuta,
                    quotaTratt, $"Trattenuta Sinergia Avviso: {comp.Descrizione}");
            }

            foreach (var c in clusterList)
            {
                decimal quota = importoBase * c.PercentualePrevisione / 100m;
                AggiungiEconomico("Entrata", c.ID_Utente, c.PercentualePrevisione,
                    quota, $"Quota Collaboratore Cluster: {comp.Descrizione}");
            }

            if (!string.IsNullOrWhiteSpace(comp.Collaboratori))
            {
                try
                {
                    var lista = Newtonsoft.Json.Linq.JArray.Parse(comp.Collaboratori);
                    foreach (var coll in lista)
                    {
                        if (!decimal.TryParse(coll["Percentuale"]?.ToString(), out decimal perc)) continue;
                        if (!int.TryParse(coll["ID_Utente"]?.ToString(), out int idCollab)) continue;

                        decimal quota = Math.Round(importoBase * (perc / 100m), 2);
                        if (quota <= 0) continue;

                        AggiungiEconomico("Entrata", idCollab, perc,
                            quota, $"Quota Collaboratore Compenso: {comp.Descrizione}");
                    }
                }
                catch { }
            }
        }

        [HttpPost]
        public ActionResult ModificaAvvisoParcella(AvvisoParcellaViewModel model)
        {
            System.Diagnostics.Debug.WriteLine("========== [ModificaAvvisoParcella] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 Timestamp: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"🟡 Model.Importo: {model.Importo}");
            System.Diagnostics.Debug.WriteLine($"🟡 Model.ImportoAcconto: {model.ImportoAcconto}");
            System.Diagnostics.Debug.WriteLine($"🟡 Model.ID_CompensoOrigine: {model.ID_CompensoOrigine}");
            System.Diagnostics.Debug.WriteLine($"🟡 ModelState valido: {ModelState.IsValid}");

            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("it-IT");

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == model.ID_AvvisoParcelle);
                if (avviso == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                DateTime now = DateTime.Now;

                int? idResponsabilePratica = pratica.ID_UtenteResponsabile;
                int? idOwnerCliente = db.Clienti
                    .Where(c => c.ID_Cliente == pratica.ID_Cliente)
                    .Select(c => c.ID_Operatore)
                    .FirstOrDefault();

                decimal importoBase = model.Importo ?? 0;
                decimal importoAcconto = model.ImportoAcconto ?? 0;

                if (importoBase <= 0 && importoAcconto > 0)
                    importoBase = importoAcconto;

                if (importoBase <= 0)
                    return Json(new { success = false, message = "Importo non valido (vuoto o nullo)." });

                // Percentuali
                decimal aliquotaIVA = model.AliquotaIVA ?? 0;
                decimal rimborsoPerc = model.RimborsoSpesePercentuale ?? 0;

                // Recupero percentuale CI
                var operatoreResp = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_UtenteCollegato == idResponsabilePratica && o.TipoCliente == "Professionista");

                decimal percentualeCI = 0;
                if (model.ContributoIntegrativoPercentuale.HasValue)
                    percentualeCI = model.ContributoIntegrativoPercentuale.Value;
                else if (operatoreResp?.ID_Professione != null)
                {
                    percentualeCI = db.Professioni
                        .Where(p => p.ProfessioniID == operatoreResp.ID_Professione)
                        .Select(p => p.PercentualeContributoIntegrativo)
                        .FirstOrDefault() ?? 0;
                }

                // 1️⃣ Rimborso spese (su imponibile)
                decimal importoRimborso = Math.Round(importoBase * rimborsoPerc / 100m, 2);

                // 2️⃣ Base per CI (imponibile + rimborso spese)
                decimal baseCI = importoBase + importoRimborso;

                // 3️⃣ Contributo integrativo (su imponibile + rimborso)
                decimal contributo = Math.Round(baseCI * percentualeCI / 100m, 2);

                // 4️⃣ IVA su imponibile + rimborso + contributo
                decimal imponibileIVA = importoBase + importoRimborso + contributo;
                decimal importoIVA = Math.Round(imponibileIVA * aliquotaIVA / 100m, 2);

                // 5️⃣ Totale avviso
                decimal totaleLordo = importoBase + importoRimborso + contributo + importoIVA;



                int ultimaVersione = db.AvvisiParcella_a
                    .Where(a => a.ID_Archivio == avviso.ID_AvvisoParcelle)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => a.NumeroVersione)
                    .FirstOrDefault();

                List<string> modifiche = new List<string>();
                void CheckModifica(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                CheckModifica("Importo", avviso.Importo, importoBase);
                CheckModifica("ImportoAcconto", avviso.ImportoAcconto, importoAcconto);
                CheckModifica("AliquotaIVA", avviso.AliquotaIVA, aliquotaIVA);
                CheckModifica("ContributoIntegrativo", avviso.ContributoIntegrativoPercentuale, percentualeCI);
                CheckModifica("Rimborso", avviso.RimborsoSpesePercentuale, rimborsoPerc);

                DateTime dataAvviso = model.DataAvviso ?? now;
                DateTime dataCompetenza = dataAvviso;

                if (model.Stato == "Inviato" && !avviso.DataInvio.HasValue)
                    avviso.DataInvio = now;

                avviso.DataCompetenzaEconomica = dataCompetenza;
                avviso.ID_Pratiche = model.ID_Pratiche;
                avviso.DataAvviso = dataAvviso;
                avviso.TitoloAvviso = model.TitoloAvviso;
                avviso.Importo = importoBase;
                avviso.ImportoAcconto = importoAcconto;
                avviso.Note = model.Note?.Trim();
                avviso.Stato = model.Stato?.Trim();
                avviso.AliquotaIVA = aliquotaIVA;
                avviso.ImportoIVA = importoIVA;
                avviso.ContributoIntegrativoPercentuale = percentualeCI;
                avviso.ContributoIntegrativoImporto = contributo;
                avviso.RimborsoSpesePercentuale = rimborsoPerc;
                avviso.ImportoRimborsoSpese = importoRimborso;
                avviso.TotaleAvvisiParcella = totaleLordo;
                avviso.ID_ResponsabilePratica = idResponsabilePratica;
                avviso.ID_OwnerCliente = idOwnerCliente;
                avviso.ID_CompensoOrigine = model.ID_CompensoOrigine;
                avviso.DataModifica = now;
                avviso.ID_UtenteModifica = idUtenteCorrente;

                var compensiCollegatiVecchi = db.CompensiPraticaDettaglio
                    .Where(c => c.ID_AvvisoParcella == avviso.ID_AvvisoParcelle).ToList();

                foreach (var compOld in compensiCollegatiVecchi)
                    compOld.ID_AvvisoParcella = null;

                db.SaveChanges();

                if (model.ID_CompensoOrigine.HasValue)
                {
                    var comp = db.CompensiPraticaDettaglio
                        .FirstOrDefault(c => c.ID_RigaCompenso == model.ID_CompensoOrigine.Value);
                    if (comp != null)
                    {
                        comp.ImportoInviatoAllaFatturazione = importoBase;
                        comp.ID_AvvisoParcella = avviso.ID_AvvisoParcelle;
                    }
                }
                else if (!string.IsNullOrEmpty(model.TipologiaAvviso))
                {
                    var nuoviCompensi = db.CompensiPraticaDettaglio
                        .Where(c => c.ID_Pratiche == model.ID_Pratiche &&
                                    c.TipoCompenso == model.TipologiaAvviso).ToList();

                    foreach (var comp in nuoviCompensi)
                        comp.ID_AvvisoParcella = avviso.ID_AvvisoParcelle;
                }

                db.SaveChanges();

                // ============================================================
                // ✅  RICALCOLO ECONOMICO
                // ============================================================
                var vecBilancio = db.BilancioProfessionista
                    .Where(b => b.ID_Pratiche == pratica.ID_Pratiche &&
                                b.Origine == "Avviso Parcella")
                    .ToList();
                db.BilancioProfessionista.RemoveRange(vecBilancio);

                var vecEco = db.Economico
                    .Where(e => e.ID_AvvisoParcella == avviso.ID_AvvisoParcelle)
                    .ToList();
                db.Economico.RemoveRange(vecEco);

                var idsEconomico = vecEco.Select(x => x.ID_Economico).ToList();

                var vecEcoArch = db.Economico_a
                    .Where(a => idsEconomico.Contains(a.ID_EconomicoOriginale ?? 0))
                    .ToList();

                db.Economico_a.RemoveRange(vecEcoArch);

                db.SaveChanges();

                if (model.ID_CompensoOrigine.HasValue)
                {
                    var comp = db.CompensiPraticaDettaglio
                        .FirstOrDefault(c => c.ID_RigaCompenso == model.ID_CompensoOrigine.Value);
                    if (comp != null)
                        GeneraEconomicoDaCompenso(comp, avviso.ID_AvvisoParcelle);
                }
                else if (!string.IsNullOrEmpty(model.TipologiaAvviso))
                {
                    var nuoviCompensi = db.CompensiPraticaDettaglio
                        .Where(c => c.ID_Pratiche == pratica.ID_Pratiche &&
                                    c.TipoCompenso == model.TipologiaAvviso).ToList();

                    foreach (var comp in nuoviCompensi)
                        GeneraEconomicoDaCompenso(comp, avviso.ID_AvvisoParcelle);
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Avviso parcella modificato correttamente." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore ModificaAvvisoParcella: " + ex);
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }


        // ===========================================================
        // 📄 GET AVVISO PARCELLA (dettaglio completo + documento PDF)
        // ===========================================================
        [HttpGet]
        public ActionResult GetAvvisoParcella(int id)
        {
            System.Diagnostics.Debug.WriteLine("========== [GetAvvisoParcella] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"📩 ID Avviso ricevuto: {id}");

            try
            {
                // ======================================================
                // 🔍 Recupero dati principali avviso + relazioni
                // ======================================================
                var result = (from avviso in db.AvvisiParcella
                              where avviso.ID_AvvisoParcelle == id
                              join pr in db.Pratiche on avviso.ID_Pratiche equals pr.ID_Pratiche
                              join uResp in db.Utenti on pr.ID_UtenteResponsabile equals uResp.ID_Utente into joinResp
                              from uResp in joinResp.DefaultIfEmpty()
                              join c in db.Clienti on pr.ID_Cliente equals c.ID_Cliente into joinCliente
                              from c in joinCliente.DefaultIfEmpty()
                              join oOwner in db.OperatoriSinergia on c.ID_Operatore equals oOwner.ID_Operatore into joinOwner
                              from oOwner in joinOwner.DefaultIfEmpty()
                              join uCreatore in db.Utenti on avviso.ID_UtenteCreatore equals uCreatore.ID_Utente into joinCreatore
                              from uCreatore in joinCreatore.DefaultIfEmpty()
                              select new
                              {
                                  Avviso = avviso,
                                  Pratica = pr,
                                  NomeResponsabile = uResp != null ? (uResp.Nome + " " + uResp.Cognome) : "⚠️ Responsabile mancante",
                                  ID_OwnerCliente = c != null ? c.ID_Operatore : (int?)null,
                                  NomeOwner = oOwner != null ? (oOwner.Nome + " " + oOwner.Cognome) : "⚠️ Owner mancante",
                                  NomeCreatore = uCreatore != null ? (uCreatore.Nome + " " + uCreatore.Cognome) : "N.D."
                              }).FirstOrDefault();

                if (result == null)
                    return Json(new { success = false, message = "❌ Avviso parcella non trovato." }, JsonRequestBehavior.AllowGet);

                var a = result.Avviso;
                var p = result.Pratica;

                // ======================================================
                // 💰 Calcoli economici base
                // ======================================================
                decimal importoBase = a.Importo ?? 0;
                decimal importoAcconto = a.ImportoAcconto ?? 0;
                decimal totaleAvviso = a.TotaleAvvisiParcella ?? importoBase;
                decimal importoResiduo = totaleAvviso - importoAcconto;

                var incassi = db.Incassi.Where(i => i.ID_AvvisoParcella == a.ID_AvvisoParcelle).ToList();
                decimal totaleIncassato = incassi.Sum(i => i.Importo);
                bool pagato = totaleIncassato >= totaleAvviso;
                decimal importoResiduoEffettivo = totaleAvviso - totaleIncassato;

                // ======================================================
                // ⚙️ Contributo integrativo e rimborso
                // ======================================================
                decimal percentualeCI = a.ContributoIntegrativoPercentuale ?? 0;
                decimal rimborsoPerc = a.RimborsoSpesePercentuale ?? 0;

                // ======================================================
                // 🔎 Compensi collegati (Fisso / A ore / Giudiziale)
                // ======================================================
                var righe = db.CompensiPraticaDettaglio
                    .Where(cd => cd.ID_Pratiche == p.ID_Pratiche)
                    .OrderBy(cd => cd.Ordine)
                    .ToList();

                var idCompensiUsati = db.AvvisiParcella
                    .Where(av => av.ID_Pratiche == p.ID_Pratiche &&
                                 av.ID_CompensoOrigine != null &&
                                 av.ID_AvvisoParcelle != id)
                    .Select(av => av.ID_CompensoOrigine.Value)
                    .Distinct()
                    .ToList();

                var fissi = righe
                    .Where(cd => cd.TipoCompenso != null &&
                                 cd.TipoCompenso.Trim().ToLower() == "fisso" &&
                                 !idCompensiUsati.Contains(cd.ID_RigaCompenso))
                    .Select(cd => new
                    {
                        Voce = cd.Descrizione ?? "(Senza descrizione)",
                        ImportoBase = cd.Importo ?? 0,
                        ID_CompensoOrigine = cd.ID_RigaCompenso,
                        ImportoInviatoAllaFatturazione = cd.ImportoInviatoAllaFatturazione ?? 0
                    })
                    .ToList();

                var aOre = righe
                    .Where(cd => cd.TipoCompenso != null &&
                                 cd.TipoCompenso.Trim().ToLower() == "a ore" &&
                                 !idCompensiUsati.Contains(cd.ID_RigaCompenso))
                    .Select(cd => new
                    {
                        Voce = cd.Descrizione ?? "(Senza descrizione)",
                        Importo = cd.Importo ?? 0,
                        ID_CompensoOrigine = cd.ID_RigaCompenso,
                        ImportoInviatoAllaFatturazione = cd.ImportoInviatoAllaFatturazione ?? 0
                    })
                    .ToList();

                var giudiziali = righe
                    .Where(cd => cd.TipoCompenso != null &&
                                 cd.TipoCompenso.Trim().ToLower() == "giudiziale" &&
                                 !idCompensiUsati.Contains(cd.ID_RigaCompenso))
                    .Select(cd => new
                    {
                        FaseGiudiziale = cd.Descrizione ?? cd.FaseGiudiziale ?? "(Senza descrizione)",
                        Importo = cd.Importo ?? 0,
                        ID_CompensoOrigine = cd.ID_RigaCompenso,
                        ImportoInviatoAllaFatturazione = cd.ImportoInviatoAllaFatturazione ?? 0
                    })
                    .ToList();

                // ======================================================
                // 📎 Documento PDF firmato collegato all’avviso
                // ======================================================
                var documento = db.DocumentiPratiche
                    .Where(d => d.ID_RiferimentoAvvisoParcella == a.ID_AvvisoParcelle &&
                                d.CategoriaDocumento == "Avviso Parcella")
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();

                bool haDocumento = documento != null;
                string nomeFileDocumento = documento?.NomeFile;

                // ======================================================
                // 🧾 Costruzione del ViewModel
                // ======================================================
                var model = new AvvisoParcellaViewModel
                {
                    ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                    ID_Pratiche = (int)a.ID_Pratiche,
                    DataAvviso = a.DataAvviso,
                    TitoloAvviso = a.TitoloAvviso,
                    Importo = importoBase,
                    ImportoAcconto = importoAcconto,
                    Note = a.Note,
                    Stato = pagato ? "Pagato" : a.Stato,
                    MetodoPagamento = a.MetodoPagamento,
                    ID_UtenteCreatore = a.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = percentualeCI,
                    ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                    AliquotaIVA = a.AliquotaIVA,
                    ImportoIVA = a.ImportoIVA,
                    TotaleAvvisoParcella = totaleAvviso,
                    TipologiaAvviso = a.TipologiaAvviso,
                    FaseGiudiziale = a.FaseGiudiziale,
                    RimborsoSpesePercentuale = rimborsoPerc,
                    ImportoRimborsoSpese = a.ImportoRimborsoSpese,
                    ID_CompensoOrigine = a.ID_CompensoOrigine,
                    NomePratica = p.Titolo,
                    NomeUtenteCreatore = result.NomeCreatore,
                    NomeResponsabilePratica = result.NomeResponsabile,
                    NomeOwnerCliente = result.NomeOwner,
                    ID_ResponsabilePratica = p.ID_UtenteResponsabile,
                    ID_OwnerCliente = result.ID_OwnerCliente,
                    DataInvio = a.DataInvio,
                    DataCompetenzaEconomica = a.DataCompetenzaEconomica,
                    StatoIncasso = pagato ? "Pagato" :
                                   (totaleIncassato > 0 ? "Parziale" : "Da incassare")
                };

                // ======================================================
                // ✅ Ritorno completo JSON (incluso file firmato)
                // ======================================================
                return Json(new
                {
                    success = true,
                    avviso = model,
                    fissi,
                    aOre,
                    fasiGiudiziali = giudiziali,
                    importoAcconto,
                    contributoIntegrativoPercentuale = percentualeCI,
                    rimborsoSpesePercentuale = rimborsoPerc,
                    totaleIncassato,
                    residuoEffettivo = importoResiduoEffettivo,
                    // 📎 File firmato
                    haDocumentoCaricato = haDocumento,
                    nomeFileDocumento = nomeFileDocumento
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore GetAvvisoParcella: " + ex);
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public JsonResult GetListaPratiche()
        {
            System.Diagnostics.Debug.WriteLine("========== [GetListaPratiche] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            try
            {
                var praticheBase = (from p in db.Pratiche
                                    join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente into joinCliente
                                    from c in joinCliente.DefaultIfEmpty()
                                    join uResp in db.Utenti on p.ID_UtenteResponsabile equals uResp.ID_Utente into joinResp
                                    from uResp in joinResp.DefaultIfEmpty()
                                    join oOwner in db.OperatoriSinergia on c.ID_Operatore equals oOwner.ID_Operatore into joinOwner
                                    from oOwner in joinOwner.DefaultIfEmpty()
                                    where p.Stato != "Eliminato"
                                    orderby p.Titolo
                                    select new
                                    {
                                        p.ID_Pratiche,
                                        p.Titolo,
                                        p.ID_UtenteResponsabile,
                                        NomeResponsabile = uResp != null ? (uResp.Nome + " " + uResp.Cognome) : "⚠️ Responsabile mancante",
                                        ID_OwnerCliente = c != null ? c.ID_Operatore : (int?)null,
                                        NomeOwner = oOwner != null ? (oOwner.Nome + " " + oOwner.Cognome) : "⚠️ Owner mancante",
                                        Tipologia = p.Tipologia,

                                        // Percentuale CI dalla professione del responsabile
                                        PercentualeContributoIntegrativo =
                                            (from op in db.OperatoriSinergia
                                             join prof in db.Professioni on op.ID_Professione equals prof.ProfessioniID
                                             where op.ID_UtenteCollegato == p.ID_UtenteResponsabile && op.TipoCliente == "Professionista"
                                             select prof.PercentualeContributoIntegrativo).FirstOrDefault()
                                    }).ToList();

                var praticheRisultato = praticheBase.Select(p =>
                {
                    int idPratica = p.ID_Pratiche;

                    // ============================================================
                    // 🔗 Avvisi collegati (per esclusione compensi)
                    // ============================================================
                    var avvisiCollegati = db.AvvisiParcella
                        .Where(a => a.ID_Pratiche == idPratica)
                        .Select(a => new
                        {
                            a.ID_CompensoOrigine,
                            a.FaseGiudiziale,
                            a.Importo,
                            a.ImportoAcconto,
                            a.TotaleAvvisiParcella,
                            a.Stato,
                            a.DataInvio,
                            a.DataCompetenzaEconomica
                        })
                        .ToList();

                    var idCompensiUsati = avvisiCollegati
                        .Where(a => a.ID_CompensoOrigine != null)
                        .Select(a => a.ID_CompensoOrigine.Value)
                        .Distinct()
                        .ToList();

                    var fasiGiaUsate = avvisiCollegati
                        .Where(a => !string.IsNullOrEmpty(a.FaseGiudiziale))
                        .Select(a => a.FaseGiudiziale.Trim().ToLower())
                        .Distinct()
                        .ToList();

                    // ============================================================
                    // 💰 Recupero compensi collegati alla pratica
                    // ============================================================
                    var compensiDisponibili = db.CompensiPraticaDettaglio
                        .Where(cd => cd.ID_Pratiche == idPratica)
                        .ToList();

                    // ============================================================
                    // 🔎 Calcolo importi residui effettivi (dopo incassi)
                    // ============================================================
                    var compensiDettaglio = compensiDisponibili
                        .Where(cd =>
                        {
                            bool giaUsato = idCompensiUsati.Contains(cd.ID_RigaCompenso)
                                         || (cd.Descrizione != null && fasiGiaUsate.Contains(cd.Descrizione.Trim().ToLower()));

                            // Se già usato, controlla se ha residuo
                            if (giaUsato)
                            {
                                decimal importoBase = cd.Importo ?? 0;
                                decimal importoInviato = cd.ImportoInviatoAllaFatturazione ?? 0;

                                return (importoBase > importoInviato); // includi solo se ancora parzialmente aperto
                            }

                            return true; // nuovo, sempre incluso
                        })
                        .Select(cd =>
                        {
                            // 🔍 Calcola eventuali incassi registrati su avvisi collegati
                            var incassiCollegati = (from i in db.Incassi
                                                    join av in db.AvvisiParcella on i.ID_AvvisoParcella equals av.ID_AvvisoParcelle
                                                    where av.ID_Pratiche == idPratica
                                                          && av.ID_CompensoOrigine == cd.ID_RigaCompenso
                                                    select i.Importo).ToList();

                            decimal importoIncassato = incassiCollegati.Sum();
                            decimal importoBase = cd.Importo ?? 0;
                            decimal importoInviato = cd.ImportoInviatoAllaFatturazione ?? 0;
                            decimal residuoEffettivo = (importoBase - importoInviato - importoIncassato);

                            if (residuoEffettivo < 0) residuoEffettivo = 0;

                            return new
                            {
                                cd.Categoria,
                                cd.TipoCompenso,
                                cd.Descrizione,
                                cd.FaseGiudiziale,
                                Importo = importoBase,
                                ImportoInviatoAllaFatturazione = importoInviato,
                                ImportoResiduoEffettivo = residuoEffettivo,
                                cd.Ordine,
                                cd.ID_RigaCompenso,
                                cd.ValoreStimato
                            };
                        })
                        .ToList();

                    // ============================================================
                    // ⚖️ Raggruppamento per fase giudiziale
                    // ============================================================
                    var fasiGiudiziali = compensiDettaglio
                        .Where(cd => cd.TipoCompenso != null && cd.TipoCompenso.Trim().ToLower() == "giudiziale")
                        .OrderBy(cd => cd.Ordine)
                        .Select(cd => new
                        {
                            FaseGiudiziale = cd.Descrizione ?? cd.FaseGiudiziale,
                            Importo = cd.ImportoResiduoEffettivo,
                            ID_CompensoOrigine = cd.ID_RigaCompenso
                        })
                        .ToList();

                    // ============================================================
                    // 📅 Ultima data utile (competenza o invio)
                    // ============================================================
                    DateTime? dataUltimoAvviso = avvisiCollegati
                        .Where(a => a.DataCompetenzaEconomica != null)
                        .OrderByDescending(a => a.DataCompetenzaEconomica)
                        .Select(a => a.DataCompetenzaEconomica)
                        .FirstOrDefault();

                    int totaleAvvisi = avvisiCollegati.Count();

                    // ============================================================
                    // 📦 Output finale pratica
                    // ============================================================
                    return new
                    {
                        p.ID_Pratiche,
                        p.Titolo,
                        p.ID_UtenteResponsabile,
                        p.NomeResponsabile,
                        p.ID_OwnerCliente,
                        p.NomeOwner,
                        p.Tipologia,
                        PercentualeContributoIntegrativo = p.PercentualeContributoIntegrativo ?? 0,
                        CompensiDettaglio = compensiDettaglio,
                        FasiGiudiziali = fasiGiudiziali,
                        TotaleAvvisiCreati = totaleAvvisi,
                        DataUltimoAvviso = dataUltimoAvviso
                    };
                }).ToList();

                return Json(new { success = true, pratiche = praticheRisultato }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore GetListaPratiche: " + ex);
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetIDProfessionistaByPratica(int idPratica)
        {
            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);

            if (pratica == null)
                return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

            // 👤 Responsabile pratica
            int? idResponsabile = pratica.ID_UtenteResponsabile;

            var responsabile = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idResponsabile && o.TipoCliente == "Professionista");

            // 👑 Owner cliente
            int? idOwnerCliente = null;
            OperatoriSinergia owner = null;

            if (pratica.ID_Cliente > 0)
            {
                idOwnerCliente = db.Clienti
                    .Where(c => c.ID_Cliente == pratica.ID_Cliente)
                    .Select(c => c.ID_Operatore)
                    .FirstOrDefault();

                if (idOwnerCliente.HasValue)
                {
                    owner = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Operatore == idOwnerCliente.Value && o.TipoCliente == "Professionista");
                }
            }

            if (responsabile == null && owner == null)
                return Json(new { success = false, message = "Nessun professionista associato alla pratica." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                idResponsabile = responsabile?.ID_Operatore,
                nomeResponsabile = responsabile != null ? responsabile.Nome + " " + responsabile.Cognome : null,
                idOwner = owner?.ID_Operatore,
                nomeOwner = owner != null ? owner.Nome + " " + owner.Cognome : null
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EliminaAvvisoParcella(int id)
        {
            System.Diagnostics.Debug.WriteLine("========== [EliminaAvvisoParcella] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"📩 ID Avviso ricevuto: {id}");

            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);
                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare l’avviso parcella." });

                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == id);
                if (avviso == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avviso.ID_Pratiche);
                int? idPratica = pratica?.ID_Pratiche;
                int? idResponsabile = pratica?.ID_UtenteResponsabile;
                int? idOwnerCliente = db.Clienti
                    .Where(c => c.ID_Cliente == pratica.ID_Cliente)
                    .Select(c => c.ID_Operatore)
                    .FirstOrDefault();

                // ============================================================
                // 🔒 Blocco se l'avviso ha già incassi collegati
                // ============================================================
                var incassiCollegati = db.Incassi.Where(i => i.ID_AvvisoParcella == id).ToList();
                if (incassiCollegati.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Avviso {id} ha {incassiCollegati.Count} incassi collegati.");
                    return Json(new { success = false, message = "Impossibile eliminare: l’avviso è già stato incassato o parzialmente pagato." });
                }

                // ============================================================
                // 🔢 Numero versione precedente
                // ============================================================
                int ultimaVersione = db.AvvisiParcella_a
                    .Where(a => a.ID_Archivio == avviso.ID_AvvisoParcelle)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // ============================================================
                // 🧾 Archivia versione "Eliminata" con le nuove colonne
                // ============================================================
                db.AvvisiParcella_a.Add(new AvvisiParcella_a
                {
                    ID_Archivio = avviso.ID_AvvisoParcelle,
                    ID_Pratiche = avviso.ID_Pratiche,
                    DataAvviso = avviso.DataAvviso,
                    DataInvio = avviso.DataInvio,
                    TitoloAvviso= avviso.TitoloAvviso,
                    DataCompetenzaEconomica = avviso.DataCompetenzaEconomica,
                    Importo = avviso.Importo,
                    ImportoAcconto = avviso.ImportoAcconto,
                    Note = avviso.Note,
                    Stato = "Eliminato",
                    MetodoPagamento = avviso.MetodoPagamento,
                    ID_UtenteCreatore = avviso.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = avviso.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = avviso.ContributoIntegrativoImporto,
                    AliquotaIVA = avviso.AliquotaIVA,
                    ImportoIVA = avviso.ImportoIVA,
                    TotaleAvvisiParcella = avviso.TotaleAvvisiParcella,
                    TipologiaAvviso = avviso.TipologiaAvviso,
                    FaseGiudiziale = avviso.FaseGiudiziale,
                    RimborsoSpesePercentuale = avviso.RimborsoSpesePercentuale,
                    ImportoRimborsoSpese = avviso.ImportoRimborsoSpese,
                    ID_CompensoOrigine = avviso.ID_CompensoOrigine,
                    ID_ResponsabilePratica = idResponsabile,
                    ID_OwnerCliente = idOwnerCliente,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑️ Eliminazione avviso parcella ID={avviso.ID_AvvisoParcelle} " +
                                        $"eseguita da utente ID={idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                // ============================================================
                // 🔄 Ripristina compenso collegato
                // ============================================================
                if (avviso.ID_CompensoOrigine.HasValue)
                {
                    var compenso = db.CompensiPraticaDettaglio.FirstOrDefault(c => c.ID_RigaCompenso == avviso.ID_CompensoOrigine.Value);
                    if (compenso != null)
                    {
                        decimal importoDaStornare = avviso.ImportoAcconto ?? avviso.Importo ?? 0;
                        compenso.ImportoInviatoAllaFatturazione = Math.Max(0, (compenso.ImportoInviatoAllaFatturazione ?? 0) - importoDaStornare);
                        System.Diagnostics.Debug.WriteLine($"↩️ Ripristinato compenso {compenso.ID_RigaCompenso}: -{importoDaStornare:N2} €");
                    }
                }

                // ============================================================
                // 🧹 Rimozione MOVIMENTI ECONOMICI (Bilancio + Economico + Archivio)
                // ============================================================

                // 1️⃣ Bilancio professionista
                var bilancioDelAvviso = db.BilancioProfessionista
                    .Where(b => b.ID_Pratiche == idPratica &&
                                b.Origine == "Avviso Parcella")
                    .ToList();

                foreach (var voce in bilancioDelAvviso)
                {
                    db.BilancioProfessionista.Remove(voce);
                    System.Diagnostics.Debug.WriteLine($"🧾 Rimossa voce bilancio ID={voce.ID_Bilancio}");
                }

                // 2️⃣ Movimenti Economico consolidati
                var economiciDelAvviso = db.Economico
                    .Where(e => e.ID_AvvisoParcella == id)
                    .ToList();

                var economiciIDs = economiciDelAvviso.Select(e => e.ID_Economico).ToList();

                foreach (var eco in economiciDelAvviso)
                {
                    db.Economico.Remove(eco);
                    System.Diagnostics.Debug.WriteLine($"📉 Rimossa voce Economico ID={eco.ID_Economico}");
                }

                // 3️⃣ Eliminazione archivio economico
                var economicoArchDelAvviso = db.Economico_a
                    .Where(ea => economiciIDs.Contains(ea.ID_EconomicoOriginale ?? 0))
                    .ToList();

                foreach (var ecoA in economicoArchDelAvviso)
                {
                    db.Economico_a.Remove(ecoA);
                    System.Diagnostics.Debug.WriteLine($"📜 Rimossa archivio Economico ID={ecoA.ID_EconomicoOriginale}");
                }

                db.SaveChanges();


                // ============================================================
                // ❌ Rimuovi l'avviso principale
                // ============================================================
                db.AvvisiParcella.Remove(avviso);
                db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"✅ Avviso parcella ID={id} eliminato completamente.");
                return Json(new { success = true, message = "✅ Avviso parcella eliminato correttamente." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore EliminaAvvisoParcella: " + ex);
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult EliminaDocumentoAvvisoParcella(int idAvviso)
        {
            try
            {
                System.Diagnostics.Trace.WriteLine($"🗑️ [EliminaDocumentoAvvisoParcella] Avvio per ID_Avviso = {idAvviso}");

                // ✅ Cerca documento collegato all'avviso
                var documento = db.DocumentiPratiche
                    .FirstOrDefault(d => d.ID_RiferimentoAvvisoParcella == idAvviso &&
                                         d.CategoriaDocumento == "Avviso Parcella");

                if (documento == null)
                {
                    System.Diagnostics.Trace.WriteLine("⚠️ Nessun documento trovato per questo avviso.");
                    return Json(new { success = false, message = "❌ Nessun documento associato a questo avviso." });
                }

                // ⚠️ Se il documento è già firmato, non lo eliminiamo
                if (documento.Stato == "Firmato")
                {
                    System.Diagnostics.Trace.WriteLine("⛔ Documento firmato — eliminazione bloccata.");
                    return Json(new
                    {
                        success = false,
                        message = "⚠️ Questo documento è già firmato e non può essere eliminato."
                    });
                }

                // 🗑️ Rimuove riga dal database
                db.DocumentiPratiche.Remove(documento);
                db.SaveChanges();

                System.Diagnostics.Trace.WriteLine($"✅ Documento eliminato correttamente: {documento.NomeFile}");
                return Json(new { success = true, message = "✅ Documento Avviso Parcella eliminato correttamente." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ Errore eliminazione Avviso Parcella: {ex}");
                return Json(new { success = false, message = "❌ Errore durante l'eliminazione: " + ex.Message });
            }
        }



        /* Gestione Template Avviso Parcella*/

        // ===========================================================
        // 📄 GENERA AVVISO PARCELLA DA TEMPLATE (HTML con segnaposti)
        // ===========================================================
        [HttpGet]
        public ActionResult GeneraAvvisoParcella(int idAvviso)
        {
            try
            {
                System.Diagnostics.Trace.WriteLine("══════════════════════════════════════════════════════");
                System.Diagnostics.Trace.WriteLine($"🟦 [GeneraAvvisoParcella] Avvio generazione per ID_Avviso: {idAvviso}");

                // ======================================================
                // 🔍 AVVISO + PRATICA
                // ======================================================
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso);
                if (avviso == null)
                    return Json(new { success = false, message = "❌ Avviso parcella non trovato." }, JsonRequestBehavior.AllowGet);

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avviso.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "❌ Pratica collegata non trovata." }, JsonRequestBehavior.AllowGet);

                // ======================================================
                // 🧾 CLIENTE
                // ======================================================
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);

                // ======================================================
                // 👨‍⚖️ PROFESSIONISTA RESPONSABILE
                // ======================================================
                var professionista = db.Utenti.FirstOrDefault(u => u.ID_Utente == pratica.ID_UtenteResponsabile)
                                    ?? db.Utenti.FirstOrDefault(u => u.ID_Utente == pratica.ID_Owner);

                // ======================================================
                // 🧑‍💼 OWNER SINERGIA
                // ======================================================
                OperatoriSinergia owner = null;
                if (pratica.ID_Owner.HasValue)
                {
                    int idOwner = pratica.ID_Owner.Value;
                    owner = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == idOwner);
                }

                // ======================================================
                // 📄 TEMPLATE ATTIVO
                // ======================================================
                var template = db.TemplateIncarichi
                    .FirstOrDefault(t => t.Stato == "Attivo" && t.TipoCompenso == "Avviso Parcella");

                if (template == null)
                    return Json(new { success = false, message = "❌ Nessun template attivo per Avviso Parcella." }, JsonRequestBehavior.AllowGet);

                string html = template.ContenutoHtml ?? "";

                // ======================================================
                // 💰 CALCOLI IMPORTI
                // ======================================================
                decimal sorte = avviso.Importo ?? 0;
                decimal cassa = avviso.ContributoIntegrativoImporto ?? 0;
                decimal speseGenerali = avviso.ImportoRimborsoSpese ?? 0;
                decimal imponibile = sorte + cassa + speseGenerali;
                decimal iva = avviso.ImportoIVA ?? 0;
                decimal totale = imponibile + iva;

                // ======================================================
                // 🖋️ CONVERSIONE FIRME IN BASE64
                // ======================================================
                var pathFratini = Server.MapPath("~/Content/img/Firme/Firma-Riccardo-Fratini.png");
                var pathDAmico = Server.MapPath("~/Content/img/Firme/Firma-Dario-D_Amico.png");

                string base64Fratini = "";
                string base64DAmico = "";

                if (System.IO.File.Exists(pathFratini))
                {
                    var bytes = System.IO.File.ReadAllBytes(pathFratini);
                    base64Fratini = "data:image/png;base64," + Convert.ToBase64String(bytes);
                }

                if (System.IO.File.Exists(pathDAmico))
                {
                    var bytes = System.IO.File.ReadAllBytes(pathDAmico);
                    base64DAmico = "data:image/png;base64," + Convert.ToBase64String(bytes);
                }

                // ======================================================
                // 🔁 COSTRUZIONE SEGNAPOSTI
                // ======================================================
                var placeholders = new Dictionary<string, string>
                {
                    // === GENERICI ===
                    ["[DATA_CREAZIONE_AVVISO]"] = avviso.DataAvviso?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                    ["[PROGRESSIVO_PRATICA]"] = pratica.ID_Pratiche.ToString(),
                    ["[TITOLO_AVVISO_PARCELLA]"] = avviso.TitoloAvviso ?? pratica?.Titolo ?? "__________",

                    // === FORNITORE / RESPONSABILE ===
                    ["[NOME_PROFESSIONISTA_RESPONSABILE]"] = $"{professionista?.Nome} {professionista?.Cognome}".Trim(),
                    ["[EMAIL_RESPONSABILE_PRATICA]"] = professionista?.MAIL1 ?? professionista?.MAIL2 ?? "__________",

                    // === CLIENTE ===
                    ["[NOME_COGNOME_CLIENTE]"] = $"{cliente?.Nome} {cliente?.Cognome}".Trim(),
                    ["[RAGIONE_SOCIALE_CLIENTE]"] = cliente?.RagioneSociale ?? "__________",
                    ["[INDIRIZZO_CLIENTE]"] = cliente?.Indirizzo ?? "__________",
                    ["[EMAIL_CLIENTE]"] = cliente?.Email ?? "__________",
                    ["[CF CLIENTE]"] = cliente?.CodiceFiscale ?? "__________",
                    ["[PIVA_CLIENTE]"] = cliente?.PIVA ?? "__________",

                    // === REFERENTE CLIENTE / OWNER ===
                    ["[NOME_COGNOME_PROFESSIONISTA]"] = $"{professionista?.Nome} {professionista?.Cognome}".Trim(),
                    ["[OWNER_CLIENTE]"] = $"{owner?.Nome} {owner?.Cognome}".Trim(),
                    ["[EMAIL_OWNER_CLIENTE]"] = owner?.MAIL1 ?? owner?.MAIL2 ?? "__________",

                    // === IMPORTI ===
                    ["[IMPORTO_SORTE]"] = sorte.ToString("N2") + " €",
                    ["[IMPORTO_CASSA_4]"] = cassa.ToString("N2") + " €",
                    ["[IMPORTO_SPESEGENERALI]"] = speseGenerali.ToString("N2") + " €",
                    ["[[TOTALE COMPLESSIVO]_IMPONIBILE]"] = imponibile.ToString("N2") + " €",
                    ["[IMPORTO_IVA]"] = iva.ToString("N2") + " €",
                    ["[TOTALE_COMPLESSIVO]"] = totale.ToString("N2") + " €",

                    // === LOGO ===
                    ["[LOGO_RESPONSABILE]"] = $@"
                <div style='width:100%; text-align:left; margin:0; padding:0; line-height:0;'>
                    <img src='{Url.Content("~/Content/img/Icons/Logo Nuovo.png")}'
                         alt='Logo Sinergia'
                         style='display:block; height:120px; width:auto; margin:0; padding:0; border:none;' />
                </div>",

                    // === FIRME BASE64 ===
                    ["[FIRMA_FRATINI]"] = $@"
                <div style='text-align:left; margin-top:10px;'>
                    <img src='{base64Fratini}'
                         alt='Firma Riccardo Fratini'
                         style='width:90px; height:auto; display:block; margin-top:5px;' />
                </div>",

                    ["[FIRMA_DAMICO]"] = $@"
                <div style='text-align:right; margin-top:10px;'>
                    <img src='{base64DAmico}'
                         alt='Firma Dario D’Amico'
                         style='width:90px; height:auto; display:block; margin-top:5px;' />
                </div>"
                };

                // ======================================================
                // 🔄 SOSTITUZIONE SEGNAPOSTI
                // ======================================================
                foreach (var kv in placeholders)
                    html = html.Replace(kv.Key, kv.Value ?? "__________");

                // ======================================================
                // 🧩 Allineamento orizzontale delle due firme
                // ======================================================
                html = Regex.Replace(
                    html,
                    @"(\[FIRMA_FRATINI\].*?\[FIRMA_DAMICO\])",
                    @"<div style='width:100%; margin-top:20px;'>
                 <div style='display:inline-block; width:48%; text-align:left;'>[FIRMA_FRATINI]</div>
                 <div style='display:inline-block; width:48%; text-align:right;'>[FIRMA_DAMICO]</div>
              </div>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline
                );

                System.Diagnostics.Trace.WriteLine("✅ Sostituzione completata, restituzione HTML...");
                return Json(new { success = true, html }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ Errore: {ex.Message}");
                return Json(new { success = false, message = "❌ Errore durante la generazione dell'avviso: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ===========================================================
        // 🖨️ GENERA PDF AVVISO PARCELLA (Rotativa)
        // ===========================================================
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult GeneraPDFAvvisoParcellaDaHtml()
        {
            try
            {
                // ======================================================
                // 🔍 VALIDAZIONE INPUT
                // ======================================================
                if (!int.TryParse(Request.Form["idAvviso"], out int idAvviso))
                    return Json(new { success = false, message = "❌ ID avviso non valido." });

                string html = Request.Unvalidated["html"];
                if (string.IsNullOrWhiteSpace(html))
                    return Json(new { success = false, message = "❌ Contenuto HTML mancante." });

                // ======================================================
                // 🔍 DATI AVVISO, PRATICA E CLIENTE
                // ======================================================
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso);
                if (avviso == null)
                    return Json(new { success = false, message = "❌ Avviso parcella non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avviso.ID_Pratiche);
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);

                // ======================================================
                // 🧹 PULIZIA HTML (rettangolini, shape, immagini vuote)
                // ======================================================
                html = Regex.Replace(html, @"background(-color)?:\s*[^;""']+;?", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<div[^>]*(border|width|height)[^>]*>\s*</div>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<span[^>]*(width|height)\s*:\s*\d+(\.\d+)?(pt|in|cm|mm)[^>]*>\s*</span>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<p[^>]*>\s*(<span[^>]*(width|height)\s*:\s*\d+(\.\d+)?(pt|in|cm|mm)[^>]*>\s*</span>)+\s*</p>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<p[^>]*>(\s|&nbsp;)*</p>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<div[^>]*>(\s|&nbsp;)*</div>", "", RegexOptions.IgnoreCase);
                html = Regex.Replace(html, @"<img[^>]*(src\s*=\s*['""]\s*['""][^>]*)?>", "", RegexOptions.IgnoreCase);
                html = html.Trim();

                // ======================================================
                // ✍️ FIRME (file temporanei compatibili con Rotativa)
                // ======================================================
                string firmaFratiniFile = "", firmaDAmicoFile = "";
                try
                {
                    string tempDir = Server.MapPath("~/Content/temp/");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                    string pathFratini = Server.MapPath("~/Content/img/Firme/Firma-Riccardo-Fratini.png");
                    string pathDAmico = Server.MapPath("~/Content/img/Firme/Firma-Dario-D_Amico.png");

                    if (System.IO.File.Exists(pathFratini))
                    {
                        firmaFratiniFile = Path.Combine(tempDir, "firma_fratini_temp.png");
                        System.IO.File.Copy(pathFratini, firmaFratiniFile, true);
                    }

                    if (System.IO.File.Exists(pathDAmico))
                    {
                        firmaDAmicoFile = Path.Combine(tempDir, "firma_damico_temp.png");
                        System.IO.File.Copy(pathDAmico, firmaDAmicoFile, true);
                    }

                    Trace.WriteLine($"✅ Firme temporanee salvate: {firmaFratiniFile} | {firmaDAmicoFile}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("⚠️ Errore salvataggio firme temporanee: " + ex.Message);
                }
                // ======================================================
                // 🔁 INSERISCI BLOCCO FIRME SUBITO DOPO "Distinti Saluti"
                // ======================================================
                string firmeHtml = $@"
<div style='width:100%; margin-top:5px; margin-bottom:-10px; page-break-inside:avoid;'>
    <table style='width:100%; border:none; border-collapse:collapse;'>
        <tr>
            <td style='width:50%; text-align:left; vertical-align:top; border:none;'>
                <div style='margin-bottom:2px; font-weight:bold; font-family:Calibri, sans-serif;'>
                    Avv. Riccardo Fratini – Presidente CdA
                </div>
                <img src='file:///{firmaFratiniFile.Replace("\\", "/")}' 
                     alt='Firma Riccardo Fratini'
                     style='width:95px; height:auto; margin-top:-3px; display:block;' />
            </td>
            <td style='width:50%; text-align:right; vertical-align:top; border:none;'>
                <div style='margin-bottom:2px; font-weight:bold; font-family:Calibri, sans-serif;'>
                    Avv. Dario D’Amico – Vice-Presidente CdA
                </div>
                <img src='file:///{firmaDAmicoFile.Replace("\\", "/")}' 
                     alt='Firma Dario D’Amico'
                     style='width:95px; height:auto; margin-top:-3px; display:block;' />
            </td>
        </tr>
    </table>
</div>";

                html = Regex.Replace(html,
                    "(Distinti\\s*Saluti\\.?)(</p>|</div>|<br\\s*/?>)?",
                    "$1<br/>" + firmeHtml + "$2",
                    RegexOptions.IgnoreCase);

                Trace.WriteLine("✍️ Blocco firme inserito dopo 'Distinti Saluti.'");


                // ======================================================
                // 💄 CSS PDF
                // ======================================================
                string css = @"
<style>
@page { margin: 1.2cm; }
body {
    font-family: 'Calibri', Arial, sans-serif;
    font-size: 11pt;
    line-height: 1.4;
    color: #000;
    margin: 0;
}
table {
    border-collapse: collapse !important;
    page-break-inside: avoid !important;
    width: 100% !important;
}
img {
    page-break-inside: avoid !important;
    display: inline-block !important;
}
td, th {
    padding: 3px 6px !important;
    font-size: 10pt !important;
}
.page-break { page-break-after: always !important; }
</style>";

                html = css + html;

                // ======================================================
                // 🖨️ GENERAZIONE PDF
                // ======================================================
                string nomeCliente = cliente?.RagioneSociale ?? $"{cliente?.Cognome}_{cliente?.Nome}";
                foreach (var c in Path.GetInvalidFileNameChars()) nomeCliente = nomeCliente.Replace(c, '_');
                string nomeFile = $"AvvisoParcella_{nomeCliente}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                var pdf = new Rotativa.ViewAsPdf("~/Views/TemplateIncarichi/TemplateCompilato.cshtml", (object)html)
                {
                    FileName = nomeFile,
                    PageSize = Rotativa.Options.Size.A4,
                    PageMargins = new Rotativa.Options.Margins(8, 8, 12, 8),
                    CustomSwitches = "--disable-forms --print-media-type --disable-smart-shrinking --zoom 1.1"
                };

                byte[] pdfBytes = pdf.BuildPdf(ControllerContext);

                // ======================================================
                // 💾 SALVATAGGIO DOCUMENTO
                // ======================================================
                var documento = new DocumentiPratiche
                {
                    ID_Pratiche = pratica.ID_Pratiche,
                    ID_RiferimentoAvvisoParcella = idAvviso,
                    NomeFile = nomeFile,
                    Documento = pdfBytes,
                    Estensione = ".pdf",
                    TipoContenuto = "application/pdf",
                    Stato = "Da firmare",
                    CategoriaDocumento = "Avviso Parcella",
                    Note = "Avviso parcella generato automaticamente",
                    DataCaricamento = DateTime.Now,
                    ID_UtenteCaricamento = UserManager.GetIDUtenteCollegato()
                };

                db.DocumentiPratiche.Add(documento);
                db.SaveChanges();

                // ======================================================
                // 🧹 PULIZIA FILE TEMPORANEI
                // ======================================================
                try
                {
                    if (System.IO.File.Exists(firmaFratiniFile)) System.IO.File.Delete(firmaFratiniFile);
                    if (System.IO.File.Exists(firmaDAmicoFile)) System.IO.File.Delete(firmaDAmicoFile);
                    Trace.WriteLine("🧹 File temporanei firme eliminati correttamente.");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("⚠️ Errore eliminazione file temporanei: " + ex.Message);
                }

                Trace.WriteLine($"✅ PDF salvato correttamente come {nomeFile}");
                return Json(new { success = true, message = "✅ Avviso Parcella generato e salvato correttamente." });
            }
            catch (Exception ex)
            {
                Trace.WriteLine("❌ Errore generazione PDF: " + ex.Message);
                return Json(new { success = false, message = "❌ Errore generazione PDF: " + ex.Message });
            }
        }


        // ===========================================================
        // 📥 UPLOAD AVVISO PARCELLA FIRMATO (SALVATAGGIO IN DATABASE)
        // ===========================================================
        [HttpPost]
        public ActionResult SalvaAvvisoParcellaFirmato(int idAvviso, HttpPostedFileBase file)
        {
            try
            {
                // ======================================================
                // 🔍 Validazioni base
                // ======================================================
                if (file == null || file.ContentLength == 0)
                    return Json(new { success = false, message = "❌ Nessun file selezionato." });

                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return Json(new { success = false, message = "❌ Il file deve essere in formato PDF." });

                // ======================================================
                // 📦 Recupera avviso e pratica associata
                // ======================================================
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso);
                if (avviso == null)
                    return Json(new { success = false, message = "❌ Avviso parcella non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avviso.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "❌ Pratica non trovata." });

                // ======================================================
                // 💾 Lettura file binario
                // ======================================================
                byte[] fileData;
                using (var binaryReader = new BinaryReader(file.InputStream))
                    fileData = binaryReader.ReadBytes(file.ContentLength);

                // ======================================================
                // 🗂️ Crea o aggiorna documento esistente per QUESTO AVVISO
                // ======================================================
                var documentoEsistente = db.DocumentiPratiche
                    .FirstOrDefault(d => d.ID_RiferimentoAvvisoParcella == idAvviso &&
                                         d.CategoriaDocumento == "Avviso Parcella");

                if (documentoEsistente == null)
                {
                    // 🔹 Inserimento nuovo documento
                    var documento = new DocumentiPratiche
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_RiferimentoAvvisoParcella = idAvviso,
                        NomeFile = Path.GetFileName(file.FileName),
                        Estensione = ".pdf",
                        TipoContenuto = "application/pdf",
                        Documento = fileData,
                        Stato = "Firmato",
                        CategoriaDocumento = "Avviso Parcella",
                        Note = "Avviso parcella firmato caricato manualmente",
                        DataCaricamento = DateTime.Now,
                        ID_UtenteCaricamento = UserManager.GetIDUtenteCollegato()
                    };

                    db.DocumentiPratiche.Add(documento);
                }
                else
                {
                    // 🔄 Aggiornamento documento già presente
                    documentoEsistente.Documento = fileData;
                    documentoEsistente.NomeFile = Path.GetFileName(file.FileName);
                    documentoEsistente.DataCaricamento = DateTime.Now;
                    documentoEsistente.Note = "Avviso parcella firmato aggiornato";
                    documentoEsistente.ID_UtenteCaricamento = UserManager.GetIDUtenteCollegato();
                }

                db.SaveChanges();

                // ======================================================
                // ✅ Ritorno per aggiornare UI immediatamente
                // ======================================================
                return Json(new
                {
                    success = true,
                    message = "✅ Avviso parcella firmato caricato correttamente.",
                    nomeFile = Path.GetFileName(file.FileName),
                    idAvviso = idAvviso
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ Errore durante il caricamento: " + ex.Message
                });
            }
        }


        // ===========================================================
        // 📚 ELENCO DOCUMENTI AVVISO PARCELLA
        // ===========================================================
        [HttpGet]
        public ActionResult GetDocumentiAvvisoParcella(int idAvviso)
        {
            try
            {
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso);
                if (avviso == null)
                    return Json(new { success = false, message = "❌ Avviso non trovato." }, JsonRequestBehavior.AllowGet);

                var praticaId = avviso.ID_Pratiche;

                var documenti = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == praticaId && d.CategoriaDocumento.Contains("Avviso Parcella"))
                    .Select(d => new
                    {
                        d.ID_Documento,
                        d.NomeFile,
                        d.Stato,
                        d.DataCaricamento,
                        d.Note
                    })
                    .OrderByDescending(d => d.DataCaricamento)
                    .ToList();

                return Json(new { success = true, documenti }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante il caricamento dei documenti: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult ScaricaAvvisoParcella(int idAvviso)
        {
            try
            {
                var documento = db.DocumentiPratiche
                    .Where(d => d.ID_RiferimentoAvvisoParcella == idAvviso &&
                                d.CategoriaDocumento == "Avviso Parcella")
                    .OrderByDescending(d => d.DataCaricamento)
                    .FirstOrDefault();

                if (documento == null)
                    return HttpNotFound("❌ Nessun documento trovato per questo avviso parcella.");

                string nomeFile = !string.IsNullOrWhiteSpace(documento.NomeFile)
                    ? documento.NomeFile
                    : $"AvvisoParcella_{idAvviso}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(documento.Documento, "application/pdf", nomeFile);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError,
                    "Errore durante il download: " + ex.Message);
            }
        }


        /*Fine Gestione Template Avviso Parcella */

        [HttpGet]
        public ActionResult EsportaAvvisiParcellaCsv(DateTime? da, DateTime? a)
        {
            System.Diagnostics.Debug.WriteLine("========== [EsportaAvvisiParcellaCsv] DEBUG AVVIO ==========");

            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            bool isAdmin = utenteCorrente.TipoUtente == "Admin";

            // ============================================================
            // 🔥 RECUPERO PROFESSIONISTA DA NAVBAR
            // ============================================================
            int idProfessionistaFiltro = 0;

            if (isAdmin)
            {
                idProfessionistaFiltro = Session["IDClienteProfessionistaCorrente"] != null
                    ? Convert.ToInt32(Session["IDClienteProfessionistaCorrente"])
                    : 0;

                System.Diagnostics.Debug.WriteLine("Filtro navbar Admin = " + idProfessionistaFiltro);
            }
            else
            {
                idProfessionistaFiltro = UserManager.GetOperatoreDaUtente(idUtenteLoggato);
                System.Diagnostics.Debug.WriteLine("Filtro utente non-admin = " + idProfessionistaFiltro);
            }

            // ============================================================
            // 📅 RANGE DATE
            // ============================================================
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            System.Diagnostics.Debug.WriteLine($"📅 Range CSV: {inizio:dd/MM/yyyy} → {fine:dd/MM/yyyy}");

            // ============================================================
            // 🔎 QUERY AVVISI + FILTRO OWNER (DA NAVBAR)
            // ============================================================
            var query = from avv in db.AvvisiParcella
                        join pr in db.Pratiche on avv.ID_Pratiche equals pr.ID_Pratiche
                        where avv.DataAvviso >= inizio &&
                              avv.DataAvviso <= fine &&
                              pr.Stato != "Eliminato"
                        select new { avv, pr };

            // 🔥 Applico filtro professionista
            if (idProfessionistaFiltro > 0)
            {
                query = query.Where(x => x.pr.ID_Owner == idProfessionistaFiltro);
                System.Diagnostics.Debug.WriteLine("APPLICATO filtro owner = " + idProfessionistaFiltro);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("NESSUN filtro owner applicato (admin totale)");
            }

            var lista = query.ToList();

            System.Diagnostics.Debug.WriteLine("Totale avvisi trovati = " + lista.Count);

            // ============================================================
            // 🧾 COSTRUZIONE CSV
            // ============================================================
            var sb = new StringBuilder();

            sb.AppendLine("ID Avviso;Pratica;Responsabile;Owner Cliente;Tipologia;Fase Giudiziale;" +
                          "Data Avviso;Data Invio;Data Competenza;Trimestre Competenza;" +
                          "Importo Base;Importo Acconto;Contributo Integrativo (%);Importo CI;" +
                          "IVA (%);Importo IVA;Rimborso Spese (%);Importo Rimborso;Totale Avviso;" +
                          "Totale Incassato;Residuo Effettivo;Stato Incasso;Metodo Pagamento;Note");

            foreach (var x in lista)
            {
                var avv = x.avv;
                var pratica = x.pr;

                string resp = db.Utenti.Where(u => u.ID_Utente == pratica.ID_UtenteResponsabile)
                                       .Select(u => u.Nome + " " + u.Cognome)
                                       .FirstOrDefault() ?? "(N/D)";

                string owner = db.OperatoriSinergia
                    .Where(o => o.ID_Operatore == pratica.ID_Owner)
                    .Select(o => o.Nome + " " + o.Cognome)
                    .FirstOrDefault() ?? "(N/D)";

                // 🧮 Importi
                decimal baseImp = avv.Importo ?? 0;
                decimal acconto = avv.ImportoAcconto ?? 0;
                decimal ciPerc = avv.ContributoIntegrativoPercentuale ?? 0;
                decimal ciImp = avv.ContributoIntegrativoImporto ?? 0;
                decimal ivaPerc = avv.AliquotaIVA ?? 0;
                decimal ivaImp = avv.ImportoIVA ?? 0;
                decimal rimPerc = avv.RimborsoSpesePercentuale ?? 0;
                decimal rimImp = avv.ImportoRimborsoSpese ?? 0;

                decimal totale = avv.TotaleAvvisiParcella ?? (baseImp + ciImp + ivaImp + rimImp);

                var incassi = db.Incassi.Where(i => i.ID_AvvisoParcella == avv.ID_AvvisoParcelle).ToList();
                decimal totInc = incassi.Sum(i => i.Importo);
                decimal residuo = Math.Max(0, totale - totInc);

                string statoIncasso = totInc == 0 ? "Da incassare" :
                                      residuo > 0 ? "Parziale" : "Pagato";

                DateTime? dataComp = avv.DataCompetenzaEconomica ?? avv.DataAvviso;
                string trimestre = dataComp.HasValue
                    ? $"T{((dataComp.Value.Month - 1) / 3) + 1} {dataComp.Value.Year}"
                    : "N/D";

                sb.AppendLine($"{avv.ID_AvvisoParcelle};" +
                              $"{pratica.Titolo};" +
                              $"{resp};" +
                              $"{owner};" +
                              $"{avv.TipologiaAvviso};" +
                              $"{avv.FaseGiudiziale};" +
                              $"{avv.DataAvviso:dd/MM/yyyy};" +
                              $"{(avv.DataInvio?.ToString("dd/MM/yyyy") ?? "-")};" +
                              $"{(dataComp?.ToString("dd/MM/yyyy") ?? "-")};" +
                              $"{trimestre};" +
                              $"{baseImp:N2};" +
                              $"{acconto:N2};" +
                              $"{ciPerc:N0}%;" +
                              $"{ciImp:N2};" +
                              $"{ivaPerc:N0}%;" +
                              $"{ivaImp:N2};" +
                              $"{rimPerc:N0}%;" +
                              $"{rimImp:N2};" +
                              $"{totale:N2};" +
                              $"{totInc:N2};" +
                              $"{residuo:N2};" +
                              $"{statoIncasso};" +
                              $"{avv.MetodoPagamento};" +
                              $"{avv.Note?.Replace(";", ",")}");
            }

            // ============================================================
            // 📤 DOWNLOAD CSV
            // ============================================================
            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            string fileName = $"AvvisiParcella_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv";

            return File(buffer, "text/csv", fileName);
        }




        [HttpGet]
        public ActionResult EsportaAvvisiParcellaPdf(DateTime? da, DateTime? a)
        {
            System.Diagnostics.Debug.WriteLine("========== [EsportaAvvisiParcellaPdf] DEBUG AVVIO ==========");
            System.Diagnostics.Debug.WriteLine($"🕓 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Range date (mese corrente di default)
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            System.Diagnostics.Debug.WriteLine($"📆 Range: {inizio:dd/MM/yyyy} → {fine:dd/MM/yyyy}");

            // 📊 Recupero avvisi nel range con arricchimento dati
            var lista = db.AvvisiParcella
                .Where(avv => avv.DataAvviso >= inizio && avv.DataAvviso <= fine)
                .OrderBy(avv => avv.DataAvviso)
                .ToList()
                .Select(avv =>
                {
                    // 🔍 Pratica collegata
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avv.ID_Pratiche);

                    // 👤 Responsabile pratica
                    string nomeResponsabile = "(N/D)";
                    if (pratica?.ID_UtenteResponsabile != null)
                    {
                        var resp = db.Utenti.FirstOrDefault(u => u.ID_Utente == pratica.ID_UtenteResponsabile);
                        if (resp != null)
                            nomeResponsabile = $"{resp.Nome} {resp.Cognome}";
                    }

                    // 👑 Owner cliente
                    string nomeOwner = "(N/D)";
                    if (pratica?.ID_Cliente != null)
                    {
                        nomeOwner = (from c in db.Clienti
                                     join o in db.OperatoriSinergia on c.ID_Operatore equals o.ID_Operatore
                                     where c.ID_Cliente == pratica.ID_Cliente
                                     select o.Nome + " " + o.Cognome).FirstOrDefault() ?? "(N/D)";
                    }

                    // ============================================================
                    // 💰 Calcoli economici e incassi
                    // ============================================================
                    decimal importoBase = avv.Importo ?? 0;
                    decimal importoAcconto = avv.ImportoAcconto ?? 0;
                    decimal ciPerc = avv.ContributoIntegrativoPercentuale ?? 0;
                    decimal ciImporto = avv.ContributoIntegrativoImporto ?? 0;
                    decimal ivaPerc = avv.AliquotaIVA ?? 0;
                    decimal ivaImporto = avv.ImportoIVA ?? 0;
                    decimal rimborsoPerc = avv.RimborsoSpesePercentuale ?? 0;
                    decimal rimborsoImporto = avv.ImportoRimborsoSpese ?? 0;

                    decimal totaleAvviso = avv.TotaleAvvisiParcella ??
                                           (importoBase + ciImporto + ivaImporto + rimborsoImporto);

                    // 💳 Incassi collegati
                    var incassi = db.Incassi.Where(i => i.ID_AvvisoParcella == avv.ID_AvvisoParcelle).ToList();
                    decimal totaleIncassato = incassi.Sum(i => i.Importo);
                    decimal residuoEffettivo = totaleAvviso - totaleIncassato;
                    if (residuoEffettivo < 0) residuoEffettivo = 0;

                    string statoIncasso = totaleIncassato == 0 ? "Da incassare" :
                                          (residuoEffettivo > 0 ? "Parziale" : "Pagato");

                    // ============================================================
                    // 🗓️ Date e trimestre di competenza
                    // ============================================================
                    DateTime? dataInvio = avv.DataInvio;
                    DateTime? dataCompetenza = avv.DataCompetenzaEconomica ?? avv.DataAvviso;
                    string trimestre = "N/D";

                    if (dataCompetenza.HasValue)
                    {
                        int month = dataCompetenza.Value.Month;
                        int quarter = (month - 1) / 3 + 1;
                        trimestre = $"T{quarter} {dataCompetenza.Value.Year}";
                    }

                    return new AvvisoParcellaViewModel
                    {
                        ID_AvvisoParcelle = avv.ID_AvvisoParcelle,
                        ID_Pratiche = avv.ID_Pratiche ?? 0,
                        NomePratica = pratica?.Titolo ?? "(N/D)",
                        NomeResponsabilePratica = nomeResponsabile,
                        NomeOwnerCliente = nomeOwner,

                        DataAvviso = avv.DataAvviso,
                        DataInvio = dataInvio,
                        DataCompetenzaEconomica = dataCompetenza,
                        TrimestreCompetenza = trimestre,

                        Stato = statoIncasso,
                        MetodoPagamento = avv.MetodoPagamento,
                        Note = avv.Note,

                        TipologiaAvviso = avv.TipologiaAvviso,
                        FaseGiudiziale = avv.FaseGiudiziale,
                        ID_CompensoOrigine = avv.ID_CompensoOrigine,

                        // 💵 Dati economici completi
                        Importo = importoBase,
                        ImportoAcconto = importoAcconto,
                        ContributoIntegrativoPercentuale = ciPerc,
                        ContributoIntegrativoImporto = ciImporto,
                        AliquotaIVA = ivaPerc,
                        ImportoIVA = ivaImporto,
                        RimborsoSpesePercentuale = rimborsoPerc,
                        ImportoRimborsoSpese = rimborsoImporto,
                        TotaleAvvisoParcella = totaleAvviso,
                        TotaleIncassato = totaleIncassato,
                        ImportoResiduoEffettivo = residuoEffettivo
                    };
                })
                .ToList();

            // ============================================================
            // 📄 Generazione PDF orizzontale per leggibilità
            // ============================================================
            return new Rotativa.ViewAsPdf("~/Views/AvvisiParcella/PDF_AvvisiParcella_Riepilogo.cshtml", lista)
            {
                FileName = $"AvvisiParcella_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageMargins = new Rotativa.Options.Margins(15, 10, 15, 10)
            };
        }

    
        #endregion

        #region REGISTRAZIONE INCASSI 

        public ActionResult GestioneIncassi()
        {
            return View("~/Views/Incassi/GestioneIncassi.cshtml");
        }
        
        [HttpGet]
        public ActionResult GestioneIncassiList(int? idPratica = null)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            string tipoUtente = utenteCorrente.TipoUtente?.Trim().ToLowerInvariant() ?? "";

            // ======================================================
            // 🔐 PERMESSI
            // ======================================================
            bool puoAggiungere = false;
            bool puoModificare = false;
            bool puoEliminare = false;

            if (tipoUtente == "admin")
            {
                puoAggiungere = puoModificare = puoEliminare = true;
            }
            else
            {
                var permessiDb = db.Permessi
                    .Where(p => p.ID_Utente == idUtenteCorrente)
                    .ToList();

                puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
                puoModificare = permessiDb.Any(p => p.Modifica == true);
                puoEliminare = permessiDb.Any(p => p.Elimina == true);
            }

            // ======================================================
            // 🔎 QUERY BASE → AVVISI PARCELLA
            // ======================================================
            IQueryable<AvvisiParcella> query = db.AvvisiParcella
                .Where(a =>
                    a.Stato == "Inviato" ||
                    a.Stato == "Parziale" ||
                    a.Stato == "Pagato");

            // ======================================================
            // 🔐 FILTRO VISIBILITÀ
            // ======================================================
            if (tipoUtente == "professionista")
            {
                query = query.Where(a =>
                    a.ID_ResponsabilePratica == idUtenteCorrente);
            }
            else if (tipoUtente == "collaboratore")
            {
                var professionistiCollegati = (
                    from r in db.RelazioneUtenti
                    join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                    where r.ID_UtenteAssociato == idUtenteCorrente
                          && r.Stato == "Attivo"
                          && o.TipoCliente == "Professionista"
                          && o.ID_UtenteCollegato.HasValue
                    select o.ID_UtenteCollegato.Value
                ).Distinct().ToList();

                if (!professionistiCollegati.Any())
                    query = query.Where(a => false);
                else
                    query = query.Where(a =>
                        professionistiCollegati.Contains(a.ID_ResponsabilePratica.Value));
            }
            // ADMIN → nessun filtro

            // ======================================================
            // 🎯 FILTRO PRATICA
            // ======================================================
            if (idPratica.HasValue)
                query = query.Where(a => a.ID_Pratiche == idPratica.Value);

            // ======================================================
            // 📘 MATERIALIZZO
            // ======================================================
            var avvisi = query
                .OrderByDescending(a => a.DataAvviso)
                .ToList();

            // ======================================================
            // 📊 VIEWMODEL
            // ======================================================
            var elenco = avvisi.Select(a =>
            {
                var incasso = db.Incassi
                    .Where(i => i.ID_AvvisoParcella == a.ID_AvvisoParcelle)
                    .OrderByDescending(i => i.DataIncasso)
                    .FirstOrDefault();

                return new IncassoViewModel
                {
                    ID_AvvisoParcella = a.ID_AvvisoParcelle,
                    ID_Pratiche = a.ID_Pratiche ?? 0,

                    NomePratica = db.Pratiche
                        .Where(p => p.ID_Pratiche == a.ID_Pratiche)
                        .Select(p => p.Titolo)
                        .FirstOrDefault() ?? "(N/D)",

                    DataCompetenza = a.DataAvviso,
                    DataIncasso = incasso?.DataIncasso,
                    Importo = a.TotaleAvvisiParcella ?? a.Importo ?? 0,

                    StatoAvviso = a.Stato,
                    Stato = incasso == null
                        ? "Da incassare"
                        : incasso.StatoIncasso,

                    MetodoPagamento = incasso?.ModalitaPagamento ?? "—",
                    VersaInPlafond = incasso?.VersaInPlafond ?? false,

                    // ✅ AZIONI RIPRISTINATE
                    
                    PuoModificare = puoModificare,
                    PuoEliminare = puoEliminare
                };
            }).ToList();

            // ======================================================
            // 🔐 VIEWBAG
            // ======================================================
            ViewBag.PuoAggiungere = puoAggiungere;
            ViewBag.PuoModificare = puoModificare;
            ViewBag.PuoEliminare = puoEliminare;

            return PartialView("~/Views/Incassi/_GestioneIncassiList.cshtml", elenco);
        }




        [HttpPost]
        public ActionResult CreaIncasso(IncassoViewModel model)
        {
            // ====================================================
            // 🌍 Cultura invariata: numeri col punto (SQL friendly)
            // ====================================================
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

            // ====================================================
            // 💰 Normalizza l'importo PRIMA del controllo ModelState
            // ====================================================
            if (Request.Form["Importo"] != null)
            {
                var raw = Request.Form["Importo"].Trim().Replace(",", ".");
                if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                {
                    model.Importo = parsed;
                    System.Diagnostics.Trace.WriteLine($"✅ [CreaIncasso] Importo normalizzato: {parsed:N2}");
                    ModelState.Remove("Importo");
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine($"⚠️ [CreaIncasso] Importo non convertibile: {raw}");
                }
            }

            if (!ModelState.IsValid)
            {
                var errori = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage + (e.Exception != null ? $" | EX: {e.Exception.Message}" : ""))
                    .ToList();
                System.Diagnostics.Trace.WriteLine("❌ [CreaIncasso] Errori ModelState:");
                foreach (var err in errori)
                    System.Diagnostics.Trace.WriteLine("   → " + err);
                return Json(new { success = false, message = "Errore nei dati inviati: " + string.Join("; ", errori) });
            }

            // ====================================================
            // 🔐 Controllo utente e permessi
            // ====================================================
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                               db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Aggiungi == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per registrare un incasso." });

            try
            {
                DateTime now = DateTime.Now;

                // ====================================================
                // 1️⃣ PRATICA + AVVISO
                // ====================================================
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == model.ID_AvvisoParcella);
                if (avviso == null)
                    return Json(new { success = false, message = "Avviso parcella collegato non trovato." });

                // ====================================================
                // 2️⃣ IMPORTI E CONTROLLI
                // ====================================================
                decimal totaleAvviso = avviso.TotaleAvvisiParcella ?? avviso.Importo ?? 0m;
                if (totaleAvviso <= 0)
                    return Json(new { success = false, message = "L'importo totale dell’avviso non è valido." });

                decimal giaIncassato = db.Incassi
                    .Where(i => i.ID_AvvisoParcella == avviso.ID_AvvisoParcelle)
                    .Select(i => (decimal?)i.Importo)
                    .DefaultIfEmpty(0)
                    .Sum() ?? 0m;

                decimal residuo = Math.Max(0m, totaleAvviso - giaIncassato);
                decimal importoIncasso = model.Importo;

                if (importoIncasso <= 0)
                    return Json(new { success = false, message = "Inserire un importo incasso valido." });

                if (importoIncasso > residuo)
                    return Json(new
                    {
                        success = false,
                        message = $"L'incasso supera il residuo dell'avviso.\nResiduo: {residuo:N2} €"
                    });

                // ====================================================
                // 3️⃣ DATE DI RIFERIMENTO
                // ====================================================
                DateTime dataCompetenzaEconomica = avviso.DataAvviso ?? now;
                DateTime dataIncasso = model.DataIncasso ?? now;

                // ====================================================
                // 4️⃣ 🔢 CALCOLI ACCESSORI — LOGICA POST-TRATTENUTA
                // ====================================================
                decimal baseImponibile = avviso.Importo ?? 0m;

                // 🔹 Trattenuta Sinergia
                var ricTratt = db.RicorrenzeCosti.FirstOrDefault(r =>
                    r.Categoria == "Trattenuta Sinergia" && r.Attivo && r.TipoValore == "Percentuale");
                decimal percTrattenuta = ricTratt?.Valore ?? 0m;
                decimal quotaTrattenuta = Math.Round(baseImponibile * (percTrattenuta / 100m), 2);
                decimal baseDopoTrattenuta = baseImponibile - quotaTrattenuta;

                // 🔹 Percentuali CI e Spese Generali
                decimal percSpeseGenerali = avviso.RimborsoSpesePercentuale ?? 0m;
                decimal percCI = avviso.ContributoIntegrativoPercentuale ?? 0m;

                // 🔹 Calcoli
                // =====================================================
                // 📊 SPESE GENERALI = imponibile × %
                // =====================================================
                decimal speseGeneraliImporto = Math.Round(
                    baseImponibile * (percSpeseGenerali / 100m),
                2);

                // =====================================================
                // 📊 CONTRIBUTO INTEGRATIVO = (imponibile + speseGenerali) × %
                // =====================================================
                decimal contributoIntegrativoImporto = Math.Round(
                    (baseImponibile + speseGeneraliImporto) * (percCI / 100m),
                2);



                // 🔹 IVA
                decimal ivaAvviso = avviso.ImportoIVA ?? 0m;
                decimal fattore = totaleAvviso > 0 ? (importoIncasso / totaleAvviso) : 0m;
                decimal ivaProRata = Math.Round(ivaAvviso * fattore, 2);

                // 🔹 Totale netto plafond coerente
                decimal importoNettoPlafond = Math.Round(importoIncasso + speseGeneraliImporto, 2);

                System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");
                System.Diagnostics.Trace.WriteLine($"📘 [CreaIncasso] Calcolo post-trattenuta:");
                System.Diagnostics.Trace.WriteLine($"   Base Imponibile ............ {baseImponibile:N2} €");
                System.Diagnostics.Trace.WriteLine($"   Trattenuta Sinergia ({percTrattenuta:N1}%) .... {quotaTrattenuta:N2} €");
                System.Diagnostics.Trace.WriteLine($"   Base dopo trattenuta ....... {baseDopoTrattenuta:N2} €");
                System.Diagnostics.Trace.WriteLine($"   Spese Generali ({percSpeseGenerali:N1}%) .... {speseGeneraliImporto:N2} €");
                System.Diagnostics.Trace.WriteLine($"   Contributo Integrativo ({percCI:N1}%) .... {contributoIntegrativoImporto:N2} €");
                System.Diagnostics.Trace.WriteLine($"   IVA (pro-rata) ............. {ivaProRata:N2} €");
                System.Diagnostics.Trace.WriteLine($"   Totale Netto Plafond ....... {importoNettoPlafond:N2} €");
                System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");

                // ====================================================
                // 5️⃣ CREA INCASSO
                // ====================================================
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
                int? idOwner = cliente?.ID_Operatore;

                var incasso = new Incassi
                {
                    ID_Pratiche = pratica.ID_Pratiche,
                    ID_AvvisoParcella = avviso.ID_AvvisoParcelle,
                    DataIncasso = dataIncasso,
                    Importo = importoIncasso,
                    ImportoTotale = totaleAvviso,
                    ImportoNetto = importoNettoPlafond,
                    ImportoVersatoPlafond = 0m,
                    ModalitaPagamento = model.MetodoPagamento?.Trim(),
                    VersaInPlafond = model.VersaInPlafond ?? false,
                    StatoIncasso = "Registrato",
                    Note = model.Note?.Trim(),
                    DataCompetenzaEconomica = dataCompetenzaEconomica,
                    DataCompetenzaFinanziaria = dataIncasso,
                    ID_Responsabile = pratica.ID_UtenteResponsabile,
                    ID_OwnerCliente = idOwner,
                    ID_UtenteCreatore = idUtenteCorrente
                };

                db.Incassi.Add(incasso);
                db.SaveChanges();

                db.Incassi_a.Add(new Incassi_a
                {
                    ID_Archivio = incasso.ID_Incasso,
                    ID_Pratiche = incasso.ID_Pratiche,
                    DataIncasso = incasso.DataIncasso,
                    Importo = incasso.Importo,
                    ModalitaPagamento = incasso.ModalitaPagamento,
                    ID_UtenteCreatore = incasso.ID_UtenteCreatore,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Inserito incasso per avviso #{avviso.ID_AvvisoParcelle} (importo {importoIncasso:N2} €)"
                });
                db.SaveChanges();

                // ====================================================
                // 6️⃣ RIPARTIZIONE
                // ====================================================
                UtileHelper.EseguiRipartizioneDaIncasso(pratica.ID_Pratiche, importoIncasso, incasso.ID_Incasso, avviso.ID_AvvisoParcelle, avviso.ID_CompensoOrigine);

                // ====================================================
                // 7️⃣ COMPENSI + PLAFOND + STATO AVVISO
                // ====================================================
                var vociRicavo = db.BilancioProfessionista
                    .Where(b => b.ID_Pratiche == pratica.ID_Pratiche &&
                                b.ID_Incasso == incasso.ID_Incasso &&
                                b.Origine == "Incasso" &&
                                b.TipoVoce == "Ricavo" &&
                                !b.Categoria.Contains("Trattenuta") &&
                                b.Importo > 0)
                    .ToList();

                foreach (var voce in vociRicavo)
                {
                    var nuovoCompenso = new CompensiPratica
                    {
                        ID_Pratiche = (int)voce.ID_Pratiche,
                        ID_UtenteDestinatario = voce.ID_Professionista,
                        Importo = voce.Importo,
                        Tipo = "Incasso",
                        Descrizione = $"Quota incasso {now:MMMM yyyy} - {voce.Categoria}",
                        DataInserimento = now,
                        ID_UtenteCreatore = idUtenteCorrente
                    };
                    db.CompensiPratica.Add(nuovoCompenso);
                    db.SaveChanges();

                    db.CompensiPratica_a.Add(new CompensiPratica_a
                    {
                        ID_CompensoOriginale = nuovoCompenso.ID_Compenso,
                        ID_Pratiche = nuovoCompenso.ID_Pratiche,
                        ID_UtenteDestinatario = nuovoCompenso.ID_UtenteDestinatario,
                        Tipo = nuovoCompenso.Tipo,
                        Descrizione = nuovoCompenso.Descrizione,
                        Importo = nuovoCompenso.Importo,
                        DataInserimento = nuovoCompenso.DataInserimento,
                        ID_UtenteCreatore = nuovoCompenso.ID_UtenteCreatore,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = idUtenteCorrente,
                        NumeroVersione = 1,
                        ModificheTestuali = $"💰 Compenso registrato da incasso ID {incasso.ID_Incasso} ({voce.Categoria} - {voce.Importo:N2} €)"
                    });
                    db.SaveChanges();
                }

                // 💰 Versamento in plafond
                if (model.VersaInPlafond == true)
                {
                    foreach (var voce in vociRicavo)
                    {
                        bool esisteGia = db.PlafondUtente.Any(p =>
                            p.ID_Incasso == incasso.ID_Incasso &&
                            p.ID_Utente == voce.ID_Professionista &&
                            p.TipoPlafond == "Incasso");

                        if (esisteGia) continue;

                        var nuovoPlafond = new PlafondUtente
                        {
                            ID_Utente = voce.ID_Professionista,
                            ID_Incasso = incasso.ID_Incasso,
                            ID_Pratiche = pratica.ID_Pratiche,
                            TipoPlafond = "Incasso",
                            Importo = voce.Importo,
                            ImportoTotale = voce.Importo,
                            DataVersamento = now,
                            DataInizio = now.Date,
                            ID_UtenteCreatore = idUtenteCorrente,
                            ID_UtenteInserimento = idUtenteCorrente,
                            Operazione = "Versamento da incasso pratica",
                            DataInserimento = now,
                            Note = $"💰 Versamento da incasso ID {incasso.ID_Incasso} | Professionista ID {voce.ID_Professionista}"
                        };

                        db.PlafondUtente.Add(nuovoPlafond);
                        db.SaveChanges();

                        db.PlafondUtente_a.Add(new PlafondUtente_a
                        {
                            ID_PlannedPlafond_Archivio = nuovoPlafond.ID_PlannedPlafond,
                            ID_Utente = nuovoPlafond.ID_Utente,
                            ID_Pratiche = nuovoPlafond.ID_Pratiche,
                            TipoPlafond = nuovoPlafond.TipoPlafond,
                            ImportoTotale = nuovoPlafond.ImportoTotale,
                            Importo = nuovoPlafond.Importo,
                            DataVersamento = nuovoPlafond.DataVersamento,
                            DataArchiviazione = now,
                            NumeroVersione = 1,
                            ModificheTestuali = $"💰 Versamento da incasso pratica {pratica.ID_Pratiche} = {nuovoPlafond.Importo:N2} € (Professionista ID {nuovoPlafond.ID_Utente})"
                        });
                        db.SaveChanges();
                    }
                }

                // 🔄 Aggiorna stato avviso
                decimal incassatoLordo = db.Incassi
                    .Where(i => i.ID_AvvisoParcella == avviso.ID_AvvisoParcelle)
                    .Select(i => (decimal?)i.ImportoTotale)
                    .DefaultIfEmpty(0)
                    .Sum() ?? 0m;

                decimal residuoDopo = Math.Round(totaleAvviso - incassatoLordo, 2);
                if (Math.Abs(residuoDopo) <= 0.5m) residuoDopo = 0;

                avviso.Stato = residuoDopo <= 0.00m ? "Pagato"
                                : residuoDopo < totaleAvviso ? "Parziale"
                                : "Inviato";

                avviso.DataModifica = now;
                avviso.ID_UtenteModifica = idUtenteCorrente;
                db.Entry(avviso).State = System.Data.Entity.EntityState.Modified;

                incasso.StatoIncasso = avviso.Stato;
                incasso.ImportoVersatoPlafond = model.VersaInPlafond == true ? importoNettoPlafond : 0m;
                db.Entry(incasso).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                System.Diagnostics.Trace.WriteLine($"✅ [CreaIncasso] Completato. Stato avviso → {avviso.Stato}");

                return Json(new
                {
                    success = true,
                    message = (avviso.Stato == "Pagato")
                        ? $"✅ Incasso registrato. Avviso #{avviso.ID_AvvisoParcelle} PAGATO."
                        : $"✅ Incasso registrato. Residuo avviso: {residuoDopo:N2} €.",
                    idIncasso = incasso.ID_Incasso,
                    residuo = residuoDopo
                });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var dettagli = string.Join("; ",
                    ex.EntityValidationErrors.SelectMany(e => e.ValidationErrors)
                        .Select(v => $"Campo: {v.PropertyName} → Errore: {v.ErrorMessage}"));

                System.Diagnostics.Trace.WriteLine("❌ [CreaIncasso] Validation error: " + dettagli);
                return Json(new { success = false, message = "Errore validazione Entity: " + dettagli });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ [CreaIncasso] Errore generico: {ex}");
                return Json(new { success = false, message = "Errore durante il salvataggio: " + ex.Message });
            }
        }



        /* Get Incasso commentato in data 20/10/2025 in quanto ho commentanto  la modifica per l'incassi non va bene */
        //[HttpPost]
        //public ActionResult ModificaIncasso(IncassoViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //        return Json(new { success = false, message = "Compilare correttamente tutti i campi obbligatori." });

        //    int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
        //    var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
        //    if (utente == null)
        //        return Json(new { success = false, message = "Utente non autenticato." });

        //    bool autorizzato = utente.TipoUtente == "Admin" ||
        //                       db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Modifica == true);
        //    if (!autorizzato)
        //        return Json(new { success = false, message = "Non hai i permessi per modificare l’incasso." });

        //    try
        //    {
        //        var incasso = db.Incassi.FirstOrDefault(i => i.ID_Incasso == model.ID_Incasso);
        //        if (incasso == null)
        //            return Json(new { success = false, message = "Incasso non trovato." });

        //        var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
        //        if (pratica == null)
        //            return Json(new { success = false, message = "Pratica non trovata." });

        //        var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == model.ID_AvvisoParcella);
        //        if (avviso == null)
        //            return Json(new { success = false, message = "Avviso parcella collegato non trovato." });

        //        DateTime now = DateTime.Now;

        //        // 🔍 Calcola totale incassi escluso quello corrente
        //        decimal incassatoTotale = db.Incassi
        //            .Where(i => i.ID_Pratiche == model.ID_Pratiche && i.ID_Incasso != model.ID_Incasso)
        //            .Select(i => i.Importo)
        //            .DefaultIfEmpty(0)
        //            .Sum();

        //        decimal budget = pratica.Budget;
        //        if ((incassatoTotale + model.Importo) > budget)
        //        {
        //            return Json(new
        //            {
        //                success = false,
        //                message = $"⚠️ L'importo aggiornato supera il budget della pratica.\nTotale altri incassi: {incassatoTotale:C}\nNuovo importo: {model.Importo:C}\nBudget massimo: {budget:C}"
        //            });
        //        }

        //        int ultimaVersione = db.Incassi_a
        //            .Where(i => i.ID_Archivio == incasso.ID_Incasso)
        //            .OrderByDescending(i => i.NumeroVersione)
        //            .Select(i => i.NumeroVersione)
        //            .FirstOrDefault();

        //        List<string> modifiche = new List<string>();
        //        void Check(string campo, object oldVal, object newVal)
        //        {
        //            if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
        //                modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
        //        }

        //        Check("DataIncasso", incasso.DataIncasso, model.DataIncasso);
        //        Check("Importo", incasso.Importo, model.Importo);
        //        Check("MetodoPagamento", incasso.ModalitaPagamento, model.MetodoPagamento);
        //        Check("VersaInPlafond", incasso.VersaInPlafond, model.VersaInPlafond);

        //        // 🔁 Applica modifiche
        //        incasso.DataIncasso = model.DataIncasso ?? now;
        //        incasso.Importo = model.Importo;
        //        incasso.ModalitaPagamento = model.MetodoPagamento?.Trim();
        //        incasso.VersaInPlafond = model.VersaInPlafond == true;

        //        // 🗂 Archivia versione aggiornata
        //        db.Incassi_a.Add(new Incassi_a
        //        {
        //            ID_Archivio = incasso.ID_Incasso,
        //            ID_Pratiche = incasso.ID_Pratiche,
        //            DataIncasso = incasso.DataIncasso,
        //            Importo = incasso.Importo,
        //            ModalitaPagamento = incasso.ModalitaPagamento,
        //            ID_UtenteCreatore = incasso.ID_UtenteCreatore,
        //            NumeroVersione = ultimaVersione + 1,
        //            ModificheTestuali = modifiche.Any()
        //                ? $"✏️ Modifica effettuata da ID_Utente = {idUtenteCorrente} il {now:g}:\n{string.Join("\n", modifiche)}"
        //                : "Modifica salvata senza cambiamenti rilevanti"
        //        });

        //        // 🔁 Rimuove vecchie voci di bilancio e plafond legate a questo incasso
        //        db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
        //            .Where(b => b.ID_Incasso == incasso.ID_Incasso));

        //        db.PlafondUtente.RemoveRange(db.PlafondUtente
        //            .Where(p => p.ID_Incasso == incasso.ID_Incasso));

        //        // 🔁 Esegui ripartizione aggiornata
        //        UtileHelper.EseguiRipartizioneDaIncasso(incasso.ID_Pratiche.Value, incasso.Importo);

        //        // 💰 Versa in plafond, se selezionato
        //        if (incasso.VersaInPlafond == true)
        //        {
        //            decimal totaleAvviso = (decimal)(avviso.TotaleAvvisiParcella ?? avviso.Importo ?? 0m);
        //            decimal iva = avviso.ImportoIVA ?? 0m;
        //            decimal contributoIntegrativo = avviso.ContributoIntegrativoImporto ?? 0m;

        //            decimal? speseGenerali = db.BilancioProfessionista
        //                .Where(b => b.ID_Pratiche == pratica.ID_Pratiche &&
        //                            b.Categoria == "Spese Generali" &&
        //                            b.Stato != "Annullato")
        //                .Select(b => (decimal?)b.Importo)
        //                .DefaultIfEmpty(0)
        //                .Sum();

        //            decimal importoNettoPlafond = Math.Round((totaleAvviso + (speseGenerali ?? 0) - iva - contributoIntegrativo), 2);

        //            if (importoNettoPlafond > 0)
        //            {
        //                var nuovoPlafond = new PlafondUtente
        //                {
        //                    ID_Utente = pratica.ID_UtenteResponsabile,
        //                    ID_Incasso = incasso.ID_Incasso,
        //                    ID_Pratiche = pratica.ID_Pratiche,
        //                    TipoPlafond = "Incasso",
        //                    Importo = importoNettoPlafond,
        //                    ImportoTotale = importoNettoPlafond,
        //                    DataVersamento = now,
        //                    DataInizio = now.Date,
        //                    ID_UtenteCreatore = idUtenteCorrente,
        //                    ID_UtenteInserimento = idUtenteCorrente,
        //                    DataInserimento = now,
        //                    Note = $"💰 Versamento netto aggiornato da incasso ID {incasso.ID_Incasso} - Avviso #{avviso.ID_AvvisoParcelle} - Pratica {pratica.Titolo}"
        //                };

        //                db.PlafondUtente.Add(nuovoPlafond);
        //                db.SaveChanges();

        //                // 🗂 Archiviazione in PlafondUtente_a
        //                db.PlafondUtente_a.Add(new PlafondUtente_a
        //                {
        //                    ID_PlannedPlafond_Archivio = nuovoPlafond.ID_PlannedPlafond,
        //                    ID_Utente = nuovoPlafond.ID_Utente,
        //                    ID_Incasso = nuovoPlafond.ID_Incasso,
        //                    ID_Pratiche = nuovoPlafond.ID_Pratiche,
        //                    TipoPlafond = nuovoPlafond.TipoPlafond,
        //                    Importo = nuovoPlafond.Importo,
        //                    ImportoTotale = nuovoPlafond.ImportoTotale,
        //                    DataVersamento = nuovoPlafond.DataVersamento,
        //                    DataInizio = nuovoPlafond.DataInizio,
        //                    ID_UtenteCreatore = nuovoPlafond.ID_UtenteCreatore,
        //                    ID_UtenteInserimento = nuovoPlafond.ID_UtenteInserimento,
        //                    DataInserimento = nuovoPlafond.DataInserimento,
        //                    NumeroVersione = 1,
        //                    ModificheTestuali = $"✏️ Modifica plafond generata automaticamente dal sistema in data {now:g}"
        //                });
        //            }
        //        }

        //        // 🔄 Aggiorna stato Avviso
        //        avviso.Stato = "Pagato";
        //        avviso.DataModifica = now;
        //        avviso.ID_UtenteModifica = idUtenteCorrente;
        //        db.Entry(avviso).State = System.Data.Entity.EntityState.Modified;

        //        db.SaveChanges();

        //        return Json(new { success = true, message = "✅ Incasso modificato e aggiornato correttamente." });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
        //    }
        //}


        /* Modifca commentata in data 20/10/2025 in quanto reputo che la modifica per l'incassi non va bene */
        //[HttpGet]
        //public ActionResult GetIncasso(int id)
        //{
        //    try
        //    {
        //        var incasso = (from i in db.Incassi
        //                       join p in db.Pratiche on i.ID_Pratiche equals p.ID_Pratiche into praticaJoin
        //                       from p in praticaJoin.DefaultIfEmpty()

        //                       join a in db.AvvisiParcella on i.ID_AvvisoParcella equals a.ID_AvvisoParcelle into avvisoJoin
        //                       from a in avvisoJoin.DefaultIfEmpty()

        //                       where i.ID_Incasso == id
        //                       select new
        //                       {
        //                           i.ID_Incasso,
        //                           i.ID_Pratiche,
        //                           i.ID_AvvisoParcella,
        //                           i.DataIncasso,
        //                           i.Importo,
        //                           i.ModalitaPagamento,
        //                           i.ID_UtenteCreatore,
        //                           i.VersaInPlafond,
        //                           PraticaTitolo = p.Titolo,
        //                           Budget = p.Budget,
        //                           AvvisoImporto = a.Importo,
        //                           AvvisoTotale = a.TotaleAvvisiParcella,
        //                           AvvisoIVA = a.ImportoIVA,
        //                           AvvisoAliquota = a.AliquotaIVA,
        //                           AvvisoContributo = a.ContributoIntegrativoImporto,
        //                           AvvisoDataCompetenza = a.DataAvviso
        //                       }).FirstOrDefault();

        //        if (incasso == null)
        //            return Json(new { success = false, message = "Incasso non trovato." }, JsonRequestBehavior.AllowGet);

        //        // 🔹 Calcola il netto per il plafond
        //        decimal totaleAvviso = (decimal)(incasso.AvvisoTotale ?? incasso.AvvisoImporto ?? 0m);
        //        decimal iva = incasso.AvvisoIVA ?? 0m;
        //        decimal contributoIntegrativo = incasso.AvvisoContributo ?? 0m;

        //        // 🔹 Spese generali
        //        decimal? speseGenerali = db.BilancioProfessionista
        //            .Where(b => b.ID_Pratiche == incasso.ID_Pratiche &&
        //                        b.Categoria == "Spese Generali" &&
        //                        b.Stato != "Annullato")
        //            .Select(b => (decimal?)b.Importo)
        //            .DefaultIfEmpty(0)
        //            .Sum();

        //        decimal importoNettoPlafond = Math.Round((totaleAvviso + (speseGenerali ?? 0) - iva - contributoIntegrativo), 2);

        //        var vm = new IncassoViewModel
        //        {
        //            ID_Incasso = incasso.ID_Incasso,
        //            ID_Pratiche = incasso.ID_Pratiche ?? 0,
        //            ID_AvvisoParcella = incasso.ID_AvvisoParcella ?? 0,
        //            DataIncasso = incasso.DataIncasso,
        //            Importo = incasso.Importo,
        //            MetodoPagamento = incasso.ModalitaPagamento,
        //            ID_UtenteCreatore = incasso.ID_UtenteCreatore,
        //            VersaInPlafond = incasso.VersaInPlafond == true,

        //            // 🔹 Dati Pratica
        //            NomePratica = incasso.PraticaTitolo ?? "(Pratica sconosciuta)",
        //            TotalePratica = incasso.Budget,

        //            // 🔹 Dati Avviso Parcella
        //            ImportoAvviso = incasso.AvvisoImporto ?? 0m,
        //            ImportoIVA = incasso.AvvisoIVA ?? 0m,
        //            AliquotaIVA = incasso.AvvisoAliquota ?? 0m,
        //            DataCompetenza = incasso.AvvisoDataCompetenza,
        //            DescrizioneAvvisoParcella = $"Avviso parcella #{incasso.ID_AvvisoParcella} – Competenza {incasso.AvvisoDataCompetenza:dd/MM/yyyy}",

        //            // 🔹 Calcolato
        //            UtileNetto = importoNettoPlafond
        //        };

        //        System.Diagnostics.Debug.WriteLine($"🟢 [GetIncasso] Restituito incasso ID {id}, netto plafond = {importoNettoPlafond:N2}");

        //        return Json(new { success = true, incasso = vm }, JsonRequestBehavior.AllowGet);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
        //    }
        //}


        [HttpPost]
        public ActionResult EliminaIncasso(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

                if (utenteCorrente == null)
                    return Json(new { success = false, message = "Utente non autenticato." });

                bool haPermesso = utenteCorrente.TipoUtente == "Admin" ||
                                  db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Elimina == true);

                if (!haPermesso)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare l’incasso." });

                var incasso = db.Incassi.FirstOrDefault(i => i.ID_Incasso == id);
                if (incasso == null)
                    return Json(new { success = false, message = "Incasso non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == incasso.ID_Pratiche);
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == incasso.ID_AvvisoParcella);
                DateTime now = DateTime.Now;

                System.Diagnostics.Trace.WriteLine($"🗑️ [EliminaIncasso] Inizio eliminazione incasso ID={id} per pratica {pratica?.ID_Pratiche}");
                System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────");

                // 🔢 Numero versione precedente
                int ultimaVersione = db.Incassi_a
                    .Where(i => i.ID_Archivio == incasso.ID_Incasso)
                    .OrderByDescending(i => i.NumeroVersione)
                    .Select(i => (int?)i.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 🗂 Archivia incasso prima della rimozione
                db.Incassi_a.Add(new Incassi_a
                {
                    ID_Archivio = incasso.ID_Incasso,
                    ID_Pratiche = incasso.ID_Pratiche,
                    DataIncasso = incasso.DataIncasso,
                    Importo = incasso.Importo,
                    ModalitaPagamento = incasso.ModalitaPagamento,
                    ID_UtenteCreatore = incasso.ID_UtenteCreatore,
                    VersaInPlafond = incasso.VersaInPlafond ?? false,
                    DataArchiviazione = now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑️ Eliminazione incasso eseguita da utente ID={idUtenteCorrente} in data {now:g}"
                });

                // 🔁 Archivia e rimuovi eventuali righe di plafond collegate
                var plafonds = db.PlafondUtente.Where(p => p.ID_Incasso == incasso.ID_Incasso).ToList();
                foreach (var pl in plafonds)
                {
                    int ultimaVerPl = db.PlafondUtente_a
                        .Where(a => a.ID_PlannedPlafond_Archivio == pl.ID_PlannedPlafond)
                        .OrderByDescending(a => a.NumeroVersione)
                        .Select(a => (int?)a.NumeroVersione)
                        .FirstOrDefault() ?? 0;

                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_PlannedPlafond_Archivio = pl.ID_PlannedPlafond,
                        ID_Utente = pl.ID_Utente,
                        ID_Incasso = pl.ID_Incasso,
                        ID_Pratiche = pl.ID_Pratiche,
                        TipoPlafond = pl.TipoPlafond,
                        Importo = pl.Importo,
                        ImportoTotale = pl.ImportoTotale,
                        DataVersamento = pl.DataVersamento,
                        DataInizio = pl.DataInizio,
                        DataFine = pl.DataFine,
                        ID_UtenteCreatore = pl.ID_UtenteCreatore,
                        ID_UtenteInserimento = pl.ID_UtenteInserimento,
                        DataInserimento = pl.DataInserimento,
                        DataArchiviazione = now,
                        NumeroVersione = ultimaVerPl + 1,
                        ModificheTestuali = $"🗑️ Eliminazione automatica plafond da incasso ID={incasso.ID_Incasso}"
                    });
                }

                // 🧹 Elimina plafonds collegati (dopo archiviazione)
                if (plafonds.Any())
                {
                    System.Diagnostics.Trace.WriteLine($"🧹 Eliminazione {plafonds.Count} righe da PlafondUtente (collegate all’incasso {incasso.ID_Incasso})");
                    db.PlafondUtente.RemoveRange(plafonds);
                }

                // 🧹 Elimina voci da BilancioProfessionista collegate all’incasso
                var bilancioVoci = db.BilancioProfessionista
                    .Where(b => b.ID_Incasso == incasso.ID_Incasso)
                    .ToList();

                if (bilancioVoci.Any())
                {
                    System.Diagnostics.Trace.WriteLine($"🧾 Rimozione {bilancioVoci.Count} voci da BilancioProfessionista (origine 'Incasso')");
                    db.BilancioProfessionista.RemoveRange(bilancioVoci);
                }

                // 🔁 Elimina compensi collegati (creati al momento dell’incasso)
                var compensi = db.CompensiPratica
                    .Where(c => c.ID_Pratiche == incasso.ID_Pratiche &&
                                c.Tipo == "Incasso" &&
                                c.ID_UtenteDestinatario != null)
                    .ToList();

                if (compensi.Any())
                {
                    System.Diagnostics.Trace.WriteLine($"💼 Eliminazione {compensi.Count} compensi collegati a incasso (CompensiPratica).");
                    db.CompensiPratica.RemoveRange(compensi);
                }

                // 🔁 Aggiorna stato avviso parcella (se presente)
                if (avviso != null)
                {
                    decimal altriIncassi = db.Incassi
                        .Where(i => i.ID_AvvisoParcella == avviso.ID_AvvisoParcelle && i.ID_Incasso != incasso.ID_Incasso)
                        .Select(i => (decimal?)i.Importo)
                        .DefaultIfEmpty(0)
                        .Sum() ?? 0m;

                    decimal totaleAvviso = avviso.TotaleAvvisiParcella ?? avviso.Importo ?? 0m;
                    decimal residuo = totaleAvviso - altriIncassi;

                    if (residuo <= 0)
                        avviso.Stato = "Pagato";
                    else if (altriIncassi > 0 && residuo > 0)
                        avviso.Stato = "Parziale";
                    else
                        avviso.Stato = "Inviato";

                    avviso.DataModifica = now;
                    avviso.ID_UtenteModifica = idUtenteCorrente;
                    db.Entry(avviso).State = System.Data.Entity.EntityState.Modified;

                    System.Diagnostics.Trace.WriteLine($"📄 [Avviso] Stato aggiornato → {avviso.Stato} (Residuo {residuo:N2} €)");
                }

                // 🧹 Elimina incasso effettivo
                db.Incassi.Remove(incasso);
                db.SaveChanges();

                System.Diagnostics.Trace.WriteLine($"✅ [EliminaIncasso] Completata eliminazione per incasso {id}.\n");

                return Json(new
                {
                    success = true,
                    message = $"✅ Incasso eliminato. {(avviso != null ? $"Avviso #{avviso.ID_AvvisoParcelle} ora '{avviso.Stato}'" : "Nessun avviso collegato")}."
                });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException ex)
            {
                var dettagli = string.Join("; ",
                    ex.EntityValidationErrors.SelectMany(e => e.ValidationErrors)
                        .Select(v => $"Campo: {v.PropertyName} → Errore: {v.ErrorMessage}"));

                System.Diagnostics.Trace.WriteLine($"❌ [EliminaIncasso] Errore validazione: {dettagli}");
                return Json(new { success = false, message = "Errore validazione Entity: " + dettagli });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ [EliminaIncasso] Errore generico: {ex}");
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetDatiIncassoDaAvviso(int idAvviso)
        {
            try
            {
                System.Diagnostics.Trace.WriteLine($"🔍 [GetDatiIncassoDaAvviso] ID avviso: {idAvviso}");

                // =====================================================
                // 🔗 JOIN COMPLETA
                // =====================================================
                var avv = (from av in db.AvvisiParcella
                           join pr in db.Pratiche on av.ID_Pratiche equals pr.ID_Pratiche
                           join c in db.Clienti on pr.ID_Cliente equals c.ID_Cliente
                           join ow in db.OperatoriSinergia on c.ID_Operatore equals ow.ID_Operatore into joinOwner
                           from ow in joinOwner.DefaultIfEmpty()
                           join resp in db.Utenti on pr.ID_UtenteResponsabile equals resp.ID_Utente into joinResp
                           from resp in joinResp.DefaultIfEmpty()
                           where av.ID_AvvisoParcelle == idAvviso
                           select new
                           {
                               Avviso = av,
                               Pratica = pr,
                               Cliente = c,
                               Owner = ow,
                               Responsabile = resp
                           }).FirstOrDefault();

                if (avv == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." }, JsonRequestBehavior.AllowGet);

                var avviso = avv.Avviso;
                var pratica = avv.Pratica;
                var cliente = avv.Cliente;
                var owner = avv.Owner;
                var responsabile = avv.Responsabile;

                // =====================================================
                // 💰 CALCOLI BASE IMPORTI
                // =====================================================
                decimal importoImponibile = avviso.Importo ?? 0;
                decimal percentualeSpeseGenerali = avviso.RimborsoSpesePercentuale ?? 0;
                decimal percentualeContributoIntegrativo = avviso.ContributoIntegrativoPercentuale ?? 0;
                decimal aliquotaIVA = avviso.AliquotaIVA ?? 0;
                decimal importoIVA = avviso.ImportoIVA ?? 0;
                decimal totaleAvviso = avviso.TotaleAvvisiParcella ?? avviso.Importo ?? 0m;

                bool stessoProfessionista = (owner != null && responsabile != null)
                    ? owner.ID_Operatore == responsabile.ID_Utente
                    : false;

                // =====================================================
                // 🧩 OWNER FEE DAL CLUSTER
                // =====================================================
                decimal percentualeOwner = 0m;
                var clusterOwner = db.Cluster
                    .Where(c => c.ID_Pratiche == pratica.ID_Pratiche && c.TipoCluster == "Owner")
                    .OrderByDescending(c => c.DataAssegnazione)
                    .FirstOrDefault();

                if (clusterOwner != null)
                    percentualeOwner = clusterOwner.PercentualePrevisione;

                // =====================================================
                // 🏛️ TRATTENUTA SINERGIA (su imponibile)
                // =====================================================
                decimal percentualeTrattenutaSinergia = 0m;
                var ricorrenzaTrattenuta = db.RicorrenzeCosti
                    .FirstOrDefault(r => r.Categoria == "Trattenuta Sinergia" &&
                                         r.TipoValore == "Percentuale" &&
                                         r.Attivo == true);

                if (ricorrenzaTrattenuta != null)
                    percentualeTrattenutaSinergia = ricorrenzaTrattenuta.Valore;

                decimal quotaTrattenutaSinergia = Math.Round(importoImponibile * (percentualeTrattenutaSinergia / 100m), 2);
                decimal baseDopoTrattenuta = importoImponibile - quotaTrattenutaSinergia;

                // =====================================================
                // 👥 COLLABORATORI DA CLUSTER (su post-trattenuta)
                // =====================================================
                var listaCollaboratoriCluster = new List<dynamic>();
                decimal totaleCollaboratoriCluster = 0m;

                var clusterList = (
                    from c in db.Cluster
                    join u in db.Utenti on c.ID_Utente equals u.ID_Utente into joinUtente
                    from u in joinUtente.DefaultIfEmpty()
                    join o in db.OperatoriSinergia on c.ID_Utente equals o.ID_Operatore into joinOperatore
                    from o in joinOperatore.DefaultIfEmpty()
                    where c.ID_Pratiche == pratica.ID_Pratiche && c.TipoCluster == "Collaboratore"
                    select new { c, u, o }
                ).ToList();

                foreach (var item in clusterList)
                {
                    decimal perc = Math.Max(0, Math.Min(100, item.c.PercentualePrevisione));
                    decimal importoCalc = Math.Round(baseDopoTrattenuta * (perc / 100m), 2);
                    totaleCollaboratoriCluster += importoCalc;

                    string nome = item.u != null
                        ? $"{item.u.Nome} {item.u.Cognome}"
                        : (item.o != null ? $"{item.o.Nome} {item.o.Cognome}" : "—");

                    listaCollaboratoriCluster.Add(new
                    {
                        ID_Utente = item.c.ID_Utente,
                        Nome = nome,
                        Percentuale = perc,
                        Importo = importoCalc
                    });
                }

                // =====================================================
                // 👥 COLLABORATORI DA COMPENSI DETTAGLIO (post-trattenuta)
                // =====================================================
                var listaCollaboratoriDettaglio = new List<object>();
                int? idCompenso = avviso.ID_CompensoOrigine;

                var compensi = db.CompensiPraticaDettaglio
                    .Where(c => c.ID_Pratiche == pratica.ID_Pratiche &&
                                (!idCompenso.HasValue || c.ID_RigaCompenso == idCompenso.Value))
                    .ToList();

                decimal totaleCollaboratoriDettaglio = 0m;

                foreach (var comp in compensi)
                {
                    if (!string.IsNullOrEmpty(comp.Collaboratori))
                    {
                        var collaboratori = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(comp.Collaboratori);

                        foreach (var coll in collaboratori)
                        {
                            decimal perc = (decimal)(coll.Percentuale ?? 0);
                            if (perc <= 0) continue;

                            decimal quota = Math.Round(baseDopoTrattenuta * (perc / 100m), 2);
                            totaleCollaboratoriDettaglio += quota;

                            listaCollaboratoriDettaglio.Add(new
                            {
                                Nome = (string)(coll.NomeCollaboratore ?? "-"),
                                Percentuale = perc,
                                Importo = quota
                            });
                        }
                    }
                }

                // =====================================================
                // 📊 SPESE GENERALI = imponibile × %
                // =====================================================
                decimal speseGeneraliImporto = Math.Round(
                    importoImponibile * (percentualeSpeseGenerali / 100m),
                2);

                // =====================================================
                // 🧾 CONTRIBUTO INTEGRATIVO = (Imponibile + SpeseGenerali) × %
                // =====================================================
                decimal contributoIntegrativoImporto = Math.Round(
                    (importoImponibile + speseGeneraliImporto) * (percentualeContributoIntegrativo / 100m),
                2);

                // =====================================================
                // 💶 OWNER FEE (su base post-trattenuta)
                // =====================================================
                decimal quotaOwner = (!stessoProfessionista && percentualeOwner > 0)
                    ? Math.Round(baseDopoTrattenuta * (percentualeOwner / 100m), 2)
                    : 0m;

                // =====================================================
                // 💶 CALCOLO NETTO PROFESSIONISTA
                // =====================================================
                decimal totaleCollaboratori = totaleCollaboratoriCluster + totaleCollaboratoriDettaglio;

                decimal importoNettoFinale = Math.Round(
                    (baseDopoTrattenuta + speseGeneraliImporto)
                    - (quotaOwner + contributoIntegrativoImporto + totaleCollaboratori),
                    2);

                // =====================================================
                // 📦 MODELLO RISPOSTA COMPLETO
                // =====================================================
                var model = new
                {
                    ID_AvvisoParcelle = avviso.ID_AvvisoParcelle,
                    TitoloAvviso = avviso.TitoloAvviso,
                    ID_Pratiche = pratica.ID_Pratiche,
                    NomePratica = pratica.Titolo,
                    NomeCliente = cliente != null
                        ? (!string.IsNullOrEmpty(cliente.RagioneSociale)
                            ? cliente.RagioneSociale
                            : $"{cliente.Nome} {cliente.Cognome}")
                        : "-",
                    NomeOwner = owner != null ? $"{owner.Nome} {owner.Cognome}" : "-",
                    NomeResponsabile = responsabile != null ? $"{responsabile.Nome} {responsabile.Cognome}" : "-",


                    TotaleAvviso = totaleAvviso,
                    ImportoImponibile = importoImponibile,
                    ImportoIVA = importoIVA,
                    AliquotaIVA = aliquotaIVA,

                    BaseDopoTrattenuta = baseDopoTrattenuta,
                    QuotaTrattenutaSinergia = quotaTrattenutaSinergia,
                    PercentualeTrattenutaSinergia = percentualeTrattenutaSinergia,

                    PercentualeOwner = percentualeOwner,
                    QuotaOwner = quotaOwner,

                    PercentualeSpeseGenerali = percentualeSpeseGenerali,
                    SpeseGeneraliImporto = speseGeneraliImporto,

                    PercentualeContributoIntegrativo = percentualeContributoIntegrativo,
                    ContributoIntegrativoImporto = contributoIntegrativoImporto,

                    CollaboratoriCluster = listaCollaboratoriCluster,
                    CollaboratoriDettaglio = listaCollaboratoriDettaglio,
                    TotaleCollaboratori = totaleCollaboratori,

                    ImportoNettoFinale = importoNettoFinale
                };

                return Json(new { success = true, dati = model }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ Errore GetDatiIncassoDaAvviso: " + ex);
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }



        [HttpGet]
        public ActionResult RiepilogoTrattenuteESinergia(int idAvviso)
        {
            System.Diagnostics.Trace.WriteLine("═══════════════════════════════════════════════════");
            System.Diagnostics.Trace.WriteLine($"📥 [RiepilogoTrattenuteESinergia] Avvio metodo per ID_AvvisoParcella = {idAvviso}");
            System.Diagnostics.Trace.WriteLine($"🕓 {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            try
            {
                using (var db = new SinergiaDB())
                {
                    DateTime oggi = DateTime.Today;
                    DateTime primoDelMese = new DateTime(oggi.Year, oggi.Month, 1);
                    DateTime primoDelMeseSuccessivo = primoDelMese.AddMonths(1);

                    var listaGrezza = (
                        from b in db.BilancioProfessionista
                        join i in db.Incassi on b.ID_Incasso equals i.ID_Incasso
                        join a in db.AvvisiParcella on i.ID_AvvisoParcella equals a.ID_AvvisoParcelle
                        join p in db.Pratiche on b.ID_Pratiche equals p.ID_Pratiche into joinPratiche
                        from p in joinPratiche.DefaultIfEmpty()
                        join u in db.Utenti on b.ID_Professionista equals u.ID_Utente into joinU
                        from u in joinU.DefaultIfEmpty()
                        where
                            a.ID_AvvisoParcelle == idAvviso &&
                            a.Stato == "Pagato" &&
                            b.Origine == "Incasso" &&
                            (
                                b.Categoria == "Trattenuta Sinergia" ||
                                b.Categoria == "Rimborso Spese Sinergia"
                            ) &&
                            DbFunctions.TruncateTime(b.DataRegistrazione) >= primoDelMese &&
                            DbFunctions.TruncateTime(b.DataRegistrazione) < primoDelMeseSuccessivo
                        select new
                        {
                            b.ID_Bilancio,
                            b.ID_Pratiche,
                            b.ID_Professionista,
                            TitoloPratica = p.Titolo,
                            Nome = u.Nome,
                            Cognome = u.Cognome,
                            b.Categoria,
                            b.Importo,
                            b.DataRegistrazione,
                            b.Origine,
                            ID_Avviso = a.ID_AvvisoParcelle,
                            StatoAvviso = a.Stato,
                            ImportoAvviso = a.Importo,
                            TotaleAvviso = a.TotaleAvvisiParcella,
                            i.DataIncasso
                        }
                    ).ToList();

                    var lista = listaGrezza.Select(x => new RicavoSinergiaViewModel
                    {
                        ID_Pratiche = x.ID_Pratiche,
                        ID_Professionista = x.ID_Professionista,
                        Titolo = x.TitoloPratica ?? "N.D.",
                        NomeProfessionista = (x.Nome != null && x.Cognome != null)
                            ? $"{x.Nome} {x.Cognome}"
                            : "N.D.",
                        Categoria = x.Categoria,
                        Importo = x.Importo,
                        DataRegistrazione = x.DataRegistrazione,
                        DataIncasso = x.DataIncasso,
                        StatoAvviso = x.StatoAvviso,
                        DescrizioneAvviso = $"Avviso Parcella #{x.ID_Avviso}",
                        ImportoAvviso = x.ImportoAvviso ?? 0m,
                        TotaleAvviso = x.TotaleAvviso ?? 0m
                    }).ToList();

                    System.Diagnostics.Trace.WriteLine($"✅ [RiepilogoTrattenuteESinergia] Query OK → {lista.Count} righe trovate.");
                    return PartialView("~/Views/Incassi/_RiepilogoRicaviSinergia.cshtml", lista);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("❌ [RiepilogoTrattenuteESinergia] ERRORE!");
                System.Diagnostics.Trace.WriteLine($"💬 {ex.Message}");
                return Content($"<div style='padding:20px;color:red;font-weight:bold'>❌ Errore nel riepilogo Sinergia:<br>{ex.Message}</div>");
            }
        }


        [HttpGet]
        public ActionResult GetDettaglioIncasso(int id)
        {
            try
            {
                var incasso = db.Incassi.FirstOrDefault(i => i.ID_Incasso == id);
                if (incasso == null)
                    return Json(new { success = false, message = "Incasso non trovato." },
                                JsonRequestBehavior.AllowGet);

                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == incasso.ID_AvvisoParcella);
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == incasso.ID_Pratiche);

                // ============================================================
                // 💰 IMPORTO TOTALE DELL'AVVISO (Importo + IVA + Contributo)
                // ============================================================
                decimal importoTotale = 0;
                if (avviso != null)
                {
                    importoTotale =
                        (avviso.Importo ?? 0) +
                        (avviso.ImportoIVA ?? 0) +
                        (avviso.ContributoIntegrativoImporto ?? 0);
                }
                else
                {
                    importoTotale = incasso.Importo;
                }

                // ============================================================
                // 📦 COSTRUZIONE OGGETTO RISULTATO
                // ============================================================
                var result = new
                {
                    ID_Incasso = incasso.ID_Incasso,
                    Pratica = pratica?.Titolo ?? "(N/D)",
                    Avviso = avviso?.ID_AvvisoParcelle.ToString() ?? "(N/D)",
                    TitoloAvviso = avviso?.TitoloAvviso ?? "(Senza titolo)",

                    DataCompetenzaEconomica = Convert.ToDateTime(incasso.DataCompetenzaEconomica).ToString("dd/MM/yyyy"),
                    DataCompetenzaFinanziaria = Convert.ToDateTime(incasso.DataCompetenzaFinanziaria).ToString("dd/MM/yyyy"),
                    DataIncasso = Convert.ToDateTime(incasso.DataIncasso).ToString("dd/MM/yyyy"),

                    MetodoPagamento = incasso.ModalitaPagamento ?? avviso?.MetodoPagamento ?? "—",
                    ImportoTotale = $"{importoTotale:N2} €",
                    VersaInPlafond = incasso.VersaInPlafond == true ? "Sì" :
                                     incasso.VersaInPlafond == false ? "No" : "(N/D)",
                    Note = incasso.Note ?? ""
                };

                return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [GetDettaglioIncasso] Errore: {ex}");
                return Json(new { success = false, message = "Errore durante il caricamento del dettaglio incasso." },
                            JsonRequestBehavior.AllowGet);
            }
        }



        //[HttpGet]
        //public ActionResult GetDatiAvvisoParcella(int idPratica)
        //{
        //    try
        //    {
        //        var avviso = (
        //            from a in db.AvvisiParcella
        //            join p in db.Pratiche on a.ID_Pratiche equals p.ID_Pratiche
        //            where a.ID_Pratiche == idPratica
        //                  && a.Stato != "Eliminato"
        //                  && a.Stato != "Pagato" // 🔹 Evita di proporre avvisi già saldati
        //            orderby a.DataAvviso descending
        //            select new
        //            {
        //                a.ID_AvvisoParcelle,
        //                a.DataAvviso,
        //                a.Stato,
        //                a.MetodoPagamento,
        //                a.Importo,
        //                a.ImportoIVA,
        //                a.TotaleAvvisiParcella,
        //                p.Titolo,
        //                p.Budget
        //            }).FirstOrDefault();

        //        if (avviso == null)
        //        {
        //            return Json(new
        //            {
        //                success = true,
        //                avviso = "Nessun avviso da incassare",
        //                importoSenzaIVA = 0m,
        //                importoConIVA = 0m,
        //                metodoPagamento = "",
        //                nomePratica = "(Nessuna pratica trovata o avviso già pagato)",
        //                totalePratica = 0m,
        //                dataAvviso = (DateTime?)null,
        //                stato = ""
        //            }, JsonRequestBehavior.AllowGet);
        //        }

        //        // ✅ Restituisce dati più completi per la modale incasso
        //        return Json(new
        //        {
        //            success = true,
        //            idAvvisoParcella = avviso.ID_AvvisoParcelle,
        //            descrizioneAvviso = $"Avviso parcella #{avviso.ID_AvvisoParcelle}",
        //            dataAvviso = avviso.DataAvviso?.ToString("dd/MM/yyyy"),
        //            stato = avviso.Stato,
        //            importoSenzaIVA = avviso.Importo ?? 0m,
        //            importoIVA = avviso.ImportoIVA ?? 0m,
        //            importoConIVA = avviso.TotaleAvvisiParcella ?? 0m,
        //            metodoPagamento = avviso.MetodoPagamento,
        //            nomePratica = avviso.Titolo,
        //            totalePratica = avviso.Budget
        //        }, JsonRequestBehavior.AllowGet);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new
        //        {
        //            success = false,
        //            message = "Errore durante il recupero dati avviso: " + ex.Message
        //        }, JsonRequestBehavior.AllowGet);
        //    }
        //}

        [HttpGet]
        public ActionResult EsportaIncassiCsv(DateTime? da, DateTime? a)
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            bool isAdmin = utenteCorrente.TipoUtente == "Admin";

            // ===============================================
            // 🔥 RECUPERO PROFESSIONISTA DALLA NAVBAR
            // ===============================================
            int idProfessionistaFiltro = 0;

            if (isAdmin)
            {
                idProfessionistaFiltro = Session["IDClienteProfessionistaCorrente"] != null
                    ? Convert.ToInt32(Session["IDClienteProfessionistaCorrente"])
                    : 0;
            }
            else
            {
                idProfessionistaFiltro = UserManager.GetOperatoreDaUtente(idUtenteLoggato);
            }

            // ===============================================
            // 📆 RANGE DATE
            // ===============================================
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            // ===============================================
            // 🔍 QUERY PRINCIPALE CON JOIN COMPLETE
            // ===============================================
            var query =
                from i in db.Incassi
                join p in db.Pratiche on i.ID_Pratiche equals p.ID_Pratiche into joinPratiche
                from p in joinPratiche.DefaultIfEmpty()

                join cli in db.Clienti on p.ID_Cliente equals cli.ID_Cliente into joinClienti
                from cli in joinClienti.DefaultIfEmpty()

                join own in db.OperatoriSinergia on p.ID_Owner equals own.ID_Operatore into joinOwner
                from own in joinOwner.DefaultIfEmpty()

                join avv in db.AvvisiParcella on i.ID_AvvisoParcella equals avv.ID_AvvisoParcelle into joinAvv
                from avv in joinAvv.DefaultIfEmpty()

                where i.DataIncasso >= inizio && i.DataIncasso <= fine
                select new
                {
                    Incasso = i,
                    Pratica = p,
                    Cliente = cli,
                    Owner = own,
                    Avviso = avv
                };

            // ===============================================
            // 🔥 APPLICAZIONE FILTRO PROFESSIONISTA NAVBAR
            // ===============================================
            if (idProfessionistaFiltro > 0)
            {
                query = query.Where(x => x.Pratica.ID_Owner == idProfessionistaFiltro);
            }

            var incassi = query.OrderBy(x => x.Incasso.DataIncasso).ToList();

            // ===============================================
            // 💰 COMPENSI DELLA PRATICA
            // ===============================================
            decimal GetTotaleCompensi(int idPratica)
            {
                return db.CompensiPraticaDettaglio
                    .Where(c => c.ID_Pratiche == idPratica)
                    .Sum(c => (decimal?)c.Importo) ?? 0m;
            }

            // ===============================================
            // 🧾 COSTRUZIONE CSV
            // ===============================================
            var sb = new StringBuilder();

            sb.AppendLine("ID Incasso;Cliente;Pratica;Owner;Totale Compensi;Data Incasso;Importo;Modalità Pagamento;Versa in Plafond;ID Avviso;Data Avviso;Totale Avviso;Stato Avviso");

            foreach (var x in incassi)
            {
                var i = x.Incasso;
                var p = x.Pratica;
                var cli = x.Cliente;
                var own = x.Owner;
                var avv = x.Avviso;

                string nomeCliente = cli != null
                    ? $"{cli.Nome} {cli.Cognome}"
                    : "(N/D)";

                string nomeOwner = own != null
                    ? $"{own.Nome} {own.Cognome}"
                    : "(N/D)";

                decimal totCompensi = p != null ? GetTotaleCompensi(p.ID_Pratiche) : 0;

                sb.AppendLine(string.Join(";", new string[]
              {
                i.ID_Incasso.ToString(),
                nomeCliente.Replace(";", ","),
                (p?.Titolo ?? "(N/D)").Replace(";", ","),
                nomeOwner.Replace(";", ","),
                totCompensi.ToString("N2"),
                i.DataIncasso.ToString("dd/MM/yyyy"),
                i.Importo.ToString("N2"),
                (avv?.MetodoPagamento ?? "-"),   // ✔ CORRETTO
                (i.VersaInPlafond == true ? "SI" : "NO"),
                (avv != null ? avv.ID_AvvisoParcelle.ToString() : ""),
                (avv?.DataAvviso?.ToString("dd/MM/yyyy") ?? ""),
                (avv?.TotaleAvvisiParcella?.ToString("N2") ?? "0.00"),
                (avv?.Stato ?? "-")
              }));

            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            string nomeFile = $"Incassi_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv";

            return File(buffer, "text/csv", nomeFile);
        }


        [HttpGet]
        public ActionResult EsportaIncassiPdf(DateTime? da, DateTime? a)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Periodo (mese corrente se non specificato)
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            // 🔹 Recupera incassi nel periodo con join alle pratiche e avvisi parcella
            var lista = (
                from i in db.Incassi
                join p in db.Pratiche on i.ID_Pratiche equals p.ID_Pratiche into praticheJoin
                from p in praticheJoin.DefaultIfEmpty()

                join avv in db.AvvisiParcella on i.ID_AvvisoParcella equals avv.ID_AvvisoParcelle into avvisiJoin
                from avv in avvisiJoin.DefaultIfEmpty()

                where i.DataIncasso >= inizio && i.DataIncasso <= fine
                orderby i.DataIncasso
                select new IncassoViewModel
                {
                    ID_Incasso = i.ID_Incasso,
                    ID_Pratiche = i.ID_Pratiche ?? 0,
                    DataIncasso = i.DataIncasso,
                    Importo = i.Importo,
                    MetodoPagamento = i.ModalitaPagamento,
                    VersaInPlafond = i.VersaInPlafond,
                    NomePratica = p != null ? p.Titolo : "(N/D)",

                    // 🔹 Dati Avviso collegato
                    ID_AvvisoParcella = avv != null ? avv.ID_AvvisoParcelle : 0,
                    DescrizioneAvvisoParcella = avv != null ? $"Avviso #{avv.ID_AvvisoParcelle}" : "—",
                    TotalePratica = p != null ? p.Budget : 0,
                    ImportoAvviso = avv != null ? (decimal?)avv.Importo : 0,
                    ImportoIVA = avv != null ? (decimal?)avv.ImportoIVA : 0,
                    AliquotaIVA = avv != null ? (decimal?)avv.AliquotaIVA : 0,
                    Stato = avv != null ? avv.Stato : "—"
                }).ToList();

            // ✅ Genera il PDF con Rotativa
            return new Rotativa.ViewAsPdf("~/Views/Incassi/ReportIncassiPdf.cshtml", lista)
            {
                FileName = $"Incassi_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape, // 📄 meglio in orizzontale se ci sono molte colonne
                CustomSwitches = "--encoding UTF-8"
            };
        }




        /* ex versa utile in plafond non serve commentato in data 15/7/2025 */
        //[HttpPost]
        //public JsonResult VersaUtileInPlafond(int idIncasso)
        //{
        //    try
        //    {
        //        var incasso = db.Incassi.FirstOrDefault(i => i.ID_Incasso == idIncasso);
        //        if (incasso == null)
        //            return Json(new { success = false, message = "Incasso non trovato." });

        //        var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == incasso.ID_Pratiche);
        //        if (pratica == null)
        //            return Json(new { success = false, message = "Pratica collegata non trovata." });

        //        int idProfessionista = pratica.ID_UtenteResponsabile;

        //        // Verifica se già esiste un versamento in plafond per questo incasso
        //        bool esisteGia = db.PlafondUtente.Any(p =>
        //            p.ID_Incasso == idIncasso &&
        //            p.ID_Utente == idProfessionista);

        //        if (esisteGia)
        //            return Json(new { success = false, message = "L’utile è già stato versato in plafond per questo incasso." });

        //        // Recupera utile netto dalla tabella BilancioProfessionista
        //        decimal utileNetto = db.BilancioProfessionista
        //            .Where(b => b.ID_Incasso == idIncasso &&
        //                        b.ID_Professionista == idProfessionista &&
        //                        b.Categoria == "Utile netto da incasso")
        //            .Select(b => (decimal?)b.Importo)
        //            .FirstOrDefault() ?? 0;

        //        if (utileNetto <= 0)
        //            return Json(new { success = false, message = "Utile netto non disponibile o pari a zero." });

        //        var now = DateTime.Now;
        //        int idUtenteCorrente = UserManager.GetIDUtenteCollegato();

        //        var nuovoVersamento = new PlafondUtente
        //        {
        //            ID_Utente = idProfessionista,
        //            ID_Incasso = idIncasso,
        //            ID_Pratiche = incasso.ID_Pratiche,
        //            Importo = utileNetto,
        //            ImportoTotale = utileNetto,
        //            TipoPlafond = "Incasso",
        //            DataVersamento = now,
        //            DataInizio = now.Date,
        //            DataFine = null,
        //            ID_UtenteCreatore = idUtenteCorrente,
        //            ID_UtenteUltimaModifica = null,
        //            DataUltimaModifica = null,
        //            ID_UtenteInserimento = idUtenteCorrente,
        //            DataInserimento = now,
        //            Note = $"Versamento utile netto da incasso ID {idIncasso} per pratica ID {incasso.ID_Pratiche}"
        //        };

        //        db.PlafondUtente.Add(nuovoVersamento);
        //        db.SaveChanges();

        //        return Json(new { success = true, message = "✅ Utile versato correttamente nel plafond." });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { success = false, message = "Errore durante il versamento: " + ex.Message });
        //    }
        //}


        #endregion

    }
}
