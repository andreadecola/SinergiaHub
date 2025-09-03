using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class RicavoSinergiaViewModel
    {
        public int? ID_Pratiche { get; set; }
        public int ID_Professionista { get; set; }
        public string Titolo { get; set; }
        public string NomeProfessionista { get; set; }
        public string Categoria { get; set; }
        public decimal Importo { get; set; }
        public DateTime DataRegistrazione { get; set; }

        public decimal TotaleTrattenute { get; set; }
        public decimal TotaleResident { get; set; }

        public decimal TotaleComplessivo => TotaleTrattenute + TotaleResident;
    }
}