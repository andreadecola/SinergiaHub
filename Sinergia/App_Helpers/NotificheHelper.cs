using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public static class NotificheHelper
    {
        /* ============================================================
           🔧 BASE: METODI GENERALI
           ============================================================ */

        public static void InviaNotifica(int idUtente, string titolo, string descrizione, string tipo = "Sistema", string stato = "Attiva", int? idPratica = null)
        {
            using (var db = new SinergiaDB())
            {
                // Evita duplicati identici nelle ultime 48 ore
                DateTime limite = DateTime.Now.AddHours(-48);
                bool esisteGia = db.Notifiche.Any(n =>
                    n.ID_Utente == idUtente &&
                    n.Tipo == tipo &&
                    n.Titolo == titolo &&
                    n.DataCreazione > limite);

                if (esisteGia)
                    return;

                db.Notifiche.Add(new Notifiche
                {
                    ID_Utente = idUtente,
                    Titolo = titolo,
                    Descrizione = descrizione,
                    Tipo = tipo,
                    Stato = stato,
                    Contatore = 0,
                    Letto = false,
                    DataCreazione = DateTime.Now,
                    ID_Pratiche = idPratica
                });

                db.SaveChanges();
            }
        }

        public static void InviaNotificaAdmin(string titolo, string descrizione, string tipo = "Costi", string stato = "Critica")
        {
            using (var db = new SinergiaDB())
            {
                var adminList = db.Utenti
                    .Where(u => u.TipoUtente == "Admin" && u.Stato == "Attivo")
                    .ToList();

                foreach (var admin in adminList)
                {
                    InviaNotifica(admin.ID_Utente, titolo, descrizione, tipo, stato);
                }
            }
        }

        /* ============================================================
           💰 PLAFOND
           ============================================================ */

        public static void CreaNotificaPlafondAssente(int idUtenteProfessionista, string noteExtra = null)
        {
            var msg = "Plafond non configurato per il professionista." +
                      (string.IsNullOrWhiteSpace(noteExtra) ? "" : " " + noteExtra);
            InviaNotifica(idUtenteProfessionista, "Plafond assente", msg, "PLAFOND_ASSENTE", "Attiva");
        }


        public static void CreaNotificaPlafondSoglia(int idUtenteProfessionista, decimal residuo, decimal soglia)
        {
            var msg = $"Plafond residuo {residuo:C} inferiore alla soglia {soglia:C}.";
            InviaNotifica(idUtenteProfessionista, "Plafond in soglia", msg, "PLAFOND_SOGLIA", "Attiva");
        }

        public static void CreaNotificaCostiNonPagati(int idUtenteProfessionista, int mesi, decimal totale)
        {
            var msg = $"Il professionista ha costi non pagati da oltre {mesi} mesi per un totale di {totale:C}.";
            InviaNotifica(idUtenteProfessionista, "Costi non pagati da troppo tempo", msg, "PLAFOND_COSTI_SCADUTI", "Attiva");
        }

        public static void CreaNotificaCostiNonPagatiConPlafond(int idUtenteProfessionista, decimal plafond, decimal costi)
        {
            var msg = $"Hai un plafond disponibile di {plafond:C}, ma costi da pagare per {costi:C}. Verifica il saldo.";
            InviaNotifica(idUtenteProfessionista, "Costi non pagati con plafond disponibile", msg, "PLAFOND_COSTI_DA_PAGARE", "Attiva");
        }

        /* ============================================================
           📁 PRATICHE
           ============================================================ */

        // ==========================================================
        // 🕒 Wrapper automatico: controlla tutte le pratiche ferme
        // ==========================================================
        public static void CreaNotificaPraticaFerma(int giorniSoglia = 30)
        {
            using (var db = new SinergiaDB())
            {
                DateTime limite = DateTime.Today.AddDays(-giorniSoglia);

                var praticheFerme = db.Pratiche
                    .Where(p => p.Stato != "Eliminato" && p.UltimaModifica < limite)
                    .ToList();

                foreach (var p in praticheFerme)
                {
                    int idUtenteDestinatario = p.ID_UtenteResponsabile;
                    int giorniFerma = (DateTime.Today - ((DateTime)(p.UltimaModifica ?? p.DataCreazione))).Days;
                    DateTime dataUltimoAggiornamento = (DateTime)(p.UltimaModifica ?? p.DataCreazione);

                    // Chiama la versione dettagliata
                    CreaNotificaPraticaFerma(idUtenteDestinatario, p.ID_Pratiche, dataUltimoAggiornamento, giorniFerma);
                }
            }
        }

        // Versione originale (dettagliata)
        public static void CreaNotificaPraticaFerma(int idUtenteDestinatario, int idPratica, DateTime dataUltimoAggiornamento, int giorniFerma)
        {
            string msg = $"La pratica è ferma da {giorniFerma} giorni (ultimo aggiornamento il {dataUltimoAggiornamento:dd/MM/yyyy}).";
            InviaNotifica(idUtenteDestinatario, "Pratica ferma da troppo tempo", msg, "PRATICA_FERMA", "Attiva", idPratica);
        }

        // ==========================================================
        // 💼 Wrapper automatico: pratiche senza avviso parcella
        // ==========================================================
        public static void CreaNotificaPraticaSenzaAvviso()
        {
            using (var db = new SinergiaDB())
            {
                var praticheChiuse = db.Pratiche
                    .Where(p => (p.Stato == "Chiusa" || p.Stato == "Completata") && p.Stato != "Eliminato")
                    .ToList();

                foreach (var p in praticheChiuse)
                {
                    bool haAvviso = db.AvvisiParcella.Any(a => a.ID_Pratiche == p.ID_Pratiche && a.Stato != "Annullato");
                    if (!haAvviso)
                    {
                        int idAdmin = db.Utenti.Where(u => u.TipoUtente == "Admin").Select(u => u.ID_Utente).FirstOrDefault();
                        CreaNotificaPraticaSenzaAvviso(p.ID_Pratiche, idAdmin);
                    }
                }
            }
        }

        public static void CreaNotificaPraticaSenzaAvviso(int idPratica, int idDestinatarioAdmin)
        {
            InviaNotifica(idDestinatarioAdmin, "Pratica senza avviso di parcella", "Non risulta alcun avviso emesso.", "PRATICA_SENZA_AVVISO", "Attiva", idPratica);
        }

        // ==========================================================
        // ⏰ Wrapper automatico: pratiche con scadenza imminente
        // ==========================================================
        public static void CreaNotificaPraticaScadenzaImminente(int giorniAvviso = 5)
        {
            using (var db = new SinergiaDB())
            {
                DateTime oggi = DateTime.Today;
                DateTime limite = oggi.AddDays(giorniAvviso);

                var praticheInScadenza = db.Pratiche
                    .Where(p => p.DataFineAttivitaStimata.HasValue && p.DataFineAttivitaStimata.Value >= oggi && p.DataFineAttivitaStimata.Value <= limite)
                    .ToList();

                foreach (var p in praticheInScadenza)
                {
                    int giorniResidui = (p.DataFineAttivitaStimata.Value - oggi).Days;
                    int idUtenteProfessionista = p.ID_UtenteResponsabile;

                    CreaNotificaPraticaScadenzaImminente(idUtenteProfessionista, p.ID_Pratiche, p.DataFineAttivitaStimata.Value, giorniResidui);
                }
            }
        }

        public static void CreaNotificaPraticaScadenzaImminente(int idUtenteProfessionista, int idPratica, DateTime dataScadenza, int giorniResidui)
        {
            var msg = $"La pratica con scadenza {dataScadenza:dd/MM/yyyy} è imminente ({giorniResidui} giorni rimanenti).";
            InviaNotifica(idUtenteProfessionista, "Scadenza pratica imminente", msg, "PRATICA_SCADENZA_IMMINENTE", "Attiva", idPratica);
        }

        /* ============================================================
           ⚙️ SISTEMA / GENERAZIONE COSTI
           ============================================================ */

        public static void CreaNotificaGenerazioneCostoFallita(int idDestinatarioAdmin, string descrErrore)
        {
            var msg = "Errore in generazione costi: " + (descrErrore ?? "verificare log.");
            InviaNotifica(idDestinatarioAdmin, "Errore generazione costi", msg, "SYS_GENERAZIONE_COSTO_FALLITA", "Critica");
        }

        public static void CreaNotificaCostoBloccatoDaEccezione(int idDestinatarioAdmin, string categoria, DateTime dal, DateTime al)
        {
            var msg = $"Costo '{categoria}' bloccato da eccezione nel periodo {dal:dd/MM/yyyy} – {al:dd/MM/yyyy}.";
            InviaNotifica(idDestinatarioAdmin, "Costo bloccato da eccezione", msg, "SYS_COSTO_BLOCCATO_ECCEZIONE", "Attiva");
        }

     
        public static void CreaNotificaPermessoIncoerente(int idDestinatarioAdmin, int idUtente, string azione)
        {
            var msg = $"Utente #{idUtente} ha tentato l'azione '{azione}' senza permessi.";
            InviaNotifica(idDestinatarioAdmin, "Permesso incoerente", msg, "SYS_PERMESSO_INCOERENTE", "Attiva");
        }

        /* ============================================================
           🔍 VERIFICHE AUTOMATICHE
           ============================================================ */

        public static void VerificaPraticheFerme(int sogliaGiorni = 30)
        {
            using (var db = new SinergiaDB())
            {
                DateTime oggi = DateTime.Today;
                DateTime limite = oggi.AddDays(-sogliaGiorni);

                var praticheFerme = db.Pratiche
                    .Where(p => p.Stato != "Eliminato"
                                && p.UltimaModifica < limite
                                && !db.Notifiche.Any(n => n.ID_Pratiche == p.ID_Pratiche && n.Tipo == "PRATICA_FERMA"))
                    .ToList();

                foreach (var pratica in praticheFerme)
                {
                    int idDestinatario = (int)(pratica.ID_UtenteResponsabile > 0
                        ? pratica.ID_UtenteResponsabile
                        : (pratica.ID_Owner > 0 ? pratica.ID_Owner : 0));

                    if (idDestinatario <= 0) continue;

                    DateTime dataRiferimento = (pratica.UltimaModifica ?? pratica.DataCreazione ?? DateTime.Now);
                    int giorniFerma = (oggi - dataRiferimento).Days;

                    CreaNotificaPraticaFerma(pratica.ID_Pratiche, idDestinatario, dataRiferimento, giorniFerma);
                }

                if (praticheFerme.Count > 0)
                    System.Diagnostics.Debug.WriteLine($"✅ [VerificaPraticheFerme] Create {praticheFerme.Count} notifiche di pratiche ferme.");
            }
        }

        public static void VerificaCostiNonPagati(int sogliaMesi = 1)
        {
            using (var db = new SinergiaDB())
            {
                DateTime limite = DateTime.Today.AddMonths(-sogliaMesi);

                var professionisti = db.Utenti
                    .Where(u => u.TipoUtente == "Professionista" && u.Stato == "Attivo")
                    .ToList();

                foreach (var prof in professionisti)
                {
                    var costiNonPagati = db.GenerazioneCosti
                        .Where(c => c.ID_Utente == prof.ID_Utente
                                    && c.Stato == "Previsionale"
                                    && c.Approvato == false
                                    && c.DataRegistrazione < limite)
                        .ToList();

                    if (!costiNonPagati.Any()) continue;

                    decimal totaleCosti = costiNonPagati.Sum(c => c.Importo ?? 0);

                    CreaNotificaCostiNonPagati(prof.ID_Utente, sogliaMesi, totaleCosti);

                    decimal plafond = db.PlafondUtente
                        .Where(p => p.ID_Utente == prof.ID_Utente)
                        .Sum(p => (decimal?)p.Importo) ?? 0;

                    if (plafond > totaleCosti)
                        CreaNotificaCostiNonPagatiConPlafond(prof.ID_Utente, plafond, totaleCosti);
                }

                System.Diagnostics.Debug.WriteLine("✅ [VerificaCostiNonPagati] Controllo completato.");
            }
        }
    }
}
