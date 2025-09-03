using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class OperazioniEconomicheViewModel
    {
        public int ID_Transazione { get; set; }
        public int? ID_Pratiche { get; set; }
        public string Categoria { get; set; }
        public decimal Importo { get; set; }
        public string Descrizione { get; set; }
        public DateTime? DataOperazione { get; set; }
        public string Stato { get; set; }

        public int ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataArchiviazione { get; set; }
        public int? ID_UtenteArchiviazione { get; set; }
        public string TipoCliente { get; set; } // <-- AGGIUNGERE QUESTO
        public string TipoOperazione { get; set; }

        // Campi aggiuntivi per la visualizzazione
        public string NomePratica { get; set; }
        public string NomeUtenteCreatore { get; set; }
        public string NomeUtenteUltimaModifica { get; set; }

    }
}