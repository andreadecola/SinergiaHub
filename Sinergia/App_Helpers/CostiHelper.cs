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
            int idUtente = UserManager.GetIDUtenteCollegato();

            var logOutput = new List<string>();
            var righeDaSalvare = new List<GenerazioneCosti>();
            var coppieTeamCostoElaborate = new HashSet<string>();
            var coppieGeneraliUtente = new HashSet<string>();

            using (var db = new SinergiaDB())
            {
                var ricorrenzeBase = db.RicorrenzeCosti.Where(r => r.Attivo).ToList();
                var ricorrenze = new List<RicorrenzeCosti>();

                foreach (var ric in ricorrenzeBase)
                {
                    string idCosto = ric.ID_CostoTeam?.ToString() ?? ric.ID_CostoProfessionista?.ToString() ?? ric.ID_TipoCostoGenerale?.ToString();
                    string logTeam = ric.ID_Team.HasValue ? $"Team={ric.ID_Team}" : "Team=NULL";
                    logOutput.Add($"📌 Analizzo: AnagraficaCosto={idCosto}, Categoria={ric.Categoria}, {logTeam}, Valore={ric.Valore:N2}");

                    if (ric.Categoria == "Trattenuta Sinergia" || ric.Categoria == "Owner Fee")
                    {
                        logOutput.Add($"ℹ️ Ricorrenza {ric.Categoria} ignorata (non gestita da GenerazioneCosti)");
                        continue;
                    }

                    if (ric.Categoria == CategorieCostiHelper.Generale || ric.Categoria == CategorieCostiHelper.Professionista)
                    {
                        ricorrenze.Add(ric);
                        continue;
                    }

                    if (ric.Categoria == CategorieCostiHelper.Team && ric.ID_Team != null)
                    {
                        string chiave = $"{ric.ID_Team}_{ric.ID_CostoTeam}";
                        if (coppieTeamCostoElaborate.Contains(chiave))
                        {
                            logOutput.Add($"⛔ Ricorrenza TEAM già gestita per Team={ric.ID_Team}, Costo={ric.ID_CostoTeam}");
                            continue;
                        }

                        coppieTeamCostoElaborate.Add(chiave);
                        ricorrenze.Add(ric);
                        logOutput.Add($"✅ Ricorrenza diretta TEAM ID={ric.ID_Team} per CostoTeam ID={ric.ID_CostoTeam}");
                        continue;
                    }

                    if (ric.Categoria == CategorieCostiHelper.Team && ric.ID_Team == null)
                    {
                        var distribuzioni = db.DistribuzioneCostiTeam
                            .Where(d => d.ID_AnagraficaCostoTeam == ric.ID_CostoTeam)
                            .ToList();

                        if (!distribuzioni.Any())
                        {
                            logOutput.Add($"⚠️ Nessuna distribuzione trovata per CostoTeam ID={ric.ID_CostoTeam}");
                            continue;
                        }

                        foreach (var dist in distribuzioni)
                        {
                            string chiave = $"{dist.ID_Team}_{ric.ID_CostoTeam}";
                            if (coppieTeamCostoElaborate.Contains(chiave))
                            {
                                logOutput.Add($"⛔ CLONE SKIPPATO → Ricorrenza TEAM già esistente per Team={dist.ID_Team}, Costo={ric.ID_CostoTeam}");
                                continue;
                            }

                            coppieTeamCostoElaborate.Add(chiave);

                            var ricClone = new RicorrenzeCosti
                            {
                                ID_CostoTeam = ric.ID_CostoTeam,
                                ID_Team = dist.ID_Team,
                                Categoria = ric.Categoria,
                                Periodicita = ric.Periodicita,
                                Valore = ric.Valore,
                                DataInizio = ric.DataInizio,
                                DataFine = ric.DataFine,
                                Attivo = ric.Attivo
                            };

                            ricorrenze.Add(ricClone);
                            logOutput.Add($"🔁 CLONE TEAM → CostoTeam={ric.ID_CostoTeam}, Team={dist.ID_Team}");
                        }
                    }
                }

                foreach (var ric in ricorrenze)
                {
                    string descrizione = null;
                    DateTime dataRegistrazione = (ric.DataInizio == ric.DataFine)
                        ? ric.DataInizio.Value.Date
                        : new DateTime(oggi.Year, oggi.Month, 1);

                    // 🔍 Recupera eventuale eccezione attiva
                    var eccezione = db.EccezioniRicorrenzeCosti.FirstOrDefault(e =>
                        e.Categoria == ric.Categoria &&
                        (
                            (ric.Categoria == CategorieCostiHelper.Generale && e.ID_TipologiaCosto == ric.ID_TipoCostoGenerale) ||
                            (ric.Categoria == CategorieCostiHelper.Team && e.ID_Team == ric.ID_Team) ||
                            (ric.Categoria == CategorieCostiHelper.Professionista && e.ID_Professionista == ric.ID_Professionista)
                        ) &&
                        e.DataInizio <= dataRegistrazione &&
                        (e.DataFine == null || e.DataFine >= dataRegistrazione)
                    );

                    if (eccezione != null && eccezione.SaltaCosto == true)
                    {
                        logOutput.Add($"⛔ BLOCCATO da eccezione → Ricorrenza {ric.Categoria} non generata (costo ID={ric.ID_CostoTeam ?? ric.ID_CostoProfessionista ?? ric.ID_TipoCostoGenerale})");
                        continue;
                    }

                    // Usa l'importo personalizzato dell'eccezione se presente
                    decimal importoDaUsare = eccezione?.NuovoImporto ?? ric.Valore;

                    // --- Recupero descrizione ---
                    if (ric.Categoria == CategorieCostiHelper.Team)
                    {
                        descrizione = db.AnagraficaCostiTeam
                            .Where(a => a.ID_AnagraficaCostoTeam == ric.ID_CostoTeam)
                            .Select(a => a.Descrizione)
                            .FirstOrDefault();

                        if (ric.ID_Team.HasValue)
                        {
                            var nomeTeam = db.TeamProfessionisti
                                .Where(t => t.ID_Team == ric.ID_Team.Value)
                                .Select(t => t.Nome)
                                .FirstOrDefault();

                            if (!string.IsNullOrWhiteSpace(nomeTeam))
                                descrizione += $" per team {nomeTeam}";
                        }
                    }
                    else if (ric.Categoria == CategorieCostiHelper.Professionista)
                    {
                        descrizione = db.AnagraficaCostiProfessionista
                            .Where(a => a.ID_AnagraficaCostoProfessionista == ric.ID_CostoProfessionista)
                            .Select(a => a.Descrizione)
                            .FirstOrDefault();
                    }
                    else if (ric.Categoria == CategorieCostiHelper.Generale)
                    {
                        descrizione = db.TipologieCosti
                            .Where(a => a.ID_TipoCosto == ric.ID_TipoCostoGenerale)
                            .Select(a => a.Nome)
                            .FirstOrDefault();
                    }

                    if (string.IsNullOrWhiteSpace(descrizione))
                    {
                        logOutput.Add($"⚠️ Descrizione mancante per ricorrenza ID_Costo={ric.ID_CostoTeam ?? ric.ID_CostoProfessionista ?? ric.ID_TipoCostoGenerale}");
                        continue;
                    }

                    // --- Inserimento a seconda della categoria ---
                    if (ric.Categoria == CategorieCostiHelper.Professionista && ric.ID_Professionista != null)
                    {
                        bool giaPresente = db.GenerazioneCosti.Any(x =>
                            x.ID_Utente == ric.ID_Professionista &&
                            x.Categoria == ric.Categoria &&
                            x.DataRegistrazione == dataRegistrazione &&
                            x.Descrizione == descrizione);

                        if (giaPresente)
                        {
                            logOutput.Add($"⛔ SKIP PROFESSIONISTA {ric.ID_Professionista} → {descrizione} già presente");
                            continue;
                        }

                        righeDaSalvare.Add(new GenerazioneCosti
                        {
                            ID_Utente = ric.ID_Professionista,
                            Categoria = ric.Categoria,
                            Descrizione = descrizione,
                            Importo = importoDaUsare,
                            Periodicita = ric.Periodicita,
                            Origine = "Ricorrenza",
                            Stato = "Previsionale",
                            Approvato = false,
                            DataRegistrazione = dataRegistrazione,
                            ID_UtenteCreatore = idUtente,
                            DataCreazione = DateTime.Now,
                            ID_UtenteUltimaModifica = idUtente,
                            DataUltimaModifica = DateTime.Now,
                            ID_Riferimento = ric.ID_CostoProfessionista
                        });

                        logOutput.Add($"✅ PROFESSIONISTA {ric.ID_Professionista} → {descrizione} | {importoDaUsare:N2}€");
                    }
                    else if (ric.Categoria == CategorieCostiHelper.Team && ric.ID_Team.HasValue)
                    {
                        int idTeam = ric.ID_Team.Value;
                        var dist = db.DistribuzioneCostiTeam
                            .FirstOrDefault(d => d.ID_Team == idTeam && d.ID_AnagraficaCostoTeam == ric.ID_CostoTeam);

                        if (dist == null || dist.Percentuale <= 0)
                        {
                            logOutput.Add($"⛔ NESSUNA DISTRIBUZIONE valida per Team={idTeam} e Costo={ric.ID_CostoTeam}");
                            continue;
                        }

                        var membri = db.MembriTeam
                            .Where(m => m.ID_Team == idTeam)
                            .Select(m => m.ID_Professionista)
                            .ToList();

                        if (!membri.Any())
                        {
                            logOutput.Add($"⛔ Nessun membro per Team {idTeam}");
                            continue;
                        }

                        decimal importoTeam = importoDaUsare * (dist.Percentuale / 100M);
                        decimal quota = membri.Count > 0 ? Math.Round(importoTeam / membri.Count, 2) : 0;

                        foreach (var idMembro in membri)
                        {
                            bool giaPresente = db.GenerazioneCosti.Any(x =>
                                x.ID_Utente == idMembro &&
                                x.ID_Team == idTeam &&
                                x.Categoria == ric.Categoria &&
                                x.Descrizione == descrizione &&
                                x.DataRegistrazione == dataRegistrazione &&
                                Math.Abs((x.Importo ?? 0) - quota) < 0.01M);

                            if (giaPresente)
                            {
                                logOutput.Add($"⛔ SKIP TEAM → Membro {idMembro}, Team {idTeam} | {descrizione} già esiste");
                                continue;
                            }

                            righeDaSalvare.Add(new GenerazioneCosti
                            {
                                ID_Utente = idMembro,
                                ID_Team = idTeam,
                                Categoria = ric.Categoria,
                                Descrizione = descrizione,
                                Importo = quota,
                                Periodicita = ric.Periodicita,
                                Origine = "Ricorrenza",
                                Stato = "Previsionale",
                                Approvato = false,
                                DataRegistrazione = dataRegistrazione,
                                ID_UtenteCreatore = idUtente,
                                DataCreazione = DateTime.Now,
                                ID_UtenteUltimaModifica = idUtente,
                                DataUltimaModifica = DateTime.Now,
                                ID_Riferimento = ric.ID_CostoTeam
                            });

                            logOutput.Add($"✅ TEAM → Membro {idMembro}, Team {idTeam} | {descrizione} | {quota:N2}€");
                        }
                    }
                    else if (ric.Categoria == CategorieCostiHelper.Generale)
                    {
                        if (ric.ID_TipoCostoGenerale == null)
                        {
                            logOutput.Add($"⚠️ Ricorrenza GENERALE senza ID_TipoCostoGenerale → salto");
                            continue;
                        }

                        var idTipoCosto = ric.ID_TipoCostoGenerale.Value;
                        var assegnazioniGenerali = db.CostiGeneraliUtente
                            .Where(c => c.ID_TipoCosto == idTipoCosto)
                            .Select(c => c.ID_Utente)
                            .Distinct()
                            .ToList();

                        foreach (var idProfessionista in assegnazioniGenerali)
                        {
                            string chiave = $"{idProfessionista}_{idTipoCosto}";

                            if (coppieGeneraliUtente.Contains(chiave))
                            {
                                logOutput.Add($"⛔ SKIP GENERALE → {descrizione} per {idProfessionista} (già elaborato nel ciclo)");
                                continue;
                            }

                            bool giaPresente = db.GenerazioneCosti.Any(x =>
                                x.ID_Utente == idProfessionista &&
                                x.ID_Team == null &&
                                x.Categoria == ric.Categoria &&
                                x.Descrizione == descrizione &&
                                x.DataRegistrazione == dataRegistrazione);

                            if (giaPresente)
                            {
                                logOutput.Add($"⛔ SKIP GENERALE → {descrizione} per {idProfessionista} già presente nel DB");
                                continue;
                            }

                            coppieGeneraliUtente.Add(chiave);

                            righeDaSalvare.Add(new GenerazioneCosti
                            {
                                ID_Utente = idProfessionista,
                                Categoria = ric.Categoria,
                                Descrizione = descrizione,
                                Importo = importoDaUsare,
                                Periodicita = ric.Periodicita,
                                Origine = "Ricorrenza",
                                Stato = "Previsionale",
                                Approvato = false,
                                DataRegistrazione = dataRegistrazione,
                                ID_UtenteCreatore = idUtente,
                                DataCreazione = DateTime.Now,
                                ID_UtenteUltimaModifica = idUtente,
                                DataUltimaModifica = DateTime.Now,
                                ID_Riferimento = ric.ID_TipoCostoGenerale
                            });

                            logOutput.Add($"✅ GENERALE → {descrizione} per utente {idProfessionista} | {importoDaUsare:N2}€");
                        }
                    }
                }


                // Recupera i costi di progetto attivi (o con criteri)
                var costiProgetto = db.CostiPratica
                      .Where(c => c.Stato != "Eliminato")
                      .ToList();

                Debug.WriteLine($"DEBUG: Trovati {costiProgetto.Count} costi di progetto non eliminati.");

                foreach (var costo in costiProgetto)
                {
                    Debug.WriteLine($"DEBUG: Elaboro costo progetto ID_CostoPratica={costo.ID_CostoPratica}, ID_Pratiche={costo.ID_Pratiche}");

                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == costo.ID_Pratiche);
                    if (pratica == null)
                    {
                        Debug.WriteLine($"⚠️ Costo progetto ID={costo.ID_CostoPratica} con pratica non trovata.");
                        continue;
                    }

                    var idProfessionista = pratica.ID_UtenteResponsabile;
                    DateTime dataReg = costo.DataInserimento;

                    // Prova a recuperare il nome da AnagraficaCostiPratica
                    string nomeCostoProgetto = null;
                    if (costo.ID_AnagraficaCosto.HasValue)
                    {
                        nomeCostoProgetto = db.AnagraficaCostiPratica
                            .Where(a => a.ID_AnagraficaCosto == costo.ID_AnagraficaCosto.Value)
                            .Select(a => a.Nome)
                            .FirstOrDefault();

                        Debug.WriteLine($"DEBUG: Nome da anagrafica: '{nomeCostoProgetto}'");
                    }

                    if (string.IsNullOrWhiteSpace(nomeCostoProgetto))
                    {
                        nomeCostoProgetto = costo.Descrizione;
                        Debug.WriteLine($"DEBUG: Uso descrizione diretta da costo: '{nomeCostoProgetto}'");
                    }

                    if (string.IsNullOrWhiteSpace(nomeCostoProgetto))
                    {
                        Debug.WriteLine($"⚠️ Nome mancante per costo ID_CostoPratica={costo.ID_CostoPratica}, salto voce");
                        continue;
                    }

                    bool giaPresente = db.GenerazioneCosti.Any(x =>
                        x.ID_Pratiche == costo.ID_Pratiche &&
                        x.Categoria == "Costo Progetto" &&
                        x.Descrizione == nomeCostoProgetto &&
                        x.DataRegistrazione == dataReg &&
                        ((x.Importo ?? 0m) - (costo.Importo ?? 0m) < 0.01m &&
                         (x.Importo ?? 0m) - (costo.Importo ?? 0m) > -0.01m)
                    );

                    if (giaPresente)
                    {
                        Debug.WriteLine($"⛔ SKIP Costo Progetto per pratica {costo.ID_Pratiche} già presente");
                        continue;
                    }

                    righeDaSalvare.Add(new GenerazioneCosti
                    {
                        Categoria = "Costo Progetto",
                        ID_Pratiche = costo.ID_Pratiche,
                        ID_Utente = idProfessionista,
                        Descrizione = nomeCostoProgetto,
                        Importo = costo.Importo,
                        DataRegistrazione = dataReg,
                        Origine = "Costo Pratica",
                        Stato = "Previsionale",
                        Approvato = false,
                        DataCreazione = DateTime.Now,
                        DataUltimaModifica = DateTime.Now,
                        ID_UtenteCreatore = idUtente,
                        ID_UtenteUltimaModifica = idUtente,
                        ID_Riferimento = costo.ID_CostoPratica,

                    });

                    Debug.WriteLine($"✅ Costo Progetto {nomeCostoProgetto} per pratica {costo.ID_Pratiche} | {costo.Importo:N2}€");
                }


                db.GenerazioneCosti.AddRange(righeDaSalvare);
                db.SaveChanges();

                return logOutput;
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

                // ✅ Somma dei finanziamenti registrati
                decimal finanziamenti = db.FinanziamentiProfessionisti
                    .Where(f => f.ID_Professionista == prof.ID_Utente)
                    .Sum(f => (decimal?)f.Importo) ?? 0m;

                // ✅ Somma degli incassi versati in plafond
                var idClientiCollegati = db.OperatoriSinergia
                    .Where(os => os.ID_UtenteCollegato == prof.ID_Utente && os.TipoCliente == "Professionista")
                    .Select(os => os.ID_Cliente)
                    .ToList();

                decimal incassiDaPratiche = 0m;
                if (idClientiCollegati.Any())
                {
                    var idClientiFinali = db.Clienti
                        .Where(c => idClientiCollegati.Contains(c.ID_Operatore))
                        .Select(c => c.ID_Cliente)
                        .ToList();

                    var praticheCollegate = db.Pratiche
                        .Where(p => idClientiFinali.Contains(p.ID_Cliente) && p.Stato != "Annullata")
                        .Select(p => p.ID_Pratiche)
                        .ToList();

                    incassiDaPratiche = db.Incassi
                        .Where(i => i.VersaInPlafond == true && praticheCollegate.Contains(i.ID_Pratiche ?? 0))
                        .Sum(i => (decimal?)i.Importo) ?? 0m;
                }

                // ✅ Somma dei pagamenti già fatti (da scalare)
                decimal pagamentiEffettuati = db.PlafondUtente
                    .Where(p => p.ID_Utente == prof.ID_Utente && p.TipoPlafond == "Pagamento Costi")
                    .Sum(p => (decimal?)p.Importo) ?? 0m;

                decimal plafondDisponibile = finanziamenti + incassiDaPratiche + pagamentiEffettuati; // pagamentiEffettuati è negativo

                // Utenti validi = utente + eventuale cliente collegato
                var idUtentiValidi = new List<int> { prof.ID_Utente };
                var idClienteCollegato = db.OperatoriSinergia
                    .Where(o => o.ID_UtenteCollegato == prof.ID_Utente && o.TipoCliente == "Professionista")
                    .Select(o => (int?)o.ID_Cliente)
                    .FirstOrDefault();

                if (idClienteCollegato.HasValue)
                    idUtentiValidi.Add(idClienteCollegato.Value);

                var costiPrevisionali = db.GenerazioneCosti
                    .Where(c =>
                        c.Approvato == false &&
                        c.Stato == "Previsionale" &&
                        c.DataRegistrazione.HasValue &&
                        c.ID_Utente.HasValue &&
                        idUtentiValidi.Contains(c.ID_Utente.Value))
                    .OrderBy(c => c.DataRegistrazione)
                    .ToList();

                if (!costiPrevisionali.Any())
                {
                    return new RisultatoPagamento
                    {
                        Successo = true,
                        Messaggio = $"✅ Nessun costo da pagare per il professionista {prof.Nome} {prof.Cognome}."
                    };
                }

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
                            .Select(e => $"👎 Validazione COSTI: {e.PropertyName} – {e.ErrorMessage}")
                            .ToList();

                        System.Diagnostics.Debug.WriteLine("Errore su costi:\n" + string.Join("\n", dettagli));
                        throw new Exception("Errore su COSTI: \n" + string.Join("\n", dettagli));
                    }


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

                    try
                    {
                       
                        db.PlafondUtente.Add(nuovaVoce);
                        db.SaveChanges();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        var dettagli = ex.EntityValidationErrors
                            .SelectMany(e => e.ValidationErrors)
                            .Select(e => $"👍 Validazione PlafondUtente: {e.PropertyName} – {e.ErrorMessage}")
                            .ToList();

                        System.Diagnostics.Debug.WriteLine("Errore su PlafondUtente:\n" + string.Join("\n", dettagli));
                        throw new Exception("Errore su PlafondUtente: \n" + string.Join("\n", dettagli));
                    }


                    try
                    {
                        db.PlafondUtente_a.Add(new PlafondUtente_a
                        {
                            ID_PlannedPlafond_Archivio = nuovaVoce.ID_PlannedPlafond,
                            ID_Utente = nuovaVoce.ID_Utente,
                            ImportoTotale = nuovaVoce.ImportoTotale,
                            TipoPlafond = nuovaVoce.TipoPlafond,
                            Operazione = "Pagamento Costi", // ✅ aggiunto obbligatorio

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
                    }
                    catch (DbEntityValidationException ex)
                    {
                        var dettagli = ex.EntityValidationErrors
                            .SelectMany(e => e.ValidationErrors)
                            .Select(e => $"🧾 Validazione Archivio: {e.PropertyName} – {e.ErrorMessage}")
                            .ToList();

                        System.Diagnostics.Debug.WriteLine("Errore su Archivio:\n" + string.Join("\n", dettagli));
                        throw new Exception("Errore su ARCHIVIO: \n" + string.Join("\n", dettagli));
                    }


                    // Notifica se rimangono costi non pagati
                    var costiNonPagati = costiPrevisionali.Except(costiPagati).ToList();
                    if (costiNonPagati.Any())
                    {
                        string elencoCosti = string.Join(Environment.NewLine, costiNonPagati.Select(c =>
                            $"- {c.Descrizione} ({c.Importo?.ToString("C2")})"));

                        string descrizione = $@"
                    Professionista: {prof.Nome} {prof.Cognome}
                    Mese: {oggi:MMMM yyyy}
                    Plafond disponibile: {plafondDisponibile:C}
                    Totale costi da pagare: {(totalePagato + costiNonPagati.Sum(c => c.Importo ?? 0)):C}

                    Costi non pagati:
                    {elencoCosti}"; 

                        NotificheHelper.InviaNotificaAdmin(
                            titolo: "⚠️ Plafond insufficiente per pagamento costi",
                            descrizione: descrizione,
                            tipo: "Costi",
                            stato: "Critica"
                        );
                    }

                    return new RisultatoPagamento
                    {
                        Successo = true,
                        Messaggio = $"✅ Pagati {costiPagati.Count} costi per il professionista {prof.Nome} {prof.Cognome} – Totale: {totalePagato:N2} €"
                    };
                }

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