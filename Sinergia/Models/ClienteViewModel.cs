using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class ClienteViewModel
    {
        public int ID_Cliente { get; set; }

        public string TipoCliente { get; set; } // "Azienda" o "Professionista"

        public string Nome { get; set; }
        public string Cognome { get; set; }

        public string CodiceFiscale { get; set; }
        public string PIVA { get; set; }
        public string CodiceUnivoco { get; set; }

        public string Indirizzo { get; set; }
        public int? ID_Citta { get; set; }
        public int? ID_Nazione { get; set; }

        public string Telefono { get; set; }
        public string MAIL1 { get; set; }
        public string MAIL2 { get; set; }
        public string SitoWEB { get; set; }

        public string Stato { get; set; }
        public string DescrizioneAttivita { get; set; }
        public string Note { get; set; }

        public DateTime? DataCreazione { get; set; }
        public DateTime? UltimaModifica { get; set; }
        public int? ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }

        public byte[] Documento { get; set; }

        public string TipoRagioneSociale { get; set; } // Solo per aziende
        public int? ID_Practice { get; set; } // Solo per professionisti

        public bool? ÈCliente { get; set; }
        public bool? ÈFornitore { get; set; }

        // Proprietà di comodo
        public bool ÈAzienda => TipoCliente?.ToLower() == "azienda";
        public string NomeVisualizzato => ÈAzienda ? Nome : $"{Nome} {Cognome}";

        // Per vista dropdown
        public string NomeCitta { get; set; }
        public string NomeNazione { get; set; }
        public string NomePractice { get; set; }

       

    }
}