using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class RicorrenzaCostoViewModel
    {
        public int? ID_Ricorrenza { get; set; }

        public int ID_AnagraficaCosto { get; set; } // ✅ Campo corretto per identificare la voce di costo

        public int? ID_Professionista { get; set; }

        public int? ID_Professione { get; set; }

        public int? ID_Team { get; set; } // ✅ nuovo campo previsto nella tabella

        [StringLength(20)]
        public string Periodicita { get; set; } // es: "Mensile", "Annuale"

        [Required]
        [StringLength(20)]
        public string TipoValore { get; set; } // "Fisso", "Percentuale"

        [Required]
        [DataType(DataType.Currency)]
        public decimal? Valore { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DataInizio { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DataFine { get; set; }

        [Required]
        [StringLength(50)]
        public string Categoria { get; set; } // es: "Pratica", "Team", "Personale", ecc.

        public bool EreditaDatiDaPratica { get; set; }

        public bool Attivo { get; set; } = true;

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}