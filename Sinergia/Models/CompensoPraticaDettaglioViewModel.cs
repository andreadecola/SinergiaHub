using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CompensoPraticaDettaglioViewModel
    {
        public int ID_RigaCompenso { get; set; }
        public int ID_Pratiche { get; set; }

        public string TipoCompenso { get; set; }          // Fisso / A ore / Giudiziale
        public string Descrizione { get; set; }           // Attività / Fase / Ruolo
        public decimal? Importo { get; set; }             // Importo pattuito
        public string Categoria { get; set; }             // Mensile, Annuale, %, ecc.
        public decimal? ValoreStimato { get; set; }       // Unità o valore stimato
        public int Ordine { get; set; }                   // Progressivo 1), 2)…

        // Campi specifici
        public string EstremiGiudizio { get; set; }       // Solo per giudiziale
        public string OggettoIncarico { get; set; }       // Solo per "a ore"

        // Metadati
        public DateTime? DataCreazione { get; set; }
        public int ID_UtenteCreatore { get; set; }
        public DateTime? UltimaModifica { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }

        // Nuovo campo intestatario
        public int? ID_ProfessionistaIntestatario { get; set; }
        public string NomeProfessionistaIntestatario { get; set; } // opzionale, join con Utenti

        // Campo extra per la view
        public string NomeCreatore { get; set; }          // opzionale, join con tabella Utenti
    }
}