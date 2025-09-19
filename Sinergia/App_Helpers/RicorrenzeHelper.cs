using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;

namespace Sinergia.App_Helpers
{
    public static class RicorrenzeHelper
    {
        public static void EseguiRicorrenzeCostiSeNecessario()
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    DateTime oggi = DateTime.Today;
                    DateTime primoDelMese = new DateTime(oggi.Year, oggi.Month, 1);
                    int idUtenteInserimento = UserManager.GetIDUtenteCollegato();

                    int vociBilancioAggiunte = 0;
                    List<int> praticheRegistrate = new List<int>();

                    // 🔹 Carica eccezioni attive per oggi
                    var eccezioni = db.EccezioniRicorrenzeCosti
                        .Where(e => e.DataInizio <= oggi && e.DataFine >= oggi)
                        .ToList();

                    // 🔹 RICORRENZE COSTI (esclude "Costo Progetto")
                    var ricorrenze = db.RicorrenzeCosti
                        .Where(r => r.Attivo == true &&
                                    r.DataInizio <= oggi &&
                                    (r.DataFine == null || r.DataFine >= oggi) &&
                                    r.Categoria != "Costo Progetto")
                        .ToList();

                    foreach (var ric in ricorrenze)
                    {
                        // ✅ Verifica se è ricorrenza "una tantum"
                        bool isUnaTantum = string.IsNullOrWhiteSpace(ric.Periodicita)
                                           && ric.DataInizio.HasValue
                                           && ric.DataFine.HasValue
                                           && ric.DataInizio.Value.Date == oggi
                                           && ric.DataFine.Value.Date == oggi;

                        // ⛔️ Se non è una tantum e non è giorno di ricorrenza → salta
                        if (!isUnaTantum && !E_Giorno_Ricorrenza(oggi, ric.Periodicita, ric.DataInizio))
                            continue;

                        // 🔍 Verifica se c'è un'eccezione attiva
                        bool haEccezione = eccezioni.Any(e =>
                            (e.ID_Professionista != null && e.ID_Professionista == ric.ID_Professionista) ||
                            (e.ID_Team != null && e.ID_Team == ric.ID_Team));

                        if (haEccezione)
                            continue;

                        int idProfessionista = ric.ID_Professionista ?? 0;
                        if (idProfessionista == 0)
                            continue;

                        decimal importo = CalcolaImportoDaRicorrenza(ric, 0);

                        bool giaPresente = db.BilancioProfessionista.Any(b =>
                            b.ID_Professionista == idProfessionista &&
                            b.DataRegistrazione == primoDelMese &&
                            b.Categoria == ric.Categoria &&
                            b.Origine == "Ricorrenza");

                        if (!giaPresente)
                        {
                            AggiungiVoceRicorrenza(
                                db,
                                idProfessionista,
                                primoDelMese,
                                "Costo",
                                ric.Categoria,
                                $"Ricorrenza - {ric.Categoria}",
                                importo,
                                "Ricorrenza",
                                idUtenteInserimento
                            );
                            vociBilancioAggiunte++;
                        }
                    }

                    // 🔹 PRATICHE MODIFICATE OGGI → REGISTRAZIONE VOCI (inclusi costi progetto)
                    var praticheDelGiorno = db.Pratiche
                        .Where(p => p.Stato != "Annullata" && p.UltimaModifica >= oggi)
                        .Select(p => p.ID_Pratiche)
                        .ToList();

                    foreach (var idPratica in praticheDelGiorno)
                    {
                        RicorrenzeHelper.EseguiRegistrazioniDaPratica(idPratica);
                        praticheRegistrate.Add(idPratica);
                    }

                    // 🔹 LOG DI SISTEMA
                    db.LogOperazioniSistema.Add(new LogOperazioniSistema
                    {
                        NomeOperazione = "RicorrenzeCosti",
                        DataEsecuzione = DateTime.Now,
                        Descrizione = $"Registrate {vociBilancioAggiunte} voci bilancio. " +
                                      (praticheRegistrate.Any()
                                          ? $"Pratiche elaborate: {string.Join(", ", praticheRegistrate)}"
                                          : "Nessuna pratica modificata oggi.")
                    });

