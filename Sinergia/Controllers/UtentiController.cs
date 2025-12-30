using Sinergia.Model;
using Sinergia.ActionFilters;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using Sinergia.App_Helpers;
using Sinergia.Models;
using System.Diagnostics;
using System.EnterpriseServices;
using System.Data.Entity.Validation;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Globalization;
using System.Net;
using System.Text;

namespace SinergiaMvc.Controllers
{
    [PermissionsActionFilter]
    public class UtentiController : Controller
    {
        private SinergiaDB db = new SinergiaDB();

        // Vista principale di gestione utenti
        #region GESTIONE UTENTI
        public ActionResult GestioneUtenti()
        {
            return View("GestioneUtenti");
        }

        public ActionResult GestioneUtentiList()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            IQueryable<Utenti> utentiQuery = db.Utenti.Where(u => u.Stato != "Eliminato");

            if (utenteCorrente != null && utenteCorrente.TipoUtente == "Professionista")
            {
                var idProfessionista = utenteCorrente.ID_Utente;

                // Utenti creati da lui
                var idsCreati = db.Utenti
                    .Where(u => u.ID_UtenteCreatore == idProfessionista)
                    .Select(u => u.ID_Utente);

                // Utenti assegnati a lui tramite relazione (collaboratori)
                var idsAssegnati = db.RelazioneUtenti
                    .Where(r => r.ID_Utente == idProfessionista && r.TipoRelazione == "Collaboratore")
                    .Select(r => r.ID_Utente);

                // Mostra sé stesso, utenti creati da lui e collaboratori assegnati
                utentiQuery = utentiQuery.Where(u =>
                    u.ID_Utente == idProfessionista ||
                    idsCreati.Contains(u.ID_Utente) ||
                    idsAssegnati.Contains(u.ID_Utente));
            }

            var utenti = utentiQuery
                .Select(u => new UtenteViewModel
                {
                    ID_Utente = u.ID_Utente,
                    TipoUtente = u.TipoUtente,
                    Nome = u.Nome,
                    Cognome = u.Cognome,
                    CodiceFiscale = u.CodiceFiscale,
                    PIVA = u.PIVA,
                    CodiceUnivoco = u.CodiceUnivoco,
                    Telefono = u.Telefono,
                    Cellulare1 = u.Cellulare1,
                    Cellulare2 = u.Cellulare2,
                    MAIL1 = u.MAIL1,
                    MAIL2 = u.MAIL2,
                    Stato = u.Stato,
                    DescrizioneAttivita = u.DescrizioneAttivita,
                    Note = u.Note,
                    Indirizzo = u.Indirizzo,
                    DataCreazione = u.DataCreazione,
                    UltimaModifica = u.UltimaModifica,
                    ID_UtenteCreatore = u.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = u.ID_UtenteUltimaModifica,
                    Ruolo = u.Ruolo
                })
                .ToList();

            var permessiUtente = new PermessiViewModel
            {
                ID_Utente = utenteCorrente?.ID_Utente ?? 0,
                NomeUtente = utenteCorrente != null ? $"{utenteCorrente.Nome} {utenteCorrente.Cognome}" : "",
                Permessi = new List<PermessoSingoloViewModel>()
            };

            bool puoAggiungere = false;

            if (utenteCorrente != null)
            {
                if (utenteCorrente.TipoUtente == "Admin")
                {
                    permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                    {
                        Aggiungi = true,
                        Modifica = true,
                        Elimina = true
                    });
                    puoAggiungere = true;

                    foreach (var ut in utenti)
                    {
                        ut.PuoModificare = true;
                        ut.PuoEliminare = true;
                    }
                    // 👑 Flag per la view
                    ViewBag.UtenteCorrente = "Admin";
                }
                else if (utenteCorrente.TipoUtente == "Professionista")
                {
                    var permessiDb = db.Permessi
                        .Where(p => p.ID_Utente == utenteCorrente.ID_Utente)
                        .ToList();

                    bool puoModificare = permessiDb.Any(p => p.Modifica ?? false);
                    bool puoEliminare = permessiDb.Any(p => p.Elimina ?? false);
                    puoAggiungere = permessiDb.Any(p => p.Aggiungi ?? false);

                    foreach (var ut in utenti)
                    {
                        ut.PuoModificare = puoModificare;
                        ut.PuoEliminare = puoEliminare;
                    }
                }
                else
                {
                    foreach (var ut in utenti)
                    {
                        ut.PuoModificare = false;
                        ut.PuoEliminare = false;
                    }
                }
            }

            ViewBag.Permessi = permessiUtente;
            ViewBag.PuoAggiungere = puoAggiungere;

