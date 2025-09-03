using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class AvvisoParcellaPdfViewModel
    {

        // 📅 Dati documento
        public DateTime DataAvviso { get; set; }
        public string Stato { get; set; }
        public string MetodoPagamento { get; set; }

        // 💶 Dettaglio economico
        public decimal Importo { get; set; }
        public decimal ContributoIntegrativoPercentuale { get; set; }
        public decimal ContributoIntegrativoImporto { get; set; }
        public decimal AliquotaIVA { get; set; }
        public decimal ImportoIVA { get; set; }
        public decimal Totale => Importo + ContributoIntegrativoImporto + ImportoIVA;

        public string Note { get; set; }
        public string DescrizionePratica { get; set; }




        // 🧾 Professionista (fornitore)
        public string NomeProfessionista { get; set; }
        public string CognomeProfessionista { get; set; }
        public string RagioneSocialeProfessionista { get; set; }
        public string IndirizzoProfessionista { get; set; }
        public string CittaProfessionista { get; set; }
        public string CAPProfessionista { get; set; }
        public string PartitaIVAProfessionista { get; set; }

        // 👤 Cliente
        public string RagioneSocialeCliente { get; set; }
        public string IndirizzoCliente { get; set; }
        public string CittaCliente { get; set; }
        public string CAPCliente { get; set; }
        public string PartitaIVACliente { get; set; }
    }
}