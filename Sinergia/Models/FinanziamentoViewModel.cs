using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class FinanziamentoViewModel
    {
        public int ID_Finanziamento { get; set; }

        public string TipoPlafond { get; set; } // "Finanziamento" o "Incasso"
        public int? ID_Plafond { get; set; }    // Solo se è un incasso
        public int ID_Professionista { get; set; }

        public string NomeProfessionista { get; set; }

        public decimal Importo { get; set; }

        public DateTime DataVersamento { get; set; }


        public DateTime? DataInizio { get; set; } // da PlafondUtente
        public DateTime? DataFine { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
    }
}
