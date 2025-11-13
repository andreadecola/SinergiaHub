using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class AvvisoParcellaPdfViewModel
    {
        // =======================================================
        // 📅 DATI DOCUMENTO
        // =======================================================
        public DateTime DataAvviso { get; set; }
        public string Stato { get; set; }
        public string MetodoPagamento { get; set; }
        public string Note { get; set; }

        // =======================================================
        // 📌 DETTAGLIO ECONOMICO COMPLETO
        // =======================================================
        public decimal Importo { get; set; }

        // 🔹 Contributo integrativo (CPA)
        public decimal ContributoIntegrativoPercentuale { get; set; }
        public decimal ContributoIntegrativoImporto { get; set; }

        // 🔹 IVA
        public decimal AliquotaIVA { get; set; }
        public decimal ImportoIVA { get; set; }

        // 🔹 Rimborso spese (variabile 0% / 15%)
        public decimal RimborsoSpesePercentuale { get; set; }
        public decimal ImportoRimborsoSpese { get; set; }

        // 🔹 Totale complessivo
        public decimal TotaleAvvisoParcella { get; set; }

        // 🔹 Campo calcolato di fallback (compatibilità retroattiva)
        public decimal Totale =>
            (TotaleAvvisoParcella > 0 ? TotaleAvvisoParcella :
            Importo + ContributoIntegrativoImporto + ImportoIVA + ImportoRimborsoSpese);

        // =======================================================
        // ⚖️ INFORMAZIONI DI TIPOLOGIA / FASE
        // =======================================================
        public string TipologiaAvviso { get; set; }          // es: "Fisso", "A ore", "Giudiziale"
        public string FaseGiudiziale { get; set; }           // es: "Udienza preliminare", "Appello", ecc.
        public int? ID_CompensoOrigine { get; set; }         // collegamento a CompensoPraticaDettaglio se presente

        // =======================================================
        // 📄 DESCRIZIONE PRATICA
        // =======================================================
        public string DescrizionePratica { get; set; }

        // =======================================================
        // 👤 PROFESSIONISTA (fornitore)
        // =======================================================
        public string NomeProfessionista { get; set; }
        public string CognomeProfessionista { get; set; }
        public string RagioneSocialeProfessionista { get; set; }
        public string IndirizzoProfessionista { get; set; }
        public string CittaProfessionista { get; set; }
        public string CAPProfessionista { get; set; }
        public string PartitaIVAProfessionista { get; set; }

        // =======================================================
        // 🧾 CLIENTE
        // =======================================================
        public string RagioneSocialeCliente { get; set; }
        public string IndirizzoCliente { get; set; }
        public string CittaCliente { get; set; }
        public string CAPCliente { get; set; }
        public string PartitaIVACliente { get; set; }
    }
}