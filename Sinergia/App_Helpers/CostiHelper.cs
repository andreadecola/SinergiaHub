using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sinergia.App_Helpers
{
    public static class CostiHelper
    {
        public static List<string> EseguiGenerazioneCosti()
        {
            DateTime oggi = DateTime.Today;
            int idUtenteCorrente = UserManager.GetIDUtenteCollegato();

            var log = new List<string>();
            var righeDaSalvare = new List<GenerazioneCosti>();

            // HashSet per evitare duplicati
            var skipGenerali = new HashSet<string>();              // tipoCosto + "-" + utente
            var skipProfessionista = new HashSet<string>();        // costoProf + "-" + prof
            var skipTeamPerMembro = new HashSet<string>();         // team + "-" + costoTeam + "-" + membro

            using (var db = new SinergiaDB())
            {
                var ricBase = db.RicorrenzeCosti.Where(r => r.Attivo).ToList();
                var ricorrenze = new List<RicorrenzeCosti>();

                // ======================================================================
                // 1️⃣ PREPARAZIONE RICORRENZE (normalizzazione + clonazione team)
                // ======================================================================
                foreach (var ric in ricBase)
                {
                    // --- FIX: Normalizza "Costo Fisso Resident" come Generale ---
                    if (ric.Categoria != null &&
                        ric.Categoria.Trim().ToLower() == "costo fisso resident")
                    {
                        ric.Categoria = CategorieCostiHelper.Generale;
                        System.Diagnostics.Trace.WriteLine("NORMALIZZA → 'Costo Fisso Resident' → Generale");
                    }

                    // Skippa trattenute / owner fee
                    if (ric.Categoria == "Trattenuta Sinergia" || ric.Categoria == "Owner Fee")
                        continue;

                    // GENERALE o PROFESSIONISTA → aggiungi direttamente
                    if (ric.Categoria == CategorieCostiHelper.Generale ||
                        ric.Categoria == CategorieCostiHelper.Professionista)
                    {
                        ricorrenze.Add(ric);
                        continue;
                    }

                    // TEAM già assegnato
                    if (ric.Categoria == CategorieCostiHelper.Team && ric.ID_Team != null)
                    {
                        ricorrenze.Add(ric);
                        continue;
                    }

                    // TEAM → bisogna generare cloni per ogni team con percentuale
                    if (ric.Categoria == CategorieCostiHelper.Team && ric.ID_Team == null)
                    {
                        var dist = db.DistribuzioneCostiTeam
                            .Where(d => d.ID_AnagraficaCostoTeam == ric.ID_CostoTeam)
                            .ToList();

                        foreach (var d in dist)
                        {
                            ricorrenze.Add(new RicorrenzeCosti
                            {
                                ID_CostoTeam = ric.ID_CostoTeam,
                                ID_Team = d.ID_Team,
                                Categoria = CategorieCostiHelper.Team,
                                Periodicita = ric.Periodicita,
                                Valore = ric.Valore,
                                DataInizio = ric.DataInizio,
                                DataFine = ric.DataFine,
                                Attivo = true
                            });
                        }
                    }
                }

                // ======================================================================
                // 2️⃣ GENERAZIONE COSTI
                // ======================================================================
                foreach (var ric in ricorrenze)
                {
                    string categoria = ric.Categoria;
                    string descrizione = null;

                    // -----------------------------
                    // Recupero descrizione
                    // -----------------------------
                    if (categoria == CategorieCostiHelper.Team)
                    {
                        descrizione = db.AnagraficaCostiTeam
                            .Where(a => a.ID_AnagraficaCostoTeam == ric.ID_CostoTeam)
                            .Select(a => a.Descrizione)
                            .FirstOrDefault();
                    }
                    else if (categoria == CategorieCostiHelper.Professionista)
                    {
                        descrizione = db.AnagraficaCostiProfessionista
                            .Where(a => a.ID_AnagraficaCostoProfessionista == ric.ID_CostoProfessionista)
                            .Select(a => a.Descrizione)
                            .FirstOrDefault();
                    }
                    else if (categoria == CategorieCostiHelper.Generale)
                    {
                        descrizione = db.TipologieCosti
                            .Where(a => a.ID_TipoCosto == ric.ID_TipoCostoGenerale)
                            .Select(a => a.Nome)
                            .FirstOrDefault();
                    }

                    if (string.IsNullOrWhiteSpace(descrizione))
                    {
                        System.Diagnostics.Trace.WriteLine("⚠ Descrizione mancante per ricorrenza");
                        continue;
                    }

                    // -----------------------------
                    // Calcolo Data Registrazione
                    // -----------------------------
                    bool unaTantum =
                        ric.DataInizio.HasValue &&
                        ric.DataFine.HasValue &&
                        ric.DataInizio.Value.Date == ric.DataFine.Value.Date;

                    DateTime dataReg =
                        unaTantum ? ric.DataInizio.Value.Date :
                        (!string.IsNullOrEmpty(ric.Periodicita)
                            ? new DateTime(oggi.Year, oggi.Month, 1)
                            : ric.DataInizio ?? oggi);

                    // -----------------------------
                    // Eccezioni
                    // -----------------------------
                    var ECC = db.EccezioniRicorrenzeCosti.FirstOrDefault(e =>
                        e.Categoria == categoria &&
                        (
                            (categoria == CategorieCostiHelper.Generale && e.ID_TipologiaCosto == ric.ID_TipoCostoGenerale) ||
                            (categoria == CategorieCostiHelper.Team && e.ID_Team == ric.ID_Team) ||
                            (categoria == CategorieCostiHelper.Professionista && e.ID_Professionista == ric.ID_Professionista)
                        ) &&
                        e.DataInizio <= dataReg &&
                        (e.DataFine == null || e.DataFine >= dataReg)
                    );

                    if (ECC?.SaltaCosto == true)
                    {
                        System.Diagnostics.Trace.WriteLine("⛔ Costo bloccato da eccezione → " + descrizione);
                        continue;
                    }

                    decimal importo = ECC?.NuovoImporto ?? ric.Valore;

                    // ==================================================================
                    // 2.1️⃣ GENERALE
                    // ==================================================================
                    if (categoria == CategorieCostiHelper.Generale)
                    {
                        var assegnati = db.CostiGeneraliUtente
                            .Where(c => c.ID_TipoCosto == ric.ID_TipoCostoGenerale)
                            .Select(c => c.ID_Utente)
                            .Distinct()
                            .ToList();

                        foreach (int idProf in assegnati)
                        {
                            string chiave = ric.ID_TipoCostoGenerale + "-" + idProf;

                            if (skipGenerali.Contains(chiave))
                                continue;

                            skipGenerali.Add(chiave);

                            if (db.GenerazioneCosti.Any(x =>
                                x.ID_Utente == idProf &&
                                x.Categoria == categoria &&
                                x.Descrizione == descrizione &&
                                x.DataRegistrazione == dataReg))
                                continue;

                            righeDaSalvare.Add(new GenerazioneCosti
                            {
                                ID_Utente = idProf,
                                Categoria = categoria,
                                Descrizione = descrizione,
                                Importo = importo,
                                Periodicita = ric.Periodicita,
                                Origine = "Ricorrenza",
                                Stato = "Previsionale",
                                Approvato = false,
                                DataRegistrazione = dataReg,
                                ID_UtenteCreatore = idUtenteCorrente,
                                DataCreazione = DateTime.Now,
                                ID_UtenteUltimaModifica = idUtenteCorrente,
                                DataUltimaModifica = DateTime.Now,
                                ID_Riferimento = ric.ID_TipoCostoGenerale
                            });

                            System.Diagnostics.Trace.WriteLine($"GEN → GENERALE: {descrizione} → Utente {idProf}");
                        }

                        continue;
                    }

                    // ==================================================================
                    // 2.2️⃣ PROFESSIONISTA
                    // ==================================================================
                    if (categoria == CategorieCostiHelper.Professionista && ric.ID_Professionista != null)
                    {
                        int idProf = ric.ID_Professionista.Value;
                        string chiave = ric.ID_CostoProfessionista + "-" + idProf;

                        if (skipProfessionista.Contains(chiave))
                            continue;

                        skipProfessionista.Add(chiave);

                        if (!db.GenerazioneCosti.Any(x =>
                            x.ID_Utente == idProf &&
                            x.Categoria == categoria &&
                            x.Descrizione == descrizione &&
                            x.DataRegistrazione == dataReg))
                        {
                            righeDaSalvare.Add(new GenerazioneCosti
                            {
                                ID_Utente = idProf,
                                Categoria = categoria,
                                Descrizione = descrizione,
                                Importo = importo,
                                Periodicita = ric.Periodicita,
                                Origine = "Ricorrenza",
                                Stato = "Previsionale",
                                Approvato = false,
                                DataRegistrazione = dataReg,
                                ID_UtenteCreatore = idUtenteCorrente,
                                DataCreazione = DateTime.Now,
                                ID_UtenteUltimaModifica = idUtenteCorrente,
                                DataUltimaModifica = DateTime.Now,
                                ID_Riferimento = ric.ID_CostoProfessionista
                            });

                            System.Diagnostics.Trace.WriteLine($"GEN → PROFESSIONISTA: {descrizione} → Utente {idProf}");
                        }

                        continue;
                    }

                    // ==================================================================
                    // 2.3️⃣ TEAM
                    // ==================================================================
                    if (categoria == CategorieCostiHelper.Team && ric.ID_Team.HasValue)
                    {
                        int idTeam = ric.ID_Team.Value;

                        var dist = db.DistribuzioneCostiTeam
                            .FirstOrDefault(d => d.ID_Team == idTeam &&
                                                 d.ID_AnagraficaCostoTeam == ric.ID_CostoTeam);

                        if (dist == null || dist.Percentuale <= 0)
                            continue;

                        var membri = db.MembriTeam
                            .Where(m => m.ID_Team == idTeam)
                            .Select(m => m.ID_Professionista)
                            .ToList();

                        decimal quotaTeam = importo * (dist.Percentuale / 100m);
                        decimal quota = membri.Count > 0 ? Math.Round(quotaTeam / membri.Count, 2) : 0;

                        foreach (var membro in membri)
                        {
                            string chiave = idTeam + "-" + ric.ID_CostoTeam + "-" + membro;

                            if (skipTeamPerMembro.Contains(chiave))
                                continue;

                            skipTeamPerMembro.Add(chiave);

                            if (!db.GenerazioneCosti.Any(x =>
                                x.ID_Utente == membro &&
                                x.ID_Team == idTeam &&
                                x.Categoria == categoria &&
                                x.Descrizione == descrizione &&
                                x.DataRegistrazione == dataReg))
                            {
                                righeDaSalvare.Add(new GenerazioneCosti
                                {
                                    ID_Utente = membro,
                                    ID_Team = idTeam,
                                    Categoria = categoria,
                                    Descrizione = descrizione,
                                    Importo = quota,
                                    Periodicita = ric.Periodicita,
                                    Origine = "Ricorrenza",
                                    Stato = "Previsionale",
                                    Approvato = false,
                                    DataRegistrazione = dataReg,
                                    ID_UtenteCreatore = idUtenteCorrente,
                                    DataCreazione = DateTime.Now,
                                    ID_UtenteUltimaModifica = idUtenteCorrente,
                                    DataUltimaModifica = DateTime.Now,
                                    ID_Riferimento = ric.ID_CostoTeam
                                });

                                System.Diagnostics.Trace.WriteLine($"GEN → TEAM: {descrizione} → Membro {membro}");
                            }
                        }

                        continue;
                    }
                }

                // ======================================================================
                // 3️⃣ COSTI PROGETTO
                // ======================================================================
                var costiPratica = db.CostiPratica.Where(c => c.Stato != "Eliminato").ToList();

                foreach (var c in costiPratica)
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == c.ID_Pratiche);
                    if (pratica == null) continue;

                    string nomeCosto =
                        c.ID_AnagraficaCosto.HasValue ?
                        db.AnagraficaCostiPratica
                            .Where(a => a.ID_AnagraficaCosto == c.ID_AnagraficaCosto)
                            .Select(a => a.Nome)
                            .FirstOrDefault()
                        : c.Descrizione;

                    if (!db.GenerazioneCosti.Any(x =>
                        x.ID_Pratiche == c.ID_Pratiche &&
                        x.Categoria == "Costo Progetto" &&
                        x.Descrizione == nomeCosto &&
                        x.DataRegistrazione == c.DataInserimento))
                    {
                        righeDaSalvare.Add(new GenerazioneCosti
                        {
                            Categoria = "Costo Progetto",
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = pratica.ID_UtenteResponsabile,
                            Descrizione = nomeCosto,
                            Importo = c.Importo,
                            Origine = "Costo Pratica",
                            Stato = "Previsionale",
                            Approvato = false,
                            DataRegistrazione = c.DataInserimento,
                            DataCreazione = DateTime.Now,
                            DataUltimaModifica = DateTime.Now,
                            ID_UtenteCreatore = idUtenteCorrente,
                            ID_UtenteUltimaModifica = idUtenteCorrente,
                            ID_Riferimento = c.ID_CostoPratica
                        });
                    }
                }

                // ======================================================================
                // 4️⃣ SALVATAGGIO
                // ======================================================================
                db.GenerazioneCosti.AddRange(righeDaSalvare);
                db.SaveChanges();

                // ======================================================================
                // LOG FINALE
                // ======================================================================
                System.Diagnostics.Trace.WriteLine("══════════ RIEPILOGO FINALE GENERAZIONE COSTI ══════════");
                System.Diagnostics.Trace.WriteLine("Totale costi generati: " + righeDaSalvare.Count);

                return log;
            }
        }



        //public static void VerificaPagamentiAutomaticiConPlafond()
        //{
        //    DateTime oggi = DateTime.Today;

        //    // Esegui solo il primo giorno del mese
        //    if (oggi.Day != 1)
        //        return;

        //    using (var db = new SinergiaDB())
        //    {
        //        int idSistema = UserManager.GetIDUtenteCollegato();

        //        // 1. Recupera tutti i professionisti attivi
        //        var professionisti = db.Utenti
        //            .Where(u => u.TipoUtente == "Professionista" && u.Stato == "Attivo")
        //            .ToList();

        //        // 2. Scorri ogni professionista per gestire singolarmente i pagamenti
        //        foreach (var prof in professionisti)
        //        {
        //            // 3. Calcola il plafond totale disponibile per il professionista
        //            decimal plafondDisponibile = db.PlafondUtente
        //                .Where(p => p.ID_Utente == prof.ID_Utente)
        //                .Sum(p => (decimal?)p.Importo) ?? 0m;

        //            // 4. Recupera i costi previsionali non pagati del professionista per il mese corrente
        //            var costiPrevisionali = db.GenerazioneCosti
        //                .Where(c =>
        //                    c.ID_Utente == prof.ID_Utente &&
        //                    c.Approvato == false &&
        //                    c.Stato == "Previsionale" &&
        //                    c.DataRegistrazione.HasValue &&
        //                    c.DataRegistrazione.Value.Month == oggi.Month &&
        //                    c.DataRegistrazione.Value.Year == oggi.Year)
        //                .OrderBy(c => c.DataRegistrazione)
        //                .ToList();

        //            // 5. Se non ci sono costi, passa al professionista successivo
        //            if (!costiPrevisionali.Any())
        //                continue;

        //            decimal plafondResiduo = plafondDisponibile;
        //            decimal totalePagato = 0m;
        //            var costiPagati = new List<GenerazioneCosti>();

        //            // 6. Scorri ogni costo e pagalo se c'è plafond sufficiente
        //            foreach (var costo in costiPrevisionali)
        //            {
        //                decimal importo = costo.Importo ?? 0m;

        //                if (plafondResiduo >= importo)
        //                {
        //                    // Marca come pagato e aggiorna dati
        //                    costo.Approvato = true;
        //                    costo.Stato = "Pagato";
        //                    costo.DataUltimaModifica = oggi;
        //                    costo.ID_UtenteUltimaModifica = idSistema;

        //                    plafondResiduo -= importo;
        //                    totalePagato += importo;
        //                    costiPagati.Add(costo);
        //                }
        //                else
        //                {
        //                    // Plafond insufficiente, esci dal ciclo
        //                    break;
        //                }
        //            }

        //            // 7. Se sono stati pagati costi, salva le modifiche e registra uscita plafond
        //            if (totalePagato > 0)
        //            {
        //                db.SaveChanges();

        //                var nuovaVoce = new PlafondUtente
        //                {
        //                    ID_Utente = prof.ID_Utente,
        //                    Importo = -totalePagato,
        //                    TipoPlafond = "Investimento",
        //                    DataVersamento = oggi,
        //                    DataInserimento = oggi,
        //                    ID_UtenteInserimento = idSistema,
        //                    ID_UtenteCreatore = idSistema,
        //                    DataInizio = oggi,
        //                    DataFine = oggi,
        //                    Note = "Pagamento automatico costi generati"
        //                };

        //                db.PlafondUtente.Add(nuovaVoce);
        //                db.SaveChanges();

        //                db.PlafondUtente_a.Add(new PlafondUtente_a
        //                {
        //                    ID_PlannedPlafond_Archivio = nuovaVoce.ID_PlannedPlafond,
        //                    ID_Utente = nuovaVoce.ID_Utente,
        //                    ImportoTotale = nuovaVoce.ImportoTotale,
        //                    TipoPlafond = nuovaVoce.TipoPlafond,
        //                    DataInizio = nuovaVoce.DataInizio,
        //                    DataFine = nuovaVoce.DataFine,
        //                    ID_UtenteCreatore = nuovaVoce.ID_UtenteCreatore,
        //                    ID_UtenteUltimaModifica = nuovaVoce.ID_UtenteUltimaModifica,
        //                    DataUltimaModifica = nuovaVoce.DataUltimaModifica,
        //                    ID_Incasso = nuovaVoce.ID_Incasso,
        //                    Importo = nuovaVoce.Importo,
        //                    DataVersamento = nuovaVoce.DataVersamento,
        //                    ID_UtenteInserimento = nuovaVoce.ID_UtenteInserimento,
        //                    DataInserimento = nuovaVoce.DataInserimento,
        //                    Note = nuovaVoce.Note,
        //                    ID_Pratiche = nuovaVoce.ID_Pratiche,
        //                    ID_CostoPersonale = nuovaVoce.ID_CostoPersonale,
        //                    NumeroVersione = 1,
        //                    DataArchiviazione = DateTime.Now,
        //                    ID_UtenteArchiviazione = idSistema,
        //                    ModificheTestuali = "Pagamento automatico costi generati"
        //                });
        //                try
        //                {
        //                    db.SaveChanges();
        //                }
        //                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
        //                {
        //                    var dettagli = ex.EntityValidationErrors
        //                        .SelectMany(e => e.ValidationErrors)
        //                        .Select(e => $"❌ Campo: {e.PropertyName} → Errore: {e.ErrorMessage}")
        //                        .ToList();

        //                    string messaggioCompleto = "Errore validazione entità:\n" + string.Join("\n", dettagli);

        //                    System.Diagnostics.Debug.WriteLine(messaggioCompleto);

        //                    throw new Exception(messaggioCompleto); // Lo rilancia al controller
        //                }

        //            }

        //            // 8. Se ci sono costi non pagati, invia notifica admin con elenco dettagliato
        //            var costiNonPagati = costiPrevisionali.Except(costiPagati).ToList();
        //            if (costiNonPagati.Any())
        //            {
        //                string elencoCosti = string.Join(Environment.NewLine, costiNonPagati.Select(c =>
        //                    $"- {c.Descrizione} ({c.Importo?.ToString("C2")})"));

        //                string descrizione = $@"
        //            Professionista: {prof.Nome} {prof.Cognome}
        //            Mese: {oggi:MMMM yyyy}
        //            Plafond disponibile: {plafondDisponibile:C}
        //            Totale costi da pagare: {totalePagato + costiNonPagati.Sum(c => c.Importo ?? 0):C}

        //            Costi non pagati:
        //            {elencoCosti}";

        //                NotificheHelper.InviaNotificaAdmin(
        //                    titolo: "⚠️ Plafond insufficiente per pagamento costi",
        //                    descrizione: descrizione,
        //                    tipo: "Costi",
        //                    stato: "Critica"
        //                );
        //            }
        //        }
        //    }
        //}


        public static RisultatoPagamento VerificaPagamentoConPlafondSingolo(int idProfessionista)
        {
            DateTime oggi = DateTime.Today;

            using (var db = new SinergiaDB())
            {
                int idSistema = UserManager.GetIDUtenteCollegato();

                var prof = db.Utenti.FirstOrDefault(u =>
                    u.ID_Utente == idProfessionista &&
                    u.TipoUtente == "Professionista" &&
                    u.Stato == "Attivo");

                if (prof == null)
                {
                    return new RisultatoPagamento
                    {
                        Successo = false,
                        Messaggio = "Professionista non trovato o non attivo."
                    };
                }

                // ==========================================================
                // 💰 CALCOLO PLAFOND EFFETTIVO DAL MOVIMENTO REALE
                // ==========================================================
                decimal totaleEntrate = db.PlafondUtente
                    .Where(p => p.ID_Utente == prof.ID_Utente &&
                                (p.TipoPlafond == "Finanziamento" || p.TipoPlafond == "Incasso"))
                    .Sum(p => (decimal?)p.Importo) ?? 0m;

                decimal totaleUscite = db.PlafondUtente
                    .Where(p => p.ID_Utente == prof.ID_Utente &&
                                p.TipoPlafond == "Pagamento Costi")
                    .Sum(p => (decimal?)p.Importo) ?? 0m;

                decimal plafondDisponibile = totaleEntrate + totaleUscite; // Uscite già negative

                System.Diagnostics.Trace.WriteLine("========== [DEBUG PLAFOND PROFESSIONISTA] ==========");
                System.Diagnostics.Trace.WriteLine($"👤 Professionista: {prof.Nome} {prof.Cognome}");
                System.Diagnostics.Trace.WriteLine($"💰 Entrate (Finanziamenti + Incassi): {totaleEntrate:N2}");
                System.Diagnostics.Trace.WriteLine($"💸 Uscite (Pagamenti): {totaleUscite:N2}");
                System.Diagnostics.Trace.WriteLine($"📊 Plafond disponibile: {plafondDisponibile:N2}");
                System.Diagnostics.Trace.WriteLine("===================================================");

                // ==========================================================
                // 👥 Utenti collegati (professionista + eventuale cliente)
                // ==========================================================
                var idUtentiValidi = new List<int> { prof.ID_Utente };
                var idClienteCollegato = db.OperatoriSinergia
                    .Where(o => o.ID_UtenteCollegato == prof.ID_Utente && o.TipoCliente == "Professionista")
                    .Select(o => (int?)o.ID_Operatore)
                    .FirstOrDefault();

                if (idClienteCollegato.HasValue)
                    idUtentiValidi.Add(idClienteCollegato.Value);

                // ==========================================================
                // 📦 COSTI DA PAGARE (Previsionali, non approvati)
                // ==========================================================
                var costiPrevisionali = db.GenerazioneCosti
                    .Where(c =>
                        c.Approvato == false &&
                        c.Stato == "Previsionale" &&
                        c.DataRegistrazione.HasValue &&
                        c.ID_Utente.HasValue &&
                        idUtentiValidi.Contains(c.ID_Utente.Value))
                    .OrderBy(c => c.DataRegistrazione)
                    .ToList();

                System.Diagnostics.Trace.WriteLine($"📄 Costi previsionali trovati: {costiPrevisionali.Count}");
                if (!costiPrevisionali.Any())
                {
                    // 🔔 Esegui anche la verifica costi non pagati (per notifiche)
                    NotificheHelper.VerificaCostiNonPagati(1); // soglia di 2 mesi (puoi personalizzarla)

                    return new RisultatoPagamento
                    {
                        Successo = true,
                        Messaggio = $"✅ Nessun costo da pagare per il professionista {prof.Nome} {prof.Cognome}."
                    };
                }

                // ==========================================================
                // 💳 VERIFICA E PAGAMENTO AUTOMATICO
                // ==========================================================
                decimal plafondResiduo = plafondDisponibile;
                decimal totalePagato = 0m;
                var costiPagati = new List<GenerazioneCosti>();

                foreach (var costo in costiPrevisionali)
                {
                    decimal importo = costo.Importo ?? 0m;

                    if (plafondResiduo >= importo)
                    {
                        costo.Approvato = true;
                        costo.Stato = "Pagato";
                        costo.DataUltimaModifica = oggi;
                        costo.ID_UtenteUltimaModifica = idSistema;

                        plafondResiduo -= importo;
                        totalePagato += importo;
                        costiPagati.Add(costo);
                    }
                    else
                    {
                        break;
                    }
                }

                // ==========================================================
                // 💾 SALVATAGGI (solo se ha pagato almeno un costo)
                // ==========================================================
                if (totalePagato > 0)
                {
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        var dettagli = ex.EntityValidationErrors
                            .SelectMany(e => e.ValidationErrors)
                            .Select(e => $"❌ COSTI: {e.PropertyName} – {e.ErrorMessage}")
                            .ToList();
                        throw new Exception("Errore su COSTI:\n" + string.Join("\n", dettagli));
                    }

                    // 🔹 Movimento nel plafond
                    var nuovaVoce = new PlafondUtente
                    {
                        ID_Utente = prof.ID_Utente,
                        Importo = -totalePagato,
                        TipoPlafond = "Pagamento Costi",
                        DataVersamento = oggi,
                        DataInserimento = oggi,
                        ID_UtenteInserimento = idSistema,
                        ID_UtenteCreatore = idSistema,
                        DataInizio = oggi,
                        DataFine = oggi,
                        Note = "Pagamento manuale costi generati"
                    };

                    db.PlafondUtente.Add(nuovaVoce);
                    db.SaveChanges();

                    // 🔔 Dopo il pagamento, aggiorna le notifiche
                    NotificheHelper.VerificaCostiNonPagati(1);

                    // 🔹 Archivio
                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_PlannedPlafond_Archivio = nuovaVoce.ID_PlannedPlafond,
                        ID_Utente = nuovaVoce.ID_Utente,
                        ImportoTotale = nuovaVoce.ImportoTotale,
                        TipoPlafond = nuovaVoce.TipoPlafond,
                        Operazione = "Pagamento Costi",
                        DataInizio = nuovaVoce.DataInizio,
                        DataFine = nuovaVoce.DataFine,
                        ID_UtenteCreatore = nuovaVoce.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = nuovaVoce.ID_UtenteUltimaModifica,
                        DataUltimaModifica = nuovaVoce.DataUltimaModifica,
                        ID_Incasso = nuovaVoce.ID_Incasso,
                        Importo = nuovaVoce.Importo,
                        DataVersamento = nuovaVoce.DataVersamento,
                        ID_UtenteInserimento = nuovaVoce.ID_UtenteInserimento,
                        DataInserimento = nuovaVoce.DataInserimento,
                        Note = nuovaVoce.Note,
                        ID_Pratiche = nuovaVoce.ID_Pratiche,
                        ID_CostoPersonale = nuovaVoce.ID_CostoPersonale,
                        NumeroVersione = 1,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idSistema,
                        ModificheTestuali = "Pagamento manuale costi generati"
                    });

                    db.SaveChanges();

                    return new RisultatoPagamento
                    {
                        Successo = true,
                        Messaggio = $"✅ Pagati {costiPagati.Count} costi per il professionista {prof.Nome} {prof.Cognome} – Totale: {totalePagato:N2} €"
                    };
                }

                // ==========================================================
                // ⚠️ Nessun pagamento possibile
                // ==========================================================
                NotificheHelper.VerificaCostiNonPagati(1);

                // ==========================================================
                // ⚠️ Nessun pagamento possibile
                // ==========================================================
                return new RisultatoPagamento
                {
                    Successo = false,
                    Messaggio = plafondDisponibile <= 0
                        ? $"❌ Il professionista {prof.Nome} {prof.Cognome} non ha plafond disponibile."
                        : $"❌ I fondi presenti ({plafondDisponibile:N2} €) non coprono i costi da pagare per il professionista {prof.Nome} {prof.Cognome}."
                };
            }
        }



    }
}