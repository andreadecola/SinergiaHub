using Sinergia.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.App_Helpers
{
    public class AvvisiCompensiHelper
    {
        public static void EseguiGenerazioneAvvisiCompensi()
        {
            using (var db = new SinergiaDB())
            {
                DateTime oggi = DateTime.Today;
                DateTime primoDelMese = new DateTime(oggi.Year, oggi.Month, 1);

                System.Diagnostics.Trace.WriteLine("==================================================");
                System.Diagnostics.Trace.WriteLine("🔥 AVVIO EseguiGenerazioneAvvisiCompensi()");
                System.Diagnostics.Trace.WriteLine($"📅 Data sistema: {oggi:dd/MM/yyyy}");

                var compensiAttivi = db.CompensiPraticaDettaglio
                    .Where(c => c.IsAttivo
                             && (c.Categoria == "Mensile"
                              || c.Categoria == "Trimestrale"
                              || c.Categoria == "Semestrale"
                              || c.Categoria == "Annuale"))
                    .ToList();

                System.Diagnostics.Trace.WriteLine($"📊 Compensi attivi trovati: {compensiAttivi.Count}");

                foreach (var comp in compensiAttivi)
                {
                    try
                    {
                        if (!comp.DataCreazione.HasValue)
                            continue;

                        int intervalloMesi = GetIntervalloMesi(comp.Categoria);
                        if (intervalloMesi == 0)
                            continue;

                        DateTime dataInizio = new DateTime(
                            comp.DataCreazione.Value.Year,
                            comp.DataCreazione.Value.Month,
                            1);

                        // 🔥 Calcolo mesi trascorsi dalla creazione
                        int mesiTrascorsi =
                            ((primoDelMese.Year - dataInizio.Year) * 12)
                            + primoDelMese.Month - dataInizio.Month;

                        if (mesiTrascorsi < 0)
                            continue;

                        // 🔥 Deve essere multiplo dell'intervallo
                        if (mesiTrascorsi % intervalloMesi != 0)
                            continue;

                        // 🔎 Controllo se esiste già
                        bool esiste = db.AvvisiParcella.Any(a =>
                            a.ID_CompensoOrigine == comp.ID_RigaCompenso &&
                            a.DataAvviso == primoDelMese);

                        if (esiste)
                        {
                            System.Diagnostics.Trace.WriteLine(
                                $"⚠ Avviso già esistente per Compenso {comp.ID_RigaCompenso}");
                            continue;
                        }

                        System.Diagnostics.Trace.WriteLine(
                            $"✅ Genero avviso per Compenso {comp.ID_RigaCompenso} - {primoDelMese:dd/MM/yyyy}");

                        CreaAvvisoDaCompenso(db, comp, primoDelMese);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("❌ ERRORE GENERAZIONE:");
                        System.Diagnostics.Trace.WriteLine(ex.ToString());
                    }
                }

                db.SaveChanges();

                System.Diagnostics.Trace.WriteLine("💾 SaveChanges completato.");
                System.Diagnostics.Trace.WriteLine("🏁 FINE EseguiGenerazioneAvvisiCompensi()");
                System.Diagnostics.Trace.WriteLine("==================================================");
            }
        }
        private static int GetIntervalloMesi(string categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria))
                return 0;

            switch (categoria.Trim())
            {
                case "Mensile":
                    return 1;

                case "Trimestrale":
                    return 3;

                case "Semestrale":
                    return 6;

                case "Annuale":
                    return 12;

                default:
                    return 0;
            }
        }

        private static void CreaAvvisoDaCompenso(
       SinergiaDB db,
       CompensiPraticaDettaglio comp,
       DateTime dataAvviso)
        {
            var pratica = db.Pratiche
                .FirstOrDefault(p => p.ID_Pratiche == comp.ID_Pratiche);

            if (pratica == null)
                return;

            // 🔥 BLOCCO FONDAMENTALE
            if (!string.Equals(pratica.Stato?.Trim(), "In lavorazione",
                StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Trace.WriteLine(
                    $"⛔ Avviso NON creato. Pratica {pratica.ID_Pratiche} in stato: {pratica.Stato}");

                return; // ⛔ ESCE SENZA CREARE NULLA
            }

            int? idOwnerCliente = db.Clienti
                .Where(c => c.ID_Cliente == pratica.ID_Cliente)
                .Select(c => c.ID_Operatore)
                .FirstOrDefault();

            // 🔥 REGIME DERIVATO DAL TIPO COMPENSO
            string regime = comp.TipoCompenso?.Trim().ToLower() == "giudiziale"
                ? "giudiziale"
                : "professionale";

            var nuovoAvviso = new AvvisiParcella
            {
                ID_Pratiche = comp.ID_Pratiche,
                ID_CompensoOrigine = comp.ID_RigaCompenso,

                DataAvviso = dataAvviso,
                DataCompetenzaEconomica = dataAvviso,

                TitoloAvviso = comp.Descrizione,
                Importo = comp.Importo ?? 0m,

                Stato = "In Attesa",
                TipologiaAvviso = "Compenso Periodico",

                RegimeFiscale = regime,   // 🔥 QUI È LA SOLUZIONE

                ID_ResponsabilePratica = pratica.ID_UtenteResponsabile,
                ID_OwnerCliente = idOwnerCliente,
                ID_UtenteCreatore = pratica.ID_UtenteResponsabile,
                ID_UtenteModifica = pratica.ID_UtenteResponsabile,
                DataModifica = DateTime.Now
            };

            FiscaleHelper.ApplicaRegoleFiscali(nuovoAvviso, pratica.Tipologia);

            db.AvvisiParcella.Add(nuovoAvviso);
        }
    }
}