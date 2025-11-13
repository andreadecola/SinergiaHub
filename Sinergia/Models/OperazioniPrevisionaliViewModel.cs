using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class OperazioniPrevisionaliViewModel
    {
        public int ID_Previsione { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_Professionista { get; set; }

        public decimal? Percentuale { get; set; }

        public string TipoOperazione { get; set; }

        public string Descrizione { get; set; }

        public decimal? ImportoPrevisto { get; set; }

        public DateTime? DataPrevisione { get; set; }

        public string Stato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        // ✅ Campi aggiuntivi per visualizzazione dashboard
        public string NomeCliente { get; set; }
        public string NomeProfessionista { get; set; }
        public string NomePratica { get; set; }
        public string NomeUtenteCreatore { get; set; }

        public decimal? BudgetPratica { get; set; }      // valore complessivo della pratica

        public string DebugInfo { get; set; }

    }
}