using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class RelazioneUtentiViewModel
    {
        public int ID_Relazione { get; set; }

        public int ID_Utente { get; set; }
        public int ID_UtenteAssociato { get; set; }

        public string TipoRelazione { get; set; } // es. "Referente", "Assegnato", ecc.

        public DateTime DataInizio { get; set; }
        public DateTime? DataFine { get; set; }

        public string Stato { get; set; }         // es. "Attivo", "Sospeso", ecc.
        public string Note { get; set; }

        public int? ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }
        public DateTime? UltimaModifica { get; set; }

        // Opzionale per visualizzazione
        public string NomeUtente { get; set; }
        public string NomeAssociato { get; set; }
        public string DescrizioneRelazione => $"{NomeUtente} → {NomeAssociato} ({TipoRelazione})";

    }
}