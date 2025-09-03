using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class IncassoViewModel
    {
        public int ID_Incasso { get; set; }

        [Required(ErrorMessage = "La pratica è obbligatoria.")]
        public int ID_Pratiche { get; set; }

        public int ID_AvvisoParcella { get; set; }

        [Display(Name = "Data Incasso")]
        [DataType(DataType.Date)]
        public DateTime? DataIncasso { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 9999999, ErrorMessage = "Inserire un importo valido.")]
        public decimal Importo { get; set; }

        [StringLength(50)]
        public string MetodoPagamento { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }  // Es: "Registrato", "Confermato"

        [StringLength(500)]
        public string Note { get; set; }

        // Extra per la visualizzazione
        public string NomePratica { get; set; }
        public decimal UtileCalcolato { get; set; }

        // Permessi utente
        public bool PuoEliminare { get; set; }
        public bool PuoModificare { get; set; }


        public int? ID_UtenteCreatore { get; set; }

        public bool? VersaInPlafond { get; set; }

        public decimal UtileNetto { get; set; } // per mostrare l'utile

        public string DescrizioneAvvisoParcella { get; set; }

        public decimal? TotalePratica { get; set; }
        // 🔹 Avviso Parcella
        public decimal? ImportoSenzaIVA { get; set; }
        public decimal? ImportoAvviso { get; set; }
        public decimal? ImportoIVA { get; set; }
        public decimal? AliquotaIVA { get; set; }
    }
}