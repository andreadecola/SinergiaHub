using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class TipologieCostiViewModel
    {
        public int ID_TipoCosto { get; set; }

        [Required(ErrorMessage = "Il nome della tipologia è obbligatorio.")]
        [StringLength(100)]
        public string Nome { get; set; }

        [Range(0, 100, ErrorMessage = "Inserire una percentuale valida.")]
        public decimal? ValorePercentuale { get; set; }

        [Range(0, 1000000, ErrorMessage = "Inserire un valore fisso valido.")]
        public decimal? ValoreFisso { get; set; }

        [StringLength(50)]
        public string Tipo { get; set; } // 'Percentuale' o 'Fisso'

        [Required]
        [StringLength(20)]
        public string Stato { get; set; } // es. "Attivo", "Inattivo"
        public DateTime DataInizio { get; set; }

        public DateTime? DataFine { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public string NomeCreatore { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoCostoApplicazione { get; set; }


        public int NumeroAssegnati { get; set; }

        public string ID_CodiceCosto => $"CGEN-{ID_TipoCosto}";



        public RicorrenzaCostoViewModel Ricorrenza { get; set; }

        // campi ricorrenza
        // ✅ Info ricorrenza (opzionale)
        public string Periodicita { get; set; }
        public string TipoValore { get; set; }
        public decimal? ValoreRicorrenza { get; set; }
        public DateTime? DataInizioRicorrenza { get; set; }
        public DateTime? DataFineRicorrenza { get; set; }
        public bool? RicorrenzaAttiva { get; set; }

        public string Categoria {  get; set; }

        public string StatoRicorrenza => RicorrenzaAttiva == true ? "Attiva" : "Nessuna";

    }
}