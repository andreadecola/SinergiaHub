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

        public static void EseguiRipartizioneDaIncasso(int idPratica, decimal importoIncasso)
        {
            try
            {
                using (var db = new SinergiaDB())
                {
                    var pratica = db.Pratiche.FirstOrDefault(p => p.ID_Pratiche == idPratica);
                    if (pratica == null || pratica.Stato == "Annullata")
                        return;

                    int idProfessionista = pratica.ID_UtenteResponsabile;
                    int idUtenteInserimento = UserManager.GetIDUtenteCollegato();
                    DateTime oggi = DateTime.Today;

                    var incasso = db.Incassi.FirstOrDefault(i => i.ID_Pratiche == idPratica && i.DataIncasso == oggi);
                    bool versaInPlafond = incasso?.VersaInPlafond == true;

                    // Rimuove tutte le voci di bilancio incasso relative a questa pratica
                    db.BilancioProfessionista.RemoveRange(db.BilancioProfessionista
                        .Where(b => b.ID_Pratiche == idPratica && b.Origine == "Incasso"));

                    var voci = new List<BilancioProfessionista>();
                    decimal imponibile = importoIncasso;

                    // Gestione trattenuta Sinergia
                    decimal trattenutaSinergia = 0;

                    if (pratica.TrattenutaPersonalizzata.HasValue && pratica.TrattenutaPersonalizzata.Value > 0)
                    {
                        decimal perc = pratica.TrattenutaPersonalizzata.Value;
                        trattenutaSinergia = Math.Round(imponibile * (perc / 100m), 2);
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Costo",
                            Categoria = "Trattenuta Sinergia Personalizzata",
                            Descrizione = $"Trattenuta Personalizzata {perc:0.##}%",
                            Importo = trattenutaSinergia,
                            Stato = "Finanziario",
                            Origine = "Incasso",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });
                    }
                    else
                    {
                        var ric = db.RicorrenzeCosti.FirstOrDefault(r =>
                            r.ID_Professionista == idProfessionista &&
                            r.Categoria == "Trattenuta Sinergia" &&
                            r.Attivo == true);

                        if (ric != null)
                        {
                            trattenutaSinergia = ric.TipoValore == "Percentuale"
                                ? Math.Round(imponibile * (ric.Valore / 100m), 2)
                                : ric.Valore;

                            voci.Add(new BilancioProfessionista
                            {
                                ID_Professionista = idProfessionista,
                                DataRegistrazione = oggi,
                                TipoVoce = "Costo",
                                Categoria = "Trattenuta Sinergia",
                                Descrizione = ric.TipoValore == "Percentuale"
                                    ? $"Trattenuta Sinergia {ric.Valore:0.##}%"
                                    : "Trattenuta Sinergia Fissa",
                                Importo = trattenutaSinergia,
                                Stato = "Finanziario",
                                Origine = "Incasso",
                                ID_Pratiche = idPratica,
                                ID_UtenteInserimento = idUtenteInserimento,
                                DataInserimento = DateTime.Now
                            });
                        }
                    }

                    decimal utileNetto = Math.Round(imponibile - trattenutaSinergia, 2);

                    if (utileNetto > 0)
                    {
                        voci.Add(new BilancioProfessionista
                        {
                            ID_Professionista = idProfessionista,
                            DataRegistrazione = oggi,
                            TipoVoce = "Ricavo",
                            Categoria = "Utile netto da incasso",
                            Descrizione = "Ricavo netto da incasso",
                            Importo = utileNetto,
                            Stato = "Finanziario",
                            Origine = "Incasso",
                            ID_Pratiche = idPratica,
                            ID_UtenteInserimento = idUtenteInserimento,
                            DataInserimento = DateTime.Now
                        });

                        var comp = new CompensiPratica
                        {
                            ID_Pratiche = idPratica,
                            Tipo = "Incasso",
                            Descrizione = "Utile netto da incasso",
                            Importo = utileNetto,
                            DataInserimento = DateTime.Now,
                            ID_UtenteCreatore = idUtenteInserimento
                        };
                        db.CompensiPratica.Add(comp);

                        // Versionamento CompensiPratica_a
                        db.CompensiPratica_a.Add(new CompensiPratica_a
                        {
                            ID_CompensoArchivio = comp.ID_Compenso,
                            ID_Pratiche = comp.ID_Pratiche,
                            Tipo = comp.Tipo,
                            Descrizione = comp.Descrizione,
                            Importo = comp.Importo,
                            DataInserimento = comp.DataInserimento,
                            ID_UtenteCreatore = comp.ID_UtenteCreatore,
                            DataArchiviazione = DateTime.Now,
                            ID_UtenteArchiviazione = idUtenteInserimento,
                            ModificheTestuali = "Inserimento compenso incasso"
                        });
                    }

                    db.BilancioProfessionista.AddRange(voci);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("❌ Errore in EseguiRipartizioneDaIncasso: " + ex.Message);
                throw;
            }
        }




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