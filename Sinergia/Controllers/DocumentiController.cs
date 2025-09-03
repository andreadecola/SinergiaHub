using Sinergia.ActionFilters;
using Sinergia.App_Helpers;
using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sinergia.Controllers
{
    [PermissionsActionFilter]
    public class DocumentiController : Controller
    {
        private SinergiaDB db = new SinergiaDB();
        #region KNOWLEDGE AZIENDALE 

        [HttpGet]
        public ActionResult KnowledgeAziendale()
        {
            return View("~/Views/KnowledgeAziendale/KnowledgeAziendale.cshtml");
        }

        [HttpGet]
        public ActionResult KnowledgeAziendaleList()
        {
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
            var utenteCorrente = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteCorrente);

            // Query base
            IQueryable<KnowledgeAziendale> query = db.KnowledgeAziendale;

            // Filtro destinatario se non admin
            if (utenteCorrente != null && utenteCorrente.TipoUtente != "Admin")
            {
                query = query.Where(k => k.Destinatario == utenteCorrente.TipoUtente || k.Destinatario == "Entrambi");
            }

            // Query letture per l'utente corrente
            var lettureUtente = db.KnowledgeAziendaleLetture
                .Where(l => l.ID_Utente == idUtenteCorrente)
                .ToList();

            // Proiezione
            var lista = query
                .OrderByDescending(k => k.DataDocumento)
                .ToList()
                .Select(k =>
                {
                    var lettura = lettureUtente.FirstOrDefault(l => l.KnowledgeAziendaleID == k.KnowledgeAziendaleID);
                    return new KnowledgeAziendaleViewModel
                    {
                        KnowledgeAziendaleID = k.KnowledgeAziendaleID,
                        Tipo = k.Tipo,
                        Titolo = k.Titolo,
                        DataDocumento = k.DataDocumento,
                        Descrizione = k.Descrizione,
                        Destinatario = k.Destinatario,
                        AllegatoNome = k.AllegatoNome,
                        AllegatoPercorso = k.AllegatoPercorso,
                        Versione = k.Versione,
                        Vecchio = k.Vecchio,
                        CreatoDa = k.CreatoDa,
                        DataCreazione = k.DataCreazione,
                        ModificatoDa = k.ModificatoDa,
                        DataModifica = k.DataModifica,

                        // ✅ Campi lettura
                        Letto = lettura != null,
                        DataLettura = lettura?.DataLettura
                    };
                })
                .ToList();

            // Permessi
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
                }
                else
                {
                    var permessiDb = db.Permessi
                        .Where(p => p.ID_Utente == utenteCorrente.ID_Utente)
                        .ToList();

                    bool puoModificare = permessiDb.Any(p => p.Modifica ?? false);
                    bool puoEliminare = permessiDb.Any(p => p.Elimina ?? false);
                    puoAggiungere = permessiDb.Any(p => p.Aggiungi ?? false);

                    foreach (var doc in lista)
                    {
                        doc.PuoModificare = puoModificare;
                        doc.PuoEliminare = puoEliminare;
                    }
                }
            }

            ViewBag.Permessi = permessiUtente;
            ViewBag.PuoAggiungere = puoAggiungere;

            return PartialView("~/Views/KnowledgeAziendale/_KnowledgeAziendaleList.cshtml", lista);
        }


        [HttpPost]
        public ActionResult CreaKnowledgeAziendale()
        {
            int utenteId = UserManager.GetIDUtenteCollegato();
            if (utenteId <= 0) return Json(new { success = false, message = "Utente non autenticato." });

            var utenteLoggato = db.Utenti.Find(utenteId);
            if (utenteLoggato == null) return Json(new { success = false, message = "Utente non trovato." });

            if (utenteLoggato.TipoUtente != "Admin" && utenteLoggato.TipoUtente != "Professionista")
                return Json(new { success = false, message = "Non hai i permessi per creare documentazione." });

            try
            {
                var model = new KnowledgeAziendale
                {
                    Tipo = Request.Form["Tipo"],
                    Titolo = Request.Form["Titolo"],
                    DataDocumento = string.IsNullOrEmpty(Request.Form["DataDocumento"])
                                        ? DateTime.Now
                                        : DateTime.Parse(Request.Form["DataDocumento"]),
                    Descrizione = Request.Form["Descrizione"],
                    Destinatario = Request.Form["Destinatario"],
                    Versione = 1,
                    Vecchio = false,
                    CreatoDa = $"{utenteLoggato.Nome} {utenteLoggato.Cognome}",
                    DataCreazione = DateTime.Now
                };

                // 1) salvo senza allegati per ottenere l'ID
                db.KnowledgeAziendale.Add(model);
                db.SaveChanges();

                // 2) preparo la cartella per questo documento
                string baseRel = $"/Allegati/KnowledgeAziendale/{model.KnowledgeAziendaleID}/";
                string baseFis = Server.MapPath("~" + baseRel.TrimEnd('/'));
                if (!Directory.Exists(baseFis)) Directory.CreateDirectory(baseFis);

                // 3) salvo TUTTI i file caricati (input name="Allegati")
                var files = Request.Files;
                string primoNome = null;
                string primoPercorsoRel = null;

                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (f == null || f.ContentLength <= 0) continue;
                    if (!string.Equals(files.GetKey(i), "Allegati", StringComparison.OrdinalIgnoreCase)) continue;

                    // sanitizza nome e gestisci collisioni
                    var nome = Path.GetFileName(f.FileName) ?? "file";
                    nome = nome.Replace("..", "").Trim();
                    string destinazione = Path.Combine(baseFis, nome);
                    if (System.IO.File.Exists(destinazione))
                    {
                        string name = Path.GetFileNameWithoutExtension(nome);
                        string ext = Path.GetExtension(nome);
                        nome = $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
                        destinazione = Path.Combine(baseFis, nome);
                    }

                    f.SaveAs(destinazione);

                    var rel = baseRel + nome; // es. /Allegati/KnowledgeAziendale/15/manuale.pdf

                    // Metto il primo file nei campi della tabella (retro-compatibile)
                    if (primoNome == null)
                    {
                        primoNome = nome;
                        primoPercorsoRel = rel;
                    }
                }

                // 4) aggiorno il record con il primo allegato (se presente)
                if (primoNome != null)
                {
                    model.AllegatoNome = primoNome;
                    model.AllegatoPercorso = primoPercorsoRel;
                    db.SaveChanges();
                }

                return Json(new { success = true, message = "Documentazione creata con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }


        [HttpPost]
        public ActionResult ModificaKnowledgeAziendale(KnowledgeAziendaleViewModel model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dati non validi." });

            if (model.KnowledgeAziendaleID == 0)
                return Json(new { success = false, message = "ID documento non valido." });

            try
            {
                var originale = db.KnowledgeAziendale
                    .FirstOrDefault(k => k.KnowledgeAziendaleID == model.KnowledgeAziendaleID);
                if (originale == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                // === Aggiorna campi principali ===
                originale.Tipo = model.Tipo;
                originale.Titolo = model.Titolo;
                originale.DataDocumento = model.DataDocumento;
                originale.Descrizione = model.Descrizione;
                originale.Destinatario = model.Destinatario;
                originale.Versione = model.Versione;
                originale.Vecchio = model.Vecchio;

                int idUtenteModificatore = UserManager.GetIDUtenteCollegato();
                var utenteMod = db.Utenti.Find(idUtenteModificatore);
                originale.ModificatoDa = utenteMod != null ? $"{utenteMod.Nome} {utenteMod.Cognome}" : "Sistema";
                originale.DataModifica = DateTime.Now;

                // === Allegati multipli: aggiungi senza perdere il precedente ===
                // cartella /Allegati/KnowledgeAziendale/{ID}/
                string baseRel = $"/Allegati/KnowledgeAziendale/{originale.KnowledgeAziendaleID}/";
                string baseFis = Server.MapPath("~" + baseRel.TrimEnd('/'));
                if (!Directory.Exists(baseFis)) Directory.CreateDirectory(baseFis);

                var files = Request.Files;
                string primoNuovoNome = null;
                string primoNuovoRel = null;

                for (int i = 0; i < files.Count; i++)
                {
                    var f = files[i];
                    if (f == null || f.ContentLength <= 0) continue;

                    // accetta sia input name="Allegati" (multiplo) sia "Allegato"
                    var key = files.GetKey(i) ?? "";
                    if (!key.Equals("Allegati", StringComparison.OrdinalIgnoreCase) &&
                        !key.Equals("Allegato", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var nome = Path.GetFileName(f.FileName) ?? "file";
                    nome = nome.Replace("..", "").Trim();

                    string destinazione = Path.Combine(baseFis, nome);
                    if (System.IO.File.Exists(destinazione))
                    {
                        string name = Path.GetFileNameWithoutExtension(nome);
                        string ext = Path.GetExtension(nome);
                        nome = $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
                        destinazione = Path.Combine(baseFis, nome);
                    }

                    f.SaveAs(destinazione);
                    var rel = baseRel + nome;

                    if (primoNuovoNome == null)
                    {
                        primoNuovoNome = nome;
                        primoNuovoRel = rel;
                    }
                }

                // Se c'è almeno un nuovo file e NON avevamo un "principale", imposta il primo nuovo come principale
                if (!string.IsNullOrEmpty(primoNuovoNome) && string.IsNullOrEmpty(originale.AllegatoPercorso))
                {
                    originale.AllegatoNome = primoNuovoNome;
                    originale.AllegatoPercorso = primoNuovoRel;
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Documento aggiornato con successo!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetKnowledgeAziendale(int id)
        {
            var doc = db.KnowledgeAziendale.FirstOrDefault(k => k.KnowledgeAziendaleID == id);
            if (doc == null)
                return Json(new { success = false, message = "Documento non trovato." }, JsonRequestBehavior.AllowGet);

            // Elenco di tutti gli allegati nella cartella /Allegati/KnowledgeAziendale/{ID}/
            var baseRel = $"/Allegati/KnowledgeAziendale/{doc.KnowledgeAziendaleID}/";
            var baseFis = Server.MapPath("~" + baseRel.TrimEnd('/'));
            var allegati = new List<object>();

            if (System.IO.Directory.Exists(baseFis))
            {
                foreach (var path in System.IO.Directory.GetFiles(baseFis))
                {
                    var nome = System.IO.Path.GetFileName(path);
                    allegati.Add(new { Nome = nome, Percorso = baseRel + nome });
                }
            }

            var documento = new
            {
                doc.KnowledgeAziendaleID,
                doc.Tipo,
                doc.Titolo,
                DataDocumento = doc.DataDocumento.ToString("yyyy-MM-dd"),
                doc.Descrizione,
                doc.Destinatario,          // stringa (se usi checkbox, la comporrai lato JS)
                doc.Versione,
                doc.Vecchio,
                doc.CreatoDa,
                DataCreazione = doc.DataCreazione.ToString("yyyy-MM-dd HH:mm"),
                doc.ModificatoDa,
                DataModifica = doc.DataModifica?.ToString("yyyy-MM-dd HH:mm"),
                AllegatoPresente = !string.IsNullOrEmpty(doc.AllegatoPercorso),
                doc.AllegatoNome,          // principale (retrocompatibilità)
                doc.AllegatoPercorso,      // principale
                Allegati = allegati
            };

            return Json(new { success = true, documento }, JsonRequestBehavior.AllowGet);
        }



        [HttpPost]
        public ActionResult EliminaKnowledgeAziendale(int id)
        {
            try
            {
                int idUtenteLoggato = UserManager.GetIDUtenteCollegato();
                if (idUtenteLoggato <= 0)
                    return Json(new { success = false, message = "Utente non autenticato o sessione scaduta." });

                var utenteLoggato = db.Utenti.FirstOrDefault(u => u.ID_Utente == idUtenteLoggato);
                if (utenteLoggato == null)
                    return Json(new { success = false, message = "Utente non trovato." });

                var doc = db.KnowledgeAziendale.FirstOrDefault(k => k.KnowledgeAziendaleID == id);
                if (doc == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                // 🔐 Controllo permessi: Admin può sempre, altri solo se creatori
                if (utenteLoggato.TipoUtente != "Admin" &&
                    !string.Equals(doc.CreatoDa, $"{utenteLoggato.Nome} {utenteLoggato.Cognome}", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Non hai i permessi per eliminare questo documento." });
                }

                // ✅ Elimina dal DB
                db.KnowledgeAziendale.Remove(doc);
                db.SaveChanges();

                // 🗑️ Elimina cartella allegati
                try
                {
                    var relFolder = $"/Allegati/KnowledgeAziendale/{id}/";
                    var absFolder = Server.MapPath("~" + relFolder.TrimEnd('/'));
                    if (System.IO.Directory.Exists(absFolder))
                    {
                        System.IO.Directory.Delete(absFolder, recursive: true);
                    }
                }
                catch (Exception exAllegati)
                {
                    // Qui potresti salvare un log locale o in DB
                    System.Diagnostics.Debug.WriteLine("Errore cancellazione allegati: " + exAllegati.Message);
                }

                return Json(new { success = true, message = "Documento eliminato con successo!" });
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += " | Inner: " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null) msg += " | Inner2: " + ex.InnerException.InnerException.Message;

                return Json(new { success = false, message = "Errore nell'eliminazione: " + msg });
            }
        }

        [HttpPost]
        public ActionResult SegnaComeLetto(int id)
        {
            try
            {
                int idUtenteCorrente = UserManager.GetIDUtenteCollegato();
                if (idUtenteCorrente <= 0)
                    return Json(new { success = false, message = "Utente non autenticato." });

                var documento = db.KnowledgeAziendale.FirstOrDefault(k => k.KnowledgeAziendaleID == id);
                if (documento == null)
                    return Json(new { success = false, message = "Documento non trovato." });

                // Verifica se già letto
                var lettura = db.KnowledgeAziendaleLetture
                    .FirstOrDefault(l => l.KnowledgeAziendaleID == id && l.ID_Utente == idUtenteCorrente);

                if (lettura == null)
                {
                    // Inserisco nuova riga
                    lettura = new KnowledgeAziendaleLetture
                    {
                        KnowledgeAziendaleID = id,
                        ID_Utente = idUtenteCorrente,
                        DataLettura = DateTime.Now
                    };
                    db.KnowledgeAziendaleLetture.Add(lettura);
                }
                else
                {
                    // Aggiorno la data
                    lettura.DataLettura = DateTime.Now;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Documento segnato come letto." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore: " + ex.Message });
            }
        }




        #endregion

    }
}