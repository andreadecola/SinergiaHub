using Sinergia.Model;
using Sinergia.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Services.Description;

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
            var skipTeamPerMembro = new HashSet<string>();
            // 🔥 Cache plafond per professionista (scalato progressivamente)
            var plafondCache = new Dictionary<int, decimal>();// team + "-" + costoTeam + "-" + membro

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

                // 🔎 Funzione interna per determinare Stato in base al plafond
                Func<int, decimal, (string stato, bool approvato)> determinaStato =
                    (idProf, imp) =>
                    {
                        if (!plafondCache.ContainsKey(idProf))
                        {
                            var operatore = db.OperatoriSinergia
                                .FirstOrDefault(o =>
                                    o.ID_UtenteCollegato == idProf &&
                                    o.TipoCliente == "Professionista");

                            int idClienteProfessionista = operatore?.ID_Operatore ?? 0;

                            plafondCache[idProf] = CalcolaPlafondEffettivo(
                                db,
                                idProf,
                                idClienteProfessionista);
                        }

                        bool coperto = plafondCache[idProf] >= imp;

                        if (coperto)
                            plafondCache[idProf] -= imp;

                        return (
                            coperto ? "Autorizzato" : "Previsionale",
                            coperto
                        );
                    };
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
                                Stato = "Autorizzato",
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
                                Stato = "Autorizzato",
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
                                .Join(db.OperatoriSinergia,
                                      m => m.ID_Professionista,   // ID_Operatore
                                      o => o.ID_Operatore,
                                      (m, o) => o.ID_UtenteCollegato) // ✅ ID_Utente
                                .Where(idUt => idUt != null)
                                .Select(idUt => idUt.Value)
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
                                    Stato = "Autorizzato",
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
                        decimal importoProj = c.Importo ?? 0;
                        var statoCalc = determinaStato(pratica.ID_UtenteResponsabile, importoProj);

                       
                        righeDaSalvare.Add(new GenerazioneCosti
                        {
                            Categoria = "Costo Progetto",
                            ID_Pratiche = c.ID_Pratiche,
                            ID_Utente = pratica.ID_UtenteResponsabile,
                            Descrizione = nomeCosto,
                            Importo = importoProj,
                            Origine = "Costo Pratica",
                            Stato = statoCalc.stato,
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

        public static RisultatoPagamento PagaCostiSelezionati(
       int idProfessionista,
       List<int> idCosti)
        {
            if (idCosti == null || !idCosti.Any())
            {
                return new RisultatoPagamento
                {
                    Successo = false,
                    Messaggio = "Nessun costo selezionato."
                };
            }

            DateTime oggi = DateTime.Today;

            using (var db = new SinergiaDB())
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    int idSistema = UserManager.GetIDUtenteCollegato();

                    // ==========================================================
                    // 🔎 PROFESSIONISTA
                    // ==========================================================
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
                    // 🔎 COSTI SELEZIONATI (solo autorizzati)
                    // ==========================================================
                    var costi = db.GenerazioneCosti
                        .Where(c =>
                            idCosti.Contains(c.ID_GenerazioneCosto) &&
                            c.ID_Utente == prof.ID_Utente &&
                            (c.Stato == "Autorizzato" || c.Stato == "Previsionale"))
                        .ToList();

                    if (!costi.Any())
                    {
                        return new RisultatoPagamento
                        {
                            Successo = false,
                            Messaggio = "Nessun costo valido selezionato."
                        };
                    }

                    decimal totaleDaPagare = costi.Sum(c => c.Importo ?? 0m);

                    // ==========================================================
                    // 💰 PLAFOND REALE
                    // ==========================================================
                    decimal plafondDisponibile = db.PlafondUtente
                        .Where(p => p.ID_Utente == prof.ID_Utente)
                        .Sum(p => (decimal?)p.Importo) ?? 0m;

                    if (plafondDisponibile < totaleDaPagare)
                    {
                        return new RisultatoPagamento
                        {
                            Successo = false,
                            Messaggio = $"Fondi insufficienti. Disponibile: {plafondDisponibile:N2} €"
                        };
                    }

                    // ==========================================================
                    // 💳 AGGIORNA COSTI
                    // ==========================================================
                    foreach (var costo in costi)
                    {
                        costo.Stato = "Pagato";
                        costo.DataUltimaModifica = oggi;
                        costo.ID_UtenteUltimaModifica = idSistema;
                    }

                    db.SaveChanges();

                    // ==========================================================
                    // ➖ MOVIMENTO NEGATIVO UNICO
                    // ==========================================================
                    var movimento = new PlafondUtente
                    {
                        ID_Utente = prof.ID_Utente,
                        Importo = -totaleDaPagare,
                        TipoPlafond = "Pagamento Costi",
                        DataVersamento = oggi,
                        DataInserimento = oggi,
                        ID_UtenteInserimento = idSistema,
                        ID_UtenteCreatore = idSistema,
                        DataInizio = oggi,
                        DataFine = oggi,
                        Note = $"Pagamento costi ({costi.Count})"
                    };

                    db.PlafondUtente.Add(movimento);
                    db.SaveChanges();

                    // ==========================================================
                    // 📦 ARCHIVIO
                    // ==========================================================
                    db.PlafondUtente_a.Add(new PlafondUtente_a
                    {
                        ID_PlannedPlafond_Archivio = movimento.ID_PlannedPlafond,
                        ID_Utente = movimento.ID_Utente,
                        ImportoTotale = movimento.ImportoTotale,
                        TipoPlafond = movimento.TipoPlafond,
                        Operazione = "Pagamento Costi",
                        DataInizio = movimento.DataInizio,
                        DataFine = movimento.DataFine,
                        ID_UtenteCreatore = movimento.ID_UtenteCreatore,
                        ID_UtenteUltimaModifica = movimento.ID_UtenteUltimaModifica,
                        DataUltimaModifica = movimento.DataUltimaModifica,
                        Importo = movimento.Importo,
                        DataVersamento = movimento.DataVersamento,
                        ID_UtenteInserimento = movimento.ID_UtenteInserimento,
                        DataInserimento = movimento.DataInserimento,
                        Note = movimento.Note,
                        NumeroVersione = 1,
                        DataArchiviazione = DateTime.Now,
                        ID_UtenteArchiviazione = idSistema,
                        ModificheTestuali = "Pagamento costi selezionati"
                    });

                    db.SaveChanges();

                    transaction.Commit();

                    return new RisultatoPagamento
                    {
                        Successo = true,
                        Messaggio = $"Pagati {costi.Count} costi. Totale: {totaleDaPagare:N2} €"
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    return new RisultatoPagamento
                    {
                        Successo = false,
                        Messaggio = "Errore durante il pagamento: " + ex.Message
                    };
                }
            }
        }

        public static decimal CalcolaPlafondEffettivo(
              SinergiaDB db,
              int idUtenteCollegato,
              int idClienteProfessionista)
        {
            return db.PlafondUtente
                .Where(p => p.ID_Utente == idUtenteCollegato)
                .Sum(p => (decimal?)p.Importo) ?? 0m;
        }

    }
}