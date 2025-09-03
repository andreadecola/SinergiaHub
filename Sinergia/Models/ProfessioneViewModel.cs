using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class ProfessioneViewModel
    {
        public int ProfessioniID { get; set; }

        public string Codice { get; set; }

        public string Descrizione { get; set; }

        public int? ID_ProfessionistaRiferimento { get; set; }

        public string NomeProfessionista { get; set; } // ⚠️ Questo viene risolto dalla tabella OperatoriSinergia

        public decimal? PercentualeContributoIntegrativo { get; set; }
    }
}