            return PartialView("_GestioneUtentiList", utenti);
        }


        [HttpPost]
        public ActionResult Crea()
        {
            int utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            var utenteLoggato = db.Utenti.Find(utenteId);
            if (utenteLoggato == null)
                return Json(new { success = false, message = "Utente non trovato." });

            if (utenteLoggato.TipoUtente != "Admin" && utenteLoggato.TipoUtente != "Professionista")
                return Json(new { success = false, message = "Non hai i permessi per creare utenti." });

            string tipoDaCreare = Request.Form["TipoUtente"];

            // 🔒 REGOLE PERMESSI CREAZIONE
            if (utenteLoggato.TipoUtente == "Admin")
            {
                // Admin → nessun vincolo
            }
            else if (utenteLoggato.TipoUtente == "Professionista")
            {
                if (tipoDaCreare != "Collaboratore")
                {
                    return Json(new { success = false, message = "❌ Un Professionista può creare solo Collaboratori." });
                }
            }
            else if (utenteLoggato.TipoUtente == "Collaboratore")
            {
                return Json(new { success = false, message = "❌ Un Collaboratore non può creare utenti." });
            }

            try
            {
                var model = new Utenti
                {
                    Nome = Request.Form["Nome"],
                    Cognome = Request.Form["Cognome"],
                    Cellulare1 = Request.Form["Cellulare1"],
                    Cellulare2 = Request.Form["Cellulare2"],
                    CodiceFiscale = Request.Form["CodiceFiscale"],
                    PIVA = Request.Form["PIVA"],
                    CodiceUnivoco = null,
                    Telefono = Request.Form["Telefono"],
                    MAIL1 = Request.Form["MAIL1"],
                    MAIL2 = Request.Form["MAIL2"],
                    DescrizioneAttivita = Request.Form["DescrizioneAttivita"],
                    Note = Request.Form["Note"],
                    TipoUtente = Request.Form["TipoUtente"],
                    Ruolo = Request.Form["Ruolo"],
                    ID_CittaResidenza = string.IsNullOrEmpty(Request.Form["ID_CittaResidenza"]) ? (int?)null : int.Parse(Request.Form["ID_CittaResidenza"]),
                    ID_Nazione = string.IsNullOrEmpty(Request.Form["ID_Nazione"]) ? (int?)null : int.Parse(Request.Form["ID_Nazione"]),
                    Indirizzo = Request.Form["Indirizzo"],
                    Stato = Request.Form["Stato"],
                    DataCreazione = DateTime.Now,
                    ID_UtenteCreatore = utenteId
                };

                // Gestione password
                if (model.TipoUtente == "Admin")
                {
                    model.PasswordHash = "admin";
                    model.Salt = "";
                    model.PasswordTemporanea = null;
                }
                else
                {
                    var codiceTemp = new Random().Next(1000, 9999).ToString();
                    model.PasswordTemporanea = codiceTemp;
                    model.PasswordHash = "TEMP";
                    model.Salt = null;
                }

                model.NomeAccount = $"{model.Nome.Trim().ToLower()[0]}.{model.Cognome.Trim().ToLower().Replace(" ", "")}";

                // ✅ Gestione documenti multipli
                var files = Request.Files;
                var documentiUtente = new List<byte[]>();

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0 && file.FileName != null && files.GetKey(i) == "DOCUMENTO")
                    {
                        using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                        {
                            var fileBytes = binaryReader.ReadBytes(file.ContentLength);
                            documentiUtente.Add(fileBytes);
                        }
                    }
                }

                // Se ne prendi uno solo:
                if (documentiUtente.Any())
                    model.DOCUMENTO = documentiUtente.First();
                // ✅ Gestione FOTO PROFILO con rilevamento volto e resize automatico
                var foto = Request.Files["FotoProfilo"];
                if (foto != null && foto.ContentLength > 0)
                {
                    try
                    {
                        string pathRelativo = FotoProfiloHelper.ElaboraEFaiUpload(foto, "FotoProfilo");
                        model.FotoProfiloPath = pathRelativo;
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "❌ Errore nella foto profilo: " + ex.Message });
                    }
                }

                db.Utenti.Add(model);
                db.SaveChanges();

                // ✅ Inserimento archivio (versione 1)
                var utenteArch = new Utenti_a
                {
                    ID_UtenteOriginale = model.ID_Utente,
                    Nome = model.Nome,
                    Cognome = model.Cognome,
                    Cellulare1 = model.Cellulare1,
                    Cellulare2 = model.Cellulare2,
                    CodiceFiscale = model.CodiceFiscale,
                    PIVA = model.PIVA,
                    CodiceUnivoco = model.CodiceUnivoco,
                    Telefono = model.Telefono,
                    MAIL1 = model.MAIL1,
                    MAIL2 = model.MAIL2,
                    DescrizioneAttivita = model.DescrizioneAttivita,
                    Note = model.Note,
                    TipoUtente = model.TipoUtente,
                    Ruolo = model.Ruolo,
                    ID_CittaResidenza = model.ID_CittaResidenza,
                    ID_Nazione = model.ID_Nazione,
                    Indirizzo = model.Indirizzo,
                    Stato = model.Stato,
                    DataCreazione = model.DataCreazione,
                    ID_UtenteCreatore = model.ID_UtenteCreatore,
                    FotoProfiloPath = model.FotoProfiloPath,
                    DOCUMENTO = model.DOCUMENTO,
                    PasswordHash = model.PasswordHash,
                    Salt = model.Salt,
                    PasswordTemporanea = model.PasswordTemporanea,
                    NomeAccount = model.NomeAccount,

                    // Archivio
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = utenteId,
                    ModificheTestuali = "Inserimento iniziale"
                };

                db.Utenti_a.Add(utenteArch);
                db.SaveChanges();

                return Json(new { success = true, message = "Utente creato con successo!" });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"Campo: {e.PropertyName} → Errore: {e.ErrorMessage}");

                string fullError = string.Join(" | ", errorMessages);
                return Json(new { success = false, message = "Errore nella creazione: " + fullError });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }




        [HttpPost]
        public ActionResult Modifica([Bind(Exclude = "DOCUMENTO")] UtenteViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dati non validi." });

            if (model.ID_Utente == 0)
                return Json(new { success = false, message = "ID utente non valido." });

            try
            {
                var originale = db.Utenti.FirstOrDefault(u => u.ID_Utente == model.ID_Utente);
                if (originale == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                int idUtenteModificatore = UserManager.GetIDUtenteCollegato();
                var utenteLoggato = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteModificatore);
                if (utenteLoggato == null)
                    return Json(new { success = false, message = "Utente loggato non trovato." });

                // 🔒 REGOLE PERMESSI MODIFICA
                if (utenteLoggato.TipoUtente == "Collaboratore")
                {
                    return Json(new { success = false, message = "❌ Un Collaboratore non può modificare utenti." });
                }
                else if (utenteLoggato.TipoUtente == "Professionista")
                {
                    // Il professionista può modificare solo collaboratori
                    if (originale.TipoUtente != "Collaboratore")
                        return Json(new { success = false, message = "❌ Un Professionista può modificare solo Collaboratori." });

                    // E non può promuovere un collaboratore a Admin o Professionista
                    if (model.TipoUtente != "Collaboratore")
                        return Json(new { success = false, message = "❌ Non puoi modificare il tipo utente di un Collaboratore." });
                }
                // Admin non ha limiti

                var modifiche = new List<string>();
                void Confronta(string nomeCampo, object valO, object valN)
                {
                    if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                        modifiche.Add($"{nomeCampo}: '{valO}' → '{valN}'");
                }

                // 🔍 Confronta campi
                Confronta("Nome", originale.Nome, model.Nome);
                Confronta("Cognome", originale.Cognome, model.Cognome);
                Confronta("Cellulare1", originale.Cellulare1, model.Cellulare1);
                Confronta("Cellulare2", originale.Cellulare2, model.Cellulare2);
                Confronta("Telefono", originale.Telefono, model.Telefono);
                Confronta("MAIL1", originale.MAIL1, model.MAIL1);
                Confronta("MAIL2", originale.MAIL2, model.MAIL2);
                Confronta("Codice Fiscale", originale.CodiceFiscale, model.CodiceFiscale);
                Confronta("PIVA", originale.PIVA, model.PIVA);
                Confronta("Codice Univoco", originale.CodiceUnivoco, model.CodiceUnivoco);
                Confronta("Descrizione Attività", originale.DescrizioneAttivita, model.DescrizioneAttivita);
                Confronta("Note", originale.Note, model.Note);
                Confronta("Tipo Utente", originale.TipoUtente, model.TipoUtente);
                Confronta("Ruolo", originale.Ruolo, model.Ruolo);
                Confronta("Indirizzo", originale.Indirizzo, model.Indirizzo);
                Confronta("Stato", originale.Stato, model.Stato);
                Confronta("Nazione", originale.ID_Nazione, model.ID_Nazione);
                Confronta("Città Residenza", originale.ID_CittaResidenza, model.ID_CittaResidenza);

                if (modifiche.Any())
                {
                    int maxVersion = db.Utenti_a
                        .AsNoTracking()
                        .Where(a => a.ID_UtenteOriginale == originale.ID_Utente)
                        .Select(a => (int?)a.NumeroVersione)
                        .Max() ?? 0;

                    int nuovaVersione = maxVersion + 1;

                    var archivio = new Utenti_a
                    {
                        ID_UtenteOriginale = originale.ID_Utente,
                        TipoUtente = originale.TipoUtente,
                        Nome = originale.Nome,
                        Cognome = originale.Cognome,
                        CodiceFiscale = originale.CodiceFiscale,
                        PIVA = originale.PIVA,
                        CodiceUnivoco = originale.CodiceUnivoco,
                        Telefono = originale.Telefono,
                        Cellulare1 = originale.Cellulare1,
                        Cellulare2 = originale.Cellulare2,
                        MAIL1 = originale.MAIL1,
                        MAIL2 = originale.MAIL2,
                        DescrizioneAttivita = originale.DescrizioneAttivita,
                        Note = originale.Note,
                        Stato = originale.Stato,
                        ID_CittaResidenza = originale.ID_CittaResidenza,
                        ID_Nazione = originale.ID_Nazione,
                        Indirizzo = originale.Indirizzo,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtenteModificatore,
                        Ruolo = originale.Ruolo,
                        NumeroVersione = nuovaVersione,
                        FotoProfiloPath = originale.FotoProfiloPath,
                        DOCUMENTO = originale.DOCUMENTO,
                        PasswordHash = originale.PasswordHash,
                        Salt = originale.Salt,
                        PasswordTemporanea = originale.PasswordTemporanea,
                        NomeAccount = originale.NomeAccount,
                        ModificheTestuali = $"Modifica effettuata su ID_Utente = {originale.ID_Utente} da ID_UtenteModificatore = {idUtenteModificatore} il {DateTime.Now:g}.\nModifiche:\n- " + string.Join("\n- ", modifiche)
                    };

                    db.Utenti_a.Add(archivio);
                    db.SaveChanges();
                }

                // ✅ Aggiorna i dati correnti
                originale.Nome = model.Nome;
                originale.Cognome = model.Cognome;
                originale.Cellulare1 = model.Cellulare1;
                originale.Cellulare2 = model.Cellulare2;
                originale.CodiceFiscale = model.CodiceFiscale;
                originale.PIVA = model.PIVA;
                originale.CodiceUnivoco = model.CodiceUnivoco;
                originale.Telefono = model.Telefono;
                originale.MAIL1 = model.MAIL1;
                originale.MAIL2 = model.MAIL2;
                originale.DescrizioneAttivita = model.DescrizioneAttivita;
                originale.Note = model.Note;
                originale.TipoUtente = model.TipoUtente;
                originale.Ruolo = model.Ruolo;
                originale.Indirizzo = model.Indirizzo;
                originale.Stato = model.Stato;
                originale.UltimaModifica = DateTime.Now;
                originale.ID_UtenteUltimaModifica = idUtenteModificatore;
                originale.ID_Nazione = model.ID_Nazione;
                originale.ID_CittaResidenza = model.ID_CittaResidenza;

                // 📎 Documento multiplo → prendi il primo
                var files = Request.Files;
                var documentiUtente = new List<byte[]>();

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    if (file != null && file.ContentLength > 0 && file.FileName != null && files.GetKey(i) == "DOCUMENTO")
                    {
                        using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                        {
                            var fileBytes = binaryReader.ReadBytes(file.ContentLength);
                            documentiUtente.Add(fileBytes);
                        }
                    }
                }

                if (documentiUtente.Any())
                    originale.DOCUMENTO = documentiUtente.First();

                // 📸 Foto profilo
                var foto = Request.Files["FotoProfilo"];
                if (foto != null && foto.ContentLength > 0)
                {
                    try
                    {
                        string pathRelativo = FotoProfiloHelper.ElaboraEFaiUpload(foto, "FotoProfilo");
                        originale.FotoProfiloPath = pathRelativo;
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "❌ Errore nella foto profilo: " + ex.Message });
                    }
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Utente modificato con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }



        [HttpPost]
        public ActionResult Elimina(int id)
        {
            try
            {
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == id);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                int idUtenteLoggato = UserManager.GetIDUtenteCollegato();

                // 🔢 Calcola numero versione
                int ultimaVersione = db.Utenti_a
                    .Where(a => a.ID_UtenteOriginale == utente.ID_Utente)
                    .Select(a => a.NumeroVersione)
                    .DefaultIfEmpty(0)
                    .Max();

                var archivio = new Utenti_a
                {
                    ID_UtenteOriginale = utente.ID_Utente,
                    TipoUtente = utente.TipoUtente,
                    Nome = utente.Nome,
                    Cognome = utente.Cognome,
                    CodiceFiscale = utente.CodiceFiscale,
                    PIVA = utente.PIVA,
                    CodiceUnivoco = utente.CodiceUnivoco,
                    Telefono = utente.Telefono,
                    Cellulare1 = utente.Cellulare1,
                    Cellulare2 = utente.Cellulare2,
                    MAIL1 = utente.MAIL1,
                    MAIL2 = utente.MAIL2,
                    DescrizioneAttivita = utente.DescrizioneAttivita,
                    Note = utente.Note,
                    Stato = utente.Stato,
                    ID_CittaResidenza = utente.ID_CittaResidenza,
                    ID_Nazione = utente.ID_Nazione,
                    Indirizzo = utente.Indirizzo,
                    FotoProfiloPath = utente.FotoProfiloPath,
                    PasswordHash = utente.PasswordHash,
                    Salt = utente.Salt,
                    PasswordTemporanea = utente.PasswordTemporanea,
                    SitoWEB = utente.SitoWEB,
                    DataCreazione = utente.DataCreazione,
                    ID_UtenteCreatore = utente.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = utente.ID_UtenteUltimaModifica,
                    UltimaModifica = utente.UltimaModifica,
                    NomeAccount = utente.NomeAccount,
                    DOCUMENTO = utente.DOCUMENTO,

                    NumeroVersione = ultimaVersione + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtenteLoggato,
                    ModificheTestuali = $"Eliminazione effettuata da ID_Utente = {idUtenteLoggato} in data {DateTime.Now:g}"
                };

                db.Utenti_a.Add(archivio);

                // ✅ Rimuove l'utente dalla tabella principale
                db.Utenti.Remove(utente);
                db.SaveChanges();

                return Json(new { success = true, message = "Utente eliminato con successo!" });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null)
                    msg += " | Inner: " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null)
                    msg += " | Inner2: " + ex.InnerException.InnerException.Message;

                return Json(new { success = false, message = "Errore nella eliminazione: " + msg });
            }
        }



        [HttpGet]
        public JsonResult GetUtente(int id)
        {
            var utenteRaw = db.Utenti.FirstOrDefault(u => u.ID_Utente == id);

            if (utenteRaw == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            var result = new
            {
                utenteRaw.ID_Utente,
                utenteRaw.Nome,
                utenteRaw.Cognome,
                utenteRaw.Cellulare1,
                utenteRaw.Cellulare2,
                utenteRaw.CodiceFiscale,
                utenteRaw.PIVA,
                utenteRaw.Telefono,
                utenteRaw.MAIL1,
                utenteRaw.MAIL2,
                utenteRaw.DescrizioneAttivita,
                utenteRaw.Note,
                utenteRaw.TipoUtente,
                ID_CittaResidenza = utenteRaw.ID_CittaResidenza,
                ID_Nazione = utenteRaw.ID_Nazione,
                utenteRaw.Indirizzo,
                utenteRaw.Stato,
                utenteRaw.Ruolo,
                FotoPresente = !string.IsNullOrEmpty(utenteRaw.FotoProfiloPath),
                FotoProfiloPath = utenteRaw.FotoProfiloPath,
                NomeFileFotoProfilo = !string.IsNullOrEmpty(utenteRaw.FotoProfiloPath)
                   ? Path.GetFileName(utenteRaw.FotoProfiloPath)
                   : null
            };
            return Json(result, JsonRequestBehavior.AllowGet);
        }




        [HttpGet]
        public ActionResult GetPermessi(int idUtente)
        {
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non trovato." }, JsonRequestBehavior.AllowGet);

            var menu = db.Menu
                .Where(m => m.ÈValido == "SI" && m.MostraNelMenu == "SI")
                .OrderBy(m => m.Ordine)
                .ToList();

            var permessiEsistenti = db.Permessi
                .Where(p => p.ID_Utente == idUtente)
                .ToList();

            bool èAdmin = utente.TipoUtente == "Admin";
            bool èProfessionista = utente.TipoUtente == "Professionista";

            // ✅ Nuovo controllo: se è collegato a un professionista con PuòGestirePermessi
            bool èUtenteProfessionistaGestibile = db.RelazioneUtenti
                .Where(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo")
                .Join(db.OperatoriSinergia, r => r.ID_Utente, o => o.ID_Operatore, (r, o) => o)
                .Any(o => o.TipoCliente == "Professionista" && o.PuòGestirePermessi == true);

            // 🔄 Se non ha ancora permessi, inizializzali solo se Admin o gestibile da professionista
            if ((èAdmin || èUtenteProfessionistaGestibile || èProfessionista) && !permessiEsistenti.Any())
            {
                var menuList = db.Menu.ToList();

                permessiEsistenti = menuList.Select(m => new Permessi
                {
                    ID_Utente = utente.ID_Utente,
                    ID_Menu = m.ID_Menu,

                    // ✅ Logica permessi base
                    Vedi = èAdmin || èProfessionista,
                    Aggiungi = èAdmin || èProfessionista,
                    Modifica = èAdmin,
                    Elimina = èAdmin,

                    Abilitato = (èAdmin || èProfessionista) ? "Sì" : "No",
                    Studio = "No",

                    DataAssegnazione = DateTime.Now,
                    UltimaModifica = DateTime.Now,
                    ID_UtenteCreatore = utente.ID_Utente,
                    ID_UtenteUltimaModifica = utente.ID_Utente
                }).ToList();

                db.Permessi.AddRange(permessiEsistenti);
                db.SaveChanges();
            }

            var viewModel = new PermessiViewModel
            {
                ID_Utente = idUtente,
                NomeUtente = $"{utente.Nome} {utente.Cognome}",
                Permessi = menu.Select(m =>
                {
                    var p = permessiEsistenti.FirstOrDefault(e => e.ID_Menu == m.ID_Menu);
                    return new PermessoSingoloViewModel
                    {
                        ID_Menu = m.ID_Menu,
                        NomeMenu = m.NomeMenu,
                        Abilitato = p != null && ((p.Vedi ?? false) || (p.Aggiungi ?? false) || (p.Modifica ?? false) || (p.Elimina ?? false)),
                        Vedi = p?.Vedi ?? false,
                        Aggiungi = p?.Aggiungi ?? false,
                        Modifica = p?.Modifica ?? false,
                        Elimina = p?.Elimina ?? false
                    };
                }).ToList()
            };

            return PartialView("_Permessi", viewModel);
        }



        [HttpPost]
        public ActionResult AssegnaPermessi()
        {
            try
            {
                var jsonString = new System.IO.StreamReader(Request.InputStream).ReadToEnd();

                if (string.IsNullOrWhiteSpace(jsonString))
                    return Json(new { success = false, message = "Corpo della richiesta vuoto." });

                var model = Newtonsoft.Json.JsonConvert.DeserializeObject<PermessiViewModel>(jsonString);

                if (model == null)
                    return Json(new { success = false, message = "Errore nella deserializzazione del model." });

                if (model.Permessi == null || model.ID_Utente <= 0)
                    return Json(new { success = false, message = "Permessi o ID Utente non validi." });

                int idUtenteOperativo = 1; // TODO: sostituire con ID dell'utente loggato

                foreach (var perm in model.Permessi)
                {
                    if (perm == null || perm.ID_Menu == 0)
                        continue;

                    var esistente = db.Permessi.FirstOrDefault(p => p.ID_Utente == model.ID_Utente && p.ID_Menu == perm.ID_Menu);

                    if (esistente != null)
                    {
                        esistente.Vedi = perm.Vedi;
                        esistente.Aggiungi = perm.Aggiungi;
                        esistente.Modifica = perm.Modifica;
                        esistente.Elimina = perm.Elimina;
                        esistente.Abilitato = (perm.Vedi || perm.Aggiungi || perm.Modifica || perm.Elimina) ? "Sì" : "No";
                        esistente.UltimaModifica = DateTime.Now;
                        esistente.ID_UtenteUltimaModifica = idUtenteOperativo;
                    }
                    else
                    {
                        db.Permessi.Add(new Permessi
                        {
                            ID_Utente = model.ID_Utente,
                            ID_Menu = perm.ID_Menu,
                            Studio = "No",
                            Abilitato = (perm.Vedi || perm.Aggiungi || perm.Modifica || perm.Elimina) ? "Sì" : "No",
                            Vedi = perm.Vedi,
                            Aggiungi = perm.Aggiungi,
                            Modifica = perm.Modifica,
                            Elimina = perm.Elimina,
                            DataAssegnazione = DateTime.Now,
                            UltimaModifica = DateTime.Now,
                            ID_UtenteCreatore = idUtenteOperativo,
                            ID_UtenteUltimaModifica = idUtenteOperativo
                        });
                    }
                }
                // 🔁 Riattiva l’utente se ha almeno un permesso abilitato
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == model.ID_Utente);
                if (utente != null && utente.Stato == "Disattivato")
                {
                    utente.Stato = "Attivo";
                    utente.UltimaModifica = DateTime.Now;
                    utente.ID_UtenteUltimaModifica = idUtenteOperativo;
                }


                db.SaveChanges();

                return Json(new { success = true, message = "✅ Permessi salvati correttamente!" });
            }
            catch (Exception ex)
            {
                Exception inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                // 🔥 Scrivo l'errore nella finestra Output durante il debug!
                System.Diagnostics.Debug.WriteLine("Errore AssegnaPermessi:");
                System.Diagnostics.Debug.WriteLine(inner.Message);
                System.Diagnostics.Debug.WriteLine(inner.StackTrace);

                return Json(new
                {
                    success = false,
                    message = "Errore durante il salvataggio permessi: " + inner.Message
                });
            }
        }

        [HttpPost]
        public ActionResult DisattivaPermessi(int idUtente)
        {
            int idUtenteOperatore = UserManager.GetIDUtenteCollegato();
            if (idUtenteOperatore <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });


            try
            {
                // Recupera i permessi attivi
                var permessi = db.Permessi
                    .Where(p => p.ID_Utente == idUtente)
                    .ToList();

                if (!permessi.Any())
                    return Json(new { success = false, message = "Nessun permesso trovato per l'utente." });

                // Archivia i permessi nella tabella Permessi_a
                foreach (var p in permessi)
                {
                    db.Permessi_a.Add(new Permessi_a
                    {
                        ID_Utente = p.ID_Utente,
                        ID_Menu = p.ID_Menu,
                        Studio = p.Studio,
                        Abilitato = p.Abilitato,
                        Vedi = p.Vedi,
                        Aggiungi = p.Aggiungi,
                        Modifica = p.Modifica,
                        Elimina = p.Elimina,
                        DataAssegnazione = p.DataAssegnazione, // ✅
                        ID_UtenteCreatore = p.ID_UtenteCreatore, // ✅
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtenteOperatore
                    });
                }


                // Rimuove i permessi attivi
                db.Permessi.RemoveRange(permessi);

                // Aggiorna lo stato dell’utente
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente != null)
                {
                    utente.Stato = "Disattivato";
                    utente.UltimaModifica = DateTime.Now;
                    utente.ID_UtenteUltimaModifica = idUtenteOperatore;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Permessi disattivati e archiviati correttamente." });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "Nessun dettaglio interno";
                return Json(new { success = false, message = "Errore: " + ex.Message + " → " + inner });
            }

        }

        [HttpPost]
        public ActionResult AutorizzaProfessionistaPermessi(int idProfessionista, bool autorizzato)
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            if (idUtente <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            var prof = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == idProfessionista && c.TipoCliente == "Professionista");
            if (prof == null)
                return Json(new { success = false, message = "Professionista non trovato." });

            prof.PuòGestirePermessi = autorizzato;
            prof.ID_UtenteUltimaModifica = idUtente;
            prof.UltimaModifica = DateTime.Now;

            db.SaveChanges();
            return Json(new { success = true, message = "✅ Autorizzazione aggiornata correttamente." });
        }


        [HttpGet]
        public ActionResult GetCittaENazioni()
        {
            var citta = db.Citta
                .OrderBy(c => c.NameLocalita)
                .Select(c => new { c.ID_BPCitta, c.NameLocalita })
                .ToList();

            var nazioni = db.Nazioni
                .OrderBy(n => n.NameNazione)
                .Select(n => new { n.ID_BPCittaDN, n.NameNazione })
                .ToList();

            return Json(new { citta, nazioni }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult TestUploadFoto(HttpPostedFileBase FotoProfilo)
        {
            try
            {
                if (FotoProfilo != null && FotoProfilo.ContentLength > 0)
                {
                    string nomeFile = Path.GetFileName(FotoProfilo.FileName);
                    string percorsoCartella = Server.MapPath("~/Content/img/FotoProfilo");
                    System.Diagnostics.Debug.WriteLine("📁 Percorso cartella: " + percorsoCartella);


                    if (!Directory.Exists(percorsoCartella))
                        Directory.CreateDirectory(percorsoCartella);

                    string percorsoCompleto = Path.Combine(percorsoCartella, nomeFile);
                    FotoProfilo.SaveAs(percorsoCompleto);

                    return Json(new { success = true, message = "✅ Foto salvata con successo!", fileName = nomeFile });
                }

                return Json(new { success = false, message = "❌ Nessun file selezionato." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }

        // =======================
        // 📤 Export Utenti in CSV
        // =======================
        [HttpGet]
        public ActionResult EsportaUtentiCsv()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null || utenteCorrente.TipoUtente != "Admin")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Accesso negato");

            var utenti = db.Utenti
                .Where(u => u.Stato != "Eliminato")
                .OrderBy(u => u.Nome)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID Utente;Nome;Cognome;Tipo Utente;Codice Fiscale;P.IVA;Email;Telefono;Stato");

            foreach (var u in utenti)
            {
                sb.AppendLine($"{u.ID_Utente};" +
                              $"{u.Nome};" +
                              $"{u.Cognome};" +
                              $"{u.TipoUtente};" +
                              $"{(string.IsNullOrWhiteSpace(u.CodiceFiscale) ? "-" : u.CodiceFiscale)};" +
                              $"{(string.IsNullOrWhiteSpace(u.PIVA) ? "-" : u.PIVA)};" +
                              $"{(string.IsNullOrWhiteSpace(u.MAIL1) ? "-" : u.MAIL1)};" +
                              $"{(string.IsNullOrWhiteSpace(u.Telefono) ? "-" : u.Telefono)};" +
                              $"{u.Stato}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Utenti_{DateTime.Now:yyyyMMdd}.csv");
        }



        // =======================
        // 📤 Export Utenti in PDF
        // =======================
        [HttpGet]
        public ActionResult EsportaUtentiPdf()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null || utenteCorrente.TipoUtente != "Admin")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Accesso negato");

            var lista = db.Utenti
                .Where(u => u.Stato != "Eliminato")
                .OrderBy(u => u.Nome)
                .Select(u => new UtenteViewModel
                {
                    ID_Utente = u.ID_Utente,
                    Nome = u.Nome,
                    Cognome = u.Cognome,
                    TipoUtente = u.TipoUtente,
                    CodiceFiscale = u.CodiceFiscale,
                    PIVA = u.PIVA,
                    MAIL1 = u.MAIL1,
                    Telefono = u.Telefono,
                    Stato = u.Stato
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Utenti/ReportUtentiPdf.cshtml", lista)
            {
                FileName = $"Utenti_{DateTime.Now:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }



        #endregion

        #region GESTIONE FORNITORI

        public ActionResult GestioneFornitori()
        {
            return View("~/Views/Fornitori/GestioneAziende.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneAziendeList(string nomeFiltro = "", string statoFiltro = "Tutti")
        {
            // ======================================================
            // 1️⃣ Utente corrente + permessi
            // ======================================================
            var idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            bool isAdmin = (utenteCorrente?.TipoUtente == "Admin");

            // Permessi
            var permessiVM = new PermessiViewModel
            {
                ID_Utente = idUtente,
                Permessi = new List<PermessoSingoloViewModel>()
            };

            if (isAdmin)
            {
                permessiVM.Permessi.Add(new PermessoSingoloViewModel
                {
                    ID_Menu = 0,
                    NomeMenu = "Tutti",
                    Aggiungi = true,
                    Modifica = true,
                    Elimina = true
                });
            }
            else if (utenteCorrente != null)
            {
                var permessiDb = (from p in db.Permessi
                                  join m in db.Menu on p.ID_Menu equals m.ID_Menu
                                  where p.ID_Utente == utenteCorrente.ID_Utente
                                  select new PermessoSingoloViewModel
                                  {
                                      ID_Menu = p.ID_Menu,
                                      NomeMenu = m.NomeMenu,
                                      Aggiungi = p.Aggiungi ?? false,
                                      Modifica = p.Modifica ?? false,
                                      Elimina = p.Elimina ?? false
                                  }).ToList();

                permessiVM.Permessi.AddRange(permessiDb);
            }

            bool puoAggiungere = isAdmin || permessiVM.Permessi.Any(x => x.Aggiungi);
            bool puoModificare = isAdmin || permessiVM.Permessi.Any(x => x.Modifica);
            bool puoEliminare = isAdmin || permessiVM.Permessi.Any(x => x.Elimina);

            ViewBag.Permessi = permessiVM;
            ViewBag.UtenteCorrente = utenteCorrente;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.PuoAggiungere = puoAggiungere;

            // ======================================================
            // 2️⃣ Query base aziende (solo TipoCliente = "Azienda")
            // ======================================================
            IQueryable<OperatoriSinergia> aziendeQuery = db.OperatoriSinergia
                .Where(x => x.TipoCliente == "Azienda");

            // ======================================================
            // 3️⃣ Filtri
            // ======================================================
            if (!string.IsNullOrWhiteSpace(nomeFiltro))
                aziendeQuery = aziendeQuery.Where(x => x.Nome.Contains(nomeFiltro));

            if (statoFiltro == "Attivi")
                aziendeQuery = aziendeQuery.Where(x => x.Stato == "Attivo");
            else if (statoFiltro == "Inattivi")
                aziendeQuery = aziendeQuery.Where(x => x.Stato == "Inattivo");

            // ======================================================
            // 4️⃣ Materialize query
            // ======================================================
            var aziende = aziendeQuery.OrderBy(x => x.ID_Operatore).ToList();

            // ======================================================
            // 5️⃣ Proiezione su ViewModel + flag permessi riga
            // ======================================================
            var viewModel = aziende.Select(a =>
            {
                var conto = db.DatiBancari
                              .Where(b => b.ID_Cliente == a.ID_Operatore && b.Stato == "Attivo")
                              .OrderByDescending(b => b.DataInserimento)
                              .FirstOrDefault();

                var vm = new AziendaViewModel
                {
                    ID_Azienda = a.ID_Operatore,
                    Nome = a.Nome,
                    TipoRagioneSociale = a.TipoRagioneSociale,
                    PartitaIVA = a.PIVA,
                    CodiceFiscale = a.CodiceFiscale,
                    CodiceUnivoco = a.CodiceUnivoco,
                    Indirizzo = a.Indirizzo,
                    Telefono = a.Telefono,
                    Email = a.MAIL1,
                    PEC = a.MAIL2,
                    SitoWEB = a.SitoWEB,
                    Citta = a.ID_Citta.HasValue ? db.Citta.FirstOrDefault(c => c.ID_BPCitta == a.ID_Citta)?.NameLocalita : null,
                    Nazione = a.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == a.ID_Nazione)?.NameNazione : null,
                    Stato = a.Stato,
                    Note = a.Note,
                    DescrizioneAttivita = a.DescrizioneAttivita,

                    UtentiAssociati = db.RelazioneUtenti.Count(r => r.ID_UtenteAssociato == a.ID_Operatore && r.Stato == "Attivo"),
                    HaUtentiAssegnati = db.RelazioneUtenti.Any(r => r.ID_UtenteAssociato == a.ID_Operatore && r.Stato == "Attivo"),

                    NomeBanca = conto?.NomeBanca,
                    IBAN = conto?.IBAN,
                    Intestatario = conto?.Intestatario,

                    NomeSettoreFornitore = a.ID_SettoreFornitore.HasValue
                        ? db.SettoriFornitori.FirstOrDefault(s => s.ID_Settore == a.ID_SettoreFornitore)?.Nome
                        : null,

                    // ✅ Categoria Servizi (da CategorieCosti)
                    ID_CategoriaServizi = a.ID_CategoriaServizi,
                    NomeCategoriaServizi = a.ID_CategoriaServizi.HasValue
                        ? db.CategorieCosti
                            .Where(c => c.ID_Categoria == a.ID_CategoriaServizi)
                            .Select(c => c.Nome)
                            .FirstOrDefault()
                        : null,

                    ÈCliente = a.ECliente,
                    ÈFornitore = a.EFornitore,

                    // ✅ Flag permessi per partial _AzioniFornitore
                    UtenteCorrenteHaPermessi = isAdmin || (puoAggiungere || puoModificare || puoEliminare),
                    PuoModificare = puoModificare,
                    PuoEliminare = puoEliminare
                };

                return vm;
            }).ToList();

            // ======================================================
            // 6️⃣ Dropdown categorie per modale di creazione/modifica
            // ======================================================
            ViewBag.CategorieCosti = db.CategorieCosti
                .OrderBy(c => c.Nome)
                .Select(c => new SelectListItem
                {
                    Value = c.ID_Categoria.ToString(),
                    Text = c.Nome
                })
                .ToList();

            // ======================================================
            // 7️⃣ Mostra azioni
            // ======================================================
            ViewBag.MostraAzioni = isAdmin || puoModificare || puoEliminare || puoAggiungere;

            // ======================================================
            // 8️⃣ Return partial view
            // ======================================================
            return PartialView("~/Views/Fornitori/_GestioneAziendeList.cshtml", viewModel);
        }



        [HttpPost]
        public ActionResult CreaAzienda()
        {
            int utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                int? idOwner = null;
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == utenteId);

                // 🔒 Impostazioni forzate per tutti: Cliente = false, Fornitore = true
                bool eCliente = false;
                bool eFornitore = true;

                // 🔍 Recupero Owner se è un professionista
                if (utente?.TipoUtente == "Professionista")
                {
                    var clienteProfessionista = db.OperatoriSinergia
                        .FirstOrDefault(c => c.ID_UtenteCollegato == utenteId && c.TipoCliente == "Professionista");
                    if (clienteProfessionista != null)
                        idOwner = clienteProfessionista.ID_Operatore;
                }

                // =====================================================
                // 🏢 CREA MODELLO OPERATORE (Azienda/Fornitore)
                // =====================================================
                var model = new OperatoriSinergia
                {
                    TipoCliente = "Azienda",
                    Nome = Request.Form["Nome"],
                    TipoRagioneSociale = Request.Form["TipoRagioneSociale"],
                    PIVA = Request.Form["PartitaIVA"],
                    CodiceFiscale = Request.Form["CodiceFiscale"],
                    CodiceUnivoco = Request.Form["CodiceUnivoco"],
                    Indirizzo = Request.Form["Indirizzo"],
                    ID_Citta = string.IsNullOrEmpty(Request.Form["ID_Citta"]) ? (int?)null : int.Parse(Request.Form["ID_Citta"]),
                    ID_Nazione = string.IsNullOrEmpty(Request.Form["ID_Nazione"]) ? (int?)null : int.Parse(Request.Form["ID_Nazione"]),
                    ID_SettoreFornitore = string.IsNullOrEmpty(Request.Form["ID_SettoreFornitore"]) ? (int?)null : int.Parse(Request.Form["ID_SettoreFornitore"]),

                    // 🆕 Categoria Servizi collegata (da CategorieCosti)
                    ID_CategoriaServizi = string.IsNullOrEmpty(Request.Form["ID_CategoriaServizi"])
                        ? (int?)null
                        : int.Parse(Request.Form["ID_CategoriaServizi"]),

                    Telefono = Request.Form["Telefono"],
                    MAIL1 = Request.Form["Email"],
                    MAIL2 = Request.Form["MAIL2"],
                    SitoWEB = Request.Form["SitoWEB"],
                    Stato = Request.Form["Stato"],
                    DescrizioneAttivita = Request.Form["DescrizioneAttivita"],
                    Note = Request.Form["Note"],
                    ECliente = eCliente,
                    EFornitore = eFornitore,
                    DataCreazione = DateTime.Now,
                    ID_UtenteCreatore = utenteId,
                    ID_Owner = idOwner
                };

                db.OperatoriSinergia.Add(model);
                db.SaveChanges();

                // =====================================================
                // 🗃️ ARCHIVIA VERSIONE INIZIALE
                // =====================================================
                var archivio = new OperatoriSinergia_a
                {
                    ID_OperatoreOriginale = model.ID_Operatore,
                    TipoCliente = model.TipoCliente,
                    Nome = model.Nome,
                    TipoRagioneSociale = model.TipoRagioneSociale,
                    PIVA = model.PIVA,
                    CodiceFiscale = model.CodiceFiscale,
                    CodiceUnivoco = model.CodiceUnivoco,
                    Indirizzo = model.Indirizzo,
                    ID_Citta = model.ID_Citta,
                    ID_Nazione = model.ID_Nazione,
                    ID_SettoreFornitore = model.ID_SettoreFornitore,
                    ID_CategoriaServizi = model.ID_CategoriaServizi, // ✅ archiviamo anche la categoria
                    Telefono = model.Telefono,
                    MAIL1 = model.MAIL1,
                    MAIL2 = model.MAIL2,
                    SitoWEB = model.SitoWEB,
                    Stato = model.Stato,
                    DescrizioneAttivita = model.DescrizioneAttivita,
                    Note = model.Note,
                    ECliente = model.ECliente,
                    EFornitore = model.EFornitore,
                    DataCreazione = model.DataCreazione,
                    ID_UtenteCreatore = model.ID_UtenteCreatore,
                    ID_Owner = model.ID_Owner,
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = utenteId,
                    ModificheTestuali = "Inserimento iniziale"
                };

                db.OperatoriSinergia_a.Add(archivio);
                db.SaveChanges();

                // =====================================================
                // 🏦 DATI BANCARI (opzionali)
                // =====================================================
                if (!string.IsNullOrWhiteSpace(Request.Form["IBAN"]))
                {
                    var datiBancari = new DatiBancari
                    {
                        ID_Cliente = model.ID_Operatore,
                        Intestatario = Request.Form["Intestatario"],
                        IBAN = Request.Form["IBAN"],
                        NomeBanca = Request.Form["Banca"],
                        Stato = "Attivo",
                        DataInserimento = DateTime.Now,
                        ID_UtenteCreatore = utenteId
                    };
                    db.DatiBancari.Add(datiBancari);
                    db.SaveChanges();
                }

                // =====================================================
                // 📎 FILE ALLEGATI (opzionali)
                // =====================================================
                if (Request.Files != null && Request.Files.Count > 0)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        var file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            var documento = new DocumentiAziende
                            {
                                ID_Cliente = model.ID_Operatore,
                                NomeDocumento = file.FileName,
                                TipoMime = file.ContentType,
                                DataCaricamento = DateTime.Now,
                                ID_UtenteCaricamento = utenteId
                            };

                            using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                            {
                                documento.FileContent = binaryReader.ReadBytes(file.ContentLength);
                            }

                            db.DocumentiAziende.Add(documento);
                        }
                    }

                    db.SaveChanges();
                }

                return Json(new { success = true, message = "✅ Azienda creata con successo!" });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => $"Campo: {e.PropertyName} → Errore: {e.ErrorMessage}");
                string fullError = string.Join(" | ", errorMessages);
                return Json(new { success = false, message = "Errore nella creazione: " + fullError });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += " → " + ex.InnerException.Message;
                return Json(new { success = false, message = "Errore inatteso: " + msg });
            }
        }


        [HttpPost]
        public ActionResult ModificaAzienda()
        {
            int utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                int idAzienda = int.Parse(Request.Form["ID_Azienda"] ?? "0");
                if (idAzienda == 0)
                    return Json(new { success = false, message = "ID azienda non valido." });

                var aziendaOriginale = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == idAzienda && c.TipoCliente == "Azienda");
                if (aziendaOriginale == null)
                    return Json(new { success = false, message = "Azienda non trovata." });

                string tipoUtente = UserManager.GetTipoUtente();
                bool isAdmin = tipoUtente == "Admin";
                if (!isAdmin && aziendaOriginale.ID_Owner != utenteId)
                    return Json(new { success = false, message = "Non hai i permessi per modificare questa azienda." });

                var modifiche = new List<string>();
                void Confronta(string campo, object valO, object valN)
                {
                    if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                        modifiche.Add($"{campo}: '{valO}' → '{valN}'");
                }

                // 🔒 Impostazioni forzate per tutti: Cliente = false, Fornitore = true
                bool eCliente = false;
                bool eFornitore = true;

                // 🆕 Recupera tutti i valori aggiornati dal form
                var nuovaAzienda = new
                {
                    Nome = Request.Form["Nome"],
                    TipoRagioneSociale = Request.Form["TipoRagioneSociale"],
                    PIVA = Request.Form["PartitaIVA"],
                    CodiceFiscale = Request.Form["CodiceFiscale"],
                    CodiceUnivoco = Request.Form["CodiceUnivoco"],
                    Telefono = Request.Form["Telefono"],
                    MAIL1 = Request.Form["Email"],
                    MAIL2 = Request.Form["MAIL2"],
                    SitoWEB = Request.Form["SitoWEB"],
                    Indirizzo = Request.Form["Indirizzo"],
                    DescrizioneAttivita = Request.Form["DescrizioneAttivita"],
                    Note = Request.Form["Note"],
                    Stato = Request.Form["Stato"],
                    ECliente = eCliente,
                    EFornitore = eFornitore,
                    ID_SettoreFornitore = string.IsNullOrEmpty(Request.Form["ID_SettoreFornitore"]) ? (int?)null : int.Parse(Request.Form["ID_SettoreFornitore"]),
                    ID_Citta = string.IsNullOrEmpty(Request.Form["ID_CittaResidenza"]) ? (int?)null : int.Parse(Request.Form["ID_CittaResidenza"]),
                    ID_Nazione = string.IsNullOrEmpty(Request.Form["ID_Nazione"]) ? (int?)null : int.Parse(Request.Form["ID_Nazione"]),
                    ID_CategoriaServizi = string.IsNullOrEmpty(Request.Form["ID_CategoriaServizi"]) ? (int?)null : int.Parse(Request.Form["ID_CategoriaServizi"])
                };

                // ======================================================
                // 🔍 Confronto dei campi
                // ======================================================
                Confronta("Nome", aziendaOriginale.Nome, nuovaAzienda.Nome);
                Confronta("Tipo Ragione Sociale", aziendaOriginale.TipoRagioneSociale, nuovaAzienda.TipoRagioneSociale);
                Confronta("PIVA", aziendaOriginale.PIVA, nuovaAzienda.PIVA);
                Confronta("Codice Fiscale", aziendaOriginale.CodiceFiscale, nuovaAzienda.CodiceFiscale);
                Confronta("Codice Univoco", aziendaOriginale.CodiceUnivoco, nuovaAzienda.CodiceUnivoco);
                Confronta("Telefono", aziendaOriginale.Telefono, nuovaAzienda.Telefono);
                Confronta("MAIL1", aziendaOriginale.MAIL1, nuovaAzienda.MAIL1);
                Confronta("MAIL2", aziendaOriginale.MAIL2, nuovaAzienda.MAIL2);
                Confronta("SitoWEB", aziendaOriginale.SitoWEB, nuovaAzienda.SitoWEB);
                Confronta("Indirizzo", aziendaOriginale.Indirizzo, nuovaAzienda.Indirizzo);
                Confronta("Descrizione Attività", aziendaOriginale.DescrizioneAttivita, nuovaAzienda.DescrizioneAttivita);
                Confronta("Note", aziendaOriginale.Note, nuovaAzienda.Note);
                Confronta("Stato", aziendaOriginale.Stato, nuovaAzienda.Stato);
                Confronta("È Cliente", aziendaOriginale.ECliente, nuovaAzienda.ECliente);
                Confronta("È Fornitore", aziendaOriginale.EFornitore, nuovaAzienda.EFornitore);
                Confronta("Settore Fornitore", aziendaOriginale.ID_SettoreFornitore, nuovaAzienda.ID_SettoreFornitore);
                Confronta("Città", aziendaOriginale.ID_Citta, nuovaAzienda.ID_Citta);
                Confronta("Nazione", aziendaOriginale.ID_Nazione, nuovaAzienda.ID_Nazione);
                Confronta("Categoria Servizi", aziendaOriginale.ID_CategoriaServizi, nuovaAzienda.ID_CategoriaServizi); // ✅ nuovo confronto

                // ======================================================
                // 🗃️ CREA VERSIONE STORICA SE CI SONO MODIFICHE
                // ======================================================
                if (modifiche.Any())
                {
                    int maxVersion = db.OperatoriSinergia_a
                        .Where(a => a.ID_OperatoreOriginale == aziendaOriginale.ID_Operatore)
                        .Select(a => (int?)a.NumeroVersione)
                        .Max() ?? 0;

                    int nuovaVersione = maxVersion + 1;

                    db.OperatoriSinergia_a.Add(new OperatoriSinergia_a
                    {
                        ID_OperatoreOriginale = aziendaOriginale.ID_Operatore,
                        TipoCliente = aziendaOriginale.TipoCliente,
                        Nome = aziendaOriginale.Nome,
                        TipoRagioneSociale = aziendaOriginale.TipoRagioneSociale,
                        CodiceFiscale = aziendaOriginale.CodiceFiscale,
                        PIVA = aziendaOriginale.PIVA,
                        CodiceUnivoco = aziendaOriginale.CodiceUnivoco,
                        Indirizzo = aziendaOriginale.Indirizzo,
                        ID_Citta = aziendaOriginale.ID_Citta,
                        ID_Nazione = aziendaOriginale.ID_Nazione,
                        ID_SettoreFornitore = aziendaOriginale.ID_SettoreFornitore,
                        ID_CategoriaServizi = aziendaOriginale.ID_CategoriaServizi, // ✅ archiviamo anche la categoria
                        Telefono = aziendaOriginale.Telefono,
                        MAIL1 = aziendaOriginale.MAIL1,
                        MAIL2 = aziendaOriginale.MAIL2,
                        SitoWEB = aziendaOriginale.SitoWEB,
                        Stato = aziendaOriginale.Stato,
                        DescrizioneAttivita = aziendaOriginale.DescrizioneAttivita,
                        Note = aziendaOriginale.Note,
                        Documento = aziendaOriginale.Documento,
                        ECliente = aziendaOriginale.ECliente,
                        EFornitore = aziendaOriginale.EFornitore,
                        ID_Owner = aziendaOriginale.ID_Owner,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = utenteId,
                        NumeroVersione = nuovaVersione,
                        ModificheTestuali = $"Modifica su ID_Cliente = {aziendaOriginale.ID_Operatore} da utente = {utenteId} il {DateTime.Now:g}:\n- " + string.Join("\n- ", modifiche)
                    });
                }

                // ======================================================
                // ✏️ AGGIORNA I CAMPI CORRENTI
                // ======================================================
                aziendaOriginale.Nome = nuovaAzienda.Nome;
                aziendaOriginale.TipoRagioneSociale = nuovaAzienda.TipoRagioneSociale;
                aziendaOriginale.PIVA = nuovaAzienda.PIVA;
                aziendaOriginale.CodiceFiscale = nuovaAzienda.CodiceFiscale;
                aziendaOriginale.CodiceUnivoco = nuovaAzienda.CodiceUnivoco;
                aziendaOriginale.Telefono = nuovaAzienda.Telefono;
                aziendaOriginale.MAIL1 = nuovaAzienda.MAIL1;
                aziendaOriginale.MAIL2 = nuovaAzienda.MAIL2;
                aziendaOriginale.SitoWEB = nuovaAzienda.SitoWEB;
                aziendaOriginale.Indirizzo = nuovaAzienda.Indirizzo;
                aziendaOriginale.DescrizioneAttivita = nuovaAzienda.DescrizioneAttivita;
                aziendaOriginale.Note = nuovaAzienda.Note;
                aziendaOriginale.Stato = nuovaAzienda.Stato;
                aziendaOriginale.ECliente = nuovaAzienda.ECliente;
                aziendaOriginale.EFornitore = nuovaAzienda.EFornitore;
                aziendaOriginale.ID_SettoreFornitore = nuovaAzienda.ID_SettoreFornitore;
                aziendaOriginale.ID_Citta = nuovaAzienda.ID_Citta;
                aziendaOriginale.ID_Nazione = nuovaAzienda.ID_Nazione;
                aziendaOriginale.ID_CategoriaServizi = nuovaAzienda.ID_CategoriaServizi; // ✅ aggiorna categoria
                aziendaOriginale.UltimaModifica = DateTime.Now;
                aziendaOriginale.ID_UtenteUltimaModifica = utenteId;

                // ======================================================
                // 🚫 Disattiva relazioni se azienda diventa inattiva
                // ======================================================
                if (aziendaOriginale.Stato == "Inattivo")
                {
                    var relazioniAttive = db.RelazioneUtenti
                        .Where(r => r.ID_UtenteAssociato == aziendaOriginale.ID_Operatore && r.Stato == "Attivo")
                        .ToList();

                    foreach (var relazione in relazioniAttive)
                    {
                        relazione.Stato = "Disattivo";
                        relazione.UltimaModifica = DateTime.Now;
                        relazione.ID_UtenteUltimaModifica = utenteId;
                    }
                }

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Azienda modificata con successo!" });
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                if (ex.InnerException != null) err += " → " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null) err += " → " + ex.InnerException.InnerException.Message;

                return Json(new { success = false, message = "Errore: " + err });
            }
        }


        [HttpGet]
        public ActionResult GetAzienda(int id)
        {
            if (id == 0)
                return Json(null, JsonRequestBehavior.AllowGet);

            var azienda = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Azienda");
            if (azienda == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            var datiBancari = db.DatiBancari
                .Where(d => d.ID_Cliente == id && d.Stato == "Attivo")
                .OrderByDescending(d => d.DataInserimento)
                .FirstOrDefault();

            var owner = azienda.ID_Owner.HasValue
                ? db.Utenti.FirstOrDefault(u => u.ID_Utente == azienda.ID_Owner.Value)
                : null;

            // =====================================================
            // 🔐 Verifica permessi di modifica
            // =====================================================
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            bool puòModificare = false;
            if (utenteCorrente != null)
            {
                if (utenteCorrente.TipoUtente == "Admin")
                    puòModificare = true;
                else if (utenteCorrente.TipoUtente == "Professionista" && azienda.ID_Owner == utenteCorrente.ID_Utente)
                    puòModificare = true;
            }

            // =====================================================
            // 🧩 Recupera nome categoria (da tabella CategorieCosti)
            // =====================================================
            string nomeCategoria = null;
            if (azienda.ID_CategoriaServizi.HasValue)
            {
                nomeCategoria = db.CategorieCosti
                    .Where(c => c.ID_Categoria == azienda.ID_CategoriaServizi)
                    .Select(c => c.Nome)
                    .FirstOrDefault();
            }

            // =====================================================
            // 🧱 Costruzione ViewModel
            // =====================================================
            var viewModel = new
            {
                ID_Azienda = azienda.ID_Operatore,
                Nome = azienda.Nome,
                TipoRagioneSociale = azienda.TipoRagioneSociale,
                PartitaIVA = azienda.PIVA,
                CodiceFiscale = azienda.CodiceFiscale,
                CodiceUnivoco = azienda.CodiceUnivoco,
                Indirizzo = azienda.Indirizzo,
                Telefono = azienda.Telefono,
                Email = azienda.MAIL1,
                SitoWEB = azienda.SitoWEB,
                Stato = azienda.Stato,
                Note = azienda.Note,
                DescrizioneAttivita = azienda.DescrizioneAttivita,
                ID_Nazione = azienda.ID_Nazione,
                ID_Citta = azienda.ID_Citta,

                ÈCliente = azienda.ECliente,
                ÈFornitore = azienda.EFornitore,
                ID_Owner = azienda.ID_Owner,
                NomeOwner = owner != null ? (owner.Nome + " " + owner.Cognome) : null,

                // 🔧 Settore e categoria servizi
                ID_SettoreFornitore = azienda.ID_SettoreFornitore,
                ID_CategoriaServizi = azienda.ID_CategoriaServizi,
                NomeCategoriaServizi = nomeCategoria,

                // 🏦 Dati bancari
                NomeBanca = datiBancari?.NomeBanca,
                IBAN = datiBancari?.IBAN,
                Intestatario = datiBancari?.Intestatario,
                BIC_SWIFT = datiBancari?.BIC_SWIFT,
                NoteBancarie = datiBancari?.Note,
                StatoBancario = datiBancari?.Stato,

                // 🔐 Permesso per view
                PuòModificare = puòModificare
            };

            return Json(viewModel, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaAzienda(int id)
        {
            try
            {
                int utenteId = UserManager.GetIDUtenteCollegato();
                if (utenteId <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                var aziendaOriginale = db.OperatoriSinergia
                    .FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Azienda");

                if (aziendaOriginale == null)
                    return Json(new { success = false, message = "Azienda non trovata." });

                // ======================================================
                // 🔐 Controllo permessi
                // ======================================================
                string tipoUtente = UserManager.GetTipoUtente();
                bool isAdmin = tipoUtente == "Admin";
                if (!isAdmin && aziendaOriginale.ID_Owner != utenteId)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare questa azienda." });

                // ======================================================
                // 🧾 Calcolo numero versione archivio
                // ======================================================
                int ultimaVersione = db.OperatoriSinergia_a
                    .Where(a => a.ID_OperatoreOriginale == aziendaOriginale.ID_Operatore)
                    .Select(a => (int?)a.NumeroVersione)
                    .Max() ?? 0;

                int nuovaVersione = ultimaVersione + 1;

                // ======================================================
                // 🗃️ Archivia versione prima della disattivazione
                // ======================================================
                var archivio = new OperatoriSinergia_a
                {
                    ID_OperatoreOriginale = aziendaOriginale.ID_Operatore,
                    TipoCliente = aziendaOriginale.TipoCliente,
                    Nome = aziendaOriginale.Nome,
                    TipoRagioneSociale = aziendaOriginale.TipoRagioneSociale,
                    CodiceFiscale = aziendaOriginale.CodiceFiscale,
                    PIVA = aziendaOriginale.PIVA,
                    CodiceUnivoco = aziendaOriginale.CodiceUnivoco,
                    Indirizzo = aziendaOriginale.Indirizzo,
                    ID_Citta = aziendaOriginale.ID_Citta,
                    ID_Nazione = aziendaOriginale.ID_Nazione,
                    Telefono = aziendaOriginale.Telefono,
                    MAIL1 = aziendaOriginale.MAIL1,
                    MAIL2 = aziendaOriginale.MAIL2,
                    SitoWEB = aziendaOriginale.SitoWEB,
                    Stato = aziendaOriginale.Stato,
                    DescrizioneAttivita = aziendaOriginale.DescrizioneAttivita,
                    Note = aziendaOriginale.Note,
                    Documento = aziendaOriginale.Documento,
                    ECliente = aziendaOriginale.ECliente,
                    EFornitore = aziendaOriginale.EFornitore,
                    ID_SettoreFornitore = aziendaOriginale.ID_SettoreFornitore,

                    // 🆕 Inclusione della categoria servizi
                    ID_CategoriaServizi = aziendaOriginale.ID_CategoriaServizi,

                    ID_Owner = aziendaOriginale.ID_Owner,
                    ID_UtenteCollegato = aziendaOriginale.ID_UtenteCollegato,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = utenteId,
                    NumeroVersione = nuovaVersione,
                    ModificheTestuali = $"🗑️ Eliminazione effettuata da ID_Utente = {utenteId} in data {DateTime.Now:g}"
                };

                db.OperatoriSinergia_a.Add(archivio);

                // ======================================================
                // ❌ Disattiva azienda e aggiorna dati principali
                // ======================================================
                aziendaOriginale.Stato = "Inattivo";
                aziendaOriginale.UltimaModifica = DateTime.Now;
                aziendaOriginale.ID_UtenteUltimaModifica = utenteId;

                // ======================================================
                // ❌ Disattiva relazioni attive
                // ======================================================
                var relazioniAttive = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == aziendaOriginale.ID_Operatore && r.Stato == "Attivo")
                    .ToList();

                foreach (var relazione in relazioniAttive)
                {
                    relazione.Stato = "Terminato";
                    relazione.UltimaModifica = DateTime.Now;
                    relazione.ID_UtenteUltimaModifica = utenteId;
                }

                // ======================================================
                // ❌ Disattiva dati bancari attivi
                // ======================================================
                var contoAttivo = db.DatiBancari
                    .FirstOrDefault(b => b.ID_Cliente == aziendaOriginale.ID_Operatore && b.Stato == "Attivo");

                if (contoAttivo != null)
                {
                    contoAttivo.Stato = "Inattivo";
                    contoAttivo.DataInserimento = DateTime.Now;
                    contoAttivo.ID_UtenteCreatore = utenteId;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Azienda disattivata correttamente." });
            }
            catch (Exception ex)
            {
                Exception inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                return Json(new
                {
                    success = false,
                    message = "Errore inatteso: " + inner.Message
                });
            }
        }

        [HttpGet]
        public ActionResult GetCategorieServizi()
        {
            try
            {
                // Recupera tutte le categorie disponibili per i fornitori
                var categorie = db.CategorieCosti
                    .OrderBy(c => c.Nome)
                    .Select(c => new
                    {
                        c.ID_Categoria,
                        c.Nome
                    })
                    .ToList();

                return Json(categorie, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore nel recupero delle categorie: " + ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }



        [HttpGet]
        public ActionResult DownloadDocumentoAzienda(int id)
        {
            var documento = db.DocumentiAziende.FirstOrDefault(d => d.ID_Documento == id);
            if (documento == null)
                return HttpNotFound("Documento non trovato.");

            return File(documento.FileContent, documento.TipoMime, documento.NomeDocumento);
        }

        [HttpPost]
        public ActionResult EliminaDocumentoAzienda(int id)
        {
            try
            {
                var documento = db.DocumentiAziende.FirstOrDefault(d => d.ID_Documento == id);
                if (documento == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                db.DocumentiAziende.Remove(documento);
                db.SaveChanges();

                return Json(new { success = true, message = "Documento eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }



        [HttpGet]
        public ActionResult GetDocumentiAzienda(int idAzienda)
        {
            if (idAzienda == 0)
                return PartialView("~/Views/Fornitori/_VisualizzaDocumentiAzienda.cshtml", new List<DocumentiAziende>());

            var documenti = db.DocumentiAziende
                .Where(d => d.ID_Cliente == idAzienda)
                .OrderByDescending(d => d.DataCaricamento)
                .ToList();
            ViewBag.IDAzienda = idAzienda; // ✅ Serve per usare @ViewBag.IDAzienda nella view

            return PartialView("~/Views/Fornitori/_VisualizzaDocumentiAzienda.cshtml", documenti);
        }

        [HttpGet]
            public ActionResult GetTipiRagioneSociale()
            {
                var tipi = db.TipoRagioneSociale
                             .OrderBy(t => t.NomeTipo)
                             .Select(t => new { t.ID_TipoRagioneSociale, t.NomeTipo })
                             .ToList();

                return Json(tipi, JsonRequestBehavior.AllowGet);
            }

        [HttpGet]
        public JsonResult GetSettoriFornitori()
        {
            var settori = db.SettoriFornitori
                .Where(s => s.Stato == "Attivo")
                .OrderBy(s => s.Nome)
                .Select(s => new
                {
                    s.ID_Settore,
                    s.Nome
                })
                .ToList();

            return Json(settori, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult RiattivaAzienda(int id)
        {
            try
            {
                var azienda = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Azienda");
                if (azienda == null)
                    return Json(new { success = false, message = "Azienda non trovata." });

                int utenteId = UserManager.GetIDUtenteCollegato();
                if (utenteId <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                // Riattiva l'azienda
                azienda.Stato = "Attivo";
                azienda.UltimaModifica = DateTime.Now;
                azienda.ID_UtenteUltimaModifica = utenteId;

                // 🔁 Riattiva il conto bancario più recente, se presente
                var contoInattivo = db.DatiBancari
                    .Where(b => b.ID_Cliente == azienda.ID_Operatore && b.Stato == "Inattivo")
                    .OrderByDescending(b => b.DataInserimento)
                    .FirstOrDefault();

                if (contoInattivo != null)
                {
                    contoInattivo.Stato = "Attivo";
                    contoInattivo.DataInserimento = DateTime.Now;
                    contoInattivo.ID_UtenteCreatore = utenteId;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Azienda riattivata correttamente!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore inatteso: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetUtenti()
        {
            var utenti = db.Utenti
                .Where(u => u.Stato == "Attivo") // solo utenti attivi
                .Select(u => new
                {
                    u.ID_Utente,
                    NomeCompleto = u.Nome + " " + u.Cognome
                })
                .ToList();

            return Json(utenti, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult AssegnaEntitaAzienda(int idAzienda, List<int> idUtenti, List<int> idTeam, string tipoRelazione, string statoRelazione, string noteRelazione)
        {
            try
            {
                var idUtenteLoggato = UserManager.GetIDUtenteCollegato();

                // 🔹 Gestione utenti singoli
                if (idUtenti != null && idUtenti.Any())
                {
                    foreach (var idUtente in idUtenti)
                    {
                        var relazioneEsistente = db.RelazioneUtenti.FirstOrDefault(r =>
                            r.ID_Utente == idUtente &&
                            r.ID_UtenteAssociato == idAzienda &&
                            r.Stato == "Attivo");

                        if (relazioneEsistente == null)
                        {
                            db.RelazioneUtenti.Add(new RelazioneUtenti
                            {
                                ID_Utente = idUtente,
                                ID_Team = null,
                                ID_UtenteAssociato = idAzienda,
                                TipoRelazione = tipoRelazione,
                                DataInizio = DateTime.Now,
                                Stato = statoRelazione,
                                Note = string.IsNullOrEmpty(noteRelazione) ? null : noteRelazione,
                                ID_UtenteCreatore = idUtenteLoggato,
                                UltimaModifica = DateTime.Now
                            });
                        }
                    }
                }

                // 🔹 Gestione team
                if (idTeam != null && idTeam.Any())
                {
                    foreach (var teamId in idTeam)
                    {
                        var relazioneEsistente = db.RelazioneUtenti.FirstOrDefault(r =>
                            r.ID_Team == teamId &&
                            r.ID_UtenteAssociato == idAzienda &&
                            r.Stato == "Attivo");

                        if (relazioneEsistente == null)
                        {
                            db.RelazioneUtenti.Add(new RelazioneUtenti
                            {
                                ID_Utente = null,
                                ID_Team = teamId,
                                ID_UtenteAssociato = idAzienda,
                                TipoRelazione = tipoRelazione,
                                DataInizio = DateTime.Now,
                                Stato = statoRelazione,
                                Note = string.IsNullOrEmpty(noteRelazione) ? null : noteRelazione,
                                ID_UtenteCreatore = idUtenteLoggato,
                                UltimaModifica = DateTime.Now
                            });
                        }
                    }
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Assegnazioni completate con successo." });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                return Json(new { success = false, message = $"Errore durante l'assegnazione: {inner.Message}" });
            }
        }

        [HttpGet]
        public ActionResult GetUtentiDisponibili(int idAzienda)
        {
            // Recupera utenti già collegati DIRETTAMENTE all'azienda
            var utentiDirettiIds = db.RelazioneUtenti
                .Where(r => r.ID_UtenteAssociato == idAzienda
                            && r.Stato == "Attivo"
                            && r.ID_Utente != null)   // solo relazioni dirette
                .Select(r => r.ID_Utente.Value)
                .ToList();

            // Recupera utenti già collegati tramite TEAM
            var utentiTramiteTeam = (from rel in db.RelazioneUtenti
                                     join mt in db.MembriTeam on rel.ID_Team equals mt.ID_Team
                                     where rel.ID_UtenteAssociato == idAzienda
                                           && rel.Stato == "Attivo"
                                           && rel.ID_Team != null
                                     select mt.ID_Professionista)
                                     .ToList();

            // Unione di utenti già collegati (diretti + indiretti tramite team)
            var utentiAssegnatiIds = utentiDirettiIds.Union(utentiTramiteTeam).Distinct().ToList();

            // Seleziona solo utenti attivi e non già assegnati
            var utentiDisponibili = db.Utenti
                .Where(u => u.Stato == "Attivo" && !utentiAssegnatiIds.Contains(u.ID_Utente))
                .Select(u => new
                {
                    u.ID_Utente,
                    NomeUtente = u.Nome + " " + u.Cognome
                })
                .ToList();

            return Json(utentiDisponibili, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetTeamDisponibili(int idAzienda)
        {
            var teamGiaAssegnati = db.RelazioneUtenti
                .Where(r => r.ID_UtenteAssociato == idAzienda
                            && r.Stato == "Attivo"
                            && r.ID_Team != null)
                .Select(r => r.ID_Team.Value)
                .ToList();

            var teamDisponibili = db.TeamProfessionisti
                .Where(t => t.Attivo && !teamGiaAssegnati.Contains(t.ID_Team))
                .Select(t => new
                {
                    t.ID_Team,
                    NomeTeam = t.Nome
                })
                .ToList();

            return Json(teamDisponibili, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult VisualizzaUtentiAzienda(int idAzienda)
        {
            var relazioni = db.RelazioneUtenti
                .Where(r => r.ID_UtenteAssociato == idAzienda && r.Stato == "Attivo")
                .ToList(); // 🔥 in memoria per gestire utenti e team

            var assegnati = relazioni.Select(r =>
            {
                string nome = "";
                string cognome = "";
                int? idElemento = null;
                string tipo = "";

                if (r.ID_Utente != null)
                {
                    var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == r.ID_Utente);
                    if (utente != null)
                    {
                        nome = utente.Nome ?? "";
                        cognome = utente.Cognome ?? "";
                        idElemento = utente.ID_Utente;
                        tipo = "Utente";
                    }
                }
                else if (r.ID_Team != null)
                {
                    var team = db.TeamProfessionisti.FirstOrDefault(t => t.ID_Team == r.ID_Team);
                    if (team != null)
                    {
                        nome = team.Nome ?? "";
                        cognome = ""; // 👈 niente undefined
                        idElemento = team.ID_Team;
                        tipo = "Team";
                    }
                }

                return new
                {
                    r.ID_Relazione,
                    ID_Elemento = idElemento,  // può essere ID_Utente o ID_Team
                    Nome = nome,
                    Cognome = cognome,
                    TipoElemento = tipo,       // "Utente" o "Team"
                    r.TipoRelazione,
                    r.Note,
                    DataInizio = r.DataInizio.ToString("yyyy-MM-ddTHH:mm:ss")
                };
            }).ToList();

            return Json(assegnati, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult RimuoviUtenteAzienda(int idRelazione)
        {
            try
            {
                var relazione = db.RelazioneUtenti.FirstOrDefault(r => r.ID_Relazione == idRelazione);
                if (relazione == null)
                    return Json(new { success = false, message = "Relazione non trovata." });

                // Archiviamo prima (sia utente che team se presenti)
                var archivio = new RelazioneUtenti_a
                {
                    ID_Utente = relazione.ID_Utente,   // può essere null
                    ID_Team = relazione.ID_Team,       // nuovo campo, può essere null
                    ID_UtenteAssociato = relazione.ID_UtenteAssociato,
                    TipoRelazione = relazione.TipoRelazione,
                    DataInizio = relazione.DataInizio,
                    DataFine = DateTime.Now,
                    Stato = "Terminato",
                    Note = relazione.Note,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = UserManager.GetIDUtenteCollegato()
                };

                db.RelazioneUtenti_a.Add(archivio);

                // Disattiva la relazione attiva
                db.RelazioneUtenti.Remove(relazione);
                db.SaveChanges();

                return Json(new { success = true, message = "Relazione rimossa correttamente." });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Errore durante la rimozione: " + inner });
            }
        }


        [HttpPost]
        public ActionResult CaricaDocumentiDiretti(int idAzienda)
        {
            var utenteId = (int?)Session["ID_Utente"];
            if (utenteId == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                if (Request.Files != null && Request.Files.Count > 0)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        var file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            var documento = new DocumentiAziende
                            {
                                ID_Cliente = idAzienda,
                                NomeDocumento = file.FileName,
                                TipoMime = file.ContentType,
                                DataCaricamento = DateTime.Now,
                                ID_UtenteCaricamento = utenteId
                            };

                            using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                            {
                                documento.FileContent = binaryReader.ReadBytes(file.ContentLength);
                            }

                            db.DocumentiAziende.Add(documento);
                        }
                    }

                    db.SaveChanges();
                    return Json(new { success = true, message = "Documenti caricati correttamente!" });
                }

                return Json(new { success = false, message = "Nessun file selezionato." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult EsportaFornitoriCsv()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            var fornitori = db.OperatoriSinergia
                .Where(f => f.TipoCliente == "Azienda" && f.Stato != "Eliminato")
                .OrderBy(f => f.ID_Operatore)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID Fornitore;Nome;Tipo Ragione Sociale;Partita IVA;Codice Fiscale;Codice Univoco;Indirizzo;Telefono;Email;PEC;Sito Web;Città;Nazione;Stato;Note;Descrizione Attività;Banca;IBAN;Intestatario;BIC/SWIFT;Note Bancarie;Settore Fornitore");

            foreach (var f in fornitori)
            {
                var conto = db.DatiBancari
                              .Where(b => b.ID_Cliente == f.ID_Operatore && b.Stato == "Attivo")
                              .OrderByDescending(b => b.DataInserimento)
                              .FirstOrDefault();

                string citta = f.ID_Citta.HasValue ? db.Citta.FirstOrDefault(c => c.ID_BPCitta == f.ID_Citta)?.NameLocalita : "-";
                string nazione = f.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == f.ID_Nazione)?.NameNazione : "-";
                string settore = f.ID_SettoreFornitore.HasValue ? db.SettoriFornitori.FirstOrDefault(s => s.ID_Settore == f.ID_SettoreFornitore)?.Nome : "-";

                sb.AppendLine($"{f.ID_Operatore};" +
                              $"{(string.IsNullOrWhiteSpace(f.Nome) ? "-" : f.Nome)};" +
                              $"{(string.IsNullOrWhiteSpace(f.TipoRagioneSociale) ? "-" : f.TipoRagioneSociale)};" +
                              $"{(string.IsNullOrWhiteSpace(f.PIVA) ? "-" : f.PIVA)};" +
                              $"{(string.IsNullOrWhiteSpace(f.CodiceFiscale) ? "-" : f.CodiceFiscale)};" +
                              $"{(string.IsNullOrWhiteSpace(f.CodiceUnivoco) ? "-" : f.CodiceUnivoco)};" +
                              $"{(string.IsNullOrWhiteSpace(f.Indirizzo) ? "-" : f.Indirizzo)};" +
                              $"{(string.IsNullOrWhiteSpace(f.Telefono) ? "-" : f.Telefono)};" +
                              $"{(string.IsNullOrWhiteSpace(f.MAIL1) ? "-" : f.MAIL1)};" +
                              $"{(string.IsNullOrWhiteSpace(f.MAIL2) ? "-" : f.MAIL2)};" +
                              $"{(string.IsNullOrWhiteSpace(f.SitoWEB) ? "-" : f.SitoWEB)};" +
                              $"{citta};" +
                              $"{nazione};" +
                              $"{(string.IsNullOrWhiteSpace(f.Stato) ? "-" : f.Stato)};" +
                              $"{(string.IsNullOrWhiteSpace(f.Note) ? "-" : f.Note)};" +
                              $"{(string.IsNullOrWhiteSpace(f.DescrizioneAttivita) ? "-" : f.DescrizioneAttivita)};" +
                              $"{(string.IsNullOrWhiteSpace(conto?.NomeBanca) ? "-" : conto.NomeBanca)};" +
                              $"{(string.IsNullOrWhiteSpace(conto?.IBAN) ? "-" : conto.IBAN)};" +
                              $"{(string.IsNullOrWhiteSpace(conto?.Intestatario) ? "-" : conto.Intestatario)};" +
                              $"{(string.IsNullOrWhiteSpace(conto?.BIC_SWIFT) ? "-" : conto.BIC_SWIFT)};" +
                              $"{(string.IsNullOrWhiteSpace(conto?.Note) ? "-" : conto.Note)};" +
                              $"{settore}");
            }

            byte[] buffer = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(buffer, "text/csv", $"Fornitori_{DateTime.Today:yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaFornitoriPdf()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            var fornitori = db.OperatoriSinergia
                .Where(f => f.TipoCliente == "Azienda" && f.Stato != "Eliminato")
                .OrderBy(f => f.ID_Operatore)
                .ToList()
                .Select(f =>
                {
                    var conto = db.DatiBancari
                                  .Where(b => b.ID_Cliente == f.ID_Operatore && b.Stato == "Attivo")
                                  .OrderByDescending(b => b.DataInserimento)
                                  .FirstOrDefault();

                    return new AziendaViewModel
                    {
                        ID_Azienda = f.ID_Operatore,
                        Nome = f.Nome,
                        TipoRagioneSociale = f.TipoRagioneSociale,
                        PartitaIVA = f.PIVA,
                        CodiceFiscale = f.CodiceFiscale,
                        CodiceUnivoco = f.CodiceUnivoco,
                        Indirizzo = f.Indirizzo,
                        Telefono = f.Telefono,
                        Email = f.MAIL1,
                        PEC = f.MAIL2,
                        SitoWEB = f.SitoWEB,
                        Citta = f.ID_Citta.HasValue ? db.Citta.FirstOrDefault(c => c.ID_BPCitta == f.ID_Citta)?.NameLocalita : "-",
                        Nazione = f.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == f.ID_Nazione)?.NameNazione : "-",
                        Stato = f.Stato,
                        Note = f.Note,
                        DescrizioneAttivita = f.DescrizioneAttivita,
                        NomeBanca = conto?.NomeBanca,
                        IBAN = conto?.IBAN,
                        Intestatario = conto?.Intestatario,
                        BIC_SWIFT = conto?.BIC_SWIFT,
                        NoteBancarie = conto?.Note,
                        NomeSettoreFornitore = f.ID_SettoreFornitore.HasValue
                            ? db.SettoriFornitori.FirstOrDefault(s => s.ID_Settore == f.ID_SettoreFornitore)?.Nome
                            : "-"
                    };
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Fornitori/ReportFornitoriPdf.cshtml", fornitori)
            {
                FileName = $"Fornitori_{DateTime.Today:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }


        #endregion

        #region GESTIONE PROFESSIONISTI
        public ActionResult GestioneProfessionisti()
        {
            return View("~/Views/Professionisti/GestioneProfessionisti.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneProfessionistiList(string nomeFiltro = "", string statoFiltro = "Tutti")
        {
            var debugLog = new List<string>();
            var idUtente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);

            IQueryable<OperatoriSinergia> professionistiQuery;

            if (utenteCorrente?.TipoUtente == "Admin")
            {
                professionistiQuery = db.OperatoriSinergia.Where(x => x.TipoCliente == "Professionista");
            }
            else if (utenteCorrente?.TipoUtente == "Professionista")
            {
                professionistiQuery = db.OperatoriSinergia.Where(x =>
                    x.TipoCliente == "Professionista" &&
                    x.ID_UtenteCollegato == utenteCorrente.ID_Utente);
            }
            else if (utenteCorrente?.TipoUtente == "Collaboratore")
            {
                professionistiQuery = from rel in db.RelazioneUtenti
                                      join cli in db.OperatoriSinergia on rel.ID_Utente equals cli.ID_Operatore
                                      where rel.ID_UtenteAssociato == utenteCorrente.ID_Utente
                                            && cli.TipoCliente == "Professionista"
                                            && cli.Stato != "Eliminato"
                                      select cli;
            }
            else
            {
                professionistiQuery = db.OperatoriSinergia.Where(x => false);
            }

            // 🔍 Filtro nome
            if (!string.IsNullOrEmpty(nomeFiltro))
            {
                professionistiQuery = professionistiQuery.Where(x => x.Nome.Contains(nomeFiltro));
            }

            // 🔍 Filtro stato
            if (statoFiltro == "Attivi")
            {
                professionistiQuery = professionistiQuery.Where(x => x.Stato == "Attivo");
            }
            else if (statoFiltro == "Inattivi")
            {
                professionistiQuery = professionistiQuery.Where(x => x.Stato == "Inattivo");
            }

            var professionisti = professionistiQuery.OrderBy(x => x.ID_Operatore).ToList();

            // ✅ Carica i permessi
            PermessiViewModel permessiUtente = new PermessiViewModel
            {
                ID_Utente = idUtente,
                Permessi = new List<PermessoSingoloViewModel>()
            };

            if (utenteCorrente?.TipoUtente == "Admin")
            {
                permessiUtente.Permessi.Add(new PermessoSingoloViewModel
                {
                    ID_Menu = 0,
                    NomeMenu = "Tutti",
                    Aggiungi = true,
                    Modifica = true,
                    Elimina = true
                });
            }
            else if (utenteCorrente != null)
            {
                var permessiDb = (from p in db.Permessi
                                  join m in db.Menu on p.ID_Menu equals m.ID_Menu
                                  where p.ID_Utente == utenteCorrente.ID_Utente
                                  select new PermessoSingoloViewModel
                                  {
                                      ID_Menu = p.ID_Menu,
                                      NomeMenu = m.NomeMenu,
                                      Aggiungi = p.Aggiungi ?? false,
                                      Modifica = p.Modifica ?? false,
                                      Elimina = p.Elimina ?? false
                                  }).ToList();

                permessiUtente.Permessi.AddRange(permessiDb);
            }

            ViewBag.Permessi = permessiUtente;
            ViewBag.UtenteCorrente = utenteCorrente;
            ViewBag.IsAdmin = utenteCorrente?.TipoUtente == "Admin";

            // 👤 Se l'utente è un collaboratore, trova il nome del professionista assegnato
            string nomeProfessionistaAssegnato = "";
            if (utenteCorrente?.TipoUtente == "Collaboratore")
            {
                var relazione = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == utenteCorrente.ID_Utente)
                    .Select(r => r.ID_Utente)
                    .FirstOrDefault();

                var prof = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == relazione && c.TipoCliente == "Professionista");
                if (prof != null)
                    nomeProfessionistaAssegnato = prof.Nome + " " + prof.Cognome;
            }

            var viewModel = professionisti.Select(p =>
            {
                var conto = db.DatiBancari
                              .Where(b => b.ID_Cliente == p.ID_Operatore && b.Stato == "Attivo")
                              .OrderByDescending(b => b.DataInserimento)
                              .FirstOrDefault();

                return new ProfessionistiViewModel
                {
                    ID_Professionista = p.ID_Operatore,
                    Nome = p.Nome,
                    Cognome= p.Cognome,
                    TipoRagioneSociale = p.TipoRagioneSociale,
                    ProfessionistaAssegnatoDaCollaboratore = nomeProfessionistaAssegnato,
                    PartitaIVA = p.PIVA,
                    CodiceFiscale = p.CodiceFiscale,
                    CodiceUnivoco = p.CodiceUnivoco,
                    Indirizzo = p.Indirizzo,
                    Telefono = p.Telefono,
                    Email = p.MAIL1,
                    Citta = p.ID_Citta.HasValue ? db.Citta.FirstOrDefault(c => c.ID_BPCitta == p.ID_Citta)?.NameLocalita : null,
                    Nazione = p.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == p.ID_Nazione)?.NameNazione : null,
                    Stato = p.Stato,
                    Note = p.Note,
                    DescrizioneAttivita = p.DescrizioneAttivita,
                    UtentiAssociati = db.RelazioneUtenti.Count(r => r.ID_Utente == p.ID_Operatore && r.Stato == "Attivo"),
                    HaUtentiAssegnati = db.RelazioneUtenti.Any(r => r.ID_Utente == p.ID_Operatore && r.Stato == "Attivo"),
                    NomeBanca = conto?.NomeBanca,
                    IBAN = conto?.IBAN,
                    Intestatario = conto?.Intestatario,
                    ÈCliente = p.ECliente,
                    ÈFornitore = p.EFornitore,
                    PuoModificare = permessiUtente.Permessi.Any(x => x.Modifica),
                    PuoEliminare = permessiUtente.Permessi.Any(x => x.Elimina),
                    UtenteCorrenteHaPermessi =
                        (utenteCorrente?.TipoUtente == "Admin") ||
                        (permessiUtente.PuòAggiungere || permessiUtente.PuòModificare || permessiUtente.PuòEliminare)
                };
            }).ToList();

            return PartialView("~/Views/Professionisti/_GestioneProfessionistiList.cshtml", viewModel);
        }


        [HttpPost]
        public ActionResult CreaProfessionista()
        {
            int? utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId == null || utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                var nome = Request.Form["Nome"];
                var cognome = Request.Form["Cognome"];

                var utenteCollegato = db.Utenti.FirstOrDefault(u =>
                    u.Nome == nome &&
                    u.Cognome == cognome &&
                    u.TipoUtente == "Professionista");

                if (utenteCollegato == null)
                {
                    return Json(new { success = false, message = "⚠️ Utente professionista non trovato. Assicurati che sia stato creato prima nella sezione Utenti." });
                }

                var idUtenteCollegato = utenteCollegato.ID_Utente;

                var tipoUtenteLoggato = db.Utenti
                    .Where(u => u.ID_Utente == utenteId)
                    .Select(u => u.TipoUtente)
                    .FirstOrDefault();

                // ==========================
                // 🔎 VALIDAZIONE CODICE FISCALE SOLO PER PROFESSIONISTI
                // ==========================
                var codiceFiscale = Request.Form["CodiceFiscale"];
                if (!string.IsNullOrWhiteSpace(codiceFiscale))
                {
                    codiceFiscale = codiceFiscale.ToUpper().Trim();

                    // ✅ Se è un professionista → deve avere 16 caratteri
                    if (codiceFiscale.Length != 16)
                    {
                        return Json(new { success = false, message = "❌ Il Codice Fiscale deve avere esattamente 16 caratteri per i professionisti." });
                    }
                }
                // (Se fosse un Fornitore → nessun controllo di lunghezza, lo lasciamo libero)

                var model = new OperatoriSinergia
                {
                    TipoCliente = "Professionista",
                    Nome = nome,
                    Cognome = cognome,
                    ID_Professione = string.IsNullOrEmpty(Request.Form["ID_Professione"]) ? (int?)null : int.Parse(Request.Form["ID_Professione"]),
                    PIVA = Request.Form["PartitaIVA"],
                    CodiceFiscale = codiceFiscale, // 🔹 già validato e messo maiuscolo
                    CodiceUnivoco = Request.Form["CodiceUnivoco"],
                    Indirizzo = Request.Form["Indirizzo"],
                    ID_Citta = string.IsNullOrEmpty(Request.Form["ID_Citta"]) ? (int?)null : int.Parse(Request.Form["ID_Citta"]),
                    ID_Nazione = string.IsNullOrEmpty(Request.Form["ID_Nazione"]) ? (int?)null : int.Parse(Request.Form["ID_Nazione"]),
                    Telefono = Request.Form["Telefono"],
                    MAIL1 = Request.Form["Email"],
                    MAIL2 = Request.Form["MAIL2"],
                    SitoWEB = Request.Form["SitoWEB"],
                    Stato = Request.Form["Stato"],
                    DescrizioneAttivita = Request.Form["DescrizioneAttivita"],
                    Note = Request.Form["Note"],
                    TipoProfessionista = Request.Form["TipoProfessionista"],
                    PuòGestirePermessi = !string.IsNullOrEmpty(Request.Form["PuoGestirePermessi"]) && Request.Form["PuoGestirePermessi"].ToLower().Contains("true"),
                    DataCreazione = DateTime.Now,
                    ID_UtenteCreatore = utenteId,
                    ID_UtenteCollegato = idUtenteCollegato,

                    // Forzatura dei flag
                    ECliente = true,
                    EFornitore = false,

                    ID_Owner = tipoUtenteLoggato == "Professionista" ? utenteId : (int?)null
                };

                db.OperatoriSinergia.Add(model);
                db.SaveChanges();

                var archivio = new OperatoriSinergia_a
                {
                    ID_OperatoreOriginale = model.ID_Operatore,
                    TipoCliente = model.TipoCliente,
                    Nome = model.Nome,
                    Cognome = model.Cognome,
                    ID_Professione = model.ID_Professione,
                    PIVA = model.PIVA,
                    CodiceFiscale = model.CodiceFiscale,
                    CodiceUnivoco = model.CodiceUnivoco,
                    Indirizzo = model.Indirizzo,
                    ID_Citta = model.ID_Citta,
                    ID_Nazione = model.ID_Nazione,
                    Telefono = model.Telefono,
                    MAIL1 = model.MAIL1,
                    MAIL2 = model.MAIL2,
                    SitoWEB = model.SitoWEB,
                    Stato = model.Stato,
                    DescrizioneAttivita = model.DescrizioneAttivita,
                    Note = model.Note,
                    TipoProfessionista = model.TipoProfessionista,
                    PuòGestirePermessi = model.PuòGestirePermessi,
                    DataCreazione = model.DataCreazione,
                    ID_UtenteCreatore = model.ID_UtenteCreatore,
                    ID_UtenteCollegato = model.ID_UtenteCollegato,
                    ECliente = model.ECliente,
                    EFornitore = model.EFornitore,
                    ID_Owner = model.ID_Owner,
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = utenteId,
                    ModificheTestuali = "Inserimento iniziale"
                };

                db.OperatoriSinergia_a.Add(archivio);
                db.SaveChanges();

                // ===============================
                // 🔹 CREA RICORRENZA TRATTENUTA SINERGIA
                // ===============================
                try
                {
                    var tipoTrattenuta = db.TipologieCosti
                        .FirstOrDefault(t => t.Nome.Contains("Trattenuta Sinergia") && t.Stato == "Attivo");

                    if (tipoTrattenuta != null)
                    {
                        bool esisteGia = db.RicorrenzeCosti
                            .Any(r => r.ID_Professionista == idUtenteCollegato && r.Categoria == "Trattenuta Sinergia");

                        if (!esisteGia)
                        {
                            var ric = new RicorrenzeCosti
                            {
                                ID_TipoCostoGenerale = tipoTrattenuta.ID_TipoCosto,
                                ID_Professionista = idUtenteCollegato,
                                Categoria = "Trattenuta Sinergia",
                                TipoValore = "Percentuale",
                                Valore = tipoTrattenuta.ValorePercentuale ?? 20,
                                Attivo = true,
                                ID_UtenteCreatore = utenteId,
                                ID_UtenteUltimaModifica = utenteId,
                                DataCreazione = DateTime.Now,
                                DataUltimaModifica = DateTime.Now
                            };
                            db.RicorrenzeCosti.Add(ric);
                            db.SaveChanges();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"⚠️ [CreaProfessionista] Errore creazione Trattenuta: {ex.Message}");
                }

                if (!string.IsNullOrEmpty(Request.Form["IBAN"]) && !string.IsNullOrEmpty(Request.Form["NomeBanca"]))
                {
                    var datiBancari = new DatiBancari
                    {
                        ID_Cliente = model.ID_Operatore,
                        NomeBanca = Request.Form["NomeBanca"],
                        IBAN = Request.Form["IBAN"],
                        Intestatario = Request.Form["Intestatario"],
                        BIC_SWIFT = Request.Form["BIC_SWIFT"],
                        IndirizzoBanca = Request.Form["IndirizzoBanca"],
                        Note = Request.Form["NoteBanca"],
                        Stato = "Attivo",
                        DataInserimento = DateTime.Now,
                        ID_UtenteCreatore = utenteId
                    };

                    db.DatiBancari.Add(datiBancari);
                    db.SaveChanges();
                }

                // ==========================
                // 🔹 Gestione documenti caricati (nuova tabella DocumentiProfessionisti)
                // ==========================
                if (Request.Files != null && Request.Files.Count > 0)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        var file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            var documento = new DocumentiProfessionisti
                            {
                                ID_Professionista = model.ID_Operatore,
                                NomeDocumento = file.FileName,
                                TipoMime = file.ContentType,
                                DataCaricamento = DateTime.Now,
                                ID_UtenteCaricamento = utenteId ?? 0
                            };

                            using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                            {
                                documento.FileContent = binaryReader.ReadBytes(file.ContentLength);
                            }

                            db.DocumentiProfessionisti.Add(documento);
                        }
                    }

                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Professionista creato con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore inatteso: " + ex.Message });
            }
        }
        [HttpPost]
        public ActionResult ModificaProfessionista()
        {
            int? utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId == null || utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                int idProfessionista = int.Parse(Request.Form["ID_Professionista"] ?? "0");
                if (idProfessionista == 0)
                    return Json(new { success = false, message = "ID professionista non valido." });

                var originale = db.OperatoriSinergia
                    .FirstOrDefault(c => c.ID_Operatore == idProfessionista && c.TipoCliente == "Professionista");
                if (originale == null)
                    return Json(new { success = false, message = "Professionista non trovato." });

                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == utenteId);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                if (utente.TipoUtente != "Admin" && originale.ID_Owner != utenteId)
                    return Json(new { success = false, message = "Non hai i permessi per modificare questo professionista." });

                // ==========================
                // 🔎 VALIDAZIONE CODICE FISCALE
                // ==========================
                var codiceFiscale = Request.Form["CodiceFiscale"];
                if (!string.IsNullOrWhiteSpace(codiceFiscale))
                {
                    codiceFiscale = codiceFiscale.ToUpper().Trim();

                    if (originale.TipoCliente == "Professionista" && codiceFiscale.Length != 16)
                    {
                        return Json(new { success = false, message = "❌ Il Codice Fiscale deve avere 16 caratteri per i professionisti." });
                    }
                }

                var modifiche = new List<string>();
                void Confronta(string nomeCampo, object valO, object valN)
                {
                    if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                        modifiche.Add($"{nomeCampo}: '{valO}' → '{valN}'");
                }

                // Confronta i campi modificabili
                Confronta("Nome", originale.Nome, Request.Form["Nome"]);
                Confronta("Cognome", originale.Cognome, Request.Form["Cognome"]);
                Confronta("PIVA", originale.PIVA, Request.Form["PartitaIVA"]);
                Confronta("Codice Fiscale", originale.CodiceFiscale, codiceFiscale);
                Confronta("Codice Univoco", originale.CodiceUnivoco, Request.Form["CodiceUnivoco"]);
                Confronta("Telefono", originale.Telefono, Request.Form["Telefono"]);
                Confronta("MAIL1", originale.MAIL1, Request.Form["MAIL1"]);
                Confronta("MAIL2", originale.MAIL2, Request.Form["MAIL2"]);
                Confronta("SitoWEB", originale.SitoWEB, Request.Form["SitoWEB"]);
                Confronta("Indirizzo", originale.Indirizzo, Request.Form["Indirizzo"]);
                Confronta("Descrizione Attività", originale.DescrizioneAttivita, Request.Form["DescrizioneAttivita"]);
                Confronta("Note", originale.Note, Request.Form["Note"]);
                Confronta("Stato", originale.Stato, Request.Form["Stato"]);
                Confronta("TipoProfessionista", originale.TipoProfessionista, Request.Form["TipoProfessionista"]);
                Confronta("ID_Professione", originale.ID_Professione, Request.Form["ID_Professione"]);
                Confronta("ID_Citta", originale.ID_Citta, Request.Form["ID_CittaResidenza"]);
                Confronta("ID_Nazione", originale.ID_Nazione, Request.Form["ID_Nazione"]);

                if (modifiche.Any())
                {
                    int maxVersion = db.OperatoriSinergia_a
                        .AsNoTracking()
                        .Where(a => a.ID_OperatoreOriginale == originale.ID_Operatore)
                        .Select(a => (int?)a.NumeroVersione)
                        .Max() ?? 0;

                    int nuovaVersione = maxVersion + 1;

                    db.OperatoriSinergia_a.Add(new OperatoriSinergia_a
                    {
                        ID_OperatoreOriginale = originale.ID_Operatore,
                        TipoCliente = originale.TipoCliente,
                        Nome = originale.Nome,
                        Cognome = originale.Cognome,
                        ID_Professione = originale.ID_Professione,
                        CodiceFiscale = originale.CodiceFiscale,
                        PIVA = originale.PIVA,
                        CodiceUnivoco = originale.CodiceUnivoco,
                        Indirizzo = originale.Indirizzo,
                        ID_Citta = originale.ID_Citta,
                        ID_Nazione = originale.ID_Nazione,
                        Telefono = originale.Telefono,
                        MAIL1 = originale.MAIL1,
                        MAIL2 = originale.MAIL2,
                        SitoWEB = originale.SitoWEB,
                        Stato = originale.Stato,
                        DescrizioneAttivita = originale.DescrizioneAttivita,
                        Note = originale.Note,
                        TipoProfessionista = originale.TipoProfessionista,
                        ECliente = originale.ECliente,
                        EFornitore = originale.EFornitore,
                        ID_UtenteCollegato = originale.ID_UtenteCollegato,
                        ID_UtenteCreatore = originale.ID_UtenteCreatore,
                        ID_Owner = originale.ID_Owner,
                        DataCreazione = originale.DataCreazione,
                        NumeroVersione = nuovaVersione,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = utenteId,
                        ModificheTestuali =
                            $"Modifica effettuata su ID_Professionista = {originale.ID_Operatore} da ID_UtenteModificatore = {utenteId} il {DateTime.Now:g}.\nModifiche:\n- " +
                            string.Join("\n- ", modifiche)
                    });
                }

                // ==========================
                // 🔹 Aggiorna i campi
                // ==========================
                originale.Nome = Request.Form["Nome"];
                originale.Cognome = Request.Form["Cognome"];
                originale.ID_Professione = int.TryParse(Request.Form["ID_Professione"], out int idParsed) ? (int?)idParsed : null;
                originale.PIVA = Request.Form["PartitaIVA"];
                originale.CodiceFiscale = codiceFiscale;
                originale.CodiceUnivoco = Request.Form["CodiceUnivoco"];
                originale.Telefono = Request.Form["Telefono"];
                originale.MAIL1 = Request.Form["MAIL1"];
                originale.MAIL2 = Request.Form["MAIL2"];
                originale.SitoWEB = Request.Form["SitoWEB"];
                originale.Indirizzo = Request.Form["Indirizzo"];
                originale.DescrizioneAttivita = Request.Form["DescrizioneAttivita"];
                originale.Note = Request.Form["Note"];
                originale.Stato = Request.Form["Stato"];
                originale.TipoProfessionista = Request.Form["TipoProfessionista"];
                originale.PuòGestirePermessi = Request.Form["PuoGestirePermessi"]?.ToLower() == "true";
                originale.ID_Citta = int.TryParse(Request.Form["ID_CittaResidenza"], out int idCitta) ? (int?)idCitta : null;
                originale.ID_Nazione = int.TryParse(Request.Form["ID_Nazione"], out int idNazione) ? (int?)idNazione : null;
                originale.ECliente = true;
                originale.EFornitore = false;
                originale.UltimaModifica = DateTime.Now;
                originale.ID_UtenteUltimaModifica = utenteId;

                if (!originale.ID_Owner.HasValue && utente.TipoUtente == "Professionista")
                    originale.ID_Owner = utenteId;

                // ==========================
                // 🔹 Gestione documenti (nuova tabella DocumentiProfessionisti)
                // ==========================
                if (Request.Files != null && Request.Files.Count > 0)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        var file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            var documento = new DocumentiProfessionisti
                            {
                                ID_Professionista = originale.ID_Operatore,
                                NomeDocumento = file.FileName,
                                TipoMime = file.ContentType,
                                DataCaricamento = DateTime.Now,
                                ID_UtenteCaricamento = utenteId ?? 0
                            };

                            using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                            {
                                documento.FileContent = binaryReader.ReadBytes(file.ContentLength);
                            }

                            db.DocumentiProfessionisti.Add(documento);

                            // 🔹 archivio documento
                            db.DocumentiProfessionisti_a.Add(new DocumentiProfessionisti_a
                            {
                                ID_Professionista = originale.ID_Operatore,
                                NomeDocumento = documento.NomeDocumento,
                                TipoMime = documento.TipoMime,
                                FileContent = documento.FileContent,
                                DataCaricamento = documento.DataCaricamento,
                                ID_UtenteCaricamento = documento.ID_UtenteCaricamento,
                                NumeroVersione = 1,
                                DataArchiviazione = DateTime.Now,
                                ID_UtenteArchiviazione = (int)utenteId,
                                ModificheTestuali = "Inserimento documento in modifica professionista"
                            });
                        }
                    }
                }

                db.SaveChanges();

                // =====================================================
                // 🔁 CREA RICORRENZA MANCANTE "TRATTENUTA SINERGIA"
                // =====================================================
                try
                {
                    if (originale.TipoCliente == "Professionista" && originale.ID_UtenteCollegato.HasValue)
                    {
                        int idUtenteProfessionista = originale.ID_UtenteCollegato.Value;
                        int idUtenteCorrente = utenteId ?? 0;
                        DateTime now = DateTime.Now;

                        bool esisteTrattenuta = db.RicorrenzeCosti
                            .Any(r => r.ID_Professionista == idUtenteProfessionista && r.Categoria == "Trattenuta Sinergia");

                        if (!esisteTrattenuta)
                        {
                            var tipoTrattenuta = db.TipologieCosti
                                .FirstOrDefault(t => t.Nome.Contains("Trattenuta Sinergia") && t.Stato == "Attivo");

                            if (tipoTrattenuta != null)
                            {
                                var ric = new RicorrenzeCosti
                                {
                                    ID_TipoCostoGenerale = tipoTrattenuta.ID_TipoCosto,
                                    ID_Professionista = idUtenteProfessionista,
                                    Categoria = "Trattenuta Sinergia",
                                    TipoValore = "Percentuale",
                                    Valore = tipoTrattenuta.ValorePercentuale ?? 20,
                                    Attivo = true,
                                    ID_UtenteCreatore = idUtenteCorrente,
                                    ID_UtenteUltimaModifica = idUtenteCorrente,
                                    DataCreazione = now,
                                    DataUltimaModifica = now
                                };

                                db.RicorrenzeCosti.Add(ric);
                                db.SaveChanges();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"⚠️ [ModificaProfessionista] Errore creazione ricorrenza Trattenuta Sinergia: {ex.Message}");
                }

                return Json(new { success = true, message = "Professionista modificato correttamente (ricorrenza verificata)." });
            
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore inatteso: " + ex.Message });
            }
        }



        [HttpGet]
        public ActionResult GetProfessionista(int id)
        {
            if (id == 0)
                return Json(null, JsonRequestBehavior.AllowGet);

            var professionista = db.OperatoriSinergia
                .FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Professionista");
            if (professionista == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            var conto = db.DatiBancari.FirstOrDefault(d => d.ID_Cliente == id && d.Stato == "Attivo");

            var nomeProfessione = professionista.ID_Professione.HasValue
                ? db.Professioni.FirstOrDefault(p => p.ProfessioniID == professionista.ID_Professione)?.Descrizione
                : null;

            string nomeOwner = null;
            if (professionista.ID_Owner.HasValue)
            {
                var owner = db.Utenti.FirstOrDefault(u => u.ID_Utente == professionista.ID_Owner.Value);
                if (owner != null)
                    nomeOwner = owner.Nome + " " + owner.Cognome;
            }

            // 🔐 Logica autorizzazione modifica
            int idUtente = UserManager.GetIDUtenteCollegato();
            string tipoUtente = UserManager.GetTipoUtente();
            bool puòModificare = false;

            if (tipoUtente == "Admin")
                puòModificare = true;
            else if (tipoUtente == "Professionista" && professionista.ID_Owner == idUtente)
                puòModificare = true;

            // ✅ Carico documenti dal DB mappandoli sul ViewModel
            var documenti = db.DocumentiProfessionisti
                .Where(d => d.ID_Professionista == professionista.ID_Operatore)
                .Select(d => new DocumentoProfessionistaViewModel
                {
                    ID_Documento = d.ID_Documento,
                    NomeDocumento = d.NomeDocumento,
                    TipoMime = d.TipoMime,
                    DataCaricamento = d.DataCaricamento,
                    UtenteCaricamento = db.Utenti
                        .Where(u => u.ID_Utente == d.ID_UtenteCaricamento)
                        .Select(u => u.Nome + " " + u.Cognome)
                        .FirstOrDefault()
                })
                .ToList();

            var viewModel = new ProfessionistiViewModel
            {
                ID_Professionista = professionista.ID_Operatore,
                Nome = professionista.Nome,
                Cognome = professionista.Cognome,
                ID_Professione = professionista.ID_Professione ?? 0,
                NomePractice = nomeProfessione,
                PuòGestirePermessi = professionista.PuòGestirePermessi ?? false,

                PartitaIVA = professionista.PIVA,
                CodiceFiscale = professionista.CodiceFiscale,
                CodiceUnivoco = professionista.CodiceUnivoco,
                Indirizzo = professionista.Indirizzo,
                Telefono = professionista.Telefono,

                MAIL1 = professionista.MAIL1,
                MAIL2 = professionista.MAIL2,

                ID_Citta = professionista.ID_Citta ?? 0,
                ID_Nazione = professionista.ID_Nazione ?? 0,

                Citta = professionista.ID_Citta.HasValue
                    ? db.Citta.FirstOrDefault(c => c.ID_BPCitta == professionista.ID_Citta)?.NameLocalita
                    : null,

                Nazione = professionista.ID_Nazione.HasValue
                    ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == professionista.ID_Nazione)?.NameNazione
                    : null,

                Stato = professionista.Stato,
                Note = professionista.Note,
                DescrizioneAttivita = professionista.DescrizioneAttivita,
                TipoProfessionista = professionista.TipoProfessionista,
                ÈCliente = professionista.ECliente,
                ÈFornitore = professionista.EFornitore,

                IBAN = conto?.IBAN,
                NomeBanca = conto?.NomeBanca,
                Intestatario = conto?.Intestatario,
                BIC_SWIFT = conto?.BIC_SWIFT,
                IndirizzoBanca = conto?.IndirizzoBanca,
                NoteBanca = conto?.Note,

                NomeOwner = nomeOwner,
                UtenteCorrenteHaPermessi = puòModificare,

                // 🔹 qui aggiungiamo la lista documenti
                Documenti = documenti
            };

            return Json(viewModel, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaProfessionista(int id)
        {
            try
            {
                var professionista = db.OperatoriSinergia
                    .FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Professionista");
                if (professionista == null)
                    return Json(new { success = false, message = "Professionista non trovato." });

                int? utenteId = UserManager.GetIDUtenteCollegato();
                if (utenteId == null || utenteId <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                var tipoUtente = db.Utenti.FirstOrDefault(u => u.ID_Utente == utenteId)?.TipoUtente;
                bool autorizzato = tipoUtente == "Admin" || professionista.ID_Owner == utenteId;

                if (!autorizzato)
                    return Json(new { success = false, message = "Non hai i permessi per eliminare questo professionista." });

                // 🔎 Verifica relazioni attive
                var relazioniAttive = db.RelazioneUtenti
                    .Where(r => r.ID_UtenteAssociato == professionista.ID_Operatore && r.Stato == "Attivo")
                    .ToList();

                if (relazioniAttive.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "Impossibile disattivare il professionista perché ha ancora utenti assegnati. Rimuovi prima tutte le assegnazioni attive."
                    });
                }

                // 🔢 Calcola numero versione archivio
                int ultimaVersione = db.OperatoriSinergia_a
                    .Where(a => a.ID_OperatoreOriginale == professionista.ID_Operatore)
                    .Select(a => a.NumeroVersione)
                    .DefaultIfEmpty(0)
                    .Max();

                // Archiviazione versione precedente
                var archivio = new OperatoriSinergia_a
                {
                    ID_OperatoreOriginale = professionista.ID_Operatore,
                    TipoCliente = professionista.TipoCliente,
                    Nome = professionista.Nome,
                    Cognome = professionista.Cognome,
                    CodiceFiscale = professionista.CodiceFiscale,
                    PIVA = professionista.PIVA,
                    CodiceUnivoco = professionista.CodiceUnivoco,
                    Indirizzo = professionista.Indirizzo,
                    ID_Citta = professionista.ID_Citta,
                    ID_Nazione = professionista.ID_Nazione,
                    Telefono = professionista.Telefono,
                    MAIL1 = professionista.MAIL1,
                    MAIL2 = professionista.MAIL2,
                    SitoWEB = professionista.SitoWEB,
                    Stato = professionista.Stato,
                    DescrizioneAttivita = professionista.DescrizioneAttivita,
                    Note = professionista.Note,
                    ID_Professione = professionista.ID_Professione,
                    PuòGestirePermessi = professionista.PuòGestirePermessi,
                    ECliente = professionista.ECliente,
                    EFornitore = professionista.EFornitore,
                    ID_Owner = professionista.ID_Owner,
                    DataCreazione = professionista.DataCreazione,
                    ID_UtenteCreatore = professionista.ID_UtenteCreatore,
                    UltimaModifica = professionista.UltimaModifica,
                    ID_UtenteUltimaModifica = professionista.ID_UtenteUltimaModifica,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = utenteId,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"Eliminazione effettuata da ID_Utente = {utenteId} in data {DateTime.Now:g}"
                };
                db.OperatoriSinergia_a.Add(archivio);

                // 🗂 Archivia anche i documenti associati
                var documenti = db.DocumentiProfessionisti
                    .Where(d => d.ID_Professionista == professionista.ID_Operatore)
                    .ToList();

                foreach (var doc in documenti)
                {
                    db.DocumentiProfessionisti_a.Add(new DocumentiProfessionisti_a
                    {
                        ID_DocumentoOriginale = doc.ID_Documento,
                        ID_Professionista = doc.ID_Professionista,
                        NomeDocumento = doc.NomeDocumento,
                        TipoMime = doc.TipoMime,
                        FileContent = doc.FileContent,
                        DataCaricamento = doc.DataCaricamento,
                        ID_UtenteCaricamento = doc.ID_UtenteCaricamento,
                        NumeroVersione = 1, // se vuoi gestire versioning anche qui puoi calcolare come sopra
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = (int)utenteId,
                        ModificheTestuali = "Archiviazione automatica in fase di eliminazione professionista"
                    });
                }

                // Disattiva anche conto bancario (soft delete)
                var conto = db.DatiBancari.FirstOrDefault(c => c.ID_Cliente == professionista.ID_Operatore && c.Stato == "Attivo");
                if (conto != null)
                {
                    conto.Stato = "Inattivo";
                }

                // Soft delete professionista
                professionista.Stato = "Inattivo";
                professionista.UltimaModifica = DateTime.Now;
                professionista.ID_UtenteUltimaModifica = utenteId;

                db.SaveChanges();

                return Json(new { success = true, message = "Professionista disattivato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore inatteso: " + ex.Message });
            }
        }


        // ✅ Metodo per ottenere tutte le Practice dalla tabella Practice
        [HttpGet]
        public JsonResult GetProfessioni()
        {
            var professioni = db.Professioni
                .OrderBy(p => p.Descrizione)
                .Select(p => new
                {
                    ID_Professione = p.ProfessioniID,
                    Codice = p.Codice ?? "",
                    Descrizione = p.Descrizione ?? ""
                })
                .ToList();

            return Json(professioni, JsonRequestBehavior.AllowGet);
        }




        // ✅ Metodo per aggiungere una nuova categoria Practice
        [HttpPost]
        public ActionResult AggiungiProfessione(string codice, string descrizione)
        {
            if (string.IsNullOrWhiteSpace(descrizione))
            {
                return Json(new { success = false, message = "Descrizione non valida." });
            }

            if (string.IsNullOrWhiteSpace(codice))
            {
                return Json(new { success = false, message = "Codice non valido." });
            }

            // Verifica se esiste già una professione con lo stesso codice
            bool exists = db.Professioni.Any(p => p.Codice.ToLower() == codice.ToLower());
            if (exists)
            {
                return Json(new { success = false, message = "Codice professione già esistente." });
            }

            var nuovaProfessione = new Professioni
            {
                Codice = codice,
                Descrizione = descrizione,
                ID_ProfessionistaRiferimento = null // opzionale, o da settare se vuoi associarlo subito
            };

            db.Professioni.Add(nuovaProfessione);
            db.SaveChanges();

            return Json(new { success = true, message = "Professione aggiunta correttamente.", id = nuovaProfessione.ProfessioniID });
        }




        [HttpPost]
        public ActionResult RiattivaProfessionista(int id)
        {
            try
            {
                var professionista = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Operatore == id && c.TipoCliente == "Professionista");
                if (professionista == null)
                    return Json(new { success = false, message = "Professionista non trovato." });

                var utenteId = (int?)Session["ID_Utente"];

                // Riattiva il professionista
                professionista.Stato = "Attivo";
                professionista.UltimaModifica = DateTime.Now;
                professionista.ID_UtenteUltimaModifica = utenteId;

                // Riattiva anche il conto bancario, se presente e disattivato
                var conto = db.DatiBancari.FirstOrDefault(c => c.ID_Cliente == professionista.ID_Operatore && c.Stato == "Inattivo");
                if (conto != null)
                {
                    conto.Stato = "Attivo";
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Professionista riattivato correttamente!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore inatteso: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult AssegnaUtentiProfessionista(int idProfessionista, int idUtente, string tipoRelazione, string statoRelazione, string noteRelazione)
        {
            try
            {
                var relazioneEsistente = db.RelazioneUtenti.FirstOrDefault(r =>
                     r.ID_Utente == idUtente &&
                     r.ID_UtenteAssociato == idProfessionista &&
                     r.Stato == "Attivo");

                if (relazioneEsistente != null)
                {
                    relazioneEsistente.TipoRelazione = tipoRelazione;
                    relazioneEsistente.Note = noteRelazione;
                    relazioneEsistente.UltimaModifica = DateTime.Now;
                    relazioneEsistente.ID_UtenteUltimaModifica = UserManager.GetIDUtenteCollegato();

                    db.SaveChanges();
                    return Json(new { success = true, message = "Relazione aggiornata correttamente." });
                }

                var nuovaRelazione = new RelazioneUtenti
                {
                    ID_Utente = idProfessionista,
                    ID_UtenteAssociato = idUtente,
                    TipoRelazione = tipoRelazione,
                    DataInizio = DateTime.Now,
                    Stato = statoRelazione,
                    Note = string.IsNullOrEmpty(noteRelazione) ? null : noteRelazione,
                    ID_UtenteCreatore = UserManager.GetIDUtenteCollegato(),
                    UltimaModifica = DateTime.Now
                };

                db.RelazioneUtenti.Add(nuovaRelazione);
                db.SaveChanges();

                return Json(new { success = true, message = "Utente assegnato correttamente al professionista!" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null)
                    inner = inner.InnerException;

                return Json(new
                {
                    success = false,
                    message = $"Errore durante l'assegnazione: {inner.Message}"
                });
            }
        }

        [HttpGet]
        public ActionResult VisualizzaUtentiProfessionista(int idProfessionista)
        {
            var utentiAssegnati = (from r in db.RelazioneUtenti
                                   join u in db.Utenti on r.ID_UtenteAssociato equals u.ID_Utente
                                   where r.ID_Utente == idProfessionista && r.Stato == "Attivo"
                                   select new
                                   {
                                       r.ID_Relazione,
                                       u.ID_Utente,
                                       u.Nome,
                                       u.Cognome,
                                       r.TipoRelazione,
                                       r.Note,
                                       r.DataInizio
                                   }).ToList()
                                   .Select(r => new
                                   {
                                       r.ID_Relazione,
                                       r.ID_Utente,
                                       r.Nome,
                                       r.Cognome,
                                       r.TipoRelazione,
                                       r.Note,
                                       DataInizio = r.DataInizio.ToString("yyyy-MM-ddTHH:mm:ss")
                                   }).ToList();

            return Json(utentiAssegnati, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult RimuoviUtenteProfessionista(int idRelazione)
        {
            try
            {
                var relazione = db.RelazioneUtenti.FirstOrDefault(r => r.ID_Relazione == idRelazione);
                if (relazione == null)
                    return Json(new { success = false, message = "Relazione non trovata." });

                // Archiviamo prima
                var archivio = new RelazioneUtenti_a
                {
                    ID_Utente = relazione.ID_Utente,
                    ID_UtenteAssociato = relazione.ID_UtenteAssociato,
                    TipoRelazione = relazione.TipoRelazione,
                    DataInizio = relazione.DataInizio,
                    DataFine = DateTime.Now,
                    Stato = "Terminato",
                    Note = relazione.Note,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = UserManager.GetIDUtenteCollegato()
                };
                db.RelazioneUtenti_a.Add(archivio);

                // Disattiva la relazione attiva
                db.RelazioneUtenti.Remove(relazione);
                db.SaveChanges();

                return Json(new { success = true, message = "Utente rimosso correttamente." });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Errore durante la rimozione: " + inner });
            }
        }

        [HttpGet]
        public ActionResult GetUtentiDisponibiliProfessionista(int idProfessionista)
        {
            try
            {
                // Utenti già associati al professionista
                var utentiAssegnatiIds = db.RelazioneUtenti
                    .Where(r => r.ID_Utente == idProfessionista && r.Stato == "Attivo")
                    .Select(r => r.ID_UtenteAssociato)
                    .ToList();

                // Solo collaboratori, attivi, non ancora assegnati a questo professionista
                var utentiDisponibili = db.Utenti
                    .Where(u =>
                        u.TipoUtente == "Collaboratore" &&
                        u.Stato == "Attivo" &&
                        !utentiAssegnatiIds.Contains(u.ID_Utente))
                    .Select(u => new
                    {
                        u.ID_Utente,
                        NomeUtente = u.Nome + " " + u.Cognome
                    })
                    .OrderBy(u => u.NomeUtente)
                    .ToList();

                return Json(utentiDisponibili, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Errore: {ex.Message}"
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetDocumentiProfessionista(int idProfessionista)
        {
            var docs = db.DocumentiProfessionisti
                .Where(d => d.ID_Professionista == idProfessionista)
                .OrderByDescending(d => d.DataCaricamento)
                .Select(d => new {
                    d.ID_Documento,
                    d.NomeDocumento,
                    d.TipoMime,
                    d.DataCaricamento,
                    UtenteCaricamento = db.Utenti
                        .Where(u => u.ID_Utente == d.ID_UtenteCaricamento)
                        .Select(u => u.Nome + " " + u.Cognome)
                        .FirstOrDefault()
                })
                .ToList() // 🔹 qui i dati sono in memoria
                .Select(d => new {
                    d.ID_Documento,
                    d.NomeDocumento,
                    d.TipoMime,
                    DataCaricamento = d.DataCaricamento.ToString("dd/MM/yyyy HH:mm"), // ✅ formattato lato C#
                    d.UtenteCaricamento
                })
                .ToList();

            return Json(new { success = true, documenti = docs }, JsonRequestBehavior.AllowGet);
        }


        [HttpGet]
        public FileResult DownloadDocumentoProfessionista(int id)
        {
            var doc = db.DocumentiProfessionisti.FirstOrDefault(d => d.ID_Documento == id);
            if (doc == null) return null;

            return File(doc.FileContent, doc.TipoMime, doc.NomeDocumento);
        }


        [HttpPost]
        public ActionResult CaricaDocumentiDirettiProfessionista(int idProfessionista)
        {
            var utenteId = (int?)Session["ID_Utente"];
            if (utenteId == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                if (Request.Files != null && Request.Files.Count > 0)
                {
                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        var file = Request.Files[i];
                        if (file != null && file.ContentLength > 0)
                        {
                            var documento = new DocumentiAziende
                            {
                                ID_Cliente = idProfessionista,
                                NomeDocumento = file.FileName,
                                TipoMime = file.ContentType,
                                DataCaricamento = DateTime.Now,
                                ID_UtenteCaricamento = utenteId
                            };

                            using (var binaryReader = new System.IO.BinaryReader(file.InputStream))
                            {
                                documento.FileContent = binaryReader.ReadBytes(file.ContentLength);
                            }

                            db.DocumentiAziende.Add(documento);
                        }
                    }

                    db.SaveChanges();
                    return Json(new { success = true, message = "Documenti caricati correttamente!" });
                }

                return Json(new { success = false, message = "Nessun file selezionato." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult EliminaDocumentoProfessionista(int id)
        {
            try
            {
                var documento = db.DocumentiAziende.FirstOrDefault(d => d.ID_Documento == id);
                if (documento == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                db.DocumentiAziende.Remove(documento);
                db.SaveChanges();

                return Json(new { success = true, message = "Documento eliminato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult EsportaProfessionistiCsv()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null || utenteCorrente.TipoUtente != "Admin")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            var professionisti = db.OperatoriSinergia
                .Where(p => p.TipoCliente == "Professionista" && p.Stato != "Eliminato")
                .OrderBy(p => p.Nome)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("ID;Nome;Ragione Sociale;Partita IVA;Codice Fiscale;Email;Telefono;Stato");

            foreach (var p in professionisti)
            {
                sb.AppendLine($"{p.ID_Operatore};" +
                              $"{p.Nome};" +
                              $"{(string.IsNullOrWhiteSpace(p.TipoRagioneSociale) ? "-" : p.TipoRagioneSociale)};" +
                              $"{p.PIVA};" +
                              $"{p.CodiceFiscale};" +
                              $"{p.MAIL1};" +
                              $"{p.Telefono};" +
                              $"{p.Stato}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Professionisti_{DateTime.Today:yyyyMMdd}.csv");
        }

        [HttpGet]
        public ActionResult EsportaProfessionistiPdf()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null || utenteCorrente.TipoUtente != "Admin")
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            var lista = db.OperatoriSinergia
                .Where(p => p.TipoCliente == "Professionista" && p.Stato != "Eliminato")
                .OrderBy(p => p.Nome)
                .Select(p => new ProfessionistiViewModel
                {
                    ID_Professionista = p.ID_Operatore,
                    Nome = p.Nome,
                    TipoRagioneSociale = p.TipoRagioneSociale,
                    PartitaIVA = p.PIVA,
                    CodiceFiscale = p.CodiceFiscale,
                    Email = p.MAIL1,
                    Telefono = p.Telefono,
                    Stato = p.Stato
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Professionisti/ReportProfessionistiPdf.cshtml", lista)
            {
                FileName = $"Professionisti_{DateTime.Today:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }
 



        #endregion

        #region Dati Bancari

        [HttpPost]
        public ActionResult SalvaDatiBancari()
        {
            int utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId <= 0)
                return Json(new { success = false, message = "Utente non autenticato." });

            try
            {
                int idCliente = 0;
                string tipoCliente = Request.Form["TipoCliente"];

                if (!int.TryParse(Request.Form["ID_Cliente"]?.Trim(), out idCliente) || string.IsNullOrWhiteSpace(tipoCliente))
                    return Json(new { success = false, message = "ID cliente o tipo non valido." });

                var datiEsistenti = db.DatiBancari.FirstOrDefault(d => d.ID_Cliente == idCliente && d.TipoCliente == tipoCliente);

                if (datiEsistenti == null)
                {
                    var nuovo = new DatiBancari
                    {
                        ID_Cliente = idCliente,
                        TipoCliente = tipoCliente,
                        NomeBanca = Request.Form["NomeBanca"],
                        IBAN = Request.Form["IBAN"],
                        Intestatario = Request.Form["Intestatario"],
                        BIC_SWIFT = Request.Form["BIC_SWIFT"],
                        IndirizzoBanca = Request.Form["IndirizzoBanca"],
                        Note = Request.Form["Note"],
                        Stato = "Attivo",
                        DataInserimento = DateTime.Now,
                        ID_UtenteCreatore = utenteId
                    };

                    db.DatiBancari.Add(nuovo);
                }
                else
                {
                    var modifiche = new List<string>();

                    void Confronta(string campo, string valOld, string valNew)
                    {
                        valOld = valOld?.Trim() ?? "";
                        valNew = valNew?.Trim() ?? "";
                        if (valOld != valNew)
                            modifiche.Add($"{campo}: '{valOld}' → '{valNew}'");
                    }

                    Confronta("Nome Banca", datiEsistenti.NomeBanca, Request.Form["NomeBanca"]);
                    Confronta("IBAN", datiEsistenti.IBAN, Request.Form["IBAN"]);
                    Confronta("Intestatario", datiEsistenti.Intestatario, Request.Form["Intestatario"]);
                    Confronta("BIC/SWIFT", datiEsistenti.BIC_SWIFT, Request.Form["BIC_SWIFT"]);
                    Confronta("Indirizzo Banca", datiEsistenti.IndirizzoBanca, Request.Form["IndirizzoBanca"]);
                    Confronta("Note", datiEsistenti.Note, Request.Form["Note"]);

                    if (modifiche.Any())
                    {
                        int ultimaVersione = db.DatiBancari_a
                            .Where(a => a.ID_Cliente == datiEsistenti.ID_Cliente && a.TipoCliente == datiEsistenti.TipoCliente)
                            .Select(a => (int?)a.NumeroVersione)
                            .Max() ?? 0;

                        var dataInserimentoArch = datiEsistenti.DataInserimento.HasValue && datiEsistenti.DataInserimento.Value < new DateTime(1753, 1, 1)
                            ? (DateTime?)null
                            : datiEsistenti.DataInserimento;

                        db.DatiBancari_a.Add(new DatiBancari_a
                        {
                            ID_DatoBancario = datiEsistenti.ID_DatoBancario,
                            ID_Cliente = datiEsistenti.ID_Cliente,
                            TipoCliente = datiEsistenti.TipoCliente,
                            NomeBanca = datiEsistenti.NomeBanca,
                            IBAN = datiEsistenti.IBAN,
                            Intestatario = datiEsistenti.Intestatario,
                            BIC_SWIFT = datiEsistenti.BIC_SWIFT,
                            IndirizzoBanca = datiEsistenti.IndirizzoBanca,
                            Note = datiEsistenti.Note,
                            Stato = datiEsistenti.Stato,
                            ID_UtenteCreatore = datiEsistenti.ID_UtenteCreatore,
                            DataInserimento = dataInserimentoArch,
                            DataArchiviazione = DateTime.Now,
                            ID_UtenteArchiviazione = utenteId,
                            NumeroVersione = ultimaVersione + 1,
                            ModificheTestuali = $"Modifiche effettuate da ID_Utente = {utenteId} in data {DateTime.Now:g}:\n- " + string.Join("\n- ", modifiche)
                        });
                    }


                    datiEsistenti.NomeBanca = Request.Form["NomeBanca"];
                    datiEsistenti.IBAN = Request.Form["IBAN"];
                    datiEsistenti.Intestatario = Request.Form["Intestatario"];
                    datiEsistenti.BIC_SWIFT = Request.Form["BIC_SWIFT"];
                    datiEsistenti.IndirizzoBanca = Request.Form["IndirizzoBanca"];
                    datiEsistenti.Note = Request.Form["Note"];
                    datiEsistenti.Stato = "Attivo";
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Dati bancari salvati correttamente." });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += " | Inner: " + ex.InnerException.Message;

                return Json(new
                {
                    success = false,
                    message = "Errore inatteso: " + errorMessage,
                    dettagli = ex.StackTrace
                });
            }

        }



        [HttpGet]
        public JsonResult GetDatiBancari(int idCliente)
        {
            var dati = db.DatiBancari
                .Where(d =>
                    d.ID_Cliente == idCliente &&
                    d.Stato == "Attivo" &&
                    d.IBAN != null && d.IBAN != "" && // o altri campi obbligatori
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

        [HttpGet]
        public JsonResult GetDatiBancariProfessionista(int idProfessionista)
        {
            var dati = db.DatiBancari
                .Where(d =>
                    d.ID_Cliente == idProfessionista &&
                    d.TipoCliente == "Professionista" &&   // 👈 filtro chiaro
                    d.Stato == "Attivo" &&
                    !string.IsNullOrEmpty(d.IBAN) &&
                    !string.IsNullOrEmpty(d.NomeBanca)
                )
                .OrderByDescending(d => d.DataInserimento)
                .FirstOrDefault();

            if (dati == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                NomeBanca = dati.NomeBanca,
                IBAN = dati.IBAN,
                IntestatarioConto = dati.Intestatario,
                BIC = dati.BIC_SWIFT,
                Note = dati.Note
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetDatiBancariAzienda(int idAzienda)
        {
            var dati = db.DatiBancari
                .Where(d =>
                    d.ID_Cliente == idAzienda &&
                    d.TipoCliente == "Azienda" &&
                    d.Stato == "Attivo" &&
                    !string.IsNullOrEmpty(d.IBAN) &&
                    !string.IsNullOrEmpty(d.NomeBanca)
                )
                .OrderByDescending(d => d.DataInserimento)
                .FirstOrDefault();

            if (dati == null)
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                NomeBanca = dati.NomeBanca,
                IBAN = dati.IBAN,
                IntestatarioConto = dati.Intestatario,
                BIC = dati.BIC_SWIFT,
                Note = dati.Note
            }, JsonRequestBehavior.AllowGet);
        }



        #endregion

        #region CLIENTI ESTERNI

        // Vista principale
        public ActionResult GestioneClienti()
        {
            return View("~/Views/Clienti/GestioneClienti.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneClientiList(string filtroNome = "", string statoFiltro = "Tutti")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return PartialView("~/Views/Clienti/_GestioneClientiList.cshtml", new List<ClienteEsternoViewModel>());

            List<Clienti> clientiFiltrati = new List<Clienti>();
            List<SelectListItem> professionisti = new List<SelectListItem>();
            ViewBag.PuoAggiungere = false;

            // ==================== PROFESSIONISTA ====================
            if (utente.TipoUtente == "Professionista")
            {
                var op = db.OperatoriSinergia
                           .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente && o.TipoCliente == "Professionista");

                if (op != null)
                {
                    int idClienteProfessionista = op.ID_Operatore;

                    // 1️⃣ Clienti dove è Owner
                    var clientiOwner = db.Clienti
                        .Where(cl => cl.ID_Operatore == idClienteProfessionista
                                  && cl.TipoOperatore == "Professionista")
                        .ToList();

                    // 2️⃣ Clienti dove è associato
                    var clientiAssociati = (from cp in db.ClientiProfessionisti
                                            join cl in db.Clienti on cp.ID_Cliente equals cl.ID_Cliente
                                            where cp.ID_Professionista == idClienteProfessionista
                                            select cl).ToList();

                    clientiFiltrati = clientiOwner.Union(clientiAssociati).Distinct().ToList();

                    professionisti = new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = op.ID_Operatore.ToString(),
                    Text = op.Nome + " " + op.Cognome
                }
            };

                    ViewBag.PuoAggiungere = true;
                }
            }

            // ==================== ADMIN ====================
            else if (utente.TipoUtente == "Admin")
            {
                clientiFiltrati = db.Clienti.ToList();

                professionisti = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista")
                    .Select(o => new SelectListItem
                    {
                        Value = o.ID_Operatore.ToString(),
                        Text = o.Nome + " " + o.Cognome
                    }).ToList();

                ViewBag.PuoAggiungere = true;
            }

            // ==================== COLLABORATORE ====================
            else if (utente.TipoUtente == "Collaboratore")
            {
                // 🔹 Recupero i professionisti collegati
                var professionistiCollegati = (from r in db.RelazioneUtenti
                                               join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                                               where r.ID_UtenteAssociato == idUtente
                                                     && r.Stato == "Attivo"
                                                     && o.TipoCliente == "Professionista"
                                                     && o.Stato == "Attivo"
                                               select o).ToList();

                if (!professionistiCollegati.Any())
                {
                    clientiFiltrati = new List<Clienti>();
                    professionisti = new List<SelectListItem>();
                    ViewBag.PuoAggiungere = false;
                }
                else
                {
                    // 🔹 Lista di ID per evitare NotSupportedException
                    var idProfessionistiCollegati = professionistiCollegati.Select(p => p.ID_Operatore).ToList();

                    // 1️⃣ Clienti creati dai professionisti collegati
                    var clientiOwner = (from c in db.Clienti
                                        where idProfessionistiCollegati.Contains(c.ID_Operatore)
                                              && c.TipoOperatore == "Professionista"
                                        select c).ToList();

                    // 2️⃣ Clienti associati
                    var clientiAssociati = (from cp in db.ClientiProfessionisti
                                            join cl in db.Clienti on cp.ID_Cliente equals cl.ID_Cliente
                                            where idProfessionistiCollegati.Contains(cp.ID_Professionista)
                                            select cl).ToList();

                    // 🔄 Unione senza duplicati
                    clientiFiltrati = clientiOwner.Union(clientiAssociati).Distinct().ToList();

                    professionisti = professionistiCollegati
                        .Select(o => new SelectListItem
                        {
                            Value = o.ID_Operatore.ToString(),
                            Text = o.Nome + " " + o.Cognome
                        }).ToList();

                    ViewBag.PuoAggiungere = true;
                }
            }

            // ==================== FILTRI ====================
            ViewBag.Professionisti = professionisti;

            clientiFiltrati = clientiFiltrati
                .Where(c => string.IsNullOrEmpty(filtroNome) || (c.Nome + " " + c.Cognome).Contains(filtroNome))
                .Where(c => statoFiltro == "Tutti" || c.Stato == statoFiltro)
                .ToList();

            // ==================== MAPPING IN VIEWMODEL ====================
            var clientiVM = clientiFiltrati.Select(c =>
            {
                // Owner (legacy)
                var owner = db.OperatoriSinergia.FirstOrDefault(o =>
                    o.ID_Operatore == c.ID_Operatore && o.TipoCliente == c.TipoOperatore);

                string nomeOwner = owner != null ? $"{owner.Nome} {owner.Cognome}" : "-";

                // Professionisti associati
                var associati = (from cp in db.ClientiProfessionisti
                                 join op in db.OperatoriSinergia on cp.ID_Professionista equals op.ID_Operatore
                                 where cp.ID_Cliente == c.ID_Cliente
                                 select op.Nome + " " + op.Cognome).ToList();

                string professionistiAssociati = associati.Any()
                    ? string.Join(", ", associati)
                    : "-";

                return new ClienteEsternoViewModel
                {
                    ID_Cliente = c.ID_Cliente,
                    Nome = c.Nome,
                    Cognome = c.Cognome,
                    RagioneSociale = c.RagioneSociale,
                    CodiceFiscale = c.CodiceFiscale,
                    PIVA = c.PIVA,
                    Indirizzo = c.Indirizzo,
                    ID_Citta = c.ID_Citta,
                    ID_Nazione = c.ID_Nazione,
                    Telefono = c.Telefono,
                    Email = c.Email,
                    Note = c.Note,
                    TipoCliente = c.TipoCliente,
                    Stato = c.Stato,
                    DataCreazione = c.DataCreazione,
                    ID_Operatore = c.ID_Operatore,
                    TipoOperatore = c.TipoOperatore,
                    NomeCitta = c.ID_Citta.HasValue
                        ? db.Citta.FirstOrDefault(ci => ci.ID_BPCitta == c.ID_Citta)?.NameLocalita
                        : null,
                    NomeNazione = c.ID_Nazione.HasValue
                        ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == c.ID_Nazione)?.NameNazione
                        : null,
                    OwnerVisualizzato = nomeOwner,
                    AssociatiVisualizzati = professionistiAssociati
                };
            }).ToList();

            return PartialView("~/Views/Clienti/_GestioneClientiList.cshtml", clientiVM);
        }



        [HttpPost]
        public ActionResult CreaCliente()
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non identificato." });

            using (var trans = db.Database.BeginTransaction())
            {
                try
                {
                    // === Determina Owner (operatore principale) ===
                    int idOperatore;
                    string tipoOperatore;

                    if (utente.TipoUtente == "Admin")
                    {
                        // ✅ Admin può decidere liberamente
                        if (!int.TryParse(Request.Form["ID_Operatore"], out idOperatore) ||
                            string.IsNullOrWhiteSpace(Request.Form["TipoOperatore"]))
                        {
                            return Json(new { success = false, message = "Operatore non selezionato correttamente." });
                        }

                        tipoOperatore = Request.Form["TipoOperatore"];
                    }
                    else if (utente.TipoUtente == "Professionista")
                    {
                        // ✅ Professionista → owner sempre sé stesso
                        var op = db.OperatoriSinergia
                            .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente && o.TipoCliente == "Professionista");

                        if (op == null)
                            return Json(new { success = false, message = "Professionista non trovato." });

                        idOperatore = op.ID_Operatore;
                        tipoOperatore = "Professionista";
                    }
                    else if (utente.TipoUtente == "Collaboratore")
                    {
                        // ✅ Collaboratore → deve scegliere un professionista a cui è collegato
                        if (!int.TryParse(Request.Form["ID_Operatore"], out idOperatore))
                            return Json(new { success = false, message = "Devi selezionare un professionista owner." });

                        tipoOperatore = "Professionista";

                        // 🔒 Verifica che il collaboratore sia realmente collegato a quel professionista
                        var idUtenteProfessionista = db.OperatoriSinergia
                            .Where(o => o.ID_Operatore == idOperatore && o.TipoCliente == "Professionista")
                            .Select(o => o.ID_UtenteCollegato)
                            .FirstOrDefault();
                        // 🔒 Verifica che il collaboratore sia realmente collegato a quel professionista
                        bool collegato = (
                            from r in db.RelazioneUtenti
                            join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                            where r.ID_UtenteAssociato == idUtente    // collaboratore loggato
                                  && r.Stato == "Attivo"
                                  && o.TipoCliente == "Professionista"
                                  && o.ID_Operatore == idOperatore      // professionista scelto
                            select r
                        ).Any();

                        if (!collegato)
                            return Json(new { success = false, message = "Non puoi creare clienti per questo professionista." });

                    }
                    else
                    {
                        return Json(new { success = false, message = "Tipo utente non autorizzato." });
                    }

                    // 🔍 CONTROLLO UNICITÀ CODICE FISCALE
                    string nuovoCF = (Request.Form["CodiceFiscale"] ?? "").Trim();

                    if (!string.IsNullOrWhiteSpace(nuovoCF))
                    {
                        bool esisteCF = db.Clienti
                            .Any(c => c.CodiceFiscale == nuovoCF);

                        if (esisteCF)
                            return Json(new
                            {
                                success = false,
                                message = "❌ Esiste già un cliente con questo Codice Fiscale."
                            });
                    }

                    // === CREA CLIENTE ===
                    var cliente = new Clienti
                    {
                        Nome = Request.Form["Nome"],
                        Cognome = Request.Form["Cognome"],
                        RagioneSociale = Request.Form["RagioneSociale"],
                        Telefono = Request.Form["Telefono"],
                        Email = Request.Form["Email"],
                        PIVA = Request.Form["PIVA"],
                        CodiceFiscale = Request.Form["CodiceFiscale"],
                        ID_Citta = string.IsNullOrEmpty(Request.Form["ID_Citta"]) ? null : (int?)int.Parse(Request.Form["ID_Citta"]),
                        ID_Nazione = string.IsNullOrEmpty(Request.Form["ID_Nazione"]) ? null : (int?)int.Parse(Request.Form["ID_Nazione"]),
                        Indirizzo = Request.Form["Indirizzo"],
                        Note = Request.Form["Note"],
                        TipoCliente = Request.Form["TipoCliente"],
                        Stato = Request.Form["Stato"] ?? "Attivo",
                        DataCreazione = DateTime.Now,
                        ID_Operatore = idOperatore,     // 👈 Owner determinato sopra
                        TipoOperatore = tipoOperatore
                    };


                    db.Clienti.Add(cliente);
                    db.SaveChanges();

                    // === UPLOAD DOCUMENTO PDF (VARBINARY) ===
                    var file = Request.Files["DocumentoPDF"];
                    if (file != null && file.ContentLength > 0)
                    {
                        const int MAX_SIZE = 8 * 1024 * 1024; // 8 MB

                        if (!file.FileName.ToLower().EndsWith(".pdf"))
                            return Json(new { success = false, message = "Il documento deve essere un file PDF." });

                        if (file.ContentLength > MAX_SIZE)
                            return Json(new { success = false, message = "Il PDF non può superare 8 MB." });

                        using (var ms = new MemoryStream())
                        {
                            file.InputStream.CopyTo(ms);
                            cliente.DocumentoCliente_File = ms.ToArray();
                        }

                        cliente.DocumentoCliente_Nome = Path.GetFileName(file.FileName);
                    }


                    // === ASSOCIA PROFESSIONISTI EXTRA (se inviati dal form) ===
                    var professionistiExtra = Request.Form.GetValues("ProfessionistiAssociati[]");
                    if (professionistiExtra != null)
                    {
                        foreach (var profIdStr in professionistiExtra)
                        {
                            if (int.TryParse(profIdStr, out int idProf))
                            {
                                var relazione = new ClientiProfessionisti
                                {
                                    ID_Cliente = cliente.ID_Cliente,
                                    ID_Professionista = idProf,
                                    DataAssegnazione = DateTime.Now
                                };
                                db.ClientiProfessionisti.Add(relazione);
                            }
                        }
                        db.SaveChanges();
                    }

                    // === ARCHIVIA VERSIONE ===
                    var archivio = new Clienti_a
                    {
                        ID_Cliente_Originale = cliente.ID_Cliente,
                        Nome = cliente.Nome,
                        Cognome = cliente.Cognome,
                        RagioneSociale = cliente.RagioneSociale,
                        Telefono = cliente.Telefono,
                        Email = cliente.Email,
                        PIVA = cliente.PIVA,
                        CodiceFiscale = cliente.CodiceFiscale,
                        ID_Citta = cliente.ID_Citta,
                        ID_Nazione = cliente.ID_Nazione,
                        Indirizzo = cliente.Indirizzo,
                        Note = cliente.Note,
                        TipoCliente = cliente.TipoCliente,
                        Stato = cliente.Stato,
                        DataCreazione = cliente.DataCreazione,
                        ID_Operatore = cliente.ID_Operatore,
                        TipoOperatore = cliente.TipoOperatore,
                        NumeroVersione = 1,
                        DataArchiviazione = DateTime.Now,
                        DocumentoCliente_Nome = cliente.DocumentoCliente_Nome,
                        DocumentoCliente_File = cliente.DocumentoCliente_File,
                        ID_UtenteArchiviazione = idUtente,
                        ModificheTestuali = "Inserimento iniziale"
                    };

                    db.Clienti_a.Add(archivio);
                    db.SaveChanges();

                    trans.Commit();

                    return Json(new { success = true, message = "Cliente creato con successo." });
                }
                catch (DbEntityValidationException ex)
                {
                    var errorMessages = ex.EntityValidationErrors
                        .SelectMany(e => e.ValidationErrors)
                        .Select(e => $"{e.PropertyName}: {e.ErrorMessage}");

                    var fullMessage = string.Join("; ", errorMessages);
                    trans.Rollback();
                    return Json(new { success = false, message = "Errore validazione: " + fullMessage });
                }

            }
        }



        [HttpPost]
        public ActionResult ModificaCliente()
        {
            using (var trans = db.Database.BeginTransaction())
            {
                try
                {
                    int idUtente = UserManager.GetIDUtenteCollegato();
                    var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                    if (utente == null)
                        return Json(new { success = false, message = "Utente non identificato." });

                    int idCliente = int.Parse(Request.Form["ID_Cliente"] ?? "0");
                    var originale = db.Clienti.FirstOrDefault(c => c.ID_Cliente == idCliente);
                    if (originale == null)
                        return Json(new { success = false, message = "Cliente non trovato." });

                    // 🔍 CONTROLLO UNICITÀ CODICE FISCALE
                    string nuovoCF = (Request.Form["CodiceFiscale"] ?? "").Trim();

                    if (!string.IsNullOrWhiteSpace(nuovoCF))
                    {
                        bool esisteCF = db.Clienti
                            .Any(c => c.CodiceFiscale == nuovoCF && c.ID_Cliente != idCliente);

                        if (esisteCF)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "❌ Questo Codice Fiscale è già associato a un altro cliente."
                            });
                        }
                    }
                    // === Autorizzazioni base ===
                    if (utente.TipoUtente != "Admin")
                    {
                        var operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Operatore == originale.ID_Operatore);
                        if (operatore == null || (utente.TipoUtente == "Professionista" && operatore.ID_UtenteCollegato != idUtente))
                            return Json(new { success = false, message = "Non puoi modificare clienti di altri operatori." });

                        if (utente.TipoUtente == "Collaboratore")
                        {
                            bool collegato = (
                                from r in db.RelazioneUtenti
                                join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                                where r.ID_UtenteAssociato == idUtente
                                      && r.Stato == "Attivo"
                                      && o.TipoCliente == "Professionista"
                                      && o.ID_Operatore== originale.ID_Operatore   // 👈 owner del cliente
                                select r
                            ).Any();

                            var idMenuClienti = db.Menu.FirstOrDefault(m => m.NomeMenu == "Clienti")?.ID_Menu ?? 0;
                            bool haPermesso = db.Permessi.Any(p => p.ID_Utente == idUtente && p.ID_Menu == idMenuClienti && (p.Modifica ?? false));

                            if (!collegato || !haPermesso)
                                return Json(new { success = false, message = "Non hai i permessi per modificare questo cliente." });
                        }

                    }

                    // === Confronto modifiche ===
                    var modifiche = new List<string>();
                    void Confronta(string nomeCampo, object valO, object valN)
                    {
                        if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                            modifiche.Add($"{nomeCampo}: '{valO}' → '{valN}'");
                    }

                    Confronta("Nome", originale.Nome, Request.Form["Nome"]);
                    Confronta("Cognome", originale.Cognome, Request.Form["Cognome"]);
                    Confronta("Ragione Sociale", originale.RagioneSociale, Request.Form["RagioneSociale"]);
                    Confronta("Codice Fiscale", originale.CodiceFiscale, Request.Form["CodiceFiscale"]);
                    Confronta("P.IVA", originale.PIVA, Request.Form["PIVA"]);
                    Confronta("Telefono", originale.Telefono, Request.Form["Telefono"]);
                    Confronta("Email", originale.Email, Request.Form["Email"]);
                    Confronta("Note", originale.Note, Request.Form["Note"]);
                    Confronta("Stato", originale.Stato, Request.Form["Stato"]);
                    Confronta("ID_Citta", originale.ID_Citta, Request.Form["ID_Citta"]);
                    Confronta("ID_Nazione", originale.ID_Nazione, Request.Form["ID_Nazione"]);
                    Confronta("Indirizzo", originale.Indirizzo, Request.Form["Indirizzo"]);

                    if (utente.TipoUtente == "Admin")
                    {
                        Confronta("ID_Operatore", originale.ID_Operatore, Request.Form["ID_Operatore"]);
                        Confronta("TipoOperatore", originale.TipoOperatore, Request.Form["TipoOperatore"]);
                    }
                    else if (utente.TipoUtente == "Collaboratore")
                    {
                        if (int.TryParse(Request.Form["ID_Operatore"], out int nuovoOperatore))
                        {
                            // 🔹 Recupera l'utente collegato al professionista
                            var idUtenteProfessionista = db.OperatoriSinergia
                                .Where(o => o.ID_Operatore == nuovoOperatore && o.TipoCliente == "Professionista")
                                .Select(o => o.ID_UtenteCollegato)
                                .FirstOrDefault();

                            // 🔹 Verifica relazione usando ID_Cliente del professionista (non ID_Utente!)
                            bool collegato = (from r in db.RelazioneUtenti
                                              join o in db.OperatoriSinergia on r.ID_Utente equals o.ID_Operatore
                                              where r.ID_UtenteAssociato == idUtente
                                                    && r.Stato == "Attivo"
                                                    && o.TipoCliente == "Professionista"
                                                    && o.ID_Operatore == nuovoOperatore
                                              select r).Any();

                            if (!collegato)
                                return Json(new { success = false, message = "Non puoi riassegnare questo cliente a un professionista non collegato." });

                            if (originale.ID_Operatore != nuovoOperatore)
                                modifiche.Add($"Owner: '{originale.ID_Operatore}' → '{nuovoOperatore}'");

                            originale.ID_Operatore = nuovoOperatore;
                            originale.TipoOperatore = "Professionista";
                        }
                    }

                    // === Aggiornamento dati cliente ===
                    originale.Nome = Request.Form["Nome"];
                    originale.Cognome = Request.Form["Cognome"];
                    originale.RagioneSociale = Request.Form["RagioneSociale"];
                    originale.CodiceFiscale = Request.Form["CodiceFiscale"];
                    originale.PIVA = Request.Form["PIVA"];
                    originale.Telefono = Request.Form["Telefono"];
                    originale.Email = Request.Form["Email"];
                    originale.Note = Request.Form["Note"];
                    originale.Indirizzo = Request.Form["Indirizzo"];
                    originale.ID_Citta = string.IsNullOrWhiteSpace(Request.Form["ID_Citta"]) ? (int?)null : int.Parse(Request.Form["ID_Citta"]);
                    originale.ID_Nazione = string.IsNullOrWhiteSpace(Request.Form["ID_Nazione"]) ? (int?)null : int.Parse(Request.Form["ID_Nazione"]);
                    originale.Stato = Request.Form["Stato"];
                    originale.DataUltimaModifica = DateTime.Now;
                    originale.ID_UtenteUltimaModifica = idUtente;

                    // === UPLOAD NUOVO DOCUMENTO PDF (VARBINARY) ===
                    var file = Request.Files["DocumentoPDF"];
                    if (file != null && file.ContentLength > 0)
                    {
                        const int MAX_SIZE = 8 * 1024 * 1024; // 8 MB

                        if (!file.FileName.ToLower().EndsWith(".pdf"))
                            return Json(new { success = false, message = "Il documento deve essere un file PDF." });

                        if (file.ContentLength > MAX_SIZE)
                            return Json(new { success = false, message = "Il PDF non può superare 8 MB." });

                        using (var ms = new MemoryStream())
                        {
                            file.InputStream.CopyTo(ms);
                            originale.DocumentoCliente_File = ms.ToArray();
                        }

                        originale.DocumentoCliente_Nome = Path.GetFileName(file.FileName);

                        // Tracciamo nelle modifiche che il documento è stato aggiornato
                        modifiche.Add("DocumentoCliente: aggiornato");
                    }


                    if (utente.TipoUtente == "Admin")
                    {
                        if (int.TryParse(Request.Form["ID_Operatore"], out int idOperatore))
                            originale.ID_Operatore = idOperatore;
                        originale.TipoOperatore = Request.Form["TipoOperatore"];
                    }

                    db.SaveChanges();

                    // === Archiviazione cliente ===
                    if (modifiche.Any())
                    {
                        int maxVersion = db.Clienti_a
                            .AsNoTracking()
                            .Where(a => a.ID_Cliente_Originale == originale.ID_Cliente)
                            .Select(a => (int?)a.NumeroVersione)
                            .Max() ?? 0;

                        int nuovaVersione = maxVersion + 1;

                        var archivio = new Clienti_a
                        {
                            ID_Cliente_Originale = originale.ID_Cliente,
                            Nome = originale.Nome,
                            Cognome = originale.Cognome,
                            RagioneSociale = originale.RagioneSociale,
                            CodiceFiscale = originale.CodiceFiscale,
                            PIVA = originale.PIVA,
                            Telefono = originale.Telefono,
                            Email = originale.Email,
                            Note = originale.Note,
                            Indirizzo = originale.Indirizzo,
                            ID_Citta = originale.ID_Citta,
                            ID_Nazione = originale.ID_Nazione,
                            Stato = originale.Stato,
                            ID_Operatore = originale.ID_Operatore,
                            TipoOperatore = originale.TipoOperatore,
                            DataArchiviazione = DateTime.Now,
                            DocumentoCliente_Nome = originale.DocumentoCliente_Nome,
                            DocumentoCliente_File = originale.DocumentoCliente_File,
                            ID_UtenteArchiviazione = idUtente,
                            NumeroVersione = nuovaVersione,
                            ModificheTestuali = $"Modifica effettuata da ID_Utente = {idUtente} il {DateTime.Now:g}:\n- {string.Join("\n- ", modifiche)}"
                        };

                        db.Clienti_a.Add(archivio);
                        db.SaveChanges();
                    }

                    trans.Commit();
                    return Json(new { success = true, message = "✅ Cliente modificato correttamente." });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { success = false, message = "❌ Errore durante la modifica: " + ex.Message });
                }
            }
        }



        [HttpPost]
        public ActionResult EliminaCliente(int id)
        {
            using (var trans = db.Database.BeginTransaction())
            {
                try
                {
                    int idUtente = UserManager.GetIDUtenteCollegato();
                    var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                    if (utente == null)
                        return Json(new { success = false, message = "Utente non identificato." });

                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == id);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente non trovato." });

                    // === Archiviazione cliente prima della modifica ===
                    int versioneAttuale = db.Clienti_a
                        .Where(a => a.ID_Cliente_Originale == cliente.ID_Cliente)
                        .Select(a => (int?)a.NumeroVersione)
                        .Max() ?? 0;

                    var archivio = new Clienti_a
                    {
                        ID_Cliente_Originale = cliente.ID_Cliente,
                        Nome = cliente.Nome,
                        Cognome = cliente.Cognome,
                        RagioneSociale = cliente.RagioneSociale,
                        CodiceFiscale = cliente.CodiceFiscale,
                        PIVA = cliente.PIVA,
                        Telefono = cliente.Telefono,
                        Email = cliente.Email,
                        Note = cliente.Note,
                        Indirizzo = cliente.Indirizzo,
                        ID_Citta = cliente.ID_Citta,
                        ID_Nazione = cliente.ID_Nazione,
                        Stato = cliente.Stato,
                        ID_Operatore = cliente.ID_Operatore,
                        TipoOperatore = cliente.TipoOperatore,
                        DataArchiviazione = DateTime.Now,
                        DocumentoCliente_Nome = cliente.DocumentoCliente_Nome,
                        DocumentoCliente_File = cliente.DocumentoCliente_File,
                        ID_UtenteArchiviazione = idUtente,
                        NumeroVersione = versioneAttuale + 1,
                        ModificheTestuali = "Disattivazione cliente"
                    };
                    db.Clienti_a.Add(archivio);

                    // === Archivia anche tutte le relazioni professionisti collegate ===
                    var relazioni = db.ClientiProfessionisti.Where(r => r.ID_Cliente == cliente.ID_Cliente).ToList();
                    foreach (var rel in relazioni)
                    {
                        db.ClientiProfessionisti_a.Add(new ClientiProfessionisti_a
                        {
                            ID_Originale = rel.ID_ClientiProfessionisti,
                            ID_Cliente = rel.ID_Cliente,
                            ID_Professionista = rel.ID_Professionista,
                            Ruolo = rel.Ruolo,
                            DataAssegnazione = rel.DataAssegnazione,
                            NumeroVersione = 1,
                            DataArchiviazione = DateTime.Now,
                            ID_UtenteArchiviazione = idUtente,
                            ModificheTestuali = "Archiviazione durante eliminazione cliente"
                        });
                    }

                    // === Elimina logicamente (imposta Inattivo) ===
                    cliente.Stato = "Inattivo";
                    cliente.ID_UtenteUltimaModifica = idUtente;
                    cliente.DataUltimaModifica = DateTime.Now;

                    // === Controlli permessi ===
                    if (utente.TipoUtente == "Professionista")
                    {
                        var operatore = db.OperatoriSinergia.FirstOrDefault(o =>
                            o.ID_Operatore == cliente.ID_Operatore &&
                            o.TipoCliente == cliente.TipoOperatore &&
                            o.ID_UtenteCollegato == idUtente);

                        if (operatore == null)
                            return Json(new { success = false, message = "Non sei autorizzato a eliminare questo cliente." });
                    }
                    else if (utente.TipoUtente == "Collaboratore")
                    {
                        var operatore = db.OperatoriSinergia.FirstOrDefault(o =>
                            o.ID_Operatore == cliente.ID_Operatore &&
                            o.TipoCliente == cliente.TipoOperatore);

                        if (operatore == null)
                            return Json(new { success = false, message = "Operatore non trovato per questo cliente." });

                        var relazione = db.RelazioneUtenti.FirstOrDefault(r =>
                            r.ID_UtenteAssociato == idUtente &&
                            r.ID_Utente == operatore.ID_UtenteCollegato &&
                            r.Stato == "Attivo");

                        if (relazione == null)
                            return Json(new { success = false, message = "Non sei assegnato al professionista proprietario del cliente." });

                        var idMenuClienti = db.Menu
                            .Where(m => m.NomeMenu == "Clienti")
                            .Select(m => m.ID_Menu)
                            .FirstOrDefault();

                        bool haPermesso = db.Permessi.Any(p =>
                            p.ID_Utente == idUtente &&
                            p.ID_Menu == idMenuClienti &&
                            (p.Elimina ?? false));

                        if (!haPermesso)
                            return Json(new { success = false, message = "Non hai i permessi per eliminare clienti." });
                    }

                    db.SaveChanges();
                    trans.Commit();

                    return Json(new { success = true, message = "✅ Cliente e relazioni disattivati correttamente." });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { success = false, message = "❌ Errore durante la disattivazione: " + ex.Message });
                }
            }
        }




        private JsonResult SalvaClienteEsterno(int idOperatore, string tipoOperatore)
        {
            try
            {
                var cliente = new Clienti
                {
                    Nome = Request.Form["Nome"]?.Trim(),
                    Cognome = Request.Form["Cognome"]?.Trim(),
                    RagioneSociale = Request.Form["RagioneSociale"]?.Trim(),
                    CodiceFiscale = Request.Form["CodiceFiscale"]?.Trim(),
                    PIVA = Request.Form["PIVA"]?.Trim(),
                    Indirizzo = Request.Form["Indirizzo"]?.Trim(),
                    ID_Citta = int.TryParse(Request.Form["ID_Citta"], out int idCitta) ? idCitta : (int?)null,
                    ID_Nazione = int.TryParse(Request.Form["ID_Nazione"], out int idNazione) ? idNazione : (int?)null,
                    Telefono = Request.Form["Telefono"]?.Trim(),
                    Email = Request.Form["Email"]?.Trim(),
                    Note = Request.Form["Note"]?.Trim(),
                    TipoCliente = Request.Form["TipoCliente"]?.Trim(),
                    Stato = "Attivo",
                    DataCreazione = DateTime.Now,
                    DocumentoCliente_Nome = null,
                    DocumentoCliente_File = null,
                    ID_Operatore = idOperatore,
                    TipoOperatore = tipoOperatore
                };

                db.Clienti.Add(cliente);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Cliente creato correttamente!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante la creazione: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetClienteEsterno(int id)
        {
            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == id);
            if (cliente == null)
                return Json(null, JsonRequestBehavior.AllowGet);

            int idUtente = UserManager.GetIDUtenteCollegato();
            string tipoUtente = UserManager.GetTipoUtente();
            bool puòModificare = false;

            if (tipoUtente == "Admin")
                puòModificare = true;
            else if (tipoUtente == "Professionista")
            {
                var operatore = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_Operatore == cliente.ID_Operatore && o.TipoCliente == cliente.TipoOperatore);
                if (operatore != null && operatore.ID_UtenteCollegato == idUtente)
                    puòModificare = true;
            }
            else if (tipoUtente == "Collaboratore")
            {
                var relazione = db.RelazioneUtenti.FirstOrDefault(r =>
                    r.ID_UtenteAssociato == idUtente &&
                    r.ID_Utente == cliente.ID_Operatore &&
                    r.Stato == "Attivo");

                if (relazione != null)
                {
                    var idMenuClienti = db.Menu.FirstOrDefault(m => m.NomeMenu == "Clienti")?.ID_Menu ?? 0;
                    puòModificare = db.Permessi.Any(p =>
                        p.ID_Utente == idUtente &&
                        p.ID_Menu == idMenuClienti &&
                        (p.Modifica ?? false));
                }
            }

            var vm = new ClienteEsternoViewModel
            {
                ID_Cliente = cliente.ID_Cliente,
                Nome = cliente.Nome,
                Cognome = cliente.Cognome,
                RagioneSociale = cliente.RagioneSociale,
                CodiceFiscale = cliente.CodiceFiscale,
                PIVA = cliente.PIVA,
                Indirizzo = cliente.Indirizzo,
                ID_Citta = cliente.ID_Citta,
                ID_Nazione = cliente.ID_Nazione,
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                Note = cliente.Note,
                Stato = cliente.Stato,
                ID_Operatore = cliente.ID_Operatore,
                TipoOperatore = cliente.TipoOperatore,
                NomeCitta = cliente.ID_Citta.HasValue ? db.Citta.FirstOrDefault(ci => ci.ID_BPCitta == cliente.ID_Citta)?.NameLocalita : null,
                NomeNazione = cliente.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == cliente.ID_Nazione)?.NameNazione : null,
                UtenteCorrenteHaPermessi = puòModificare,
                DocumentoCliente_Nome = cliente.DocumentoCliente_Nome,
                HasDocumentoCliente = cliente.DocumentoCliente_File != null

            };

            return Json(vm, JsonRequestBehavior.AllowGet);
        }





        // Metodo ausiliario per identificare l'owner del nuovo cliente
        private (int? ID_Operatore, string TipoOperatore, string errore) GetOperatorePerNuovoCliente()
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return (null, null, "Utente non identificato.");

            if (utente.TipoUtente == "Professionista")
            {
                var operatore = db.OperatoriSinergia.FirstOrDefault(o => o.ID_UtenteCollegato == idUtente);
                if (operatore == null)
                    return (null, null, "Professionista non trovato.");

                return (operatore.ID_Operatore, operatore.TipoCliente, null);
            }

            if (utente.TipoUtente == "Collaboratore")
            {
                var relazione = db.RelazioneUtenti.FirstOrDefault(r =>
                    r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo");

                if (relazione == null)
                    return (null, null, "Collaboratore non assegnato a nessun professionista.");

                var prof = db.OperatoriSinergia.FirstOrDefault(o => o.ID_UtenteCollegato == relazione.ID_Utente);
                if (prof == null)
                    return (null, null, "Professionista collegato non trovato.");

                return (prof.ID_Operatore, prof.TipoCliente, null);
            }

            return (null, null, null); // Admin: gestito separatamente da UI
        }

        [HttpGet]
        public JsonResult GetProfessionistiDisponibili()
        {
            var professionisti = db.OperatoriSinergia
                .Where(o => o.TipoCliente == "Professionista")
                .Select(o => new
                {
                    o.ID_Operatore,
                    NomeCompleto = (o.Nome + " " + o.Cognome).Trim()
                })
                .OrderBy(o => o.NomeCompleto)
                .ToList();

            return Json(professionisti, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult AssegnaProfessionistiCliente(int ID_Cliente, int[] ProfessionistiAssociati)
        {
            try
            {
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == ID_Cliente);
                if (cliente == null)
                    return Json(new { success = false, message = "Cliente non trovato." });

                // 🔹 Owner (fisso dal cliente, non deve essere duplicato)
                int idOwner = cliente.ID_Operatore;

                // 🔹 Cancello relazioni esistenti
                var relazioniEsistenti = db.ClientiProfessionisti
                    .Where(r => r.ID_Cliente == ID_Cliente)
                    .ToList();
                db.ClientiProfessionisti.RemoveRange(relazioniEsistenti);

                // 🔹 Inserisco i nuovi professionisti associati
                if (ProfessionistiAssociati != null && ProfessionistiAssociati.Length > 0)
                {
                    foreach (var idOperatore in ProfessionistiAssociati.Distinct())
                    {
                        if (idOperatore == idOwner) continue; // salto l’owner

                        var professionista = db.OperatoriSinergia
                            .FirstOrDefault(o => o.ID_Operatore == idOperatore && o.TipoCliente == "Professionista");

                        if (professionista != null)
                        {
                            db.ClientiProfessionisti.Add(new ClientiProfessionisti
                            {
                                ID_Cliente = ID_Cliente,
                                ID_Professionista = idOperatore,  // NB: qui è l’ID_Cliente di OperatoriSinergia
                                Ruolo = "Associato",
                                DataAssegnazione = DateTime.Now
                            });
                        }
                    }
                }

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Professionisti aggiornati correttamente per il cliente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante l'assegnazione: " + ex.Message });
            }
        }




        [HttpGet]
        public ActionResult GetDettaglioClienteProfessionisti(int id)
        {
            try
            {
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == id);
                if (cliente == null)
                    return Json(new { success = false, message = "Cliente non trovato." }, JsonRequestBehavior.AllowGet);

                // 🔹 Owner
                var owner = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_Operatore == cliente.ID_Operatore && o.TipoCliente == "Professionista");

                var ownerInfo = owner != null
                    ? new { ID = owner.ID_Operatore, Nome = owner.Nome + " " + owner.Cognome }
                    : null;

                // 🔹 Professionisti associati (extra, escluso owner)
                var associati = (from cp in db.ClientiProfessionisti
                                 join op in db.OperatoriSinergia on cp.ID_Professionista equals op.ID_Operatore
                                 where cp.ID_Cliente == id
                                 select new
                                 {
                                     ID = op.ID_Operatore,
                                     Nome = op.Nome + " " + op.Cognome
                                 }).ToList();

                // 🔹 Tutti i professionisti disponibili (per la select multipla)
                var disponibili = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista")
                    .Select(o => new
                    {
                        ID = o.ID_Operatore,
                        Nome = o.Nome + " " + o.Cognome
                    })
                    .OrderBy(o => o.Nome)
                    .ToList();

                return Json(new
                {
                    success = true,
                    owner = ownerInfo,
                    associati = associati,
                    disponibili = disponibili
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore caricamento dettagli: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }



        [HttpPost]
            public ActionResult RiattivaCliente(int id)
            {
                try
                {
                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == id);
                    if (cliente == null)
                        return Json(new { success = false, message = "Cliente non trovato." });

                    var utenteId = UserManager.GetIDUtenteCollegato();

                    cliente.Stato = "Attivo";
                    cliente.DataUltimaModifica = DateTime.Now;
                    cliente.ID_UtenteUltimaModifica = utenteId;

                    db.SaveChanges();

                    return Json(new { success = true, message = "✅ Cliente riattivato correttamente!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "❌ Errore durante la riattivazione: " + ex.Message });
                }
            }

        [HttpGet]
        public ActionResult EsportaClientiCsv()
        {
            int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);

            if (utenteCorrente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            bool isAdmin = utenteCorrente.TipoUtente == "Admin";

            // ======================================================
            // 🔥 PROFESSIONISTA SELEZIONATO (SOLO OWNER)
            // ======================================================
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

            System.Diagnostics.Trace.WriteLine("Filtro OWNER clienti = " + idProfessionistaFiltro);

            // ======================================================
            // 🔎 QUERY (SOLO OWNER)
            // ======================================================
            var query = db.Clienti.AsQueryable();

            if (idProfessionistaFiltro > 0)
            {
                query = query.Where(c => c.ID_Operatore == idProfessionistaFiltro);
            }

            var clienti = query.OrderBy(c => c.Nome).ToList();

            // ======================================================
            // 🧾 CSV
            // ======================================================
            var sb = new StringBuilder();
            sb.AppendLine("ID Cliente;Nome;Cognome;Ragione Sociale;Codice Fiscale;P.IVA;Email;Telefono;Città;Nazione;Stato;Owner");

            foreach (var c in clienti)
            {
                string nomeCitta = c.ID_Citta.HasValue
                    ? db.Citta.FirstOrDefault(ci => ci.ID_BPCitta == c.ID_Citta)?.NameLocalita ?? "-"
                    : "-";

                string nomeNazione = c.ID_Nazione.HasValue
                    ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == c.ID_Nazione)?.NameNazione ?? "-"
                    : "-";

                string owner = "-";
                if (c.ID_Operatore > 0)
                {
                    owner = db.OperatoriSinergia
                        .Where(o => o.ID_Operatore == c.ID_Operatore)
                        .Select(o => o.Nome + " " + o.Cognome)
                        .FirstOrDefault() ?? "-";
                }

                sb.AppendLine(
                    $"{c.ID_Cliente};" +
                    $"{(c.Nome ?? "-")};" +
                    $"{(c.Cognome ?? "-")};" +
                    $"{(c.RagioneSociale ?? "-")};" +
                    $"{(c.CodiceFiscale ?? "-")};" +
                    $"{(c.PIVA ?? "-")};" +
                    $"{(c.Email ?? "-")};" +
                    $"{(c.Telefono ?? "-")};" +
                    $"{nomeCitta};" +
                    $"{nomeNazione};" +
                    $"{(c.Stato ?? "-")};" +
                    $"{owner}"
                );
            }

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            return File(buffer, "text/csv", $"Clienti_{DateTime.Today:yyyyMMdd}.csv");
        }



        [HttpGet]
        public ActionResult EsportaClientiPdf()
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);

            if (utente == null)
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);

            var lista = db.Clienti
                .OrderBy(c => c.Nome)
                .ToList()
                .Select(c => new ClienteEsternoViewModel
                {
                    ID_Cliente = c.ID_Cliente,
                    Nome = string.IsNullOrWhiteSpace(c.Nome) ? "-" : c.Nome,
                    Cognome = string.IsNullOrWhiteSpace(c.Cognome) ? "-" : c.Cognome,
                    RagioneSociale = string.IsNullOrWhiteSpace(c.RagioneSociale) ? "-" : c.RagioneSociale,
                    CodiceFiscale = string.IsNullOrWhiteSpace(c.CodiceFiscale) ? "-" : c.CodiceFiscale,
                    PIVA = string.IsNullOrWhiteSpace(c.PIVA) ? "-" : c.PIVA,
                    Email = string.IsNullOrWhiteSpace(c.Email) ? "-" : c.Email,
                    Telefono = string.IsNullOrWhiteSpace(c.Telefono) ? "-" : c.Telefono,
                    NomeCitta = c.ID_Citta.HasValue ? db.Citta.FirstOrDefault(ci => ci.ID_BPCitta == c.ID_Citta)?.NameLocalita ?? "-" : "-",
                    NomeNazione = c.ID_Nazione.HasValue ? db.Nazioni.FirstOrDefault(n => n.ID_BPCittaDN == c.ID_Nazione)?.NameNazione ?? "-" : "-",
                    Stato = string.IsNullOrWhiteSpace(c.Stato) ? "-" : c.Stato
                })
                .ToList();

            return new Rotativa.ViewAsPdf("~/Views/Clienti/ReportClientiPdf.cshtml", lista)
            {
                FileName = $"Clienti_{DateTime.Today:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Landscape
            };
        }

        [HttpGet]
        public ActionResult ScaricaDocumentoCliente(int id)
        {
            try
            {
                var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == id);

                if (cliente == null)
                {
                    return Content("Cliente non trovato.");
                }

                if (cliente.DocumentoCliente_File == null || cliente.DocumentoCliente_File.Length == 0)
                {
                    return Content("Nessun documento caricato per questo cliente.");
                }

                string nomeFile = cliente.DocumentoCliente_Nome;
                byte[] fileBytes = cliente.DocumentoCliente_File;

                // Forza il download del PDF
                return File(fileBytes, "application/pdf", nomeFile);
            }
            catch (Exception ex)
            {
                return Content("Errore nel download: " + ex.Message);
            }
        }



        #endregion

        #region PROFESSIONI TEAM
        public ActionResult GestioneProfessioni()
        {
            ViewBag.Title = "Gestione Professioni";
            return View("~/Views/Professioni/GestioneProfessioni.cshtml");
        }


        [HttpGet]
        public ActionResult GestioneProfessioniList(string filtroNome = "", string statoFiltro = "Tutti")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return PartialView("~/Views/Utenti/_GestioneProfessioniList.cshtml", new List<ProfessioneViewModel>());

            List<Professioni> professioniFiltrate = new List<Professioni>();
            List<SelectListItem> professionisti = new List<SelectListItem>();

            // ✅ Carica permessi per tabella "Professioni"
            int idMenuProfessioni = db.Menu.FirstOrDefault(m => m.NomeMenu == "Professioni")?.ID_Menu ?? 0;

            var permessi = db.Permessi
                .Where(p => p.ID_Utente == idUtente && p.ID_Menu == idMenuProfessioni)
                .ToList();


            ViewBag.Permessi = permessi;
            ViewBag.PuoAggiungere = permessi.Any(p => p.Aggiungi == true);
            ViewBag.PuoModificare = permessi.Any(p => p.Modifica == true);
            ViewBag.PuoEliminare = permessi.Any(p => p.Elimina == true );

            if (utente.TipoUtente == "Professionista")
            {
                // ✅ Mostra tutte le professioni, non solo quelle collegate all'utente
                professioniFiltrate = db.Professioni.ToList();

                // ✅ Mostra tutti i professionisti attivi
                professionisti = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista" && o.Stato == "Attivo")
                    .Select(o => new SelectListItem
                    {
                        Value = o.ID_Operatore.ToString(),
                        Text = o.Nome + " " + o.Cognome
                    })
                    .ToList();
            }

            else if (utente.TipoUtente == "Admin")
            {
                professioniFiltrate = db.Professioni.ToList();

                professionisti = db.OperatoriSinergia
                    .Where(o => o.TipoCliente == "Professionista")
                    .Select(o => new SelectListItem
                    {
                        Value = o.ID_Operatore.ToString(),
                        Text = o.Nome + " " + o.Cognome
                    }).ToList();
            }
            else if (utente.TipoUtente == "Collaboratore")
            {
                professioniFiltrate = db.Professioni
                    .Where(p => p.ID_ProfessionistaRiferimento == idUtente)
                    .ToList();
            }

            ViewBag.Professionisti = professionisti;

            // 🔍 Filtro ricerca
            professioniFiltrate = professioniFiltrate
                .Where(p => string.IsNullOrEmpty(filtroNome) || p.Descrizione.Contains(filtroNome))
                .ToList();

            var professioniVM = professioniFiltrate.Select(p =>
            {
                var professionista = db.OperatoriSinergia
                    .FirstOrDefault(o => o.ID_Operatore == p.ID_ProfessionistaRiferimento && o.TipoCliente == "Professionista");

                return new ProfessioneViewModel
                {
                    ProfessioniID = p.ProfessioniID,
                    Codice = p.Codice,
                    Descrizione = p.Descrizione,
                    ID_ProfessionistaRiferimento = p.ID_ProfessionistaRiferimento,
                    NomeProfessionista = professionista != null ? professionista.Nome + " " + professionista.Cognome : ""
                };
            }).ToList();

            return PartialView("~/Views/Professioni/_GestioneProfessioniList.cshtml", professioniVM);
        }

        [HttpPost]
        public ActionResult CreaProfessione()
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non identificato." });

            try
            {
                // int? idProfessionista = null;

                /*
                // 🔴 BLOCCO OBBLIGATORIETÀ RIMOSSO:
                // Qui prima veniva imposto di selezionare o recuperare
                // sempre un professionista (Admin, Professionista, Collaboratore).
                // Ora è stato commentato, quindi la creazione può avvenire
                // senza alcun riferimento obbligatorio.

                if (utente.TipoUtente == "Admin")
                {
                    if (!int.TryParse(Request.Form["ID_ProfessionistaRiferimento"], out int idProf))
                        return Json(new { success = false, message = "Professionista non selezionato correttamente." });

                    idProfessionista = idProf;
                }
                else if (utente.TipoUtente == "Professionista")
                {
                    var op = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_UtenteCollegato == idUtente && o.TipoCliente == "Professionista");
                    if (op == null)
                        return Json(new { success = false, message = "Professionista non trovato." });

                    idProfessionista = op.ID_Cliente;
                }
                else if (utente.TipoUtente == "Collaboratore")
                {
                    var relazione = db.RelazioneUtenti
                        .FirstOrDefault(r => r.ID_UtenteAssociato == idUtente && r.Stato == "Attivo");
                    if (relazione == null)
                        return Json(new { success = false, message = "Nessuna relazione attiva." });

                    var op = db.OperatoriSinergia
                        .FirstOrDefault(o => o.ID_Cliente == relazione.ID_Utente);
                    if (op == null)
                        return Json(new { success = false, message = "Professionista collegato non trovato." });

                    idProfessionista = op.ID_Cliente;
                }
                else
                {
                    return Json(new { success = false, message = "Tipo utente non autorizzato." });
                }
                */

                // === CREA PROFESSIONE ===
                var professione = new Professioni
                {
                    Codice = Request.Form["Codice"],
                    Descrizione = Request.Form["Descrizione"],

                    // 🔴 OBBLIGO RIMOSSO: prima qui era sempre richiesto
                    // ID_ProfessionistaRiferimento = idProfessionista,
                    // Ora lasciato libero/null → il professionista diventa opzionale.

                    PercentualeContributoIntegrativo = string.IsNullOrWhiteSpace(Request.Form["PercentualeContributoIntegrativo"])
                        ? (decimal?)null : decimal.Parse(Request.Form["PercentualeContributoIntegrativo"], CultureInfo.InvariantCulture)
                };

                db.Professioni.Add(professione);
                db.SaveChanges();

                // === CREA VERSIONE ARCHIVIO ===
                var professioneArchivio = new Professioni_a
                {
                    ID_Archivio = professione.ProfessioniID,
                    Codice = professione.Codice,
                    Descrizione = professione.Descrizione,

                    // 🔴 Anche qui non lo salviamo più (prima copiava l’ID obbligatorio).
                    // ID_ProfessionistaRiferimento = professione.ID_ProfessionistaRiferimento,

                    PercentualeContributoIntegrativo = professione.PercentualeContributoIntegrativo,
                    NumeroVersione = 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = "Inserimento iniziale",
                };

                db.Professioni_a.Add(professioneArchivio);
                db.SaveChanges();

                return Json(new { success = true, message = "Professione creata con successo." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la creazione: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult ModificaProfessione()
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non identificato." });

                int id = int.Parse(Request.Form["ProfessioniID"] ?? "0");
                var originale = db.Professioni.FirstOrDefault(p => p.ProfessioniID == id);
                if (originale == null)
                    return Json(new { success = false, message = "Professione non trovata." });

                // 🔴 BLOCCO OBBLIGATORIETÀ: qui veniva forzato il controllo sul professionista di riferimento
                // Se vuoi rendere opzionale il professionista, puoi commentare tutto questo blocco.
                /*
                if (utente.TipoUtente != "Admin")
                {
                    var op = db.OperatoriSinergia.FirstOrDefault(o => o.ID_Cliente == originale.ID_ProfessionistaRiferimento);
                    if (op == null || (op.ID_UtenteCollegato != idUtente && utente.TipoUtente != "Collaboratore"))
                        return Json(new { success = false, message = "Non hai i permessi per modificare questa professione." });

                    if (utente.TipoUtente == "Collaboratore")
                    {
                        var relazione = db.RelazioneUtenti.FirstOrDefault(r =>
                            r.ID_UtenteAssociato == idUtente &&
                            r.ID_Utente == op.ID_UtenteCollegato &&
                            r.Stato == "Attivo");

                        var idMenu = db.Menu.FirstOrDefault(m => m.NomeMenu == "Professioni")?.ID_Menu ?? 0;
                        bool haPermesso = db.Permessi.Any(p => p.ID_Utente == idUtente && p.ID_Menu == idMenu && (p.Modifica ?? false));

                        if (relazione == null || !haPermesso)
                            return Json(new { success = false, message = "Non hai i permessi per modificare questa professione." });
                    }
                }
                */

                // Confronto modifiche
                var modifiche = new List<string>();
                void Confronta(string campo, object valO, object valN)
                {
                    if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                        modifiche.Add($"{campo}: '{valO}' → '{valN}'");
                }

                Confronta("Codice", originale.Codice, Request.Form["Codice"]);
                Confronta("Descrizione", originale.Descrizione, Request.Form["Descrizione"]);

                // 🔴 RIMOSSO: confronto su ID_ProfessionistaRiferimento
                // Confronta("ID_ProfessionistaRiferimento", originale.ID_ProfessionistaRiferimento, Request.Form["ID_ProfessionistaRiferimento"]);

                Confronta("PercentualeContributoIntegrativo", originale.PercentualeContributoIntegrativo, Request.Form["PercentualeContributoIntegrativo"]);


                // Se ci sono modifiche → archivia
                if (modifiche.Any())
                {
                    int maxVersion = db.Professioni_a
                        .Where(p => p.ID_Archivio == originale.ProfessioniID)
                        .Select(p => (int?)p.NumeroVersione)
                        .Max() ?? 0;

                    int nuovaVersione = maxVersion + 1;

                    var archivio = new Professioni_a
                    {
                        ID_Archivio = originale.ProfessioniID,
                        Codice = originale.Codice,
                        Descrizione = originale.Descrizione,

                        // 🔴 RIMOSSO: salvataggio obbligatorio del professionista
                        // ID_ProfessionistaRiferimento = originale.ID_ProfessionistaRiferimento,

                        PercentualeContributoIntegrativo = originale.PercentualeContributoIntegrativo,
                        NumeroVersione = nuovaVersione,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        ModificheTestuali = $"Modifica effettuata da ID_Utente = {idUtente} il {DateTime.Now:g}:\n- {string.Join("\n- ", modifiche)}",
                    };

                    db.Professioni_a.Add(archivio);
                }

                // Aggiorna i dati
                originale.Codice = Request.Form["Codice"];
                originale.Descrizione = Request.Form["Descrizione"];

                // 🔴 RIMOSSO: obbligo di aggiornare sempre il professionista
                // originale.ID_ProfessionistaRiferimento = string.IsNullOrEmpty(Request.Form["ID_ProfessionistaRiferimento"])
                //     ? (int?)null : int.Parse(Request.Form["ID_ProfessionistaRiferimento"]);

                originale.PercentualeContributoIntegrativo = string.IsNullOrWhiteSpace(Request.Form["PercentualeContributoIntegrativo"])
                     ? (decimal?)null : decimal.Parse(Request.Form["PercentualeContributoIntegrativo"], CultureInfo.InvariantCulture);

                db.SaveChanges();

                return Json(new { success = true, message = "Professione modificata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult EliminaProfessione(int id)
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non identificato." });

                var professione = db.Professioni.FirstOrDefault(p => p.ProfessioniID == id);
                if (professione == null)
                    return Json(new { success = false, message = "Professione non trovata." });

                // === Archiviazione prima dell'eliminazione ===
                int versioneAttuale = db.Professioni_a
                    .Where(a => a.ID_Archivio == professione.ProfessioniID)
                    .Select(a => (int?)a.NumeroVersione)
                    .Max() ?? 0;

                var archivio = new Professioni_a
                {
                    ID_Archivio = professione.ProfessioniID,
                    Codice = professione.Codice,
                    Descrizione = professione.Descrizione,

                    // 🔴 Qui veniva copiato sempre l’ID_ProfessionistaRiferimento obbligatorio.
                    // Se vuoi renderlo opzionale → commentalo o lascialo libero.
                    // ID_ProfessionistaRiferimento = professione.ID_ProfessionistaRiferimento,

                    PercentualeContributoIntegrativo = professione.PercentualeContributoIntegrativo,
                    NumeroVersione = versioneAttuale + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = "Eliminazione definitiva della professione"
                };
                db.Professioni_a.Add(archivio);

                // === Eliminazione effettiva ===
                db.Professioni.Remove(professione);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Professione eliminata definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante l'eliminazione: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetProfessione(int id)
        {
            var prof = db.Professioni.FirstOrDefault(p => p.ProfessioniID == id);

            if (prof == null)
            {
                return Json(new { success = false, message = "Professione non trovata." }, JsonRequestBehavior.AllowGet);
            }

            // 🔴 OBBLIGO ATTUALE:
            // qui cerchi sempre il nome del professionista collegato.
            // Se ID_ProfessionistaRiferimento è null o non esiste, nomeProfessionista rimane null.
            // Per renderlo opzionale non serve bloccare: basta gestirlo come null.
            var nomeProfessionista = (prof.ID_ProfessionistaRiferimento != null)
                ? db.Utenti
                    .Where(u => u.ID_Utente == prof.ID_ProfessionistaRiferimento)
                    .Select(u => u.Nome + " " + u.Cognome)
                    .FirstOrDefault()
                : null; // 👉 Se non c’è professionista collegato, resta null

            return Json(new
            {
                success = true,
                professione = new
                {
                    prof.ProfessioniID,
                    prof.Codice,
                    prof.Descrizione,

                    // 🔴 Qui l’ID del professionista diventa opzionale
                    ID_ProfessionistaRiferimento = prof.ID_ProfessionistaRiferimento,

                    PercentualeContributoIntegrativo = prof.PercentualeContributoIntegrativo,

                    // Se non c’è professionista, questo campo sarà null/vuoto
                    NomeProfessionista = nomeProfessionista
                }
            }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region TEAM

        public ActionResult GestioneTeam()
        {
            ViewBag.Title = "Gestione Team";
            return View("~/Views/Team/GestioneTeam.cshtml");
        }

        [HttpGet]
        public ActionResult GestioneTeamList(string filtroNome = "", string statoFiltro = "Tutti")
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return PartialView("~/Views/Team/_GestioneTeamList.cshtml", new List<TeamProfessionistiViewModel>());

            // ✅ Carica permessi per tabella "Team"
            int idMenuTeam = db.Menu.FirstOrDefault(m => m.NomeMenu == "Team")?.ID_Menu ?? 0;

            var permessi = db.Permessi
                .Where(p => p.ID_Utente == idUtente && p.ID_Menu == idMenuTeam)
                .ToList();

            ViewBag.Permessi = permessi;
            ViewBag.PuoAggiungere = permessi.Any(p => p.Aggiungi == true);
            ViewBag.PuoModificare = permessi.Any(p => p.Modifica == true);
            ViewBag.PuoEliminare = permessi.Any(p => p.Elimina == true);

            // ✅ Carica i team (senza filtro attivo/disattivo per ora)
            var teamFiltrati = db.TeamProfessionisti
                .Where(t => string.IsNullOrEmpty(filtroNome) || t.Nome.Contains(filtroNome))
                .ToList();

            // ✅ Carica tutti i professionisti attivi + categoria (professione)
            var professionisti = db.OperatoriSinergia
                 .Where(o => o.TipoCliente == "Professionista" && o.Stato == "Attivo")
                 .ToList() // 👈 switch to LINQ to Objects
                 .Select(o =>
                 {
                     var professione = db.Professioni.FirstOrDefault(p => p.ProfessioniID == o.ID_Professione);
                     return new SelectListItem
                     {
                         Value = o.ID_Operatore.ToString(),
                         Text = o.Nome + " " + o.Cognome + (professione != null ? $" ({professione.Descrizione})" : "")
                     };
                 })
                 .ToList();


            ViewBag.Professionisti = professionisti;

            // ✅ Mapping in ViewModel
            var teamVM = teamFiltrati.Select(t => new TeamProfessionistiViewModel
            {
                ID_Team = t.ID_Team,
                Nome = t.Nome,
                Descrizione = t.Descrizione,
                Attivo = t.Attivo,
                DataCreazione = t.DataCreazione
            }).ToList();

            return PartialView("~/Views/Team/_GestioneTeamList.cshtml", teamVM);
        }

        [HttpPost]
        public ActionResult CreaTeam()
        {
            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non identificato." });

            try
            {
                DateTime now = DateTime.Now;

                // 1️⃣ Crea nuovo Team (senza ancora fare SaveChanges)
                var team = new TeamProfessionisti
                {
                    Nome = Request.Form["Nome"],
                    Descrizione = Request.Form["Descrizione"],
                    Attivo = true,
                    ID_UtenteCreatore = idUtente,
                    DataCreazione = now
                };

                db.TeamProfessionisti.Add(team);

                // 2️⃣ Archivio: verrà associato dopo che il team è tracciato
                var teamArch = new TeamProfessionisti_a
                {
                    // ID_Team verrà popolato da EF prima del SaveChanges finale
                    ID_VersioneTeam = team.ID_Team,
                    Nome = team.Nome,
                    Descrizione = team.Descrizione,
                    Attivo = team.Attivo,
                    ID_UtenteCreatore = team.ID_UtenteCreatore,
                    DataCreazione = team.DataCreazione,
                    NumeroVersione = 1,
                    DataArchiviazione = now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = "Inserimento iniziale"
                };

                db.TeamProfessionisti_a.Add(teamArch);

                // 3️⃣ Recupera i professionisti selezionati dal campo hidden
                string[] idProfessionisti = (Request.Form["MembriSelezionati"] ?? "")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (idProfessionisti != null)
                {
                    foreach (string idStr in idProfessionisti)
                    {
                        if (int.TryParse(idStr, out int idProf))
                        {
                            var membro = new MembriTeam
                            {
                                ID_Team = team.ID_Team,
                                ID_Professionista = idProf,
                                Attivo = true,
                                ID_UtenteCreatore = idUtente,
                                DataCreazione = now
                            };
                            db.MembriTeam.Add(membro);
                        }
                    }
                }

                // 4️⃣ ✅ Unico salvataggio finale
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Team creato con successo." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante la creazione: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult ModificaTeam()
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non identificato." });

                int id = int.Parse(Request.Form["ID_Team"] ?? "0");
                var originale = db.TeamProfessionisti.FirstOrDefault(t => t.ID_Team == id);
                if (originale == null)
                    return Json(new { success = false, message = "Team non trovato." });

                // 🔒 Permessi collaboratore
                if (utente.TipoUtente == "Collaboratore")
                {
                    var idMenu = db.Menu.FirstOrDefault(m => m.NomeMenu == "Team")?.ID_Menu ?? 0;
                    bool haPermesso = db.Permessi.Any(p => p.ID_Utente == idUtente && p.ID_Menu == idMenu && (p.Modifica ?? false));
                    if (!haPermesso)
                        return Json(new { success = false, message = "Non hai i permessi per modificare questo team." });
                }

                // 🔍 Confronto modifiche
                var modifiche = new List<string>();
                void Confronta(string campo, object valO, object valN)
                {
                    if ((valO ?? "").ToString().Trim() != (valN ?? "").ToString().Trim())
                        modifiche.Add($"{campo}: '{valO}' → '{valN}'");
                }

                string nuovoNome = Request.Form["Nome"];
                string nuovaDescrizione = Request.Form["Descrizione"];
                bool nuovoStatoAttivo = Request.Form["Attivo"] == "true";

                Confronta("Nome", originale.Nome, nuovoNome);
                Confronta("Descrizione", originale.Descrizione, nuovaDescrizione);
                Confronta("Attivo", originale.Attivo, nuovoStatoAttivo);

                // 📚 Archivia Team se ci sono modifiche
                if (modifiche.Any())
                {
                    int maxVersion = db.TeamProfessionisti_a
                        .Where(t => t.ID_VersioneTeam == originale.ID_Team)
                        .Select(t => (int?)t.NumeroVersione)
                        .Max() ?? 0;

                    db.TeamProfessionisti_a.Add(new TeamProfessionisti_a
                    {
                        ID_VersioneTeam = originale.ID_Team,
                        Nome = originale.Nome,
                        Descrizione = originale.Descrizione,
                        Attivo = originale.Attivo,
                        ID_UtenteCreatore = originale.ID_UtenteCreatore,
                        DataCreazione = originale.DataCreazione,
                        ID_UtenteUltimaModifica = originale.ID_UtenteUltimaModifica,
                        DataUltimaModifica = originale.DataUltimaModifica,
                        NumeroVersione = maxVersion + 1,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        ModificheTestuali = $"Modifica effettuata da ID_Utente = {idUtente} il {DateTime.Now:g}:\n- {string.Join("\n- ", modifiche)}"
                    });
                }

                // 💾 Aggiorna dati team
                originale.Nome = nuovoNome;
                originale.Descrizione = nuovaDescrizione;
                originale.Attivo = nuovoStatoAttivo;
                originale.ID_UtenteUltimaModifica = idUtente;
                originale.DataUltimaModifica = DateTime.Now;

                // 🔄 Archivia e Rimuovi membri attuali
                var membriAttuali = db.MembriTeam.Where(m => m.ID_Team == id).ToList();
                int versioneBase = 1;
                foreach (var membro in membriAttuali)
                {
                    db.MembriTeam_a.Add(new MembriTeam_a
                    {
                        ID_MembroTeam = membro.ID_MembroTeam,
                        ID_Team = membro.ID_Team,
                        ID_Professionista = membro.ID_Professionista,
                        PercentualeCondivisione = membro.PercentualeCondivisione,
                        Attivo = membro.Attivo,
                        ID_UtenteCreatore = membro.ID_UtenteCreatore,
                        DataCreazione = membro.DataCreazione,
                        ID_UtenteUltimaModifica = membro.ID_UtenteUltimaModifica,
                        DataUltimaModifica = membro.DataUltimaModifica,
                        NumeroVersione = versioneBase++,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        ModificheTestuali = "Rimozione da team durante modifica"
                    });
                }
                db.MembriTeam.RemoveRange(membriAttuali);

                // 🆕 Inserimento nuovi membri
                string selectedIds = Request.Form["MembriSelezionati"];
                if (!string.IsNullOrWhiteSpace(selectedIds))
                {
                    var idProfessionisti = selectedIds
                      .Split(',')
                      .Select(idStr => int.TryParse(idStr, out var i) ? i : 0)
                      .Where(i => i > 0);

                    foreach (int idProf in idProfessionisti)
                    {
                        db.MembriTeam.Add(new MembriTeam
                        {
                            ID_Team = id,
                            ID_Professionista = idProf,
                            PercentualeCondivisione = 0,
                            Attivo = true,
                            ID_UtenteCreatore = idUtente,
                            DataCreazione = DateTime.Now
                        });
                    }
                }

                // ✅ Unico salvataggio finale
                db.SaveChanges();

                return Json(new { success = true, message = "Team modificato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la modifica: " + ex.Message });
            }
        }


        [HttpGet]
        public ActionResult GetTeam(int id)
        {
            var team = db.TeamProfessionisti.FirstOrDefault(t => t.ID_Team == id);
            if (team == null)
            {
                return Json(new { success = false, message = "Team non trovato." }, JsonRequestBehavior.AllowGet);
            }

            var nomeCreatore = db.Utenti
                .Where(u => u.ID_Utente == team.ID_UtenteCreatore)
                .Select(u => u.Nome + " " + u.Cognome)
                .FirstOrDefault();

            // 🔁 Carica professionisti associati (MembriTeam)
            var membri = db.MembriTeam
                .Where(m => m.ID_Team == id)
                .Join(
                    db.OperatoriSinergia.Where(o => o.TipoCliente == "Professionista"),
                    m => m.ID_Professionista,
                    o => o.ID_Operatore,
                    (m, o) => new
                    {
                        ID = o.ID_Operatore,
                        Nome = o.Nome + " " + o.Cognome
                    })
                .ToList();

            return Json(new
            {
                success = true,
                team = new
                {
                    team.ID_Team,
                    team.Nome,
                    team.Descrizione,
                    team.Attivo,
                    team.ID_UtenteCreatore,
                    team.DataCreazione,
                    team.ID_UtenteUltimaModifica,
                    team.DataUltimaModifica,
                    NomeCreatore = nomeCreatore,
                    Membri = membri.Select(m => m.ID).ToList(), // solo ID per selezione automatica
                    MembriDettaglio = membri // dettagli per lista visiva
                }
            }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult EliminaTeam(int id)
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
                if (utente == null)
                    return Json(new { success = false, message = "Utente non identificato." });

                var team = db.TeamProfessionisti.FirstOrDefault(t => t.ID_Team == id);
                if (team == null)
                    return Json(new { success = false, message = "Team non trovato." });

                // === Archiviazione Team ===
                int versioneAttuale = db.TeamProfessionisti_a
                    .Where(a => a.ID_VersioneTeam == team.ID_Team)
                    .Select(a => (int?)a.NumeroVersione)
                    .Max() ?? 0;

                var archivio = new TeamProfessionisti_a
                {
                    ID_VersioneTeam = team.ID_Team,
                    Nome = team.Nome,
                    Descrizione = team.Descrizione,
                    Attivo = team.Attivo,
                    ID_UtenteCreatore = team.ID_UtenteCreatore,
                    DataCreazione = team.DataCreazione,
                    ID_UtenteUltimaModifica = team.ID_UtenteUltimaModifica,
                    DataUltimaModifica = team.DataUltimaModifica,
                    NumeroVersione = versioneAttuale + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = "Eliminazione definitiva del team"
                };
                db.TeamProfessionisti_a.Add(archivio);

                // === Archiviazione MembriTeam ===
                var membri = db.MembriTeam.Where(m => m.ID_Team == id).ToList();

                int versioneMembro = 1;
                foreach (var membro in membri)
                {
                    var archMembro = new MembriTeam_a
                    {
                        ID_MembroTeam = membro.ID_MembroTeam,
                        ID_Team = membro.ID_Team,
                        ID_Professionista = membro.ID_Professionista,
                        PercentualeCondivisione = membro.PercentualeCondivisione,
                        Attivo = membro.Attivo,
                        ID_UtenteCreatore = membro.ID_UtenteCreatore,
                        DataCreazione = membro.DataCreazione,
                        ID_UtenteUltimaModifica = membro.ID_UtenteUltimaModifica,
                        DataUltimaModifica = membro.DataUltimaModifica,
                        NumeroVersione = versioneMembro++,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idUtente,
                        ModificheTestuali = "Eliminazione membro team assieme al team"
                    };
                    db.MembriTeam_a.Add(archMembro);
                }

                // === Eliminazione Membri e Team ===
                db.MembriTeam.RemoveRange(membri);
                db.TeamProfessionisti.Remove(team);

                db.SaveChanges();

                return Json(new { success = true, message = "✅ Team e membri archiviati ed eliminati correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante l'eliminazione: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult RimuoviMembroTeam(int idTeam, int idProfessionista)
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();

                var membro = db.MembriTeam.FirstOrDefault(m => m.ID_Team == idTeam && m.ID_Professionista == idProfessionista);
                if (membro == null)
                {
                    return Json(new { success = false, message = "Membro non trovato." });
                }

                // Versionamento prima dell'eliminazione
                int ultimaVersione = db.MembriTeam_a
                    .Where(a => a.ID_VersioneMembroTeam == membro.ID_MembroTeam)
                    .Select(a => (int?)a.NumeroVersione)
                    .Max() ?? 0;

                db.MembriTeam_a.Add(new MembriTeam_a
                {
                    ID_VersioneMembroTeam = membro.ID_MembroTeam,
                    ID_Team = membro.ID_Team,
                    ID_Professionista = membro.ID_Professionista,
                    PercentualeCondivisione = membro.PercentualeCondivisione,
                    Attivo = membro.Attivo,
                    ID_UtenteCreatore = membro.ID_UtenteCreatore,
                    DataCreazione = membro.DataCreazione,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    DataArchiviazione = DateTime.Now,
                    ID_UtenteArchiviazione = idUtente,
                    ModificheTestuali = "Eliminazione del membro dal team"
                });


                db.MembriTeam.Remove(membro);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Membro rimosso correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante la rimozione: " + ex.Message });
            }
        }

        #endregion

        #region COSTI DI PROGETTO (ANAGRAFICA COSTI PRATICA) 

        public ActionResult GestioneSpeseProgetto()
        {
            return View("~/Views/CostiProgetto/GestioneCostiProgetto.cshtml");
        }

        public ActionResult GestioneSpeseProgettoList()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            if (utenteCorrente == null)
            {
                ViewBag.MessaggioErrore = "Utente non autenticato o sessione scaduta.";
                return PartialView("~/Views/Shared/_MessaggioErrore.cshtml");
            }

            IQueryable<AnagraficaCostiPratica> query = db.AnagraficaCostiPratica
                .Where(t => t.Stato != "Eliminato");

            // 🔐 Gestione Permessi (uguale)
            bool puoAggiungere = false, puoModificare = false, puoEliminare = false;
            if (utenteCorrente.TipoUtente == "Admin")
                puoAggiungere = puoModificare = puoEliminare = true;
            else if (utenteCorrente.TipoUtente == "Professionista" || utenteCorrente.TipoUtente == "Collaboratore")
            {
                var permessiDb = db.Permessi.Where(p => p.ID_Utente == idUtenteCorrente).ToList();
                puoAggiungere = permessiDb.Any(p => p.Aggiungi == true);
                puoModificare = permessiDb.Any(p => p.Modifica == true);
                puoEliminare = permessiDb.Any(p => p.Elimina == true);
            }

            var lista = query
                .OrderBy(t => t.ID_AnagraficaCosto)
                .ToList()
                .Select(t => new AnagraficaCostiPraticaViewModel
                {
                    ID_AnagraficaCosto = t.ID_AnagraficaCosto,
                    Nome = t.Nome,
                    Descrizione = t.Descrizione,
                    Stato = t.Stato,
                    TipoCreatore = t.TipoCreatore,
                    ID_UtenteCreatore = t.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = t.ID_UtenteUltimaModifica,
                    DataUltimaModifica = t.DataUltimaModifica,

                    // 🏷️ Categoria
                    ID_Categoria = t.ID_Categoria,
                    NomeCategoria = db.CategorieCosti
                        .Where(c => c.ID_Categoria == t.ID_Categoria)
                        .Select(c => c.Nome)
                        .FirstOrDefault(),

                    NomeCreatore = db.Utenti
                        .Where(u => u.ID_Utente == t.ID_UtenteCreatore)
                        .Select(u => u.Nome + " " + u.Cognome)
                        .FirstOrDefault(),

                    NomeUltimaModifica = t.ID_UtenteUltimaModifica.HasValue
                        ? db.Utenti
                            .Where(u => u.ID_Utente == t.ID_UtenteUltimaModifica.Value)
                            .Select(u => u.Nome + " " + u.Cognome)
                            .FirstOrDefault()
                        : null
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

            // 📋 Popola ViewBag per dropdown categorie (modale)
            ViewBag.CategorieCosti = db.CategorieCosti
                .Where(c => c.Attivo)
                .OrderBy(c => c.Nome)
                .Select(c => new SelectListItem
                {
                    Value = c.ID_Categoria.ToString(),
                    Text = c.Nome
                })
                .ToList();

            return PartialView("~/Views/CostiProgetto/_GestioneCostiProgettoList.cshtml", lista);
        }

        [HttpPost]
        public ActionResult CreaCostoProgetto(AnagraficaCostiPraticaViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compila correttamente tutti i campi obbligatori." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                db.Permessi.Any(p => p.ID_Utente == idUtente && p.Aggiungi == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per aggiungere costi di progetto." });

            try
            {
                var nuovo = new AnagraficaCostiPratica
                {
                    Nome = model.Nome?.Trim(),
                    Descrizione = model.Descrizione?.Trim(),
                    ID_Categoria = model.ID_Categoria,
                    Attivo = true,
                    DataCreazione = DateTime.Now,
                    ID_UtenteCreatore = idUtente,
                    TipoCreatore = utente.TipoUtente,
                    Stato = model.Stato,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now
                };

                db.AnagraficaCostiPratica.Add(nuovo);
                db.SaveChanges();

                // 📦 Archivia versione iniziale
                db.AnagraficaCostiPratica_a.Add(new AnagraficaCostiPratica_a
                {
                    ID_AnagraficaCosto_a = nuovo.ID_AnagraficaCosto,
                    Nome = nuovo.Nome,
                    Descrizione = nuovo.Descrizione,
                    ID_Categoria = nuovo.ID_Categoria,
                    Attivo = nuovo.Attivo,
                    Stato = nuovo.Stato,
                    DataCreazione = nuovo.DataCreazione,
                    ID_UtenteCreatore = nuovo.ID_UtenteCreatore,
                    TipoCreatore = nuovo.TipoCreatore,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = 1,
                    ModificheTestuali = $"✅ Inserimento effettuato da ID_Utente = {idUtente} il {DateTime.Now:g}",
                });

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo di progetto creato correttamente." });
            }
            catch (Exception ex)
            {
                string errore = ex.Message;
                if (ex.InnerException != null)
                    errore += " → " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null)
                    errore += " → " + ex.InnerException.InnerException.Message;

                return Json(new { success = false, message = "❌ Errore durante il salvataggio: " + errore });
            }

        }

        [HttpPost]
        public ActionResult ModificaCostoProgetto(AnagraficaCostiPraticaViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Compila correttamente tutti i campi obbligatori." });

            int idUtente = UserManager.GetIDUtenteCollegato();
            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);
            if (utente == null)
                return Json(new { success = false, message = "Utente non autenticato." });

            bool autorizzato = utente.TipoUtente == "Admin" ||
                db.Permessi.Any(p => p.ID_Utente == idUtente && p.Modifica == true);
            if (!autorizzato)
                return Json(new { success = false, message = "Non hai i permessi per modificare i costi di progetto." });

            try
            {
                var esistente = db.AnagraficaCostiPratica.FirstOrDefault(c => c.ID_AnagraficaCosto == model.ID_AnagraficaCosto);
                if (esistente == null)
                    return Json(new { success = false, message = "Costo di progetto non trovato." });

                int ultimaVersione = db.AnagraficaCostiPratica_a
                    .Where(a => a.ID_AnagraficaCosto_a == esistente.ID_AnagraficaCosto)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => a.NumeroVersione)
                    .FirstOrDefault();

                List<string> modifiche = new List<string>();
                void CheckModifica(string campo, object oldVal, object newVal)
                {
                    if ((oldVal?.ToString() ?? "") != (newVal?.ToString() ?? ""))
                        modifiche.Add($"- {campo}: '{oldVal}' → '{newVal}'");
                }

                // 🔍 Confronto campi
                CheckModifica("Nome", esistente.Nome, model.Nome?.Trim());
                CheckModifica("Descrizione", esistente.Descrizione, model.Descrizione?.Trim());
                CheckModifica("Categoria", esistente.ID_Categoria, model.ID_Categoria);
                CheckModifica("Attivo", esistente.Attivo, model.Attivo);
                CheckModifica("Stato", esistente.Stato, model.Stato);

                // ✏️ Aggiorna
                esistente.Nome = model.Nome?.Trim();
                esistente.Descrizione = model.Descrizione?.Trim();
                esistente.ID_Categoria = model.ID_Categoria;
                esistente.Attivo = model.Stato == "Attivo";
                esistente.Stato = model.Stato;
                esistente.ID_UtenteUltimaModifica = idUtente;
                esistente.DataUltimaModifica = DateTime.Now;

                // 🗂️ Archivia versione precedente
                db.AnagraficaCostiPratica_a.Add(new AnagraficaCostiPratica_a
                {
                    ID_AnagraficaCosto_a = esistente.ID_AnagraficaCosto,
                    Nome = esistente.Nome,
                    Descrizione = esistente.Descrizione,
                    ID_Categoria = esistente.ID_Categoria,
                    Attivo = esistente.Attivo,
                    Stato = esistente.Stato,
                    DataCreazione = esistente.DataCreazione,
                    ID_UtenteCreatore = esistente.ID_UtenteCreatore,
                    TipoCreatore = esistente.TipoCreatore,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = modifiche.Any()
                        ? $"✏️ Modifiche effettuate da ID_Utente = {idUtente} il {DateTime.Now:g}:\n{string.Join("\n", modifiche)}"
                        : "Modifica salvata senza cambiamenti rilevanti",

                });

                db.SaveChanges();
                return Json(new { success = true, message = "✅ Costo di progetto modificato correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "❌ Errore durante la modifica: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetCostoProgetto(int id)
        {
            var costo = db.AnagraficaCostiPratica
                .Where(c => c.ID_AnagraficaCosto == id && c.Stato != "Eliminato")
                .Select(c => new AnagraficaCostiPraticaViewModel
                {
                    ID_AnagraficaCosto = c.ID_AnagraficaCosto,
                    Nome = c.Nome,
                    Descrizione = c.Descrizione,
                    Attivo = c.Attivo,
                    Stato = c.Stato,
                    ID_Categoria = c.ID_Categoria,  // 🏷️ nuovo
                    NomeCategoria = db.CategorieCosti
                    .Where(cat => cat.ID_Categoria == c.ID_Categoria)
                    .Select(cat => cat.Nome)
                    .FirstOrDefault(),
                    DataCreazione = (DateTime)c.DataCreazione,
                    ID_UtenteCreatore = c.ID_UtenteCreatore,
                    TipoCreatore = c.TipoCreatore,
                    ID_UtenteUltimaModifica = c.ID_UtenteUltimaModifica,
                    DataUltimaModifica = c.DataUltimaModifica
                })
                .FirstOrDefault();

            if (costo == null)
                return Json(new { success = false, message = "Costo di progetto non trovato." }, JsonRequestBehavior.AllowGet);

            return Json(new { success = true, costo }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult EliminaCostoProgetto(int id)
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
                    return Json(new { success = false, message = "Non hai i permessi per eliminare il costo di progetto." });

                // 🗑️ Recupera il costo
                var costo = db.AnagraficaCostiPratica.FirstOrDefault(c => c.ID_AnagraficaCosto == id);
                if (costo == null)
                    return Json(new { success = false, message = "Costo di progetto non trovato." });

                // 🔁 Numero versione
                int ultimaVersione = db.AnagraficaCostiPratica_a
                    .Where(a => a.ID_AnagraficaCosto_a == costo.ID_AnagraficaCosto)
                    .OrderByDescending(a => a.NumeroVersione)
                    .Select(a => (int?)a.NumeroVersione)
                    .FirstOrDefault() ?? 0;

                // 💾 Archivia costo eliminato
                db.AnagraficaCostiPratica_a.Add(new AnagraficaCostiPratica_a
                {
                    ID_AnagraficaCosto_a = costo.ID_AnagraficaCosto,
                    Nome = costo.Nome,
                    Descrizione = costo.Descrizione,
                    Attivo = costo.Attivo,
                    ID_Categoria = costo.ID_Categoria,
                    Stato = "Eliminato",
                    DataCreazione = costo.DataCreazione,
                    TipoCreatore = costo.TipoCreatore,
                    ID_UtenteCreatore = costo.ID_UtenteCreatore,
                    ID_UtenteUltimaModifica = idUtenteCorrente,
                    DataUltimaModifica = DateTime.Now,
                    NumeroVersione = ultimaVersione + 1,
                    ModificheTestuali = $"🗑️ Eliminazione effettuata da ID_Utente = {idUtenteCorrente} il {DateTime.Now:g}"
                });

                db.AnagraficaCostiPratica.Remove(costo);
                db.SaveChanges();

                return Json(new { success = true, message = "✅ Costo di progetto eliminato definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante l'eliminazione: " + ex.Message });
            }
        }

        #endregion

        #region GESTIONE CATEGORIE PRATICHE

        
        public ActionResult GestioneCategorie()
        {
            return View("~/Views/GestioneCategoriePratiche/GestioneCategoriePratiche.cshtml");
        }

 
        [HttpGet]
        public ActionResult GestioneCategoriePraticheList(string filtro = "", string statoFiltro = "Tutti")
        {
            Trace.WriteLine("══════════════════════════════════════════════");
            Trace.WriteLine("📌 [GestioneCategoriePraticheList] INIZIO");

            int idUtente = UserManager.GetIDUtenteCollegato();
            Trace.WriteLine($"👤 ID Utente collegato = {idUtente}");

            var utente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtente);

            if (utente == null)
            {
                Trace.WriteLine("❌ Utente NON trovato — ritorno lista vuota");
                return PartialView("~/Views/GestioneCategoriePratiche/_GestioneCategoriePraticheList.cshtml",
                    new List<CategoriaPraticaViewModel>());
            }

            Trace.WriteLine($"✔ Utente trovato: {utente.Nome} {utente.Cognome}, Tipo: {utente.TipoUtente}");

            // =====================================================
            // PERMESSI
            // =====================================================
            bool puoAggiungere = utente.TipoUtente == "Admin";
            bool puoModificare = utente.TipoUtente == "Admin";
            bool puoEliminare = utente.TipoUtente == "Admin";

            Trace.WriteLine("🔐 PERMESSI:");
            Trace.WriteLine($"   ➕ Aggiungi: {puoAggiungere}");
            Trace.WriteLine($"   ✏ Modifica: {puoModificare}");
            Trace.WriteLine($"   🗑 Elimina: {puoEliminare}");

            ViewBag.PuoAggiungere = puoAggiungere;
            ViewBag.PuoModificare = puoModificare;
            ViewBag.PuoEliminare = puoEliminare;

            // =====================================================
            // QUERY BASE
            // =====================================================
            Trace.WriteLine("📌 Costruzione query...");

            var query = db.AnagraficaCategoriePratiche.AsQueryable();
            Trace.WriteLine("✔ Query iniziale caricata");

            // FILTRO TESTO
            if (!string.IsNullOrWhiteSpace(filtro))
            {
                Trace.WriteLine($"🔍 Filtro applicato: '{filtro}'");
                query = query.Where(c => c.Tipo.Contains(filtro) || (c.Note ?? "").Contains(filtro));
            }

            // FILTRO STATO
            Trace.WriteLine($"🔍 Stato filtro = {statoFiltro}");

            if (statoFiltro == "Attivi")
                query = query.Where(c => c.Attivo == true);
            else if (statoFiltro == "Inattivi")
                query = query.Where(c => c.Attivo == false);

            // MATERIALIZZA LISTA DAL DB
            var listaDb = query.OrderBy(c => c.Tipo).ToList();
            Trace.WriteLine($"📌 Totale categorie DB = {listaDb.Count}");

            // =====================================================
            // COSTRUZIONE VIEWMODEL
            // =====================================================
            Trace.WriteLine("📌 Costruzione ViewModel...");

            var lista = listaDb.Select(c => new CategoriaPraticaViewModel
            {
                ID_CategoriaPratica = c.ID_CategoriaPratica,
                Tipo = c.Tipo,
                Note = c.Note,
                Attivo = c.Attivo,

                PuoModificare = puoModificare,
                PuoEliminare = puoEliminare,
                UtenteCorrenteHaPermessi = (puoModificare || puoEliminare)
            }).ToList();

            Trace.WriteLine($"✔ ViewModel costruito correttamente. Totale = {lista.Count}");
            Trace.WriteLine("══════════════════════════════════════════════");

            return PartialView("~/Views/GestioneCategoriePratiche/_GestioneCategoriePraticheList.cshtml", lista);
        }


        [HttpGet]
        public ActionResult GetCategoriaPratica(int id)
        {
            var c = db.AnagraficaCategoriePratiche.FirstOrDefault(x => x.ID_CategoriaPratica == id);

            if (c == null)
                return Json(new { success = false, message = "Categoria non trovata." },
                    JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                data = new CategoriaPraticaViewModel
                {
                    ID_CategoriaPratica = c.ID_CategoriaPratica,
                    Tipo = c.Tipo,
                    Note = c.Note,
                    Attivo = c.Attivo
                }
            }, JsonRequestBehavior.AllowGet);
        }


        [HttpPost]
        public ActionResult CreaCategoriaPratica(
            string Tipo,
            string Note)
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                if (idUtente <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                if (string.IsNullOrWhiteSpace(Tipo))
                    return Json(new { success = false, message = "Il campo Categoria è obbligatorio." });

                var cat = new AnagraficaCategoriePratiche
                {
                    Tipo = Tipo.Trim(),
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    Materia = null,
                    Attivo = true,

                    ID_UtenteCreatore = idUtente,
                    DataCreazione = DateTime.Now,
                    ID_UtenteUltimaModifica = idUtente,
                    DataUltimaModifica = DateTime.Now
                };

                db.AnagraficaCategoriePratiche.Add(cat);
                db.SaveChanges();

                AggiungiArchivio(cat, idUtente);

                return Json(new { success = true, message = "Categoria creata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }




        [HttpPost]
        public ActionResult ModificaCategoriaPratica(
      int ID_CategoriaPratica,
      string Tipo,
      string Note,
      bool Attivo)
        {
            try
            {
                int idUtente = UserManager.GetIDUtenteCollegato();
                if (idUtente <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                var cat = db.AnagraficaCategoriePratiche
                            .FirstOrDefault(x => x.ID_CategoriaPratica == ID_CategoriaPratica);

                if (cat == null)
                    return Json(new { success = false, message = "Categoria non trovata." });

                cat.Tipo = Tipo?.Trim();
                cat.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
                cat.Attivo = Attivo;
                cat.ID_UtenteUltimaModifica = idUtente;
                cat.DataUltimaModifica = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = "Categoria modificata correttamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult EliminaCategoriaPratica(int id)
        {
            try
            {
                var cat = db.AnagraficaCategoriePratiche.FirstOrDefault(x => x.ID_CategoriaPratica == id);
                if (cat == null)
                    return Json(new { success = false, message = "Categoria non trovata." });

                // 🔥 ELIMINAZIONE DEFINITIVA
                db.AnagraficaCategoriePratiche.Remove(cat);
                db.SaveChanges();

                return Json(new { success = true, message = "Categoria eliminata definitivamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }




        // =============================================================
        // 📦 FUNZIONE ARCHIVIO
        // =============================================================
        private void AggiungiArchivio(AnagraficaCategoriePratiche cat, int idUtente)
        {
            var arch = new AnagraficaCategoriePratiche_a
            {
                ID_CategoriaPratica = cat.ID_CategoriaPratica,
                Tipo = cat.Tipo,
                Note = cat.Note,
                Materia = null,
                Attivo = cat.Attivo,
                ID_UtenteCreatore = cat.ID_UtenteCreatore,
                DataCreazione = cat.DataCreazione,
                ID_UtenteUltimaModifica = cat.ID_UtenteUltimaModifica,
                DataUltimaModifica = cat.DataUltimaModifica,
                ID_UtenteArchiviazione = idUtente,
                DataArchiviazione = DateTime.Now
            };

            db.AnagraficaCategoriePratiche_a.Add(arch);
            db.SaveChanges();
        }


        #endregion

    }
}