                    db.SaveChanges();
                }
            }
            catch
            {
                // Silenzioso per ora
            }
        }


        public static void EseguiRegistrazioniDaPratica(int idPratica)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                    if (pratica == null || pratica.Stato == "Annullata")
                        return;

                    int idResponsabile = pratica.ID_UtenteResponsabile;
                    int? idOwner = pratica.ID_Owner; // può essere nullo
                    decimal budget = pratica.Budget;
                    DateTime oggi = DateTime.Today;
                    int idUtenteInserimento = UserManager.GetIDUtenteCollegato();

                    // 🔁 Rimuove righe esistenti della pratica
                    db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
                        .Where(b => b.ID_Pratiche == idPratica && b.Origine == "Pratica"));

                    var voci = new List<BilancioProfessionista>();

                    decimal totaleTrattenute = 0;
                    decimal totaleOwnerFee = 0;
                    decimal totaleCostiPratica = 0;
                    decimal totaleQuoteCollaboratori = 0;

                    // ============================
                    // ✅ COSTI TRATTENUTA
                    // ============================
                    bool haTrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata.HasValue && pratica.TrattenutaPersonalizzata.Value > 0;

                    if (haTrattenutaPersonalizzata)
                    {
                        decimal perc = pratica.TrattenutaPersonalizzata.Value;
                        decimal trattenuta = Math.Round(budget * (perc / 100m), 2);
                        totaleTrattenute += trattenuta;

                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idResponsabile,
                            DataRegistrazione = oggi,
                            TipoVoce = "Costo",
                            Categoria = "Trattenuta Sinergia Personalizzata",
                            Descrizione = $"Trattenuta Personalizzata {perc:0.##}%",
                            Importo = trattenuta,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }
                    else
                    {
                        var ricTrattenuta = db.RicorrenzeCosti
                            .Where(r => r.Categoria == "Trattenuta Sinergia" && r.Attivo &&
                                        (r.ID_Professionista == idResponsabile || r.ID_Professionista == null))
                            .OrderByDescending(r => r.ID_Professionista == idResponsabile)
                            .FirstOrDefault();

                        if (ricTrattenuta != null)
                        {
                            decimal trattenuta = ricTrattenuta.TipoValore == "Percentuale"
                                  ? Math.Round(budget * (ricTrattenuta.Valore / 100m), 2)
                                  : ricTrattenuta.Valore;

                            totaleTrattenute += trattenuta;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = idResponsabile,
                                DataRegistrazione = oggi,
                                TipoVoce = "Costo",
                                Categoria = "Trattenuta Sinergia",
                                Descrizione = ricTrattenuta.TipoValore == "Percentuale"
                                    ? $"Trattenuta Sinergia {ricTrattenuta.Valore:0.##}%"
                                    : "Trattenuta Sinergia Fissa",
                                Importo = trattenuta,
                                Stato = "Previsionale",
                                Origine = "Pratica",
                                ID_Pratiche = idPratica,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }
                    }

                    // ============================
                    // ✅ OWNER FEE
                    // ============================
                    if (idOwner.HasValue)
                    {
                        decimal ownerFee = Math.Round(budget * 0.05m, 2);
                        totaleOwnerFee += ownerFee;

                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idOwner.Value,
                            DataRegistrazione = oggi,
                            TipoVoce = "Ricavo",
                            Categoria = "Owner Fee",
                            Descrizione = "Ricavo Owner 5%",
                            Importo = ownerFee,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ============================
                    // ✅ COLLABORATORI
                    // ============================
                    var collaboratori = db.Cluster
                        .Where(c => c.ID_Pratiche == idPratica && c.TipoCluster == "Collaboratore")
                        .ToList();

                    foreach (var collab in collaboratori)
                    {
                        if (collab.PercentualePrevisione > 0)
                        {
                            decimal quotaRicavo = Math.Round(budget * (collab.PercentualePrevisione / 100m), 2);
                            totaleQuoteCollaboratori += quotaRicavo;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = collab.ID_Utente,
                                DataRegistrazione = oggi,
                                TipoVoce = "Ricavo",
                                Categoria = "Quota Collaboratore",
                                Descrizione = $"Quota ricavo collaboratore {collab.PercentualePrevisione:0.##}%",
                                Importo = quotaRicavo,
                                Stato = "Previsionale",
                                Origine = "Pratica",
                                ID_Pratiche = idPratica,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }
                    }

                    // ============================
                    // ✅ COSTI MANUALI
                    // ============================
                    var costiPratica = db.CostiPratica
                        .Where(c => c.ID_Pratiche == idPratica)
                        .ToList();

                    foreach (var c in costiPratica)
                    {
                        decimal importo = c.Importo ?? 0;
                        totaleCostiPratica += importo;

                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idResponsabile,
                            DataRegistrazione = oggi,
                            TipoVoce = "Costo",
                            Categoria = "Costo Pratica",
                            Descrizione = string.IsNullOrWhiteSpace(c.Descrizione) ? "Costo Pratica" : c.Descrizione,
                            Importo = importo,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ============================
                    // ✅ RICAVO RESIDUO RESPONSABILE
                    // ============================
                    decimal ricavoResponsabile = budget - (totaleTrattenute + totaleOwnerFee + totaleQuoteCollaboratori + totaleCostiPratica);

                    if (ricavoResponsabile > 0)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idResponsabile,
                            DataRegistrazione = oggi,
                            TipoVoce = "Ricavo",
                            Categoria = "Compenso Responsabile",
                            Descrizione = "Quota residua responsabile",
                            Importo = ricavoResponsabile,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ============================
                    // ✅ Salvataggio
                    // ============================
                    db.BilancioProfessionista.AddRange(voci);
                    db.SaveChanges();
                }
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var eve in ex.EntityValidationErrors)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Entità: {eve.Entry.Entity.GetType().Name} - Stato: {eve.Entry.State}");
                    foreach (var ve in eve.ValidationErrors)
                    {
                        System.Diagnostics.Debug.WriteLine($"  ➤ Errore proprietà: {ve.PropertyName} - Messaggio: {ve.ErrorMessage}");
                    }
                }
                throw;
            }
        }






        private static void AggiungiVoceRicorrenza(SinergiaDB db, int idProf, DateTime data, string tipo, string categoria, string descrizione, decimal importo, string origine, int idUtenteInserimento)
        {
            bool giaPresente = db.BilancioProfessionista.Any(b =>
                b.ID_Professionista == idProf && b.DataRegistrazione == data &&
                b.Categoria == categoria && b.Origine == origine);

            if (!giaPresente)
            {
                db.BilancioProfessionista.Add(new BilancioProfessionista
                {
                    ID_Professionista = idProf,
                    DataRegistrazione = data,
                    TipoVoce = tipo,
                    Categoria = categoria,
                    Descrizione = descrizione,
                    Importo = importo,
                    Stato = "Previsionale",
                    Origine = origine,
                    ID_UtenteInserimento = idUtenteInserimento,
                    DataInserimento = DateTime.Now
                });
            }
        }

        private static decimal CalcolaImportoDaRicorrenza(RicorrenzeCosti ric, decimal baseImporto)
        {
            return ric.TipoValore == "Percentuale" ? Math.Round(baseImporto * (ric.Valore / 100m), 2) : ric.Valore  ;
        }

        private static bool E_Giorno_Ricorrenza(DateTime oggi, string periodicita, DateTime? dataInizio)
        {
            if (string.IsNullOrWhiteSpace(periodicita) || dataInizio == null) return false;

            var giorniPassati = (oggi - dataInizio.Value).Days;

            switch (periodicita)
            {
                case "Mensile": return oggi.Day == dataInizio.Value.Day;
                case "Bimestrale": return giorniPassati % 60 == 0;
                case "Trimestrale": return giorniPassati % 90 == 0;
                case "Semestrale": return giorniPassati % 180 == 0;
                case "Annuale": return oggi.Day == dataInizio.Value.Day && oggi.Month == dataInizio.Value.Month;
                default: return false;
            }
        }

        public static bool EsisteEccezione(SinergiaDB db, int? idProfessionista, int? idTeam, DateTime data, string categoria)
        {
            return db.EccezioniRicorrenzeCosti.Any(e =>
                e.Categoria == categoria &&
                (
                    (idProfessionista != null && e.ID_Professionista == idProfessionista) ||
                    (idTeam != null && e.ID_Team == idTeam)
                ) &&
                e.DataInizio <= data && e.DataFine >= data
            );
        }

    }
}

