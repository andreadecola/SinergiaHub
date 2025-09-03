using System.Collections.Generic;
using System;

namespace Sinergia.Models
{
    public class UtenteViewModel
    {
        public int ID_Utente { get; set; }

        public string TipoUtente { get; set; } // Admin, Partner, Collaboratore, Professionista

        public string Nome { get; set; }

        public string Cognome { get; set; }

        public string CodiceFiscale { get; set; }

        public string PIVA { get; set; }

        public string CodiceUnivoco { get; set; }

        public string Telefono { get; set; }

        public string Cellulare1 { get; set; }

        public string Cellulare2 { get; set; }

        public string MAIL1 { get; set; }

        public string MAIL2 { get; set; }

        public string Stato { get; set; } // Attivo / Non attivo

        public string DescrizioneAttivita { get; set; }

        public string Note { get; set; }

        public string Indirizzo { get; set; }

        public int? ID_CittaResidenza { get; set; }
        public int? ID_Nazione { get; set; }

        public string NomeCompleto => $"{Nome} {Cognome}";

        // Per gestione permessi
        public bool PuòEssereAttivato => Stato == "Non attivo";

        public bool ÈAttivo => Stato == "Attivo";

        // Per assegnazioni future
        public List<int> ID_AziendeAssegnate { get; set; }

        public DateTime? DataCreazione { get; set; }
        public DateTime? UltimaModifica { get; set; }

        public int? ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }

        public string Ruolo { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
    }
}
