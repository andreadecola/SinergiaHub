using Sinergia.Model;
using System;
using System.Collections.Generic;

namespace Sinergia.Models
{
    public class AziendaViewModel
    {
        public int ID_Azienda { get; set; }

        // 🔹 Dati anagrafici principali
        public string Nome { get; set; } // Nome commerciale (breve)
        public string TipoRagioneSociale { get; set; } // Ragione sociale ufficiale
        public string PartitaIVA { get; set; }
        public string CodiceFiscale { get; set; }
        public string CodiceUnivoco { get; set; }
        public string Indirizzo { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }
        public string PEC { get; set; }
        public string Citta { get; set; }
        public string Nazione { get; set; }
        public string Stato { get; set; }
        public string Note { get; set; }
        public string DescrizioneAttivita { get; set; }
        public string SitoWEB { get; set; }

        // 🔹 Dati di relazione
        public int UtentiAssociati { get; set; }
        public bool HaUtentiAssegnati { get; set; }

        // 🔹 Settore e categoria servizi
        public string NomeSettoreFornitore { get; set; }

        /// <summary>
        /// ID logico della categoria servizi collegata all'azienda
        /// (campo OperatoriSinergia.ID_CategoriaServizi)
        /// </summary>
        public int? ID_CategoriaServizi { get; set; }

        /// <summary>
        /// Nome della categoria (recuperato da CategorieCosti.Nome)
        /// </summary>
        public string NomeCategoriaServizi { get; set; }

        // 🔹 Dati bancari associati
        public DatiBancariViewModel DatiBancari { get; set; }
        public string NomeBanca { get; set; }
        public string IBAN { get; set; }
        public string Intestatario { get; set; }
        public string BIC_SWIFT { get; set; }
        public string NoteBancarie { get; set; }

        // 🔹 Ruolo commerciale
        public bool ÈCliente { get; set; }
        public bool ÈFornitore { get; set; }

        public string TipoRuolo
        {
            get
            {
                if (ÈCliente && ÈFornitore) return "Cliente / Fornitore";
                if (ÈCliente) return "Cliente";
                if (ÈFornitore) return "Fornitore";
                return "N/D";
            }
        }

        // 🔹 Permessi (partial _AzioniFornitore)
        public bool UtenteCorrenteHaPermessi { get; set; } // almeno permessi base per vedere le azioni
        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
    }
}
