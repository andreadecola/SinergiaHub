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
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;   // WordprocessingDocument
using OpenXmlPowerTools;                  // HtmlConverter 
using static Sinergia.Models.PraticaViewModel;
using HtmlAgilityPack;
using System.Text;
using Rotativa;

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
        public ActionResult GestionePraticheList(string ricerca = "", int giorniFiltro = 30, string tipoFiltro = "Tutti")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utenteCorrente == null)
                return Json(new { success = false, message = "Utente non autenticato." }, JsonRequestBehavior.AllowGet);

            DateTime dataLimite = DateTime.Now.AddDays(-giorniFiltro);
            var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Conclusa" };

            var query = db.Pratiche.Where(p =>
                p.Stato != "Eliminato" &&
                statiValidi.Contains(p.Stato));

            if (!string.IsNullOrWhiteSpace(ricerca))
            {
                query = query.Where(p =>
                    p.Titolo.Contains(ricerca) ||
                    p.Descrizione.Contains(ricerca));
            }

            if (giorniFiltro > 0)
            {
                query = query.Where(p => p.DataCreazione >= dataLimite);
            }

            // 🔍 Clienti visibili
            var clientiDisponibili = DashboardHelper.GetClientiDisponibiliPerNavbar(idUtente, utenteCorrente.TipoUtente);

            var idClientiTutti = clientiDisponibili
                .Select(x =>
                {
                    var parts = x.Value.Split('_');
                    return int.Parse(parts.Length > 1 ? parts[1] : parts[0]);
                }).ToList();

            // query = query.Where(p => idClientiTutti.Contains(p.ID_Cliente));

            var clientiEsterniIds = db.Clienti.Select(c => c.ID_Cliente).ToHashSet();
            var operatoriDict = db.OperatoriSinergia.ToDictionary(c => c.ID_Cliente);
            var utentiDict = db.Utenti.ToDictionary(u => u.ID_Utente);

            var praticheList = query.ToList().Select(p =>
            {
                string tipoCliente = "";
                string nomeCliente = "";

                if (clientiEsterniIds.Contains(p.ID_Cliente))
                {
                    tipoCliente = "ClienteEsterno";
                    var cliEst = db.Clienti.FirstOrDefault(c => c.ID_Cliente == p.ID_Cliente);
                    nomeCliente = cliEst != null ? $"{cliEst.Nome} {cliEst.Cognome}".Trim() : "";
                }
                else if (operatoriDict.ContainsKey(p.ID_Cliente))
                {
                    var op = operatoriDict[p.ID_Cliente];
                    tipoCliente = op.TipoCliente ?? "";
                    nomeCliente = string.IsNullOrWhiteSpace(op.TipoRagioneSociale)
                        ? $"{op.Nome} {op.Cognome}"
                        : op.TipoRagioneSociale;
                }

                string nomeOwner = "";
                if (p.ID_Owner.HasValue && operatoriDict.ContainsKey(p.ID_Owner.Value))
                {
                    var owner = operatoriDict[p.ID_Owner.Value];
                    nomeOwner = $"{owner.Nome} {owner.Cognome}";
                }

                string nomeResponsabile = "";
                if (utentiDict.ContainsKey(p.ID_UtenteResponsabile))
                {
                    var u = utentiDict[p.ID_UtenteResponsabile];
                    nomeResponsabile = $"{u.Nome} {u.Cognome}";
                }

                bool haIncaricoGenerato = db.DocumentiPratiche.Any(d =>
                    d.ID_Pratiche == p.ID_Pratiche &&
                    (d.NomeFile.Contains("Incarico") || d.Note.Contains("Incarico")));

                int? idDocumentoIncarico = db.DocumentiPratiche
                    .Where(d => d.ID_Pratiche == p.ID_Pratiche &&
                                (d.NomeFile.Contains("Incarico") || d.Note.Contains("Incarico")))
                    .OrderByDescending(d => d.DataCaricamento)
                    .Select(d => (int?)d.ID_Documento)
                    .FirstOrDefault();

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
                    HaIncaricoGenerato = haIncaricoGenerato,
                    ID_DocumentoIncarico = idDocumentoIncarico
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

            // ✅ Flags per la partial _AzioniPratica.cshtml
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
            ViewBag.IDClienteProfessionistaCorrente = Session["IDClienteProfessionistaCorrente"] as int?;

            return PartialView("~/Views/Pratiche/_GestionePraticheList.cshtml", praticheList);
        }


        [HttpPost]
        public ActionResult CreaPratica(PraticaViewModel model)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            System.Diagnostics.Debug.WriteLine("⏱ [CreaPratica] Inizio esecuzione");

            try
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    int idUtente = UserManager.GetIDUtenteCollegato();
                    DateTime now = DateTime.Now;

                    // 1️⃣ Carica cliente esterno
                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == model.ID_Cliente);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente esterno non trovato." });

                    // 2️⃣ Recupera Operatore (professionista)
                    var operatore = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Cliente == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);
                    if (operatore == null)
                        return Json(new { success = false, message = "Professionista collegato non trovato." });

                    if (operatore.ID_Owner == null)
                    {
                        operatore.ID_Owner = operatore.ID_Cliente;
                        db.SaveChanges();
                    }

                    int idOwner = operatore.ID_Owner.Value;

                    // 3️⃣ Crea pratica
                    var pratica = new Pratiche
                    {
                        Titolo = model.Titolo,
                        Descrizione = model.Descrizione,
                        DataInizioAttivitaStimata = model.DataInizioAttivitaStimata,
                        DataFineAttivitaStimata = model.DataFineAttivitaStimata,
                        Stato = model.Stato,
                        ID_Cliente = cliente.ID_Cliente,
                        ID_UtenteResponsabile = idOwner,
                        ID_UtenteCreatore = idUtente,
                        ID_Owner = idOwner,
                        Budget = model.Budget,
                        Note = model.Note,
                        DataCreazione = now,
                        UltimaModifica = now,
                        TrattenutaPersonalizzata = model.TrattenutaPersonalizzata,

                        //ImportoIncassato = 0
                    };
                    db.Pratiche.Add(pratica);
                    db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"⏱ Dopo salvataggio Pratica: {stopwatch.ElapsedMilliseconds}ms");


                    // 1️⃣3️⃣ Versionamento - PRATICHE
                    db.Pratiche_a.Add(new Pratiche_a
                    {
                        ID_Pratiche_a = pratica.ID_Pratiche,
                        Titolo = pratica.Titolo,
                        Descrizione = pratica.Descrizione,
                        Stato = pratica.Stato,
                        DataInizioAttivitaStimata = pratica.DataInizioAttivitaStimata,
                        DataFineAttivitaStimata = pratica.DataFineAttivitaStimata,
                        Note = pratica.Note,
                        Budget = pratica.Budget,
                        ID_Cliente = pratica.ID_Cliente,
                        ID_UtenteResponsabile = pratica.ID_UtenteResponsabile,
                        ID_UtenteCreatore = pratica.ID_UtenteCreatore,
                        ID_Owner = pratica.ID_Owner,
                        DataCreazione = now,
                        UltimaModifica = now,
                        TrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata,
                        NumeroVersione = 1,
                        ID_UtenteArchiviazione = idUtente,
                        DataArchiviazione = now,
                        ModificheTestuali = "Creazione Pratica"
                    });

                    // 4️⃣ Salva file incarico
                    if (model.IncaricoProfessionale != null && model.IncaricoProfessionale.ContentLength > 0)
                    {
                        var nomeFile = Path.GetFileName(model.IncaricoProfessionale.FileName);
                        var cartella = Server.MapPath($"~/Documenti/Pratiche/{pratica.ID_Pratiche}/");
                        Directory.CreateDirectory(cartella);
                        var path = Path.Combine(cartella, nomeFile);
                        model.IncaricoProfessionale.SaveAs(path);
                    }

                    // 5️⃣ Inserisci Owner nel cluster
                    var ownerFee = db.TipologieCosti
                        .FirstOrDefault(t => t.Nome == "Owner Fee" && t.Stato == "Attivo" && t.Tipo == "Percentuale");
                    decimal percentualeOwner = ownerFee?.ValorePercentuale ?? 5;

                    db.Cluster.Add(new Cluster
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = idOwner,
                        TipoCluster = "Owner",
                        PercentualePrevisione = percentualeOwner,
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    });

                    // 6️⃣ Collaboratori
                    if (model.UtentiAssociati != null)
                    {
                        foreach (var collab in model.UtentiAssociati)
                        {
                            if (collab.ID_Utente == idOwner) continue;

                            db.Cluster.Add(new Cluster
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = collab.ID_Utente,
                                TipoCluster = collab.TipoCluster,
                                PercentualePrevisione = collab.PercentualePrevisione,
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"⏱ Dopo cluster/relazioni/costi: {stopwatch.ElapsedMilliseconds}ms");


                    // 7️⃣ Relazioni
                    db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = idOwner,
                        Ruolo = "Owner",
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    });

                    if (model.UtentiAssociati != null)
                    {
                        foreach (var collab in model.UtentiAssociati)
                        {
                            db.RelazionePraticheUtenti.Add(new RelazionePraticheUtenti
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = collab.ID_Utente,
                                Ruolo = "Collaboratore",
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"⏱ Dopo cluster/relazioni/costi: {stopwatch.ElapsedMilliseconds}ms");

                    // 8️⃣ Salva dati di compenso direttamente nella pratica
                    if (!string.IsNullOrEmpty(model.Tipologia))
                    {
                        pratica.Tipologia = model.Tipologia;

                        if (model.Tipologia == "Fisso" && model.ImportoFisso.HasValue)
                        {
                            pratica.ImportoFisso = model.ImportoFisso.Value;
                            pratica.TerminiPagamento = model.TerminiPagamento; // ✅ aggiunto anche per Fisso
                        }
                        else if (model.Tipologia == "A ore" && model.TariffaOraria.HasValue)
                        {
                            pratica.TariffaOraria = model.TariffaOraria.Value;
                            pratica.TerminiPagamento = model.TerminiPagamento; // ✅ aggiunto anche per A ore
                        }
                        else if (model.Tipologia == "Giudiziale" && model.AccontoGiudiziale.HasValue)
                        {
                            pratica.AccontoGiudiziale = model.AccontoGiudiziale.Value;
                            pratica.GradoGiudizio = model.GradoGiudizio;
                            pratica.TerminiPagamento = model.TerminiPagamento;
                        }

                        // 🔄 Comuni a tutte le tipologie (se presenti)
                        if (model.OrePreviste.HasValue)
                            pratica.OrePreviste = model.OrePreviste;

                        if (model.OreEffettive.HasValue)
                            pratica.OreEffettive = model.OreEffettive;
                    }

                    System.Diagnostics.Debug.WriteLine($"⏱ Dopo cluster/relazioni/costi: {stopwatch.ElapsedMilliseconds}ms");



                    // 9️⃣ Rimborsi
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

                    if (model.CostiPratica != null)
                    {
                        foreach (var cp in model.CostiPratica)
                        {
                            var anagrafica = db.AnagraficaCostiPratica
                                .FirstOrDefault(a => a.ID_AnagraficaCosto == cp.ID_AnagraficaCosto); // ✅ CAMBIATO

                            if (anagrafica != null)
                            {
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    ID_AnagraficaCosto = anagrafica.ID_AnagraficaCosto,
                                    Descrizione = $"{anagrafica.Nome} - {anagrafica.Descrizione}",
                                    Importo = cp.Importo,
                                    ID_UtenteCreatore = idUtente,
                                    DataInserimento = now
                                });
                            }
                            else
                            {
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    Descrizione = cp.Descrizione ?? "Voce non trovata",
                                    Importo = cp.Importo,
                                    ID_UtenteCreatore = idUtente,
                                    DataInserimento = now
                                });
                            }
                        }
                    }



                    db.SaveChanges();

                    // 1️⃣4️⃣ Versionamento - CLUSTER
                    var clusterSalvati = db.Cluster.Where(c => c.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var c in clusterSalvati)
                    {
                        db.Cluster_a.Add(new Cluster_a
                        {
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = c.ID_Utente,
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
                    var costiPratica = db.CostiPratica.Where(c => c.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var c in costiPratica)
                    {
                        db.CostiPratica_a.Add(new CostiPratica_a
                        {
                            ID_Pratiche = c.ID_Pratiche,
                            ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                            Descrizione = c.Descrizione,
                            Importo = c.Importo,
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

                    transaction.Commit();
                    stopwatch.Stop();
                    System.Diagnostics.Debug.WriteLine($"⏱ [CreaPratica] Fine metodo - Totale: {stopwatch.ElapsedMilliseconds}ms");


                    return Json(new { success = true, message = "✅ Pratica creata correttamente!" });
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errors = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"Campo: {e.PropertyName} - Errore: {e.ErrorMessage}")
                    .ToList();

                // 🔍 Log in console Output di Visual Studio
                foreach (var errore in errors)
                    System.Diagnostics.Debug.WriteLine("❌ " + errore);

                return Json(new
                {
                    success = false,
                    message = "Errore di validazione",
                    dettagli = errors
                });
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

                    int idUtente = UserManager.GetIDUtenteCollegato();
                    DateTime now = DateTime.Now;

                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == model.ID_Cliente);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente esterno non trovato." });

                    var operatore = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Cliente == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);
                    if (operatore == null)
                        return Json(new { success = false, message = "Professionista collegato non trovato." });

                    if (operatore.ID_Owner == null)
                    {
                        operatore.ID_Owner = operatore.ID_Cliente;
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
                    pratica.Note = model.Note;
                    pratica.ID_UtenteResponsabile = idOwner;
                    pratica.ID_Owner = idOwner;
                    pratica.UltimaModifica = now;
                    pratica.OrePreviste = model.OrePreviste;
                    pratica.OreEffettive = model.OreEffettive;


                    if (!string.IsNullOrEmpty(model.Tipologia))
                    {
                        pratica.Tipologia = model.Tipologia;
                        pratica.ImportoFisso = null;
                        pratica.TariffaOraria = null;
                        pratica.AccontoGiudiziale = null;
                        pratica.GradoGiudizio = null;
                        pratica.TerminiPagamento = null;
                        pratica.OrePreviste = null;
                        pratica.OreEffettive = null;

                        if (model.Tipologia == "Fisso" && model.ImportoFisso.HasValue)
                        {
                            pratica.ImportoFisso = model.ImportoFisso.Value;
                            pratica.TerminiPagamento = model.TerminiPagamento;
                        }
                        else if (model.Tipologia == "A ore" && model.TariffaOraria.HasValue)
                        {
                            pratica.TariffaOraria = model.TariffaOraria.Value;
                            pratica.OrePreviste = model.OrePreviste;
                            pratica.OreEffettive = model.OreEffettive;
                            pratica.TerminiPagamento = model.TerminiPagamento;
                        }
                        else if (model.Tipologia == "Giudiziale" && model.AccontoGiudiziale.HasValue)
                        {
                            pratica.AccontoGiudiziale = model.AccontoGiudiziale.Value;
                            pratica.GradoGiudizio = model.GradoGiudizio;
                            pratica.TerminiPagamento = model.TerminiPagamento;
                        }
                    }

                    else
                    {
                        pratica.Tipologia = null;
                        pratica.ImportoFisso = null;
                        pratica.TariffaOraria = null;
                        pratica.AccontoGiudiziale = null;
                        pratica.GradoGiudizio = null;
                        pratica.TerminiPagamento = null;
                        pratica.OreEffettive = null;
                        pratica.OrePreviste = null;
                    }

                    db.SaveChanges();


                    // ... (inizio del metodo già definito sopra)

                    // 🔄 Cluster
                    var clusterEsistenti = db.Cluster.Where(c => c.ID_Pratiche == pratica.ID_Pratiche).ToList();
                    foreach (var c in clusterEsistenti)
                    {
                        db.Cluster_a.Add(new Cluster_a
                        {
                            ID_Cluster_Originale = c.ID_Cluster,
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = c.ID_Utente,
                            TipoCluster = c.TipoCluster,
                            PercentualePrevisione = c.PercentualePrevisione,
                            DataAssegnazione = c.DataAssegnazione,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = (db.Cluster_a.Where(x => x.ID_Cluster_Originale == c.ID_Cluster).Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                        });
                    }
                    db.Cluster.RemoveRange(clusterEsistenti);

                    var ownerFee = db.TipologieCosti.FirstOrDefault(t => t.Nome == "Owner Fee" && t.Stato == "Attivo" && t.Tipo == "Percentuale");
                    decimal percentualeOwner = ownerFee?.ValorePercentuale ?? 5;

                    var clusterOwner = new Cluster
                    {
                        ID_Pratiche = pratica.ID_Pratiche,
                        ID_Utente = idOwner,
                        TipoCluster = "Owner",
                        PercentualePrevisione = percentualeOwner,
                        DataAssegnazione = now,
                        ID_UtenteCreatore = idUtente
                    };
                    db.Cluster.Add(clusterOwner);

                    if (model.UtentiAssociati != null)
                    {
                        foreach (var u in model.UtentiAssociati)
                        {
                            if (u.ID_Utente == idOwner) continue;
                            db.Cluster.Add(new Cluster
                            {
                                ID_Pratiche = pratica.ID_Pratiche,
                                ID_Utente = u.ID_Utente,
                                TipoCluster = u.TipoCluster,
                                PercentualePrevisione = u.PercentualePrevisione,
                                DataAssegnazione = now,
                                ID_UtenteCreatore = idUtente
                            });
                        }
                    }
                    db.SaveChanges();

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
                    var costiPraticaEsistenti = db.CostiPratica.Where(c => c.ID_Pratiche == pratica.ID_Pratiche).ToList();
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
                            DataInserimento = c.DataInserimento,
                            ID_UtenteCreatore = c.ID_UtenteCreatore,
                            DataArchiviazione = now,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = (db.CostiPratica_a.Where(x => x.ID_CostoPratica_Originale == c.ID_CostoPratica).Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                        });
                    }
                    db.CostiPratica.RemoveRange(costiPraticaEsistenti);

                    if (model.CostiPratica != null)
                    {
                        foreach (var costo in model.CostiPratica)
                        {
                            if (costo.ID_AnagraficaCosto <= 0)
                                continue;

                            var voceAnagrafica = db.AnagraficaCostiPratica.FirstOrDefault(a => a.ID_AnagraficaCosto == costo.ID_AnagraficaCosto);
                            if (voceAnagrafica != null)
                            {
                                db.CostiPratica.Add(new CostiPratica
                                {
                                    ID_Pratiche = pratica.ID_Pratiche,
                                    Descrizione = voceAnagrafica.Nome + " - " + voceAnagrafica.Descrizione,
                                    ID_AnagraficaCosto = voceAnagrafica.ID_AnagraficaCosto,
                                    Importo = costo.Importo,
                                    ID_ClienteAssociato = costo.ID_ClienteAssociato,
                                    DataInserimento = now,
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

                    if (model.Stato == "Lavorazione")
                    {
                        bool filePDFPresente = db.DocumentiPratiche.Any(d =>
                            d.ID_Pratiche == pratica.ID_Pratiche &&
                            d.NomeFile.ToLower().EndsWith(".pdf"));

                        if (!filePDFPresente)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "⚠️ Non puoi passare allo stato 'Lavorazione' senza aver caricato un file PDF di incarico."
                            });
                        }
                    }


                    if (pratica.Stato == "Conclusa")
                    {
                        ArchiviazioneHelper.ArchiviaPratica(pratica.ID_Pratiche, db, idUtente);
                        transaction.Commit();
                        return Json(new { success = true, message = "✅ Pratica conclusa e archiviata." });
                    }
                    transaction.Commit();
                    return Json(new { success = true, message = "✅ Pratica modificata con successo!" });
                }
            }
            catch (DbEntityValidationException ex)
            {
                var dettagli = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(e => $"{e.PropertyName}: {e.ErrorMessage}");
                return Json(new { success = false, message = $"Errore validazione: {string.Join("; ", dettagli)}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Errore generico: {ex.Message}" });
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

                    Archivia(db.Cluster.Where(c => c.ID_Pratiche == id), c => new Cluster_a
                    {
                        ID_Cluster_Originale = c.ID_Cluster,
                        ID_Pratiche = c.ID_Pratiche,
                        ID_Utente = c.ID_Utente,
                        TipoCluster = c.TipoCluster,
                        PercentualePrevisione = c.PercentualePrevisione,
                        DataAssegnazione = c.DataAssegnazione,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = (db.Cluster_a.Where(x => x.ID_Cluster_Originale == c.ID_Cluster).Max(x => (int?)x.NumeroVersione) ?? 0) + 1
                    });

                    Archivia(db.RelazionePraticheUtenti.Where(r => r.ID_Pratiche == id), r => new RelazionePraticheUtenti_a
                    {
                        ID_Relazione_Originale = 0, // se hai un ID_Relazione, usalo qui
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

                    Archivia(db.CostiPratica.Where(c => c.ID_Pratiche == id), c => new CostiPratica_a
                    {
                        ID_CostoPratica_Originale = c.ID_CostoPratica,
                        ID_Pratiche = c.ID_Pratiche,
                        Descrizione = c.Descrizione,
                        Importo = c.Importo,
                        ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                        ID_ClienteAssociato = c.ID_ClienteAssociato,
                        DataInserimento = c.DataInserimento,
                        ID_UtenteCreatore = c.ID_UtenteCreatore,
                        DataArchiviazione = now,
                        ID_UtenteArchiviazione = userId,
                        NumeroVersione = 1
                    });


                    // 🔁 Archivia tabelle correlate
                    Archivia(db.DocumentiPratiche.Where(d => d.ID_Pratiche == id), d => new DocumentiPratiche_a
                    {
                        // ID originale se esiste
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

                    // Include l'owner del cliente, se esiste
                    var ownerId = db.OperatoriSinergia
                        .Where(o => o.ID_Cliente == pratica.ID_Cliente)
                        .Select(o => o.ID_Owner)
                        .FirstOrDefault();

                    if (ownerId.HasValue)
                        destinatari.Add(ownerId.Value);

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
                    TrattenutaPersonalizzata = p.TrattenutaPersonalizzata,
                    Budget = p.Budget,
                    DataCreazione = p.DataCreazione,
                    UltimaModifica = p.UltimaModifica,
                    Note = p.Note,
                    Tipologia = p.Tipologia,
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
            {
                Debug.WriteLine($"❌ Pratica ID {id} non trovata o eliminata.");
                return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);
            }

            Debug.WriteLine($"✅ Caricata pratica ID {pratica.ID_Pratiche} - Titolo: {pratica.Titolo}");
            Debug.WriteLine($"🧑‍💼 ID_UtenteResponsabile: {pratica.ID_UtenteResponsabile}");

            var nomeOwner = (from o in db.OperatoriSinergia
                             join u in db.Utenti on o.ID_UtenteCollegato equals u.ID_Utente
                             where o.ID_Cliente == pratica.ID_UtenteResponsabile
                             select u.Nome + " " + u.Cognome).FirstOrDefault();

            Debug.WriteLine($"👤 Nome owner recuperato: {nomeOwner}");

            var nomeCliente = (from cli in db.Clienti
                               join op in db.OperatoriSinergia on cli.ID_Operatore equals op.ID_Cliente
                               where cli.ID_Cliente == pratica.ID_Cliente
                               select op.Nome ?? op.TipoRagioneSociale).FirstOrDefault();

            Debug.WriteLine($"🏢 Nome cliente: {nomeCliente}");

            var utentiAssociati = db.Cluster
                .Where(c => c.ID_Pratiche == id && c.TipoCluster == "Collaboratore")
                .Select(c => new
                {
                    ID_Utente = c.ID_Utente,
                    Nome = db.Utenti.Where(u => u.ID_Utente == c.ID_Utente)
                                    .Select(u => u.Nome + " " + u.Cognome)
                                    .FirstOrDefault(),
                    Percentuale = c.PercentualePrevisione
                })
                .Distinct()
                .ToList();

            Debug.WriteLine($"👥 Utenti associati (cluster): {utentiAssociati.Count}");


            var costiPratica = (from c in db.CostiPratica
                                join a in db.AnagraficaCostiPratica on c.ID_AnagraficaCosto equals a.ID_AnagraficaCosto into joined
                                from a in joined.DefaultIfEmpty()
                                where c.ID_Pratiche == id
                                select new
                                {
                                    ID_CostoPratica = c.ID_CostoPratica,
                                    IDCostoHidden = c.ID_CostoPratica,
                                    ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                                    Descrizione = (c.Descrizione ?? ((a.Nome ?? "") + " - " + (a.Descrizione ?? ""))).Trim(),
                                    Importo = c.Importo
                                }).ToList();


            Debug.WriteLine($"📋 Costi Pratica: {costiPratica.Count}");

            var rimborsi = db.RimborsiPratica
                .Where(r => r.ID_Pratiche == id)
                .Select(r => new
                {
                    r.Descrizione,
                    r.Importo
                }).ToList();

            Debug.WriteLine($"💰 Rimborsi trovati: {rimborsi.Count}");

            var documentoIncarico = db.DocumentiPratiche
                .Where(d => d.ID_Pratiche == id && d.Estensione == ".html" && d.Stato == "Da firmare")
                .OrderByDescending(d => d.DataCaricamento)
                .FirstOrDefault();

            if (documentoIncarico != null)
                Debug.WriteLine($"📄 Documento incarico trovato: {documentoIncarico.NomeFile}");

            return Json(new
            {
                success = true,
                data = pratica,
                nomeOwner,
                nomeCliente,
                utentiAssociati,
                costi = costiPratica,
                rimborsi,
                incarico = documentoIncarico?.NomeFile,
                incaricoHtml = documentoIncarico != null
                    ? System.Text.Encoding.UTF8.GetString(documentoIncarico.Documento)
                    : null
            }, JsonRequestBehavior.AllowGet);
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

                var cliente = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Cliente == praticaArchivio.ID_Cliente);
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


        // Gestione Template

        [HttpGet]
        public ActionResult GetTemplateByProfessionista(int idProfessionista)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    // 🔍 Recupera la professione associata all'operatore (professionista)
                    var idProfessione = db.OperatoriSinergia
                        .Where(o => o.ID_Cliente == idProfessionista && o.TipoCliente == "Professionista")
                        .Select(o => o.ID_Professione)
                        .FirstOrDefault();

                    if (idProfessione == 0 || idProfessione == null)
                    {
                        return Json(new { success = false, message = "Professione non associata a questo professionista." }, JsonRequestBehavior.AllowGet);
                    }

                    // 🔍 Recupera il template attivo per quella professione
                    var template = db.TemplateIncarichi
                        .Where(t => t.ID_Professione == idProfessione && t.Stato == "Attivo")
                        .Select(t => new
                        {
                            t.IDTemplateIncarichi,
                            t.NomeTemplate,
                            t.ContenutoHtml
                        })
                        .FirstOrDefault();

                    if (template == null)
                    {
                        return Json(new { success = false, message = "Nessun template attivo trovato per questa professione." }, JsonRequestBehavior.AllowGet);
                    }

                    return Json(new { success = true, data = template }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore durante il recupero del template: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }


        [HttpGet]
        public ActionResult GeneraFoglioIncarico(int idPratica)
        {
            try
            {
                // 🔁 Recupera la pratica con join esplicita sul cliente
                var praticaJoin = (from p in db.Pratiche
                                   join c in db.Clienti on p.ID_Cliente equals c.ID_Cliente
                                   where p.ID_Pratiche == idPratica
                                   select new
                                   {
                                       Pratica = p,
                                       Cliente = c
                                   }).FirstOrDefault();

                if (praticaJoin == null)
                    return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

                var pratica = praticaJoin.Pratica;
                var cliente = praticaJoin.Cliente;

                // 🔍 Recupera il professionista owner collegato alla pratica
                var professionista = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_Cliente == pratica.ID_UtenteResponsabile && o.TipoCliente == "Professionista");

                if (professionista == null)
                    return Json(new { success = false, message = "Professionista non trovato." }, JsonRequestBehavior.AllowGet);

                // 🔍 Carica il template incarico attivo per la professione
                var template = db.TemplateIncarichi
                    .FirstOrDefault(t => t.ID_Professione == professionista.ID_Professione && t.Stato == "Attivo");

                if (template == null)
                    return Json(new { success = false, message = "Template incarico non trovato per la professione." }, JsonRequestBehavior.AllowGet);

                // ✅ Sostituzione dei placeholder
                string contenuto = template.ContenutoHtml;

                string nomeCliente = cliente.RagioneSociale
                ?? (!string.IsNullOrEmpty(cliente.Cognome) || !string.IsNullOrEmpty(cliente.Nome)
                   ? $"{cliente.Cognome} {cliente.Nome}"
                      : "Cliente");

                contenuto = contenuto.Replace("[NOME_CLIENTE]", nomeCliente);
                contenuto = contenuto.Replace("[TITOLO_PRATICA]", pratica.Titolo);
                contenuto = contenuto.Replace("[DATA_INIZIO]", pratica.DataInizioAttivitaStimata?.ToString("dd/MM/yyyy") ?? "");
                contenuto = contenuto.Replace("[DATA_FINE]", pratica.DataFineAttivitaStimata?.ToString("dd/MM/yyyy") ?? "");
                contenuto = contenuto.Replace("[BUDGET]", pratica.Budget.ToString("N2") + " €");

                // 🔁 Altri placeholder
                string nomeProfessionista = $"{professionista.Nome} {professionista.Cognome}".Trim();
                // 🔍 Recupera il nome della città
                string luogo = "";
                if (cliente.ID_Citta.HasValue)
                {
                    var citta = db.Citta.FirstOrDefault(c => c.ID_BPCitta == cliente.ID_Citta.Value);
                    if (citta != null)
                        luogo = citta.NameLocalita;
                }

                string dataGenerazione = DateTime.Now.ToString("dd/MM/yyyy");

                contenuto = contenuto.Replace("[NOME_PROFESSIONISTA]", nomeProfessionista);
                contenuto = contenuto.Replace("[LUOGO]", luogo);
                contenuto = contenuto.Replace("[DATA_GENERAZIONE]", dataGenerazione);

                // 🔁 Conversione newline in <br />
                contenuto = contenuto.Replace("\r\n", "<br />")
                                     .Replace("\n", "<br />")
                                     .Replace("\\n", "<br />"); // <- aggiungi questa


                return Json(new
                {
                    success = true,
                    html = contenuto
                }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore durante la generazione dell’incarico: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
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


        [HttpGet]
        public ActionResult ScaricaFoglioIncarico(int idDocumento)
        {
            var doc = db.DocumentiPratiche.FirstOrDefault(d => d.ID_Documento == idDocumento);
            if (doc == null || doc.Documento == null || doc.Documento.Length == 0)
                return Content("Documento non trovato o vuoto.");

            // Recupera la pratica per il nome
            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == doc.ID_Pratiche);

            string nomeFile = doc.NomeFile;

            if (pratica != null)
            {
                // Crea un nome file usando il titolo della pratica, pulito da caratteri invalidi
                string titoloPulito = string.Concat(pratica.Titolo.Split(System.IO.Path.GetInvalidFileNameChars()));
                nomeFile = $"Incarico_{titoloPulito}.pdf";
            }

            return File(doc.Documento, "application/pdf", nomeFile);
        }


        [HttpPost]
        [ValidateInput(false)]
        public ActionResult GeneraPDFIncaricoDaHtml()
        {
            try
            {
                if (!int.TryParse(Request.Form["idPratica"], out int idPratica))
                    return Json(new { success = false, message = "ID pratica non valido." });

                // Usa Request.Unvalidated per leggere i dati html senza validazione
                string html = Request.Unvalidated["html"];

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                // Percorso cartella App_Data\Incarichi
                string debugFolder = Server.MapPath("~/App_Data/Incarichi");
                if (!System.IO.Directory.Exists(debugFolder))
                {
                    System.IO.Directory.CreateDirectory(debugFolder);
                }

                // Nome file personalizzato con ID pratica e timestamp
                string nomeFileDebug = $"_incarico_pratica_{idPratica}_{DateTime.Now:yyyyMMddHHmmss}.html";
                string filePath = System.IO.Path.Combine(debugFolder, nomeFileDebug);

                // Salva il file HTML di debug
                System.IO.File.WriteAllText(filePath, html);

                string nomeFile = $"Incarico_{pratica.ID_Pratiche}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

                var pdf = new Rotativa.ViewAsPdf("~/Views/TemplateIncarichi/TemplateCompilato.cshtml", (object)html)

                {
                    FileName = nomeFile,
                    PageSize = Rotativa.Options.Size.A4,
                    PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
                };

                byte[] pdfBytes = pdf.BuildPdf(ControllerContext);

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
                    Note = "Incarico generato da template"
                };

                db.DocumentiPratiche.Add(documento);
                db.SaveChanges();

                return Json(new { success = true, message = "PDF generato e salvato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore generazione PDF: " + ex.Message });
            }
        }


        // Fine Gestione Template 

        [HttpGet]
        public ActionResult DettaglioPratica(int id, string tipoCliente)
        {
            int idUtenteCollegato = UserManager.GetIDUtenteCollegato();

            var praticaEntity = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == id && p.Stato != "Eliminato");
            if (praticaEntity == null)
                return View("~/Views/Shared/Error.cshtml", model: "Pratica non trovata.");

            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == praticaEntity.ID_Cliente);
            string nomeCliente = cliente != null ? $"{cliente.Nome} {cliente.Cognome}".Trim() : "Cliente sconosciuto";

            // ✅ Recupero Nome e Cognome del professionista direttamente da OperatoriSinergia se è l'owner
            string nomeResponsabile = "Professionista sconosciuto";

            // 1. Prova come collegato (caso collaboratore)
            var operatoreCollegato = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == praticaEntity.ID_UtenteResponsabile);

            if (operatoreCollegato != null && operatoreCollegato.ID_Owner != null)
            {
                var owner = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Cliente == operatoreCollegato.ID_Owner);
                if (owner != null)
                    nomeResponsabile = $"{owner.Nome} {owner.Cognome}".Trim();
            }
            else
            {
                // 2. Prova come owner diretto (caso ID_Cliente = ID_UtenteResponsabile)
                var ownerDiretto = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_Cliente == praticaEntity.ID_UtenteResponsabile);

                if (ownerDiretto != null)
                    nomeResponsabile = $"{ownerDiretto.Nome} {ownerDiretto.Cognome}".Trim();
            }

            // 🔎 Recupero Cluster collegati alla pratica
            var clusterList = (
                from c in db.Cluster
                join u in db.Utenti on c.ID_Utente equals u.ID_Utente
                where c.ID_Pratiche == id
                select new ClusterViewModel
                {
                    ID_Pratiche = c.ID_Pratiche,
                    ID_Utente = c.ID_Utente,
                    TipoCluster = c.TipoCluster,
                    PercentualePrevisione = c.PercentualePrevisione,
                    DataAssegnazione = c.DataAssegnazione,
                    NomeUtente = u.Nome + " " + u.Cognome,
                    ImportoCalcolato = (praticaEntity.Budget) * (c.PercentualePrevisione / 100)
                }
            ).ToList();

            // 📝 Debug output
            System.Diagnostics.Debug.WriteLine("=== CLUSTER LIST ===");
            System.Diagnostics.Debug.WriteLine($"Cluster trovati: {clusterList.Count}");
            foreach (var cl in clusterList)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ID_Pratiche: {cl.ID_Pratiche}, " +
                    $"Utente: {cl.NomeUtente}, " +
                    $"Percentuale: {cl.PercentualePrevisione}, " +
                    $"Importo: {cl.ImportoCalcolato}");
            }
            System.Diagnostics.Debug.WriteLine("====================");

            var pratica = new PraticaViewModel
            {
                ID_Pratiche = praticaEntity.ID_Pratiche,
                Titolo = praticaEntity.Titolo,
                Descrizione = praticaEntity.Descrizione,
                DataInizioAttivitaStimata = praticaEntity.DataInizioAttivitaStimata,
                DataFineAttivitaStimata = praticaEntity.DataFineAttivitaStimata,
                Stato = praticaEntity.Stato,
                ID_Cliente = praticaEntity.ID_Cliente,
                ID_UtenteResponsabile = praticaEntity.ID_UtenteResponsabile,
                ID_UtenteUltimaModifica = praticaEntity.ID_UtenteUltimaModifica,
                Budget = praticaEntity.Budget,
                DataCreazione = praticaEntity.DataCreazione,
                UltimaModifica = praticaEntity.UltimaModifica,
                Note = praticaEntity.Note,
                NomeCliente = nomeCliente,
                TipoCliente = cliente?.TipoCliente ?? "N/D",
                NomeUtenteResponsabile = nomeResponsabile
            };

            pratica.Compensi = db.CompensiPratica
                .Where(c => c.ID_Pratiche == id)
                .Select(c => new CompensoViewModel
                {
                    Tipo = c.Tipo,
                    Descrizione = c.Descrizione,
                    Importo = c.Importo
                }).ToList();

            pratica.Rimborsi = db.RimborsiPratica
                .Where(r => r.ID_Pratiche == id)
                .Select(r => new RimborsoViewModel
                {
                    Descrizione = r.Descrizione,
                    Importo = r.Importo
                }).ToList();

            pratica.CostiPratica = db.CostiPratica
                .Where(c => c.ID_Pratiche == id)
                .Select(c => new CostoPraticaViewModel
                {
                    Descrizione = c.Descrizione,
                    Importo = c.Importo ?? 0,
                    ID_ClienteAssociato = c.ID_ClienteAssociato,
                    NomeFornitoreManuale = c.ID_ClienteAssociato.HasValue
                        ? db.Clienti.Where(cl => cl.ID_Cliente == c.ID_ClienteAssociato.Value).Select(cl => cl.Nome).FirstOrDefault()
                        : null
                }).ToList();

            var utentiAssociati = (from rel in db.RelazionePraticheUtenti
                                   join u in db.Utenti on rel.ID_Utente equals u.ID_Utente
                                   where rel.ID_Pratiche == id
                                   select new UtenteViewModel
                                   {
                                       ID_Utente = u.ID_Utente,
                                       Nome = u.Nome,
                                       Cognome = u.Cognome
                                   }).ToList();

            var avvisiParcella = db.AvvisiParcella
                .Where(a => a.ID_Pratiche == id && a.Stato != "Annullato")
                .Select(a => new AvvisoParcellaViewModel
                {
                    ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                    ID_Pratiche = a.ID_Pratiche.Value,
                    DataAvviso = a.DataAvviso,
                    Importo = a.Importo,
                    Stato = a.Stato,
                    MetodoPagamento = a.MetodoPagamento,
                    ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                    AliquotaIVA = a.AliquotaIVA,
                    ImportoIVA = a.ImportoIVA,
                    NomePratica = pratica.Titolo
                }).ToList();

            decimal totaleCompensi = pratica.Compensi.Sum(c => c.Importo);
            decimal totaleRimborsi = pratica.Rimborsi.Sum(r => r.Importo);
            decimal totaleCosti = pratica.CostiPratica.Sum(c => c.Importo);
            decimal totalePercentuale = clusterList.Sum(c => c.PercentualePrevisione);
            decimal importoFinale = pratica.Budget * (totalePercentuale / 100);

            var model = new VisualizzaDettaglioPraticaViewModel
            {
                Pratica = pratica,
                Cluster = clusterList,
                ImportoFinale = importoFinale,
                Utenti = utentiAssociati,
                TotaleCompensi = totaleCompensi,
                TotaleRimborsi = totaleRimborsi,
                TotaleCosti = totaleCosti,
                AvvisiParcella = avvisiParcella
            };

            return View("~/Views/Pratiche/VisualizzaDettaglio.cshtml", model);
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
        public ActionResult DownloadDocumentoPratica(int id)
        {
            var documento = db.DocumentiPratiche.FirstOrDefault(d => d.ID_Documento == id && d.Stato == "Attivo");
            if (documento == null)
                return HttpNotFound("Documento non trovato.");

            return File(documento.Documento, documento.TipoContenuto, documento.NomeFile);
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
        public JsonResult GetUtentiAttivi()
        {
            var utentiAttivi = db.Utenti
                .Where(u => u.Stato == "Attivo" && u.TipoUtente == "Professionista") // filtro qui
                .Select(u => new
                {
                    u.ID_Utente,
                    NomeCompleto = u.Nome + " " + u.Cognome
                })
                .ToList();

            return Json(utentiAttivi, JsonRequestBehavior.AllowGet);
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
            var utenti = db.RelazioneUtenti
                .Where(r => r.ID_UtenteAssociato == idCliente && r.Stato == "Attivo")
                .Join(db.Utenti, r => r.ID_Utente, u => u.ID_Utente, (r, u) => new
                {
                    ID_Utente = u.ID_Utente,
                    NomeCompleto = u.Nome + " " + u.Cognome
                })
                .OrderBy(u => u.NomeCompleto)
                .ToList();

            return Json(utenti, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetClientiAttivi()
        {
            // 👤 Usa l'utente ATTIVO (impersonificato o loggato normale)
            int idUtente = UserManager.GetIDUtenteAttivo();

            // 🔍 Trova l'operatore (professionista) collegato a quell'utente
            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente);

            if (operatore == null)
            {
                return Json(new { success = false, message = "Operatore non trovato." }, JsonRequestBehavior.AllowGet);
            }

            int idOperatore = operatore.ID_Cliente;

            var clienti = db.Clienti
                .Where(c => c.ID_Operatore == idOperatore && c.Stato == "Attivo")
                .Select(c => new
                {
                    c.ID_Cliente,
                    Nome = (string.IsNullOrEmpty(c.RagioneSociale))
                            ? (c.Nome + " " + c.Cognome)
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
            var cliente = db.OperatoriSinergia.Find(model.ID_Cliente);
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
        public JsonResult GetOwnerCliente(int idCliente)
        {
            try
            {
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == idCliente && c.Stato != "Eliminato");
                if (cliente == null)
                    return Json(new { success = false, message = "Cliente non trovato." }, JsonRequestBehavior.AllowGet);

                if (cliente.ID_Operatore == 0)
                    return Json(new { success = false, message = "Cliente senza operatore." }, JsonRequestBehavior.AllowGet);

                // 🔍 Recupera record OperatoriSinergia (rappresenta il professionista owner collegato al cliente)
                var owner = db.OperatoriSinergia.FirstOrDefault(o =>
                    o.ID_Cliente == cliente.ID_Operatore &&
                    o.TipoCliente == "Professionista" &&
                    o.Stato != "Eliminato");

                if (owner == null)
                    return Json(new { success = false, message = "Owner non trovato." }, JsonRequestBehavior.AllowGet);

                // 🔍 Ora cerchiamo l’utente collegato a questo Operatore
                var utenteOwner = db.Utenti.FirstOrDefault(u => u.ID_Utente == owner.ID_Cliente);

                string nomeCompleto = string.IsNullOrWhiteSpace(owner.Cognome)
                    ? owner.Nome
                    : $"{owner.Nome} {owner.Cognome}";

                // 🔍 Estrai anche la professione collegata
                int? idProfessione = owner.ID_Professione;

                return Json(new
                {
                    success = true,
                    nomeOwner = nomeCompleto,
                    idOperatore = owner.ID_Cliente,              // ← per assegnare alla pratica
                    idOwner = utenteOwner?.ID_Utente,            // ← per escludere dai collaboratori
                    idProfessione = idProfessione
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
        public ActionResult EsportaPraticheCsv(DateTime? da, DateTime? a)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Range date
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Conclusa" };

            var pratiche = db.Pratiche
                .Where(p => p.Stato != "Eliminato"
                         && statiValidi.Contains(p.Stato)
                         && p.DataCreazione >= inizio
                         && p.DataCreazione <= fine)
                .OrderBy(p => p.DataCreazione)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID Pratica;Titolo;Cliente;Owner;Responsabile;Data Creazione;Stato;Budget");

            foreach (var p in pratiche)
            {
                string cliente = db.Clienti
                    .Where(c => c.ID_Cliente == p.ID_Cliente)
                    .Select(c => c.Nome + " " + c.Cognome)
                    .FirstOrDefault() ?? "-";

                string owner = "-";
                if (p.ID_Owner.HasValue)
                {
                    owner = db.OperatoriSinergia
                        .Where(o => o.ID_Cliente == p.ID_Owner.Value)
                        .Select(o => o.Nome + " " + o.Cognome)
                        .FirstOrDefault() ?? "-";
                }

                string responsabile = db.Utenti
                    .Where(u => u.ID_Utente == p.ID_UtenteResponsabile)
                    .Select(u => u.Nome + " " + u.Cognome)
                    .FirstOrDefault() ?? "-";

                sb.AppendLine($"{p.ID_Pratiche};" +
                              $"{p.Titolo};" +
                              $"{cliente};" +
                              $"{owner};" +
                              $"{p.DataCreazione:dd/MM/yyyy};" +
                              $"{p.Stato};" +
                              $"{p.Budget.ToString("N2")}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Pratiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }


        [HttpGet]
        public ActionResult EsportaPratichePdf(DateTime? da, DateTime? a)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Range date
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var statiValidi = new[] { "Attiva", "Inattiva", "In lavorazione", "Contrattualizzazione", "Conclusa" };

            var lista = db.Pratiche
                .Where(p => p.Stato != "Eliminato"
                         && statiValidi.Contains(p.Stato)
                         && p.DataCreazione >= inizio
                         && p.DataCreazione <= fine)
                .OrderBy(p => p.DataCreazione)
                .ToList()
                .Select(p => new PraticaViewModel
                {
                    ID_Pratiche = p.ID_Pratiche,
                    Titolo = p.Titolo,
                    NomeCliente = db.Clienti
                        .Where(c => c.ID_Cliente == p.ID_Cliente)
                        .Select(c => c.Nome + " " + c.Cognome)
                        .FirstOrDefault() ?? "-",

                    // 🔹 sostituito Responsabile con Owner
                    NomeOwner = db.OperatoriSinergia
                        .Where(o => o.ID_Cliente == p.ID_Owner)
                        .Select(o => o.Nome + " " + o.Cognome)
                        .FirstOrDefault() ?? "-",

                    DataCreazione = p.DataCreazione,
                    Stato = p.Stato
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Pratiche/ReportPratichePdf.cshtml", lista)
            {
                FileName = $"Pratiche_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
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

                if (ricorrenza == null)
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);

                // Recupero anche il nome del costo generale
                var nomeCosto = db.TipologieCosti
                    .Where(t => t.ID_TipoCosto == idAnagraficaCosto)
                    .Select(t => t.Nome)
                    .FirstOrDefault() ?? "-";

                return Json(new
                {
                    success = true,
                    ricorrenza = new
                    {
                        ID_Ricorrenza = ricorrenza.ID_Ricorrenza,
                        ID_TipoCosto = ricorrenza.ID_TipoCostoGenerale,
                        ID_Professione = ricorrenza.ID_Professione,
                        ID_Professionista = ricorrenza.ID_Professionista,
                        Categoria = ricorrenza.Categoria,
                        Periodicita = ricorrenza.Periodicita,
                        TipoValore = ricorrenza.TipoValore,
                        Valore = ricorrenza.Valore,
                        DataInizio = ricorrenza.DataInizio?.ToString("yyyy-MM-dd"),
                        DataFine = ricorrenza.DataFine?.ToString("yyyy-MM-dd"),
                        Attivo = ricorrenza.Attivo,
                        NomeCosto = nomeCosto
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
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();   // chi salva
                int idProfessionistaAttivo = UserManager.GetIDUtenteAttivo(); // professionista effettivo impersonificato
                DateTime now = DateTime.Now;
                RicorrenzeCosti ricorrenza;
                bool isModifica = model.ID_Ricorrenza.HasValue;

                // 🔍 Validazione manuale base
                if (model.ID_AnagraficaCosto <= 0 || string.IsNullOrEmpty(model.Categoria) ||
                    string.IsNullOrEmpty(model.TipoValore) || model.Valore == null)
                {
                    return Json(new { success = false, message = "Compilare tutti i campi obbligatori." });
                }

                // ✅ Costi speciali (senza periodicità/data)
                bool èSpeciale = model.Categoria == "Trattenuta Sinergia" || model.Categoria == "Owner Fee";
                if (!èSpeciale)
                {
                    // Per le altre categorie → periodicità e data inizio sono obbligatorie
                    if (string.IsNullOrEmpty(model.Periodicita) || !model.DataInizio.HasValue)
                    {
                        return Json(new { success = false, message = "Compilare Periodicità e Data Inizio." });
                    }
                }
                else
                {
                    // Pulizia forzata per i costi speciali
                    model.Periodicita = null;
                    model.DataInizio = null;
                    model.DataFine = null;
                }

                // 🧠 Una Tantum → se DataInizio = DataFine, imposta Giornaliero se manca
                if (model.DataInizio.HasValue && model.DataFine.HasValue && model.DataInizio.Value == model.DataFine.Value)
                {
                    if (string.IsNullOrEmpty(model.Periodicita))
                        model.Periodicita = "Giornaliero";
                }

                // 🎯 Assegna il professionista effettivo se non impostato
                if (model.Categoria == "Costo Generale" && (model.ID_Professionista == null || model.ID_Professionista == 0))
                {
                    var professionista = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_UtenteCollegato == idProfessionistaAttivo && o.TipoCliente == "Professionista");

                    if (professionista != null)
                    {
                        model.ID_Professionista = professionista.ID_Cliente;
                        model.ID_Professione = professionista.ID_Professione;
                    }
                }

                if (isModifica)
                {
                    ricorrenza = db.RicorrenzeCosti.FirstOrDefault(r => r.ID_Ricorrenza == model.ID_Ricorrenza);
                    if (ricorrenza == null)
                        return Json(new { success = false, message = "Ricorrenza non trovata." });

                    ricorrenza.ID_Professionista = model.ID_Professionista;
                    ricorrenza.ID_Professione = model.ID_Professione;
                    ricorrenza.Periodicita = model.Periodicita;
                    ricorrenza.TipoValore = model.TipoValore;
                    ricorrenza.Categoria = model.Categoria;
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
                        ID_TipoCostoGenerale = model.ID_AnagraficaCosto,
                        ID_Professione = model.ID_Professione,
                        ID_Professionista = model.ID_Professionista,
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

                // 📦 Versionamento
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
                    ModificheTestuali = $"💾 {(isModifica ? "Modificata" : "Creata")} ricorrenza (TipoCosto #{ricorrenza.ID_TipoCostoGenerale}) da utente {idUtenteCorrente}"
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
                                    where os.ID_Cliente == c.ID_Utente && os.TipoCliente == "Professionista"
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

        public ActionResult GestionePlafondList(bool mostraPagamenti = false)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            System.Diagnostics.Debug.WriteLine("🟢 ID Utente collegato: " + idUtenteCorrente);

            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utenteCorrente == null)
            {
                System.Diagnostics.Debug.WriteLine("🔴 Utente non trovato");
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
            }

            System.Diagnostics.Debug.WriteLine($"👤 Utente corrente: {utenteCorrente.Nome} {utenteCorrente.Cognome} | Tipo: {utenteCorrente.TipoUtente}");

            bool puoAggiungere = false;
            bool puoModificare = false;
            bool puoEliminare = false;

            if (utenteCorrente.TipoUtente == "Admin")
            {
                puoAggiungere = puoModificare = puoEliminare = true;
            }
            else
            {
                var permessi = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
                puoAggiungere = permessi.Any(p => p.Aggiungi == true);
                puoModificare = permessi.Any(p => p.Modifica == true);
                puoEliminare = permessi.Any(p => p.Elimina == true);

                System.Diagnostics.Debug.WriteLine($"🛡 Permessi - Aggiungi: {puoAggiungere}, Modifica: {puoModificare}, Elimina: {puoEliminare}");
            }

            var finanziamenti = db.FinanziamentiProfessionisti.AsQueryable();
            var versamenti = db.PlafondUtente.AsQueryable();

            if (utenteCorrente.TipoUtente == "Professionista")
            {
                finanziamenti = finanziamenti.Where(f => f.ID_Professionista == idUtenteCorrente);
                System.Diagnostics.Debug.WriteLine("🔎 Filtro finanziamenti per ID_Professionista = " + idUtenteCorrente);

                var idCliente = db.OperatoriSinergia
                    .Where(o => o.ID_UtenteCollegato == idUtenteCorrente && o.TipoCliente == "Professionista")
                    .Select(o => (int?)o.ID_Cliente)
                    .FirstOrDefault();

                System.Diagnostics.Debug.WriteLine("📌 ID Cliente da OperatoriSinergia: " + (idCliente.HasValue ? idCliente.Value.ToString() : "null"));

                if (idCliente.HasValue && idCliente.Value > 0)
                {
                    versamenti = versamenti.Where(v => v.ID_Utente == idCliente.Value);
                    System.Diagnostics.Debug.WriteLine("📥 Versamenti filtrati per ID_Utente = ID_Cliente = " + idCliente.Value);
                }
                else
                {
                    versamenti = versamenti.Where(v => v.ID_Utente == idUtenteCorrente);
                    System.Diagnostics.Debug.WriteLine("📥 Versamenti filtrati per ID_Utente = " + idUtenteCorrente);
                }
            }
            else if (utenteCorrente.TipoUtente == "Collaboratore")
            {
                var professionistiAssegnati = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == idUtenteCorrente && r.Stato == "Attivo")
                    .Select(r => r.ID_Utente)
                    .ToList();

                System.Diagnostics.Debug.WriteLine("👥 Professionisti assegnati: " + string.Join(", ", professionistiAssegnati));

                finanziamenti = finanziamenti.Where(f => professionistiAssegnati.Contains(f.ID_Professionista));
                versamenti = versamenti.Where(v => professionistiAssegnati.Contains(v.ID_Utente));
            }

            var listaFin = (from f in finanziamenti
                            join u in db.Utenti on f.ID_Professionista equals u.ID_Utente
                            select new FinanziamentiProfessionistiViewModel
                            {
                                ID_Finanziamento = f.ID_Finanziamento,
                                ID_Plafond = null,
                                ID_Professionista = f.ID_Professionista,
                                NomeProfessionista = u.Cognome + " " + u.Nome,
                                Importo = f.Importo,
                                DataVersamento = (DateTime)f.DataVersamento,
                                TipoPlafond = "Finanziamento",
                                DataInizio = f.DataVersamento,
                                DataFine = null,
                                PuoModificare = puoModificare,
                                PuoEliminare = puoEliminare
                            });

            var listaInc = (from v in versamenti
                            join o in db.OperatoriSinergia on v.ID_Utente equals o.ID_Cliente
                            select new FinanziamentiProfessionistiViewModel
                            {
                                ID_Finanziamento = 0,
                                ID_Plafond = v.ID_PlannedPlafond,
                                ID_Professionista = (int)o.ID_UtenteCollegato,
                                NomeProfessionista = o.Cognome + " " + o.Nome,
                                Importo = v.Importo,
                                DataVersamento = (DateTime)v.DataVersamento,
                                TipoPlafond = v.TipoPlafond ?? "Incasso",
                                DataInizio = v.DataInizio,
                                DataFine = v.DataFine,
                                PuoModificare = false,
                                PuoEliminare = false
                            });

            var listaCosti = (from c in db.CostiPersonaliUtente
                              join u in db.Utenti on c.ID_Utente equals u.ID_Utente
                              select new FinanziamentiProfessionistiViewModel
                              {
                                  ID_Finanziamento = 0,
                                  ID_Plafond = 0,
                                  ID_CostoPersonale = c.ID_CostoPersonale,  // <-- aggiungi questa riga
                                  ID_Professionista = c.ID_Utente,
                                  NomeProfessionista = u.Cognome + " " + u.Nome,
                                  Importo = (decimal)-c.Importo, // visualizzato come negativo
                                  DataVersamento = c.DataInserimento,
                                  TipoPlafond = "Costo Personale",
                                  DataInizio = null,
                                  DataFine = null,
                                  PuoModificare = false,
                                  PuoEliminare = false
                              });

            var listaPagamentiDaPlafond = new List<FinanziamentiProfessionistiViewModel>();

            if (mostraPagamenti)
            {
                listaPagamentiDaPlafond = (
                    from g in db.GenerazioneCosti
                    where g.Approvato == true && g.Stato == "Pagato" && g.ID_Utente.HasValue
                    let utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == g.ID_Utente.Value)
                    let operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Cliente == g.ID_Utente.Value && o.TipoCliente == "Professionista")
                    where utente != null || operatore != null
                    select new FinanziamentiProfessionistiViewModel
                    {
                        ID_Finanziamento = 0,
                        ID_Plafond = null,
                        ID_CostoPersonale = null,
                        ID_Professionista = g.ID_Utente.Value,
                        NomeProfessionista = utente != null
                            ? utente.Cognome + " " + utente.Nome
                            : (operatore.Cognome + " " + operatore.Nome),
                        Importo = (decimal)-g.Importo,
                        DataVersamento = g.DataRegistrazione,
                        TipoPlafond = "Pagamento Costo: " + (g.Descrizione ?? "–") + " [" + g.Categoria + "]",
                        DataInizio = null,
                        DataFine = null,
                        PuoModificare = false,
                        PuoEliminare = false
                    }).ToList();
            }

            int countFin = listaFin.Count();
            int countInc = listaInc.Count();
            int countCosti = listaCosti.Count();
            int countPagamenti = listaPagamentiDaPlafond.Count();



            System.Diagnostics.Debug.WriteLine("📊 Finanziamenti trovati: " + countFin);
            System.Diagnostics.Debug.WriteLine("📊 Incassi trovati: " + countInc);
            System.Diagnostics.Debug.WriteLine("📊 Costi personali trovati: " + countCosti);
            System.Diagnostics.Debug.WriteLine("📊 Costi pagati da plafond trovati: " + countPagamenti);

            var lista = listaFin.ToList()
                .Concat(listaInc.ToList())
                .Concat(listaCosti.ToList())
                .Concat(listaPagamentiDaPlafond.ToList()) // 👈 AGGIUNTA
                .OrderByDescending(x => x.DataVersamento)
                .ToList();


            System.Diagnostics.Debug.WriteLine("📋 Totale voci da visualizzare: " + lista.Count);

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

            if (utenteCorrente.TipoUtente == "Admin" || utenteCorrente.TipoUtente == "Collaboratore")
            {
                ViewBag.Professionisti = db.Utenti
                    .Where(u => u.TipoUtente == "Professionista")
                    .OrderBy(u => u.Cognome)
                    .Select(u => new SelectListItem
                    {
                        Value = u.ID_Utente.ToString(),
                        Text = u.Cognome + " " + u.Nome
                    }).ToList();
            }
            else
            {
                ViewBag.Professionisti = new List<SelectListItem>();
            }

            ViewBag.MostraPagamenti = mostraPagamenti; // ← se lo stai già facendo, va bene così

            // ✅ Calcolo del totale effettivo del plafond
            decimal totalePlafondEffettivo =
                     listaFin.Select(x => x.Importo).DefaultIfEmpty(0m).Sum()
                     + listaInc.Select(x => x.Importo).DefaultIfEmpty(0m).Sum()
                     + listaCosti.Select(x => x.Importo).DefaultIfEmpty(0m).Sum();


            // I pagamenti devono essere sempre scalati anche se non mostrati
            var pagamentiPlafondEffettivi = (
                from g in db.GenerazioneCosti
                where g.Approvato == true && g.Stato == "Pagato" && g.ID_Utente.HasValue
                select -(g.Importo ?? 0m)
            ).ToList();

            totalePlafondEffettivo += pagamentiPlafondEffettivi.Sum();

            ViewBag.TotalePlafond = totalePlafondEffettivo;


            return PartialView("~/Views/Plafond/_GestionePlafondList.cshtml", lista);
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

                idProfessionista = rel.ID_Utente;
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
                    int? idCliente = operatore?.ID_Cliente;

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

        public ActionResult GestioneTemplateIncarichiList(int? idProfessione = null)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 🔎 Query base
            IQueryable<TemplateIncarichi> query = db.TemplateIncarichi
                .Where(t => t.Stato != "Eliminato");

            if (idProfessione.HasValue)
                query = query.Where(t => t.ID_Professione == idProfessione.Value);

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
                    ID_Professione = t.ID_Professione,
                    Stato = t.Stato,
                    PuoModificare = puoModificare,
                    PuoEliminare = puoEliminare,
                    NomeProfessione = db.Professioni
                        .Where(p => p.ProfessioniID == t.ID_Professione)
                        .Select(p => p.Descrizione)
                        .FirstOrDefault()
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

            // ViewBag select professioni
            ViewBag.Professioni = db.Professioni
                .OrderBy(p => p.Descrizione)
                .Select(p => new SelectListItem
                {
                    Value = p.ProfessioniID.ToString(),
                    Text = (p.Codice ?? "") + " - " + p.Descrizione
                })
                .ToList();



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
                    ID_Professione = model.ID_Professione
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
            var professionista = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Cliente == pratica.ID_UtenteResponsabile && o.TipoCliente == "Professionista");

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
    public ActionResult UploadTemplateDocx(HttpPostedFileBase file, int? idProfessione)
    {
        if (file == null || file.ContentLength == 0)
            return Json(new { success = false, message = "❌ Nessun file caricato." });

        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "❌ Caricare solo file in formato DOCX." });

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
                        PageTitle = "Template incarico importato"
                    };

                    XElement html = HtmlConverter.ConvertToHtml(doc, settings);
                    var body = html.Descendants().FirstOrDefault(x => x.Name.LocalName == "body");

                    htmlContent = body != null
                        ? string.Join("", body.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting)))
                        : "";
                }
            }

            // ✅ Pulisci con HtmlAgilityPack
            var docHtml = new HtmlDocument();
            docHtml.LoadHtml(htmlContent);

            // lista tag ammessi
            var allowedTags = new HashSet<string>
        {
            "p","h1","h2","h3","h4","h5","h6",
            "b","strong","i","em","u",
            "ul","ol","li",
            "table","tr","td","th"
        };

            // Rimuove tutti i nodi che non sono ammessi
            foreach (var node in docHtml.DocumentNode.Descendants().ToList())
            {
                if (!allowedTags.Contains(node.Name.ToLower()))
                {
                    // Mantieni solo il testo (senza il tag)
                    node.ParentNode.ReplaceChild(HtmlNode.CreateNode(node.InnerText), node);
                }
            }

            htmlContent = docHtml.DocumentNode.InnerHtml;

            if (string.IsNullOrWhiteSpace(htmlContent))
                return Json(new { success = false, message = "❌ Conversione fallita: HTML vuoto." });

            string nomeTemplate = Path.GetFileNameWithoutExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(nomeTemplate))
                nomeTemplate = "Template_" + DateTime.Now.ToString("yyyyMMdd_HHmm");

            int? idProf = idProfessione > 0 ? idProfessione : null;

            var nuovoTemplate = new TemplateIncarichi
            {
                NomeTemplate = nomeTemplate,
                ContenutoHtml = htmlContent,
                Stato = "Attivo",
                ID_Professione = idProf ?? 0
            };

            db.TemplateIncarichi.Add(nuovoTemplate);
            db.SaveChanges();

            db.TemplateIncarichi_a.Add(new TemplateIncarichi_a
            {
                ID_Archivio = nuovoTemplate.IDTemplateIncarichi,
                NomeTemplate = nuovoTemplate.NomeTemplate,
                ContenutoHtml = nuovoTemplate.ContenutoHtml,
                Stato = nuovoTemplate.Stato,
                ID_Professione = idProf ?? 0,
                NumeroVersione = 1,
                DataArchiviazione = DateTime.Now,
                ID_UtenteArchiviazione = idUtente,
                ModificheTestuali = $"📄 Importato da file DOCX ({file.FileName}) da utente {idUtente} il {DateTime.Now:g}"
            });
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = $"✅ Template '{nuovoTemplate.NomeTemplate}' importato correttamente!"
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

        public ActionResult GestioneAvvisiParcellaList(int? idPratica = null)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 🔍 Query base
            IQueryable<AvvisiParcella> query = db.AvvisiParcella;

            if (idPratica.HasValue)
                query = query.Where(a => a.ID_Pratiche == idPratica.Value);

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

            // 🔄 Proiezione in ViewModel
            var lista = query
                .OrderByDescending(a => a.DataAvviso)
                .ToList()
                .Select(a => new AvvisoParcellaViewModel
                {
                    ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                    ID_Pratiche = a.ID_Pratiche ?? 0,
                    DataAvviso = a.DataAvviso,
                    Importo = a.Importo ?? 0,
                    Note = a.Note,
                    Stato = a.Stato,
                    MetodoPagamento = a.MetodoPagamento,
                    PuoEliminare = puoEliminare,
                    PuoModificare = puoModificare,
                    NomePratica = db.Pratiche
                        .Where(p => p.ID_Pratiche == a.ID_Pratiche)
                        .Select(p => p.Titolo)
                        .FirstOrDefault() ?? "(N/D)",

                    // ✅ Nuovi campi
                    ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                    AliquotaIVA = a.AliquotaIVA,
                    ImportoIVA = a.ImportoIVA,
                    TotaleAvvisoParcella =
                        (a.Importo ?? 0) +
                        (a.ContributoIntegrativoImporto ?? 0) +
                        (a.ImportoIVA ?? 0)
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

            if (idPratica.HasValue)
            {
                var idProfessionista = db.Pratiche
                    .Where(p => p.ID_Pratiche == idPratica.Value)
                    .Select(p => p.ID_UtenteResponsabile)
                    .FirstOrDefault();

                ViewBag.IDProfessionista = idProfessionista;
            }

            ViewBag.IDPraticaCorrente = idPratica;

            return PartialView("~/Views/AvvisiParcella/_GestioneAvvisiParcellaList.cshtml", lista);
        }


        [HttpPost]
        public ActionResult CreaAvvisoParcella(AvvisoParcellaViewModel model)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                               db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Aggiungi == true);

            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per creare un avviso parcella." });

            try
            {
                DateTime now = DateTime.Now;

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                int? idProfessionista = pratica.ID_UtenteResponsabile;

                // 🔍 Percentuale contributo dalla professione
                decimal? percentuale = db.Professioni
                    .Where(p => p.ID_ProfessionistaRiferimento == idProfessionista)
                    .Select(p => p.PercentualeContributoIntegrativo)
                    .FirstOrDefault();

                decimal importoBase = model.Importo.GetValueOrDefault();
                decimal? contributo = percentuale.HasValue
                    ? (decimal?)Math.Round(importoBase * percentuale.Value / 100, 2)
                    : null;

                // 🔍 Calcola IVA
                decimal? aliquotaIVA = model.AliquotaIVA;
                decimal? importoIVA = aliquotaIVA.HasValue
                    ? (decimal?)Math.Round(importoBase * aliquotaIVA.Value / 100, 2)
                    : null;

                decimal? totaleAvviso = importoBase + (contributo ?? 0) + (importoIVA ?? 0);

                // ✅ Crea l’avviso
                var nuovo = new AvvisiParcella
                {
                    ID_Pratiche = model.ID_Pratiche,
                    DataAvviso = model.DataAvviso,
                    Importo = model.Importo,
                    Note = model.Note?.Trim(),
                    Stato = model.Stato?.Trim(),
                    MetodoPagamento = model.MetodoPagamento?.Trim(),
                    ID_UtenteCreatore = idUtenteCorrente,
                    ContributoIntegrativoPercentuale = percentuale,
                    ContributoIntegrativoImporto = contributo,
                    AliquotaIVA = aliquotaIVA,
                    ImportoIVA = importoIVA,
                    TotaleAvvisiParcella = totaleAvviso
                };

                db.AvvisiParcella.Add(nuovo);
                db.SaveChanges();

                // 🗂️ Archivio
                db.AvvisiParcella_a.Add(new AvvisiParcella_a
                {
                    ID_Archivio = nuovo.ID_AvvisoParcelle,
                    ID_Pratiche = nuovo.ID_Pratiche,
                    DataAvviso = nuovo.DataAvviso,
                    Importo = nuovo.Importo,
                    Note = nuovo.Note,
                    Stato = nuovo.Stato,
                    MetodoPagamento = nuovo.MetodoPagamento,
                    ID_UtenteCreatore = nuovo.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = nuovo.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = nuovo.ContributoIntegrativoImporto,
                    TotaleAvvisiParcella = nuovo.TotaleAvvisiParcella,
                    AliquotaIVA = nuovo.AliquotaIVA,
                    ImportoIVA = nuovo.ImportoIVA,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Avviso creato da utente ID {idUtenteCorrente} in data {now:g}"
                });

                // ✅ Registrazione nel Bilancio (stato ECONOMICO)
                if (idProfessionista.HasValue && totaleAvviso.HasValue)
                {
                    string descrizione = "";
                    decimal importoRicavo = 0;

                    switch (pratica.Tipologia)
                    {
                        case "Fisso":
                            descrizione = "Compenso fisso per pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value; // normalmente = pratica.Budget + contributo + IVA
                            break;

                        case "A ore":
                            if (pratica.TariffaOraria.HasValue && pratica.OreEffettive.HasValue)
                            {
                                importoRicavo = Math.Round(pratica.TariffaOraria.Value * pratica.OreEffettive.Value, 2);
                                descrizione = $"Compenso orario ({pratica.OreEffettive} ore × {pratica.TariffaOraria} €/h)";
                            }
                            else
                            {
                                descrizione = "Compenso orario - dati incompleti";
                                importoRicavo = totaleAvviso.Value; // fallback: usa l'importo dell’avviso
                            }
                            break;

                        case "Giudiziale":
                            descrizione = "Compenso giudiziale per pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value; // può essere acconto, saldo, ecc.
                            break;

                        default:
                            descrizione = "Ricavo pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value;
                            break;
                    }

                    db.BilancioProfessionista.Add(new BilancioProfessionista
                    {
                        ID_Professionista = idProfessionista.Value,
                        ID_Pratiche = pratica.ID_Pratiche,
                        Descrizione = descrizione,
                        DataRegistrazione = DateTime.Now,
                        DataInserimento = DateTime.Now,
                        Categoria = "Ricavo Parcella",
                        TipoVoce = "Ricavo",
                        Importo = importoRicavo,
                        Stato = "Economico",
                        Origine = "AvvisoParcella",
                        ID_UtenteInserimento = idUtenteCorrente
                    });
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Avviso parcella creato correttamente." });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"{e.PropertyName}: {e.ErrorMessage}");

                var fullMessage = string.Join("; ", errorMessages);

                return Json(new { success = false, message = "Errore validazione: " + fullMessage });
            }

        }



        [HttpPost]
        public ActionResult ModificaAvvisoParcella(AvvisoParcellaViewModel model)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                               db.Permessi.Any(p => p.ID_Utente == idUtenteCorrente && p.Modifica == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per modificare l’avviso parcella." });

            try
            {
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == model.ID_AvvisoParcelle);
                if (avviso == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." });

                // 🔁 Ricalcolo contributo integrativo
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                int? idProfessionista = pratica?.ID_UtenteResponsabile;

                decimal? percentuale = db.Professioni
                    .Where(p => p.ID_ProfessionistaRiferimento == idProfessionista)
                    .Select(p => p.PercentualeContributoIntegrativo)
                    .FirstOrDefault();

                decimal importo = model.Importo ?? 0;
                decimal? contributo = percentuale.HasValue
                    ? (decimal?)Math.Round(importo * percentuale.Value / 100, 2)
                    : null;

                // 🔢 Calcolo IVA
                decimal? aliquotaIVA = model.AliquotaIVA;
                decimal? importoIVA = aliquotaIVA.HasValue
                    ? (decimal?)Math.Round(importo * aliquotaIVA.Value / 100, 2)
                    : null;
                decimal? totaleAvviso = importo + (contributo ?? 0) + (importoIVA ?? 0);

                // 🔢 Versionamento
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

                // 🔍 Confronto
                CheckModifica("ID_Pratiche", avviso.ID_Pratiche, model.ID_Pratiche);
                CheckModifica("DataAvviso", avviso.DataAvviso, model.DataAvviso);
                CheckModifica("Importo", avviso.Importo, model.Importo);
                CheckModifica("Note", avviso.Note, model.Note);
                CheckModifica("Stato", avviso.Stato, model.Stato);
                CheckModifica("MetodoPagamento", avviso.MetodoPagamento, model.MetodoPagamento);
                CheckModifica("ContributoIntegrativoPercentuale", avviso.ContributoIntegrativoPercentuale, percentuale);
                CheckModifica("ContributoIntegrativoImporto", avviso.ContributoIntegrativoImporto, contributo);
                CheckModifica("AliquotaIVA", avviso.AliquotaIVA, aliquotaIVA);
                CheckModifica("ImportoIVA", avviso.ImportoIVA, importoIVA);
                CheckModifica("TotaleAvvisoParcella", avviso.TotaleAvvisiParcella, totaleAvviso);


                // ✏️ Aggiorna i campi
                avviso.ID_Pratiche = model.ID_Pratiche;
                avviso.DataAvviso = model.DataAvviso;
                avviso.Importo = model.Importo;
                avviso.Note = model.Note?.Trim();
                avviso.Stato = model.Stato?.Trim();
                avviso.MetodoPagamento = model.MetodoPagamento?.Trim();
                avviso.TotaleAvvisiParcella = totaleAvviso;
                avviso.ContributoIntegrativoPercentuale = percentuale;
                avviso.ContributoIntegrativoImporto = contributo;
                avviso.AliquotaIVA = aliquotaIVA;
                avviso.ImportoIVA = importoIVA;

                // 🗂️ Salva versione archivio
                db.AvvisiParcella_a.Add(new AvvisiParcella_a
                {
                    ID_Archivio = avviso.ID_AvvisoParcelle,
                    ID_Pratiche = avviso.ID_Pratiche,
                    DataAvviso = avviso.DataAvviso,
                    Importo = avviso.Importo,
                    Note = avviso.Note,
                    Stato = avviso.Stato,
                    MetodoPagamento = avviso.MetodoPagamento,
                    TotaleAvvisiParcella = avviso.TotaleAvvisiParcella,
                    ID_UtenteCreatore = avviso.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = avviso.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = avviso.ContributoIntegrativoImporto,
                    AliquotaIVA = avviso.AliquotaIVA,
                    ImportoIVA = avviso.ImportoIVA,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = modifiche.Any()
                        ? $"✏️ Modifica effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica salvata senza cambiamenti rilevanti"
                });
                // 💰 REGISTRAZIONE NEL BILANCIO (con logica per tipologia pratica)
                if (idProfessionista.HasValue && totaleAvviso.HasValue)
                {
                    string descrizione = "";
                    decimal importoRicavo = 0;

                    switch (pratica.Tipologia)
                    {
                        case "Fisso":
                            descrizione = "Compenso fisso per pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value;
                            break;

                        case "A ore":
                            if (pratica.TariffaOraria.HasValue && pratica.OreEffettive.HasValue)
                            {
                                importoRicavo = Math.Round(pratica.TariffaOraria.Value * pratica.OreEffettive.Value, 2);
                                descrizione = $"Compenso orario ({pratica.OreEffettive} ore × {pratica.TariffaOraria} €/h)";
                            }
                            else
                            {
                                importoRicavo = totaleAvviso.Value; // fallback
                                descrizione = "Compenso orario - dati incompleti";
                            }
                            break;

                        case "Giudiziale":
                            descrizione = "Compenso giudiziale per pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value;
                            break;

                        default:
                            descrizione = "Ricavo pratica #" + pratica.ID_Pratiche;
                            importoRicavo = totaleAvviso.Value;
                            break;
                    }

                    var voceEsistente = db.BilancioProfessionista.FirstOrDefault(b =>
                        b.ID_Pratiche == pratica.ID_Pratiche &&
                        b.Origine == "AvvisoParcella" &&
                        b.Categoria == "Ricavo Parcella");

                    if (voceEsistente != null)
                    {
                        voceEsistente.Importo = importoRicavo;
                        voceEsistente.DataRegistrazione = DateTime.Now;
                        voceEsistente.Descrizione = descrizione;
                        voceEsistente.Stato = "Economico";
                    }
                    else
                    {
                        db.BilancioProfessionista.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista.Value,
                            ID_Pratiche = pratica.ID_Pratiche,
                            DataRegistrazione = DateTime.Now,
                            Categoria = "Ricavo Parcella",
                            TipoVoce = "Ricavo",
                            Descrizione = descrizione,
                            Importo = importoRicavo,
                            Stato = "Economico",
                            Origine = "AvvisoParcella",
                            ID_UtenteInserimento = idUtenteCorrente,
                            DataInserimento = DateTime.Now
                        });
                    }
                }


                db.SaveChanges();

                return Json(new { success = true, message = "✅ Avviso parcella modificato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetAvvisoParcella(int id)
        {
            try
            {
                // ✅ Prima recupera l'avviso e la pratica associata con join manuale
                var result = (from avviso in db.AvvisiParcella
                              where avviso.ID_AvvisoParcelle == id
                              join pratiche in db.Pratiche on avviso.ID_Pratiche equals pratiche.ID_Pratiche
                              join u in db.Utenti on avviso.ID_UtenteCreatore equals u.ID_Utente into utenti
                              from utente in utenti.DefaultIfEmpty()
                              select new
                              {
                                  Avviso = avviso,
                                  Pratica = pratiche,
                                  NomeCreatore = utente.Nome + " " + utente.Cognome
                              }).FirstOrDefault();

                if (result == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." }, JsonRequestBehavior.AllowGet);

                var a = result.Avviso;
                var p = result.Pratica;

                // 🧾 Descrizione compenso
                string descrizioneCompenso = "";
                if (p.Tipologia == "Fisso")
                {
                    descrizioneCompenso = $"Compenso fisso: {a.Importo:C}";
                }
                else if (p.Tipologia == "A ore")
                {
                    if (p.TariffaOraria.HasValue && p.OreEffettive.HasValue)
                    {
                        decimal totale = p.TariffaOraria.Value * p.OreEffettive.Value;
                        descrizioneCompenso = $"Compenso orario: {p.OreEffettive}h × {p.TariffaOraria}€/h = {totale:C}";
                    }
                    else
                    {
                        descrizioneCompenso = "Compenso orario (dati incompleti)";
                    }
                }
                else if (p.Tipologia == "Giudiziale")
                {
                    descrizioneCompenso = "Compenso giudiziale (importo inserito manualmente)";
                }

                // 🎯 Costruisci il ViewModel
                var model = new AvvisoParcellaViewModel
                {
                    ID_AvvisoParcelle = a.ID_AvvisoParcelle,
                    ID_Pratiche = (int)a.ID_Pratiche,
                    DataAvviso = a.DataAvviso,
                    Importo = a.Importo,
                    Note = a.Note,
                    Stato = a.Stato,
                    MetodoPagamento = a.MetodoPagamento,
                    ID_UtenteCreatore = a.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = a.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = a.ContributoIntegrativoImporto,
                    TotaleAvvisoParcella = a.TotaleAvvisiParcella,
                    AliquotaIVA = a.AliquotaIVA,
                    ImportoIVA = a.ImportoIVA,

                    NomePratica = p.Titolo,
                    NomeUtenteCreatore = result.NomeCreatore,

                    // ➕ Ausiliari
                    TipologiaPratica = p.Tipologia,
                    TariffaOraria = p.TariffaOraria,
                    OreEffettive = p.OreEffettive,
                    DescrizioneCompenso = descrizioneCompenso
                };

                System.Diagnostics.Debug.WriteLine($"✅ Avviso #{id} - Tipologia: {p.Tipologia}, Descrizione: {descrizioneCompenso}");

                return Json(new { success = true, avviso = model }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetListaPratiche()
        {
            var pratiche = (from p in db.Pratiche
                            join o in db.OperatoriSinergia on p.ID_UtenteResponsabile equals o.ID_Cliente into joinOperatore
                            from o in joinOperatore.DefaultIfEmpty()
                            where p.Stato != "Eliminato"
                            orderby p.Titolo
                            select new
                            {
                                p.ID_Pratiche,
                                p.Titolo,
                                p.ID_UtenteResponsabile,
                                NomeProfessionista = o != null ? (o.Nome + " " + o.Cognome) : "⚠️ MANCANTE",
                                Tipologia = p.Tipologia,
                                PercentualeContributoIntegrativo = db.Professioni
                                    .Where(pr => pr.ID_ProfessionistaRiferimento == o.ID_Cliente)
                                    .Select(pr => pr.PercentualeContributoIntegrativo)
                                    .FirstOrDefault()
                            }).ToList();

            // 🔍 Debug: stampo tutte le pratiche con tipologia
            foreach (var p in pratiche)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Pratica ID: {p.ID_Pratiche}, Titolo: {p.Titolo}, Tipologia: {p.Tipologia}");
            }

            return Json(new { success = true, pratiche }, JsonRequestBehavior.AllowGet);
        }



        public JsonResult GetIDProfessionistaByPratica(int idPratica)
        {
            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);

            if (pratica == null)
                return Json(new { success = false, message = "Pratica non trovata." }, JsonRequestBehavior.AllowGet);

            var idUtente = pratica.ID_UtenteResponsabile;

            var operatore = db.OperatoriSinergia
                .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente && o.TipoCliente == "Professionista");

            if (operatore == null)
                return Json(new { success = false, message = "Professionista non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, idProfessionista = operatore.ID_Cliente }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaAvvisoParcella(int id)
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
                    return Json(new { success = false, message = "Non hai i permessi per eliminare l’avviso parcella." });

                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == id);
                if (avviso == null)
                    return Json(new { success = false, message = "Avviso parcella non trovato." });

                int? idPratica = avviso.ID_Pratiche;

                // 🔁 Numero versione precedente
                int ultimaVersione = db.AvvisiParcella_a
                    .Where(a => a.ID_Archivio == avviso.ID_AvvisoParcelle)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 💾 Archivia
                db.AvvisiParcella_a.Add(new AvvisiParcella_a
                {
                    ID_Archivio = avviso.ID_AvvisoParcelle,
                    ID_Pratiche = avviso.ID_Pratiche,
                    DataAvviso = avviso.DataAvviso,
                    Importo = avviso.Importo,
                    Note = avviso.Note,
                    Stato = "Eliminato",
                    MetodoPagamento = avviso.MetodoPagamento,
                    ID_UtenteCreatore = avviso.ID_UtenteCreatore,
                    ContributoIntegrativoPercentuale = avviso.ContributoIntegrativoPercentuale,
                    ContributoIntegrativoImporto = avviso.ContributoIntegrativoImporto,
                    AliquotaIVA = avviso.AliquotaIVA,
                    ImportoIVA = avviso.ImportoIVA,
                    TotaleAvvisiParcella = avviso.TotaleAvvisiParcella,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑️ Eliminazione avviso parcella effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                // 🧹 Cancella eventuali voci da Bilancio (origine = AvvisoParcella)
                var bilancioDaEliminare = db.BilancioProfessionista
                    .Where(b => b.ID_Pratiche == idPratica && b.Origine == "AvvisoParcella")
                    .ToList();

                foreach (var voce in bilancioDaEliminare)
                {
                    db.BilancioProfessionista.Remove(voce);
                }

                // ❌ Rimuovi avviso
                db.AvvisiParcella.Remove(avviso);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Avviso parcella eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }



        // questo mi serve in avvisi parcella per mettere i dati nel caso il pagamento avviene con il bonifico 
        [HttpGet]
        public JsonResult GetDatiBancari(int idCliente)
        {
            var dati = db.DatiBancari
                .Where(d =>
                    d.ID_Cliente == idCliente &&
                    d.Stato == "Attivo" &&
                    d.IBAN != null && d.IBAN != "" &&
                    d.NomeBanca != null
                )
                .OrderByDescending(d => d.DataInserimento)
                .FirstOrDefault();

            if (dati == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                NomeBanca = dati.NomeBanca,
                IBAN = dati.IBAN,
                IntestatarioConto = dati.Intestatario,
                BIC = dati.BIC_SWIFT,
                Note = dati.Note
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult SalvaDatiBancari(DatiBancari model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.IBAN) || string.IsNullOrWhiteSpace(model.NomeBanca))
                {
                    return Json(new { success = false, message = "IBAN e Nome Banca sono obbligatori." });
                }
                int idUtente = UserManager.GetIDUtenteCollegato();
                model.Stato = "Attivo";
                model.DataInserimento = DateTime.Now;
                model.ID_UtenteCreatore = idUtente;

                db.DatiBancari.Add(model);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Dati bancari salvati con successo." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }
        // fine dati bancari per avvisi parcella 


        public ActionResult StampaAvvisoParcella(int idAvviso)
        {
            var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvviso);
            Debug.WriteLine($"Avviso trovato: {avviso?.ID_AvvisoParcelle}, PraticaID: {avviso?.ID_Pratiche}");
            if (avviso == null)
                return HttpNotFound("Avviso non trovato.");

            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avviso.ID_Pratiche);
            Debug.WriteLine($"Pratica trovata: {pratica?.ID_Pratiche}, ClienteID: {pratica?.ID_Cliente}");
            if (pratica == null)
                return HttpNotFound("Pratica non trovata.");

            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
            Debug.WriteLine(cliente != null
                ? $"Cliente trovato: {cliente.ID_Cliente}, RagioneSociale: {cliente.RagioneSociale}, PIVA: {cliente.PIVA}"
                : "Cliente non trovato o Clienti.ID_Cliente mismatch");

            Citta cittaCliente = null;
            if (cliente?.ID_Citta != null)
            {
                cittaCliente = db.Citta.FirstOrDefault(c => c.ID_BPCitta == cliente.ID_Citta);
                Debug.WriteLine(cittaCliente != null
                    ? $"Città cliente: {cittaCliente.NameLocalita}, CAP: {cittaCliente.CAP}"
                    : "Città cliente non trovata");
            }
            else Debug.WriteLine("Cliente.ID_Citta è null");

            // 🔧 Qui correggi visibilità
            OperatoriSinergia operatore = null;
            if (cliente != null)
            {
                operatore = db.OperatoriSinergia.FirstOrDefault(o =>
                    o.ID_Cliente == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);

                Debug.WriteLine(operatore != null
                    ? $"Operatore trovato: {operatore.ID_Cliente}, PIVA: {operatore.PIVA}, Indirizzo: {operatore.Indirizzo}"
                    : "Operatore non trovato per questo cliente");
            }
            else
            {
                Debug.WriteLine("Cliente nullo: impossibile cercare l’operatore.");
            }

            Utenti utente = null;
            if (operatore?.ID_UtenteCollegato != null)
            {
                utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == operatore.ID_UtenteCollegato);
                Debug.WriteLine(utente != null
                    ? $"Utente professionista: {utente.Nome} {utente.Cognome}"
                    : "Utente collegato all'operatore non trovato");
            }
            else Debug.WriteLine("Operatore.ID_UtenteCollegato è null");

            Citta cittaProf = null;
            if (operatore?.ID_Citta != null)
            {
                cittaProf = db.Citta.FirstOrDefault(c => c.ID_BPCitta == operatore.ID_Citta);
                Debug.WriteLine(cittaProf != null
                    ? $"Città professionista: {cittaProf.NameLocalita}, CAP: {cittaProf.CAP}"
                    : "Città professionista non trovata");
            }
            else Debug.WriteLine("Operatore.ID_Citta è null");

            var model = new AvvisoParcellaPdfViewModel
            {
                DataAvviso = avviso.DataAvviso ?? DateTime.Today,
                Stato = avviso.Stato,
                MetodoPagamento = avviso.MetodoPagamento,
                Importo = avviso.Importo ?? 0,
                ContributoIntegrativoPercentuale = avviso.ContributoIntegrativoPercentuale ?? 0,
                ContributoIntegrativoImporto = avviso.ContributoIntegrativoImporto ?? 0,
                AliquotaIVA = avviso.AliquotaIVA ?? 0,
                ImportoIVA = avviso.ImportoIVA ?? 0,
                Note = avviso.Note,
                DescrizionePratica = pratica.Titolo ?? pratica.Note ?? "Servizio professionale",

                NomeProfessionista = utente?.Nome,
                CognomeProfessionista = utente?.Cognome,
                IndirizzoProfessionista = operatore?.Indirizzo,
                CittaProfessionista = cittaProf?.NameLocalita,
                CAPProfessionista = cittaProf?.CAP,
                PartitaIVAProfessionista = operatore?.PIVA,

                RagioneSocialeCliente = !string.IsNullOrWhiteSpace(cliente?.RagioneSociale) ? cliente.RagioneSociale : $"{cliente?.Nome} {cliente?.Cognome}".Trim(),
                IndirizzoCliente = cliente?.Indirizzo,
                CittaCliente = cittaCliente?.NameLocalita,
                CAPCliente = cittaCliente?.CAP,
                PartitaIVACliente = cliente?.PIVA
            };

            return new Rotativa.ViewAsPdf("~/Views/AvvisiParcella/PDF_AvvisoParcella.cshtml", model)
            {
                FileName = $"AvvisoParcella_{idAvviso}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(20, 10, 20, 10)
            };
        }

        [HttpGet]
        public ActionResult EsportaAvvisiParcellaCsv(DateTime? da, DateTime? a)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Range date (mese corrente di default)
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var avvisi = db.AvvisiParcella
                .Where(avv => avv.DataAvviso >= inizio && avv.DataAvviso <= fine)
                .OrderBy(avv => avv.DataAvviso)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID Avviso;Pratica;Data Avviso;Importo;Contributo Integrativo (%);IVA (%);Totale;Stato;Metodo Pagamento");

            foreach (var avv in avvisi)
            {
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == avv.ID_Pratiche);

                decimal importo = avv.Importo ?? 0;
                decimal ciPerc = avv.ContributoIntegrativoPercentuale ?? 0;
                decimal ivaPerc = avv.AliquotaIVA ?? 0;
                decimal totale = (avv.Importo ?? 0) + (avv.ContributoIntegrativoImporto ?? 0) + (avv.ImportoIVA ?? 0);

                sb.AppendLine($"{avv.ID_AvvisoParcelle};" +
                              $"{(pratica?.Titolo ?? "(N/D)")};" +
                              $"{avv.DataAvviso:dd/MM/yyyy};" +
                              $"{importo:N2};" +
                              $"{ciPerc:N0}%;" +
                              $"{ivaPerc:N0}%;" +
                              $"{totale:N2};" +
                              $"{avv.Stato};" +
                              $"{avv.MetodoPagamento}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"AvvisiParcella_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaAvvisiParcellaPdf(DateTime? da, DateTime? a)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var lista = db.AvvisiParcella
                .Where(avv => avv.DataAvviso >= inizio && avv.DataAvviso <= fine)
                .OrderBy(avv => avv.DataAvviso)
                .ToList()
                .Select(avv => new AvvisoParcellaViewModel
                {
                    ID_AvvisoParcelle = avv.ID_AvvisoParcelle,
                    ID_Pratiche = avv.ID_Pratiche ?? 0,
                    DataAvviso = avv.DataAvviso,
                    Importo = avv.Importo ?? 0,
                    ContributoIntegrativoImporto = avv.ContributoIntegrativoImporto,
                    AliquotaIVA = avv.AliquotaIVA,
                    ImportoIVA = avv.ImportoIVA,
                    Stato = avv.Stato,
                    MetodoPagamento = avv.MetodoPagamento,
                    NomePratica = db.Pratiche
                        .Where(p => p.ID_Pratiche == avv.ID_Pratiche)
                        .Select(p => p.Titolo)
                        .FirstOrDefault() ?? "(N/D)",
                    TotaleAvvisoParcella =
                        (avv.Importo ?? 0) +
                        (avv.ContributoIntegrativoImporto ?? 0) +
                        (avv.ImportoIVA ?? 0)
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/AvvisiParcella/PDF_AvvisoParcella.cshtml", lista)
            {
                FileName = $"AvvisiParcella_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }

        #endregion

        #region REGISTRAZIONE INCASSI 

        public ActionResult GestioneIncassi()
        {
            return View("~/Views/Incassi/GestioneIncassi.cshtml");
        }

        public ActionResult GestioneIncassiList(int? idPratica = null)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // Passo anche il tipo utente alla view
            ViewBag.TipoUtente = utenteCorrente.TipoUtente;

            // 🔍 Query base sugli incassi
            IQueryable<Incassi> query = db.Incassi;
            if (idPratica.HasValue)
                query = query.Where(i => i.ID_Pratiche == idPratica.Value);

            // 🔐 Gestione permessi
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

            // 🔄 Costruzione lista ViewModel incassi
            var lista = query
                .OrderByDescending(i => i.DataIncasso)
                .ToList()
                .Select(i =>
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == i.ID_Pratiche);

                    decimal utileNetto = db.BilancioProfessionista
                         .Where(b =>
                             b.ID_Pratiche == i.ID_Pratiche &&
                             b.Categoria == "Utile netto da incasso")
                         .OrderByDescending(b => b.DataRegistrazione)
                         .Select(b => (decimal?)b.Importo)
                         .FirstOrDefault() ?? 0;

                    return new IncassoViewModel
                    {
                        ID_Incasso = i.ID_Incasso,
                        ID_Pratiche = i.ID_Pratiche ?? 0,
                        DataIncasso = i.DataIncasso,
                        Importo = i.Importo,
                        MetodoPagamento = i.ModalitaPagamento,
                        NomePratica = pratica?.Titolo ?? "(N/D)",
                        UtileNetto = utileNetto,
                        VersaInPlafond = i.VersaInPlafond,
                        PuoEliminare = puoEliminare,
                        PuoModificare = puoModificare // 🔹 nuovo campo nel ViewModel
                    };

                })
                .ToList();

            // 🔐 Passaggio permessi alla View
            ViewBag.PuoAggiungere = puoAggiungere;
            ViewBag.PuoModificare = puoModificare;
            ViewBag.PuoEliminare = puoEliminare;
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

            // ⬇️ Carica le pratiche per la modale incasso
            ViewBag.Pratiche = db.Pratiche
                 .Where(p => p.Stato != "Eliminato")
                 .ToList()
                 .Select(p => new SelectListItem
                 {
                     Value = p.ID_Pratiche.ToString(),
                     Text = p.ID_Pratiche + " - " + p.Titolo
                 }).ToList();

            return PartialView("~/Views/Incassi/_GestioneIncassiList.cshtml", lista);
        }

        [HttpPost]
        public ActionResult CreaIncasso(IncassoViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compilare correttamente tutti i campi richiesti." });

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

                // 🔍 Recupero pratica e verifica importo
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                decimal budget = pratica.Budget;
                decimal incassatoTotale = db.Incassi
                    .Where(i => i.ID_Pratiche == model.ID_Pratiche)
                    .Select(i => i.Importo)
                    .DefaultIfEmpty(0)
                    .Sum();

                // 🔒 Blocco: non superare il budget
                if ((incassatoTotale + model.Importo) > budget)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"⚠️ L'importo supera il budget della pratica.\nTotale incassato: {incassatoTotale:C}\nImporto nuovo: {model.Importo:C}\nBudget massimo: {budget:C}"
                    });
                }

                var incasso = new Incassi
                {
                    ID_Pratiche = model.ID_Pratiche,
                    DataIncasso = (DateTime)model.DataIncasso,
                    Importo = model.Importo,
                    ModalitaPagamento = model.MetodoPagamento?.Trim(),
                    ID_UtenteCreatore = idUtenteCorrente,
                    VersaInPlafond = model.VersaInPlafond == true
                };

                db.Incassi.Add(incasso);
                db.SaveChanges(); // Recupero ID_Incasso

                db.Incassi_a.Add(new Incassi_a
                {
                    ID_Archivio = incasso.ID_Incasso,
                    ID_Pratiche = incasso.ID_Pratiche,
                    DataIncasso = incasso.DataIncasso,
                    Importo = incasso.Importo,
                    ModalitaPagamento = incasso.ModalitaPagamento,
                    ID_UtenteCreatore = incasso.ID_UtenteCreatore,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Inserito incasso da {incasso.Importo:C} per pratica ID = {incasso.ID_Pratiche} da utente ID = {idUtenteCorrente} in data {now:g}"
                });

                UtileHelper.EseguiRipartizioneDaIncasso(incasso.ID_Pratiche.Value, incasso.Importo);

                // ✅ Versa in plafond, se selezionato
                if (incasso.VersaInPlafond == true)
                {
                    var utile = db.BilancioProfessionista
                        .Where(b => b.ID_Pratiche == incasso.ID_Pratiche &&
                                    b.Categoria == "Utile netto da incasso")
                        .OrderByDescending(b => b.DataRegistrazione)
                        .FirstOrDefault();

                    if (utile != null && utile.Importo > 0)
                    {
                        db.PlafondUtente.Add(new PlafondUtente
                        {
                            ID_Utente = pratica.ID_UtenteResponsabile,
                            ImportoTotale = utile.Importo,
                            TipoPlafond = "Incasso",
                            DataInizio = now.Date,
                            DataFine = null,
                            ID_Pratiche = incasso.ID_Pratiche,
                            ID_UtenteCreatore = idUtenteCorrente,
                            ID_UtenteUltimaModifica = null,
                            DataUltimaModifica = null,
                            ID_Incasso = incasso.ID_Incasso,
                            Importo = utile.Importo,
                            DataVersamento = now,
                            ID_UtenteInserimento = idUtenteCorrente,
                            DataInserimento = now,
                            Note = $"Versamento da incasso ID {incasso.ID_Incasso} - pratica {pratica.ID_Pratiche}"
                        });
                    }
                }

                // ✅ Imposta lo stato dell'avviso parcella a "Pagato" se collegato
                var avviso = db.AvvisiParcella.FirstOrDefault(a => a.ID_Pratiche == incasso.ID_Pratiche);
                if (avviso != null)
                {
                    avviso.Stato = "Pagato";
                    avviso.DataModifica = DateTime.Now;
                    avviso.ID_UtenteModifica = idUtenteCorrente;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Incasso registrato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il salvataggio: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult ModificaIncasso(IncassoViewModel model)
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
                return Json(new { success = false, message = "Non hai i permessi per modificare l’incasso." });

            try
            {
                var incasso = db.Incassi.FirstOrDefault(i => i.ID_Incasso == model.ID_Incasso);
                if (incasso == null)
                    return Json(new { success = false, message = "Incasso non trovato." });

                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == model.ID_Pratiche);
                if (pratica == null)
                    return Json(new { success = false, message = "Pratica non trovata." });

                decimal budget = pratica.Budget;

                // 🔍 Calcola totale incassi escluso quello corrente
                decimal incassatoTotale = db.Incassi
                    .Where(i => i.ID_Pratiche == model.ID_Pratiche && i.ID_Incasso != model.ID_Incasso)
                    .Select(i => i.Importo)
                    .DefaultIfEmpty(0)
                    .Sum();

                // 🔒 Blocco se la modifica porterebbe a superare il budget
                if ((incassatoTotale + model.Importo) > budget)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"⚠️ L'importo aggiornato supera il budget della pratica.\nTotale altri incassi: {incassatoTotale:C}\nNuovo importo: {model.Importo:C}\nBudget massimo: {budget:C}"
                    });
                }

                int ultimaVersione = db.Incassi_a
                    .Where(i => i.ID_Archivio == incasso.ID_Incasso)
                    .OrderByDescending(i => i.NumeroVersione)
                    .Select(i => i.NumeroVersione)
                    .FirstOrDefault();

                List<string> modifiche = new List<string>();
                void Check(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                Check("ID_Pratiche", incasso.ID_Pratiche, model.ID_Pratiche);
                Check("DataIncasso", incasso.DataIncasso, model.DataIncasso);
                Check("Importo", incasso.Importo, model.Importo);
                Check("MetodoPagamento", incasso.ModalitaPagamento, model.MetodoPagamento);
                Check("VersaInPlafond", incasso.VersaInPlafond, model.VersaInPlafond);

                // 🔁 Applica modifiche
                incasso.ID_Pratiche = model.ID_Pratiche;
                incasso.DataIncasso = (DateTime)model.DataIncasso;
                incasso.Importo = model.Importo;
                incasso.ModalitaPagamento = model.MetodoPagamento?.Trim();
                incasso.VersaInPlafond = model.VersaInPlafond == true;

                db.Incassi_a.Add(new Incassi_a
                {
                    ID_Archivio = incasso.ID_Incasso,
                    ID_Pratiche = incasso.ID_Pratiche,
                    DataIncasso = incasso.DataIncasso,
                    Importo = incasso.Importo,
                    ModalitaPagamento = incasso.ModalitaPagamento,
                    ID_UtenteCreatore = incasso.ID_UtenteCreatore,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = modifiche.Any()
                        ? $"✏️ Modifica effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica salvata senza cambiamenti rilevanti"
                });

                // 🔁 Elimina voci esistenti associate all’incasso
                db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
                    .Where(b => b.ID_Incasso == incasso.ID_Incasso));

                // 🔁 Esegui ripartizione aggiornata
                UtileHelper.EseguiRipartizioneDaIncasso(incasso.ID_Pratiche.Value, incasso.Importo);

                // ✅ Versa in plafond se selezionato
                if (incasso.VersaInPlafond == true)
                {
                    var utile = db.BilancioProfessionista
                        .Where(b => b.ID_Pratiche == incasso.ID_Pratiche &&
                                    b.Categoria == "Utile netto da incasso")
                        .OrderByDescending(b => b.DataRegistrazione)
                        .FirstOrDefault();

                    if (utile != null && utile.Importo > 0)
                    {
                        db.PlafondUtente.Add(new PlafondUtente
                        {
                            ID_Utente = pratica.ID_UtenteResponsabile,
                            ImportoTotale = utile.Importo,
                            ID_Pratiche = incasso.ID_Pratiche,
                            TipoPlafond = "Incasso",
                            DataInizio = DateTime.Now.Date,
                            DataFine = null,
                            ID_UtenteCreatore = idUtenteCorrente,
                            ID_UtenteUltimaModifica = null,
                            DataUltimaModifica = null,
                            ID_Incasso = incasso.ID_Incasso,
                            Importo = utile.Importo,
                            DataVersamento = DateTime.Now,
                            ID_UtenteInserimento = idUtenteCorrente,
                            DataInserimento = DateTime.Now,
                            Note = $"Versamento da incasso ID {incasso.ID_Incasso} - pratica {pratica.ID_Pratiche}"
                        });
                    }
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Incasso modificato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }





        [HttpGet]
        public ActionResult GetIncasso(int id)
        {
            try
            {
                var incasso = (from i in db.Incassi
                               join p in db.Pratiche on i.ID_Pratiche equals p.ID_Pratiche into praticaJoin
                               from p in praticaJoin.DefaultIfEmpty()

                               join a in db.AvvisiParcella on i.ID_AvvisoParcella equals a.ID_AvvisoParcelle into avvisoJoin
                               from a in avvisoJoin.DefaultIfEmpty()

                               where i.ID_Incasso == id
                               select new IncassoViewModel
                               {
                                   ID_Incasso = i.ID_Incasso,
                                   ID_Pratiche = i.ID_Pratiche ?? 0,
                                   ID_AvvisoParcella = i.ID_AvvisoParcella ?? 0,
                                   DataIncasso = i.DataIncasso, // ✅ DateTime?

                                   Importo = i.Importo,
                                   MetodoPagamento = i.ModalitaPagamento,
                                   ID_UtenteCreatore = i.ID_UtenteCreatore,
                                   VersaInPlafond = i.VersaInPlafond == true,

                                   // Dati Pratica
                                   NomePratica = p != null ? p.Titolo : "(Pratica sconosciuta)",
                                   TotalePratica = p != null ? p.Budget : 0,

                                   // Dati Avviso Parcella
                                   ImportoAvviso = a != null ? a.Importo : 0,
                                   ImportoIVA = a != null ? a.ImportoIVA : 0,
                                   AliquotaIVA = a != null ? a.AliquotaIVA : 0
                               }).FirstOrDefault();

                if (incasso == null)
                    return Json(new { success = false, message = "Incasso non trovato." }, JsonRequestBehavior.AllowGet);

                // ✅ TEST OUTPUT SU CONSOLE
                System.Diagnostics.Debug.WriteLine("🟡 DataIncasso restituita: " + (incasso.DataIncasso?.ToString("yyyy-MM-dd") ?? "NULL"));

                return Json(new { success = true, incasso }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante il caricamento: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }



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

                // 🔢 Numero versione precedente
                int ultimaVersione = db.Incassi_a
                    .Where(i => i.ID_Archivio == incasso.ID_Incasso)
                    .OrderByDescending(i => i.NumeroVersione)
                    .Select(i => (int?)i.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 🗂 Archivia la versione prima dell'eliminazione
                db.Incassi_a.Add(new Incassi_a
                {
                    ID_Archivio = incasso.ID_Incasso,
                    ID_Pratiche = incasso.ID_Pratiche,
                    DataIncasso = incasso.DataIncasso,
                    Importo = incasso.Importo,
                    ModalitaPagamento = incasso.ModalitaPagamento,
                    ID_UtenteCreatore = incasso.ID_UtenteCreatore,
                    VersaInPlafond = incasso.VersaInPlafond ?? false,
                    DataArchiviazione = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑️ Eliminazione incasso effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:dd/MM/yyyy HH:mm}"
                });

                // 🧹 Rimozione voci collegate

                // 🔁 Bilancio
                db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
                    .Where(b => b.ID_Incasso == incasso.ID_Incasso));

                // 🔁 PlafondUtente (se presente)
                db.PlafondUtente.RemoveRange(db.PlafondUtente
                    .Where(p => p.ID_Incasso == incasso.ID_Incasso));

                // 🔁 CompensiPratica (solo quelli derivati da incasso)
                db.CompensiPratica.RemoveRange(db.CompensiPratica
                    .Where(c => c.ID_Pratiche == incasso.ID_Pratiche &&
                                c.Tipo == "Incasso" &&
                                c.Importo == incasso.Importo));

                // ❌ Rimozione incasso
                db.Incassi.Remove(incasso);

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Incasso eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult RiepilogoTrattenuteESinergia(int idPratica)
        {
            using (var db = new SinergiaDB())
            {
                DateTime oggi = DateTime.Today;
                DateTime primoDelMese = new DateTime(oggi.Year, oggi.Month, 1);
                DateTime primoDelMeseSuccessivo = primoDelMese.AddMonths(1);

                var lista = (
                    from b in db.BilancioProfessionista
                    join p in db.Pratiche on b.ID_Pratiche equals p.ID_Pratiche into praticheJoin
                    from p in praticheJoin.DefaultIfEmpty()
                    join os in db.OperatoriSinergia on b.ID_Professionista equals os.ID_Cliente into osJoin
                    from os in osJoin.DefaultIfEmpty()
                    join u in db.Utenti on os.ID_UtenteCollegato equals u.ID_Utente into utentiJoin
                    from u in utentiJoin.DefaultIfEmpty()
                    where
                        b.ID_Pratiche == idPratica && // ✅ FILTRO per la pratica selezionata
                        (
                            b.Categoria == "Trattenuta Sinergia" ||
                            b.Categoria == "Trattenuta Sinergia Personalizzata"
                        // Se hai rimosso "Costo Resident", puoi togliere anche qui
                        ) &&
                        DbFunctions.TruncateTime(b.DataRegistrazione) >= primoDelMese &&
                        DbFunctions.TruncateTime(b.DataRegistrazione) < primoDelMeseSuccessivo &&
                        (
                            p == null ||
                            (p.Stato == "Contrattualizzazione" || p.Stato == "Lavorazione" || p.Stato == "Concluse")
                        )
                    select new RicavoSinergiaViewModel
                    {
                        ID_Pratiche = b.ID_Pratiche,
                        ID_Professionista = b.ID_Professionista,
                        Titolo = p != null ? p.Titolo : "N.D.",
                        NomeProfessionista = u != null ? u.Nome + " " + u.Cognome : "N.D.",
                        Categoria = b.Categoria,
                        Importo = b.Importo,
                        DataRegistrazione = b.DataRegistrazione
                    }).ToList();

                return PartialView("~/Views/Incassi/_RiepilogoRicaviSinergia.cshtml", lista);
            }
        }


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

        [HttpGet]
        public ActionResult EsportaIncassiCsv(DateTime? da, DateTime? a)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Periodo (mese corrente se non specificato)
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var incassi = db.Incassi
                .Where(i => i.DataIncasso >= inizio && i.DataIncasso <= fine)
                .OrderBy(i => i.DataIncasso)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID Incasso;Pratica;Data Incasso;Importo;Metodo Pagamento;Versa in Plafond");

            foreach (var i in incassi)
            {
                var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == i.ID_Pratiche);
                sb.AppendLine($"{i.ID_Incasso};" +
                              $"{(pratica?.Titolo ?? "(N/D)")};" +
                              $"{i.DataIncasso:dd/MM/yyyy};" +
                              $"{i.Importo:N2};" +
                              $"{i.ModalitaPagamento};" +
                              $"{(i.VersaInPlafond == true ? "SI" : "NO")}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Incassi_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.csv");
        }



        [HttpGet]
        public ActionResult EsportaIncassiPdf(DateTime? da, DateTime? a)
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            // 📅 Periodo
            DateTime inizio = da ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime fine = a ?? DateTime.Today.AddDays(1).AddTicks(-1);

            var lista = db.Incassi
                .Where(i => i.DataIncasso >= inizio && i.DataIncasso <= fine)
                .OrderBy(i => i.DataIncasso)
                .ToList()
                .Select(i => new IncassoViewModel
                {
                    ID_Incasso = i.ID_Incasso,
                    ID_Pratiche = i.ID_Pratiche ?? 0,
                    DataIncasso = i.DataIncasso,
                    Importo = i.Importo,
                    MetodoPagamento = i.ModalitaPagamento,
                    NomePratica = db.Pratiche
                                    .Where(p => p.ID_Pratiche == i.ID_Pratiche)
                                    .Select(p => p.Titolo)
                                    .FirstOrDefault() ?? "(N/D)",
                    VersaInPlafond = i.VersaInPlafond
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Incassi/ReportIncassiPdf.cshtml", lista)
            {
                FileName = $"Incassi_{inizio:yyyyMMdd}_{fine:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait
            };
        }



        [HttpGet]
        public ActionResult GetDatiAvvisoParcella(int idPratica)
        {
            var avviso = (from a in db.AvvisiParcella
                          join p in db.Pratiche on a.ID_Pratiche equals p.ID_Pratiche
                          where a.ID_Pratiche == idPratica && a.Stato != "Eliminato"
                          orderby a.DataAvviso descending
                          select new
                          {
                              DescrizioneAvviso = "Avviso parcella #" + a.ID_AvvisoParcelle,
                              ImportoSenzaIVA = (decimal?)a.Importo,
                              ImportoConIVA = (decimal?)a.TotaleAvvisiParcella,
                              MetodoPagamento = a.MetodoPagamento, // ✅ aggiunto
                              NomePratica = p.Titolo,
                              TotalePratica = p.Budget
                          }).FirstOrDefault();

            if (avviso == null)
            {
                return Json(new
                {
                    success = true,
                    avviso = "Nessun avviso",
                    importoSenzaIVA = 0m,
                    importoConIVA = 0m,
                    metodoPagamento = "",
                    nomePratica = "(Nessuna pratica trovata)",
                    totalePratica = 0m
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                avviso = avviso.DescrizioneAvviso,
                importoSenzaIVA = avviso.ImportoSenzaIVA,
                importoConIVA = avviso.ImportoConIVA,
                metodoPagamento = avviso.MetodoPagamento,
                nomePratica = avviso.NomePratica,
                totalePratica = avviso.TotalePratica
            }, JsonRequestBehavior.AllowGet);
        }



        #endregion

    }
}
