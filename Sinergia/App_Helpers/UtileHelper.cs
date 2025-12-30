using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class UtileHelper
    {
        public static void EseguiRipartizioneDaIncasso(
        int idPratica,
        decimal importoIncasso,
        int? idIncasso = null,
        int? idAvvisoParcella = null,
        int? idCompensoOrigine = null)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    System.Diagnostics.Trace.WriteLine($"📘 [EseguiRipartizioneDaIncasso] Avvio per pratica {idPratica}, importo incasso = {importoIncasso:N2} €");
                    System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");

                    DateTime oggi = DateTime.Today;
                    int idUtenteInserimento = UserManager.GetIDUtenteCollegato();

                    // ==========================================================
                    // 🚫 BLOCCO ANTI-DUPLICAZIONE
                    // ==========================================================
                    if (idIncasso.HasValue && db.BilancioProfessionista.Any(b => b.ID_Incasso == idIncasso))
                    {
                        System.Diagnostics.Trace.WriteLine($"⚠️ Ripartizione già eseguita per incasso {idIncasso}, salto.");
                        return;
                    }

                    // ==========================================================
                    // 1️⃣ DATI BASE
                    // ==========================================================
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                    if (pratica == null)
                    {
                        System.Diagnostics.Trace.WriteLine("⚠️ Pratica non trovata.");
                        return;
                    }

                    int idResponsabile = pratica.ID_UtenteResponsabile;
                    var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);

                    int? idOwner = null;
                    if (idIncasso.HasValue)
                    {
                        idOwner = db.Incassi
                            .Where(i => i.ID_Incasso == idIncasso.Value)
                            .Select(i => i.ID_OwnerCliente)
                            .FirstOrDefault();
                    }
                    if (!idOwner.HasValue)
                        idOwner = cliente?.ID_Operatore;

                    // ==========================================================
                    // 2️⃣ RECUPERA L’AVVISO
                    // ==========================================================
                    var avviso = idAvvisoParcella.HasValue
                        ? db.AvvisiParcella.FirstOrDefault(a => a.ID_AvvisoParcelle == idAvvisoParcella.Value)
                        : db.AvvisiParcella.FirstOrDefault(a => a.ID_Pratiche == idPratica && a.Stato != "Annullato");

                    if (avviso == null)
                    {
                        System.Diagnostics.Trace.WriteLine("⚠️ Nessun avviso valido trovato.");
                        return;
                    }

                    // ==========================================================
                    // 3️⃣ VARIABILI BASE (ripristinate tutte)
                    // ==========================================================
                    decimal baseImponibile = avviso.Importo ?? 0m;
                    decimal speseGenerali = avviso.ImportoRimborsoSpese ?? 0m;
                    decimal contributoIntegrativo = avviso.ContributoIntegrativoImporto ?? 0m;
                    int? idCompOrig = idCompensoOrigine ?? avviso.ID_CompensoOrigine;
                    string tipoCompenso = avviso.TipologiaAvviso ?? "";
                    decimal percSpeseGenerali = avviso.RimborsoSpesePercentuale ?? 0m;
                    decimal percContributoIntegrativo = avviso.ContributoIntegrativoPercentuale ?? 0m;

                    var voci = new List<BilancioProfessionista>();

                    // ==========================================================
                    // 4️⃣ TRATTENUTA SINERGIA
                    // ==========================================================
                    decimal percTrattenutaSinergia = 0m;
                    var ricTratt = db.RicorrenzeCosti.FirstOrDefault(r =>
                        r.Categoria == "Trattenuta Sinergia" && r.Attivo && r.TipoValore == "Percentuale");

                    if (ricTratt != null)
                        percTrattenutaSinergia = ricTratt.Valore;

                    decimal quotaTrattenuta = Math.Round(baseImponibile * (percTrattenutaSinergia / 100m), 2);
                    decimal baseDopoTrattenuta = baseImponibile - quotaTrattenuta;

                    System.Diagnostics.Trace.WriteLine($"💰 Trattenuta Sinergia {percTrattenutaSinergia:N1}% = {quotaTrattenuta:N2} €");
                    System.Diagnostics.Trace.WriteLine($"➡️ Base dopo trattenuta = {baseDopoTrattenuta:N2} €");

                    if (ricTratt != null)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idResponsabile,
                            ID_Pratiche = idPratica,
                            ID_Incasso = idIncasso,
                            TipoVoce = "Costo",
                            Categoria = "Trattenuta Sinergia",
                            Descrizione = $"Trattenuta Sinergia {percTrattenutaSinergia:N1}%",
                            Importo = quotaTrattenuta,
                            Stato = "Finanziario",
                            Origine = "Incasso",
                            DataRegistrazione = oggi,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ==========================================================
                    // 5️⃣ OWNER FEE (5%) — su base dopo trattenuta
                    // ==========================================================
                    decimal ownerFee = 0m;
                    bool ownerDiverso = idOwner.HasValue && idOwner.Value != idResponsabile;

                    if (ownerDiverso)
                    {
                        ownerFee = Math.Round(baseDopoTrattenuta * 0.05m, 2);
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idOwner.Value,
                            ID_Pratiche = idPratica,
                            ID_Incasso = idIncasso,
                            TipoVoce = "Ricavo",
                            Categoria = "Owner Fee",
                            Descrizione = "Compenso Owner (5%)",
                            Importo = ownerFee,
                            Stato = "Finanziario",
                            Origine = "Incasso",
                            DataRegistrazione = oggi,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ==========================================================
                    // 6️⃣ COLLABORATORI CLUSTER — su base dopo trattenuta
                    // ==========================================================
                    decimal totaleCluster = 0m;

                    var cluster = db.Cluster
                        .Where(c => c.ID_Pratiche == idPratica && c.TipoCluster == "Collaboratore")
                        .ToList();

                    foreach (var c in cluster)
                    {
                        // 🔄 NORMALIZZAZIONE CLUSTER → ID_UTENTE
                        int idUtenteCollaboratore = 0;

                        // 1️⃣ se è già un ID_UTENTE valido
                        idUtenteCollaboratore = db.Utenti
                            .Where(u => u.ID_Utente == c.ID_Utente)
                            .Select(u => u.ID_Utente)
                            .FirstOrDefault();

                        // 2️⃣ altrimenti è un ID_OPERATORE → risali all’utente
                        if (idUtenteCollaboratore == 0)
                        {
                            idUtenteCollaboratore = db.OperatoriSinergia
                                .Where(o => o.ID_Operatore == c.ID_Utente)
                                .Select(o => o.ID_UtenteCollegato.Value)
                                .FirstOrDefault();
                        }

                        if (idUtenteCollaboratore <= 0)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                $"⚠️ [Cluster] ID non risolvibile: ClusterID={c.ID_Cluster}, Valore={c.ID_Utente}");
                            continue;
                        }

                        // 💰 Calcolo quota
                        decimal quota = Math.Round(
                            baseDopoTrattenuta * (c.PercentualePrevisione / 100m),
                            2);

                        totaleCluster += quota;

                        // 💾 Bilancio → SEMPRE ID_UTENTE
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idUtenteCollaboratore,   // ✅ SEMPRE UTENTE
                            ID_Pratiche = idPratica,
                            ID_Incasso = idIncasso,
                            TipoVoce = "Ricavo",
                            Categoria = "Collaboratore su pratica (Cluster)",
                            Descrizione = $"Quota cluster {c.PercentualePrevisione:N2}%",
                            Importo = quota,
                            Stato = "Finanziario",
                            Origine = "Incasso",
                            DataRegistrazione = oggi,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ==========================================================
                    // 7️⃣ SPESE GENERALI e CONTRIBUTO INTEGRATIVO — CORRETTO
                    // ==========================================================

                    // ✔️ Calcolo corretto delle spese generali: solo imponibile
                    speseGenerali = Math.Round(
                        baseImponibile * (percSpeseGenerali / 100m),
                    2);

                    // ✔️ Contributo integrativo = (imponibile + spese generali)
                    contributoIntegrativo = Math.Round(
                        (baseImponibile + speseGenerali) * (percContributoIntegrativo / 100m),
                    2);


                    // ==========================================================
                    // 8️⃣ COLLABORATORI DETTAGLIO — su base dopo trattenuta
                    // ==========================================================
                    decimal totaleDettaglio = 0m;
                    var compensi = db.CompensiPraticaDettaglio
                        .Where(c =>
                            c.ID_Pratiche == idPratica &&
                            (
                                (idCompOrig.HasValue && c.ID_RigaCompenso == idCompOrig.Value)
                                || (idAvvisoParcella.HasValue && c.ID_AvvisoParcella == idAvvisoParcella.Value)
                            ))
                        .ToList();

                    foreach (var comp in compensi)
                    {
                        if (string.IsNullOrWhiteSpace(comp.Collaboratori)) continue;

                        var collabs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CollaboratoreDettaglio>>(comp.Collaboratori);

                        foreach (var collab in collabs)
                        {
                            decimal quota = Math.Round(baseDopoTrattenuta * (collab.Percentuale / 100m), 2);
                            totaleDettaglio += quota;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = collab.ID_Collaboratore,
                                ID_Pratiche = idPratica,
                                ID_Incasso = idIncasso,
                                TipoVoce = "Ricavo",
                                Categoria = "Collaboratore su compenso (Dettaglio)",
                                Descrizione = $"{comp.Descrizione} ({collab.Percentuale:N2}%)",
                                Importo = quota,
                                Stato = "Finanziario",
                                Origine = "Incasso",
                                DataRegistrazione = oggi,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }
                    }

                    // ==========================================================
                    // 9️⃣ NETTO RESPONSABILE — nuova formula
                    // ==========================================================
                    decimal nettoResponsabile = (baseDopoTrattenuta + speseGenerali)
                        - (contributoIntegrativo + ownerFee + totaleCluster + totaleDettaglio);

                    System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");
                    System.Diagnostics.Trace.WriteLine($"📘 [FormulaNetto] (Base {baseDopoTrattenuta:N2} + Spese {speseGenerali:N2}) - (CI {contributoIntegrativo:N2} + Owner {ownerFee:N2} + Cluster {totaleCluster:N2} + Dettaglio {totaleDettaglio:N2})");
                    System.Diagnostics.Trace.WriteLine($"✅ [NettoResponsabile] = {nettoResponsabile:N2} €");
                    System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");

                    voci.Add(new BilancioProfessionista
                    {
                        ID_Professionista = idResponsabile,
                        ID_Pratiche = idPratica,
                        ID_Incasso = idIncasso,
                        TipoVoce = "Ricavo",
                        Categoria = "Netto Effettivo Responsabile",
                        Descrizione = "Ricavo netto effettivo post-ripartizione",
                        Importo = nettoResponsabile,
                        Stato = "Finanziario",
                        Origine = "Incasso",
                        DataRegistrazione = oggi,
                        ID_UtenteInserimento = idUtenteInserimento,
                        DataInserimento = DateTime.Now
                    });

                    // ==========================================================
                    // 🔟 SALVATAGGIO + QUADRATURA
                    // ==========================================================
                    db.BilancioProfessionista.AddRange(voci);
                    db.SaveChanges();

                    decimal totRicavi = voci.Where(v => v.TipoVoce == "Ricavo").Sum(v => v.Importo);
                    decimal totCosti = voci.Where(v => v.TipoVoce == "Costo").Sum(v => v.Importo);
                    decimal tot = totRicavi - totCosti;

                    System.Diagnostics.Trace.WriteLine($"📊 [Quadratura] Ricavi={totRicavi:N2} € | Costi={totCosti:N2} € | Totale={tot:N2} €");
                    System.Diagnostics.Trace.WriteLine("──────────────────────────────────────────────────────────────");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"❌ [EseguiRipartizioneDaIncasso] Errore: {ex.Message}");
                throw;
            }
        }

        // ==========================================================
        // 📦 SUPPORTO JSON PER COLLABORATORI
        // ==========================================================
        private class CollaboratoreDettaglio
        {
            public int ID_Collaboratore { get; set; }
            public decimal Percentuale { get; set; }
            public string NomeCollaboratore { get; set; }
        }





        //public static void EseguiRipartizioneDaIncasso(int idPratica, decimal importoIncasso)
        //{
        //    try
        //    {
        //        using (var db = new SinergiaDB())
        //        {
        //            var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
        //            if (pratica == null || pratica.Stato == "Annullata")
        //                return;

        //            int idResponsabile = pratica.ID_UtenteResponsabile;
        //            int idUtenteInserimento = UserManager.GetIDUtenteCollegato();
        //            DateTime oggi = DateTime.Today;

        //            // 🔹 Cliente e Owner
        //            var cliente = db.Clienti.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
        //            int? idOwner = cliente?.ID_Operatore;

        //            // 🔹 Incasso corrente
        //            var incasso = db.Incassi
        //                .OrderByDescending(i => i.DataIncasso)
        //                .FirstOrDefault(i => i.ID_Pratiche == idPratica);

        //            bool versaInPlafond = incasso?.VersaInPlafond == true;

        //            // 🧹 Rimuove eventuali voci precedenti collegate all’incasso
        //            db.BilancioProfessionista.RemoveRange(
        //                db.BilancioProfessionista.Where(b => b.ID_Pratiche == idPratica && b.Origine == "Incasso")
        //            );

        //            var voci = new List<BilancioProfessionista>();
        //            decimal lordo = importoIncasso;

        //            // ==========================================================
        //            // 1️⃣ OWNER FEE (5%)
        //            // ==========================================================
        //            decimal ownerFee = 0;
        //            if (idOwner.HasValue)
        //            {
        //                ownerFee = Math.Round(lordo * 0.05m, 2);
        //                voci.Add(new BilancioProfessionista
        //                {
        //                    ID_Professionista = idOwner.Value,
        //                    ID_Pratiche = idPratica,
        //                    TipoVoce = "Ricavo",
        //                    Categoria = "Owner Fee",
        //                    Descrizione = "Compenso Owner (5%)",
        //                    Importo = ownerFee,
        //                    Stato = "Finanziario",
        //                    Origine = "Incasso",
        //                    DataRegistrazione = oggi,
        //                    ID_UtenteInserimento = idUtenteInserimento,
        //                    DataInserimento = DateTime.Now
        //                });
        //            }

        //            // ==========================================================
        //            // 2️⃣ COMPENSI SPECIFICI (CompensiPraticaDettaglio)
        //            // ==========================================================
        //            decimal totaleCompensi = 0;
        //            var compensi = db.CompensiPraticaDettaglio
        //                .Where(c => c.ID_Pratiche == idPratica)
        //                .ToList();

        //            foreach (var comp in compensi)
        //            {
        //                decimal valoreBase = comp.Importo ?? 0;
        //                totaleCompensi += valoreBase;

        //                // 💡 Accredita al professionista intestatario
        //                if (comp.ID_ProfessionistaIntestatario.HasValue)
        //                {
        //                    voci.Add(new BilancioProfessionista
        //                    {
        //                        ID_Professionista = comp.ID_ProfessionistaIntestatario.Value,
        //                        ID_Pratiche = idPratica,
        //                        TipoVoce = "Ricavo",
        //                        Categoria = "Compenso Professionista",
        //                        Descrizione = comp.Descrizione,
        //                        Importo = valoreBase,
        //                        Stato = "Finanziario",
        //                        Origine = "Incasso",
        //                        DataRegistrazione = oggi,
        //                        ID_UtenteInserimento = idUtenteInserimento,
        //                        DataInserimento = DateTime.Now
        //                    });
        //                }

        //                // 👥 Collaboratori (campo "Collaboratori" → formato CSV o JSON)
        //                var collaboratori = ParseCollaboratori(comp.Collaboratori);
        //                foreach (var collab in collaboratori)
        //                {
        //                    decimal quota = Math.Round(valoreBase * (collab.Percentuale / 100m), 2);
        //                    voci.Add(new BilancioProfessionista
        //                    {
        //                        ID_Professionista = collab.ID,
        //                        ID_Pratiche = idPratica,
        //                        TipoVoce = "Ricavo",
        //                        Categoria = "Collaboratore su compenso",
        //                        Descrizione = $"{comp.Descrizione} ({collab.Percentuale:N2}% quota)",
        //                        Importo = quota,
        //                        Stato = "Finanziario",
        //                        Origine = "Incasso",
        //                        DataRegistrazione = oggi,
        //                        ID_UtenteInserimento = idUtenteInserimento,
        //                        DataInserimento = DateTime.Now
        //                    });
        //                }
        //            }

        //            // ==========================================================
        //            // 3️⃣ TRATTENUTA SINERGIA
        //            // ==========================================================
        //            decimal trattenutaSinergia = 0;
        //            var ric = db.RicorrenzeCosti.FirstOrDefault(r =>
        //                r.ID_Professionista == idResponsabile &&
        //                r.Categoria == "Trattenuta Sinergia" &&
        //                r.Attivo);

        //            if (ric != null)
        //            {
        //                trattenutaSinergia = ric.TipoValore == "Percentuale"
        //                    ? Math.Round(lordo * (ric.Valore / 100m), 2)
        //                    : ric.Valore;

        //                voci.Add(new BilancioProfessionista
        //                {
        //                    ID_Professionista = idResponsabile,
        //                    ID_Pratiche = idPratica,
        //                    TipoVoce = "Costo",
        //                    Categoria = "Trattenuta Sinergia",
        //                    Descrizione = ric.TipoValore == "Percentuale"
        //                        ? $"Trattenuta Sinergia {ric.Valore:N2}%"
        //                        : "Trattenuta Sinergia Fissa",
        //                    Importo = trattenutaSinergia,
        //                    Stato = "Finanziario",
        //                    Origine = "Incasso",
        //                    DataRegistrazione = oggi,
        //                    ID_UtenteInserimento = idUtenteInserimento,
        //                    DataInserimento = DateTime.Now
        //                });
        //            }

        //            // ==========================================================
        //            // 4️⃣ COSTI PRATICA
        //            // ==========================================================
        //            decimal costiPratica = db.CostiPratica
        //                .Where(c => c.ID_Pratiche == idPratica)
        //                .Sum(c => (decimal?)c.Importo) ?? 0;

        //            if (costiPratica > 0)
        //            {
        //                voci.Add(new BilancioProfessionista
        //                {
        //                    ID_Professionista = idResponsabile,
        //                    ID_Pratiche = idPratica,
        //                    TipoVoce = "Costo",
        //                    Categoria = "Costi Pratica",
        //                    Descrizione = "Costi collegati alla pratica",
        //                    Importo = costiPratica,
        //                    Stato = "Finanziario",
        //                    Origine = "Incasso",
        //                    DataRegistrazione = oggi,
        //                    ID_UtenteInserimento = idUtenteInserimento,
        //                    DataInserimento = DateTime.Now
        //                });
        //            }

        //            // ==========================================================
        //            // 5️⃣ COMPENSO RESPONSABILE (residuo netto)
        //            // ==========================================================
        //            decimal residuoResponsabile = lordo
        //                                          - ownerFee
        //                                          - totaleCompensi
        //                                          - trattenutaSinergia
        //                                          - costiPratica;

        //            if (residuoResponsabile > 0)
        //            {
        //                voci.Add(new BilancioProfessionista
        //                {
        //                    ID_Professionista = idResponsabile,
        //                    ID_Pratiche = idPratica,
        //                    TipoVoce = "Ricavo",
        //                    Categoria = "Compenso Responsabile",
        //                    Descrizione = "Quota residua responsabile pratica",
        //                    Importo = residuoResponsabile,
        //                    Stato = "Finanziario",
        //                    Origine = "Incasso",
        //                    DataRegistrazione = oggi,
        //                    ID_UtenteInserimento = idUtenteInserimento,
        //                    DataInserimento = DateTime.Now
        //                });
        //            }

        //            // 💾 Salva tutte le voci di bilancio
        //            db.BilancioProfessionista.AddRange(voci);
        //            db.SaveChanges();

        //            // ==========================================================
        //            // 6️⃣ VERSAMENTO IN PLAFOND (solo se attivo)
        //            // ==========================================================
        //            if (versaInPlafond)
        //            {
        //                decimal importoPlafond = importoIncasso - trattenutaSinergia - costiPratica;

        //                var plafond = new PlafondUtente
        //                {
        //                    ID_Utente = idResponsabile,
        //                    ID_Pratiche = idPratica,
        //                    TipoPlafond = "Incasso",
        //                    ImportoTotale = importoPlafond,
        //                    Importo = importoPlafond,
        //                    DataVersamento = oggi,
        //                    ID_UtenteInserimento = idUtenteInserimento,
        //                    DataInserimento = DateTime.Now,
        //                    Note = $"Versamento da incasso pratica {idPratica}"
        //                };

        //                db.PlafondUtente.Add(plafond);
        //                db.SaveChanges();

        //                db.PlafondUtente_a.Add(new PlafondUtente_a
        //                {
        //                    ID_PlannedPlafond_Archivio = plafond.ID_PlannedPlafond,
        //                    ID_Utente = plafond.ID_Utente,
        //                    ID_Pratiche = idPratica,
        //                    TipoPlafond = "Incasso",
        //                    ImportoTotale = importoPlafond,
        //                    Importo = importoPlafond,
        //                    DataVersamento = oggi,
        //                    DataArchiviazione = DateTime.Now,
        //                    NumeroVersione = 1,
        //                    ModificheTestuali = $"💰 Versamento da incasso pratica {idPratica} = {importoPlafond:N2}"
        //                });
        //                db.SaveChanges();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine("❌ Errore in EseguiRipartizioneDaIncasso: " + ex.Message);
        //        throw;
        //    }
        //}

        //private static List<(int ID, decimal Percentuale)> ParseCollaboratori(string input)
        //{
        //    var result = new List<(int, decimal)>();
        //    if (string.IsNullOrWhiteSpace(input))
        //        return result;

        //    try
        //    {
        //        // 🔹 JSON (esempio: [{"ID":123,"Percentuale":20}])
        //        if (input.Trim().StartsWith("["))
        //        {
        //            var json = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(input);
        //            foreach (var item in json)
        //            {
        //                int id = (int)item.ID;
        //                decimal perc = (decimal)item.Percentuale;
        //                result.Add((id, perc));
        //            }
        //        }
        //        else
        //        {
        //            // 🔹 CSV (esempio: "123:20;456:30")
        //            var pairs = input.Split(';');
        //            foreach (var pair in pairs)
        //            {
        //                var parts = pair.Split(':');
        //                if (parts.Length == 2 &&
        //                    int.TryParse(parts[0], out int id) &&
        //                    decimal.TryParse(parts[1], out decimal perc))
        //                {
        //                    result.Add((id, perc));
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine("⚠️ Errore parsing Collaboratori: " + ex.Message);
        //    }

        //    return result;
        //}






        //public static decimal CalcolaUtile(int idPratica, decimal incassato, SinergiaDB db)
        //{
        //    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
        //    if (pratica == null) return 0;

        //    var cliente = db.OperatoriSinergia.FirstOrDefault(c => c.ID_Cliente == pratica.ID_Cliente);
        //    if (cliente == null || cliente.ID_Owner == null) return 0;

        //    int idOwner = cliente.ID_Owner.Value;

        //    // 🟢 Verifica se il professionista è "Resident"
        //    bool isResident = cliente.TipoProfessionista?.Trim().ToLower() == "resident";

        //    // 🔢 Recupera parametri da TipologieCosti
        //    decimal costiGeneraliPct = ParametriHelper.GetCostiGenerali(db);       // es. 20%
        //    decimal ownerFeePct = ParametriHelper.GetOwnerFee(db);                 // es. 5%
        //    decimal costoFissoResident = isResident ? ParametriHelper.GetCostoFissoResident(db) : 0;

        //    // 💳 Plafond attivo alla data della pratica
        //    decimal plafond = ParametriHelper.GetPlafondAttivo(idOwner, pratica.DataCreazione, db);

        //    // 📦 Costi Pratica
        //    decimal costiPratica = db.CostiPratica
        //        .Where(c => c.ID_Pratiche == idPratica)
        //        .Sum(c => (decimal?)c.Importo) ?? 0;

        //    // 📦 Costi Practice (validi alla data della pratica)
        //    DateTime dataRiferimento = pratica.DataCreazione.HasValue ? pratica.DataCreazione.Value: DateTime.Now;

        //    decimal costiPractice = db.AnagraficaCostiPractice
        //        .Where(cp =>
        //            cp.ID_UtenteCreatore == idOwner &&
        //            cp.DataInizio <= dataRiferimento &&
        //            (cp.DataFine == null || cp.DataFine >= dataRiferimento)
        //        )
        //        .Sum(cp => (decimal?)cp.Importo) ?? 0;

        //    // 💰 Calcoli principali
        //    decimal costiGenerali = incassato * costiGeneraliPct / 100;
        //    decimal ownerFee = incassato * ownerFeePct / 100;

        //    // 🔀 OwnerFee va sommato se la pratica è assegnata all'owner, sottratto altrimenti
        //    bool assegnataAllOwner = pratica.ID_UtenteResponsabile == idOwner;
        //    ownerFee = assegnataAllOwner ? +ownerFee : -ownerFee;

        //    // 💡 Calcolo dell'utile
        //    decimal utile = incassato
        //        - costiGenerali
        //        - costoFissoResident
        //        - plafond
        //        - costiPractice
        //        - costiPratica
        //        + ownerFee;

        //    return Math.Round(utile, 2);
        //}

    }
}