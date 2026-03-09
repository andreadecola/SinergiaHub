using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class PagamentoProfessionistaViewModel
    {
        // ===============================
        // IDENTIFICATIVI TECNICI
        // ===============================
        public int ID_Bilancio { get; set; }          // Riga bilancio
        public int ID_Professionista { get; set; }    // ID Operatore
        public int ID_Utente { get; set; }            // ID Utente collegato

        public int? ID_Pratiche { get; set; }
        public int? ID_AvvisoParcella { get; set; }
        public int? ID_Incasso { get; set; }

        // ===============================
        // DATI VISUALIZZAZIONE
        // ===============================
        public string NomeProfessionista { get; set; }
        public string CodicePratica { get; set; }
        public string TitoloPratica { get; set; }

        public string Descrizione { get; set; }
        public string Categoria { get; set; }

        public DateTime? DataRegistrazione { get; set; }

        public decimal Importo { get; set; }

        public string Stato { get; set; }

        // ===============================
        // SUPPORTO UI
        // ===============================
        public bool Selezionato { get; set; }
    }
}