using Sinergia.Model;
using System;

namespace Sinergia.App_Helpers
{
    public static class FiscaleHelper
    {
        // ======================================================
        // 📌 Applica Regole Fiscali Automatiche
        // ======================================================
        public static void ApplicaRegoleFiscali(
            AvvisiParcella model,
            string tipoAttivitaPratica)
        {
            string tipoPratica = tipoAttivitaPratica?.Trim().ToLower();
            string regime = model.RegimeFiscale?.Trim().ToLower();

            // ==================================================
            // IMPOSTAZIONE PERCENTUALI AUTOMATICHE
            // ==================================================

            switch (regime)
            {
                // ----------------------------------------------
                // COMPENSO PROFESSIONALE
                // ----------------------------------------------
                case "professionale":

                    if (tipoPratica == "giudiziale")
                        throw new Exception("Compenso professionale non consentito per attività giudiziale.");

                    model.RimborsoSpesePercentuale = 0m;
                    model.ContributoIntegrativoPercentuale = 4m;
                    model.AliquotaIVA = 22m;
                    break;

                // ----------------------------------------------
                // COMPENSO GIUDIZIALE
                // ----------------------------------------------
                case "giudiziale":

                    if (tipoPratica == "a ore" || tipoPratica == "fisso")
                        throw new Exception("Compenso giudiziale non consentito per attività a ore o fisso.");

                    model.RimborsoSpesePercentuale = 15m;
                    model.ContributoIntegrativoPercentuale = 4m;
                    model.AliquotaIVA = 22m;
                    break;

                // ----------------------------------------------
                // RIMBORSO SPESE ANTICIPATE
                // ----------------------------------------------
                case "rimborso_anticipate":

                    if (tipoPratica == "giudiziale")
                        throw new Exception("Rimborso anticipate non consentito per attività giudiziale.");

                    model.RimborsoSpesePercentuale = 0m;
                    model.ContributoIntegrativoPercentuale = 0m;
                    model.AliquotaIVA = 0m;
                    break;

                // ----------------------------------------------
                // RIMBORSO SPESE URGENTI
                // ----------------------------------------------
                case "rimborso_urgenti":

                    model.RimborsoSpesePercentuale = 0m;
                    model.ContributoIntegrativoPercentuale = 0m;
                    model.AliquotaIVA = 0m;
                    break;

                default:
                    throw new Exception("Tipologia avviso non valida.");
            }

            // ==================================================
            // 🔥 RICALCOLO IMPORTI (ANTI-NULL SAFE)
            // ==================================================

            decimal baseImponibile = model.Importo ?? 0m;
            decimal percRimborso = model.RimborsoSpesePercentuale ?? 0m;
            decimal percCI = model.ContributoIntegrativoPercentuale ?? 0m;
            decimal percIVA = model.AliquotaIVA ?? 0m;

            // 1️⃣ Spese generali
            decimal importoRimborso =
                Math.Round(baseImponibile * percRimborso / 100m, 2);

            // 2️⃣ Base CI = imponibile + spese
            decimal baseCI = baseImponibile + importoRimborso;

            // 3️⃣ Contributo integrativo
            decimal contributo =
                Math.Round(baseCI * percCI / 100m, 2);

            // 4️⃣ Base IVA
            decimal imponibileIVA = baseImponibile + importoRimborso + contributo;

            // 5️⃣ IVA
            decimal importoIVA =
                Math.Round(imponibileIVA * percIVA / 100m, 2);

            // 6️⃣ Totale finale
            decimal totale = imponibileIVA + importoIVA;

            // ==================================================
            // Assegnazione finale al model
            // ==================================================

            model.ImportoRimborsoSpese = importoRimborso;
            model.ContributoIntegrativoImporto = contributo;
            model.ImportoIVA = importoIVA;
            model.TotaleAvvisiParcella = totale;
        }
    }
}