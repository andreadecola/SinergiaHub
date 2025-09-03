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

                    int idProfessionista = pratica.ID_UtenteResponsabile;
                    decimal budget = pratica.Budget;
                    DateTime oggi = DateTime.Today;
                    int idUtenteInserimento = UserManager.GetIDUtenteCollegato();

                    // 🔁 Pulisce righe esistenti per la pratica
                    db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
                        .Where(b => b.ID_Pratiche == idPratica && b.Origine == "Pratica"));

                    var voci = new List<BilancioProfessionista>();

                    // ✅ Trattenuta Personalizzata o Ricorrenza
                    bool haTrattenutaPersonalizzata = pratica.TrattenutaPersonalizzata.HasValue && pratica.TrattenutaPersonalizzata.Value > 0;
                    bool trattenutaAggiunta = false;

                    if (haTrattenutaPersonalizzata)
                    {
                        decimal perc = pratica.TrattenutaPersonalizzata.Value;
                        decimal trattenuta = Math.Round(budget * (perc / 100m), 2);

                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
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

                        trattenutaAggiunta = true;
                    }

                    if (!trattenutaAggiunta)
                    {
                        var ricTrattenuta = db.RicorrenzeCosti
                            .Where(r => r.Categoria == "Trattenuta Sinergia" && r.Attivo &&
                                        (r.ID_Professionista == idProfessionista || r.ID_Professionista == null))
                            .OrderByDescending(r => r.ID_Professionista == idProfessionista)
                            .FirstOrDefault();

                        if (ricTrattenuta != null)
                        {
                            decimal trattenuta = ricTrattenuta.TipoValore == "Percentuale"
                                  ? Math.Round(budget * (ricTrattenuta.Valore / 100m), 2)
                                  : ricTrattenuta.Valore;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = idProfessionista,
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

                    // ✅ Owner Fee 5%
                    decimal ownerFee = Math.Round(budget * 0.05m, 2);
                    voci.Add(new BilancioProfessionista
                    {
                        ID_Professionista = idProfessionista,
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

                    // ✅ Compenso
                    string tipologia = pratica.Tipologia;

                    if (tipologia == "Fisso" && pratica.ImportoFisso.HasValue)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Ricavo",
                            Categoria = "Compenso Fisso",
                            Descrizione = "Compenso Fisso da pratica",
                            Importo = pratica.ImportoFisso.Value,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }
                    else if (tipologia == "A ore" && pratica.TariffaOraria.HasValue)
                    {
                        if (pratica.OrePreviste.HasValue)
                        {
                            decimal previsionale = pratica.TariffaOraria.Value * pratica.OrePreviste.Value;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = idProfessionista,
                                DataRegistrazione = oggi,
                                TipoVoce = "Ricavo",
                                Categoria = "Compenso a ore (previsto)",
                                Descrizione = $"Compenso previsto per {pratica.OrePreviste.Value} ore",
                                Importo = previsionale,
                                Stato = "Previsionale",
                                Origine = "Pratica",
                                ID_Pratiche = idPratica,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }

                        if (pratica.OreEffettive.HasValue)
                        {
                            decimal economico = pratica.TariffaOraria.Value * pratica.OreEffettive.Value;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = idProfessionista,
                                DataRegistrazione = oggi,
                                TipoVoce = "Ricavo",
                                Categoria = "Compenso a ore (effettivo)",
                                Descrizione = $"Compenso effettivo per {pratica.OreEffettive.Value} ore",
                                Importo = economico,
                                Stato = "Economico",
                                Origine = "Pratica",
                                ID_Pratiche = idPratica,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }
                    }
                    else if (tipologia == "Giudiziale" && pratica.AccontoGiudiziale.HasValue)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Ricavo",
                            Categoria = "Acconto Giudiziale",
                            Descrizione = "Acconto iniziale per pratica giudiziale",
                            Importo = pratica.AccontoGiudiziale.Value,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ✅ Costi Generali
                    var costiGenerali = db.RicorrenzeCosti
                        .Where(r =>
                            (r.ID_Professionista == idProfessionista || r.ID_Professionista == null) &&
                            r.Categoria == "Costo Generale" && r.Attivo)
                        .ToList();

                    foreach (var ric in costiGenerali)
                    {
                        // 👉 Qui inserisci la riga di debug:
                        System.Diagnostics.Debug.WriteLine($"🧪 Ricorrenza ID = {ric.ID_Ricorrenza} | TipoValore = {ric.TipoValore} | Valore = {ric.Valore}");

                        decimal importo = ric.TipoValore == "Percentuale"
                            ? Math.Round(budget * (ric.Valore / 100m), 2)
                            : ric.Valore;

                        if (importo <= 0) continue;

                        string descrizione = "Costo Generale";

                        if (haTrattenutaPersonalizzata && descrizione.ToLower().Contains("trattenuta"))
                            continue;

                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Costo",
                            Categoria = "Costo Generale",
                            Descrizione = descrizione,
                            Importo = importo,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ✅ Costi della Pratica (manuali)
                    var costiPratica = db.CostiPratica
                        .Where(c => c.ID_Pratiche == idPratica)
                        .ToList();

                    foreach (var c in costiPratica)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Costo",
                            Categoria = "Costo Pratica",
                            Descrizione = c.Descrizione,
                            Importo = c.Importo ?? 0,
                            Stato = "Previsionale",
                            Origine = "Pratica",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }

                    // ✅ Salvataggio
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

