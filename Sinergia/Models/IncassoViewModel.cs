using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sinergia.Models
{
    public class IncassoViewModel
    {
        public int ID_Incasso { get; set; }

        [Required(ErrorMessage = "La pratica è obbligatoria.")]
        public int ID_Pratiche { get; set; }

        public int ID_AvvisoParcella { get; set; }

        public string StatoAvviso { get; set; }  // Es: "Inviato", "Pagato", "Da incassare"


        // 📅 Date
        [Display(Name = "Data Incasso")]
        [DataType(DataType.Date)]
        public DateTime? DataIncasso { get; set; }

        [Display(Name = "Data Competenza")]
        [DataType(DataType.Date)]
        public DateTime? DataCompetenza { get; set; }   // <-- aggiunta

        // 💰 Importi
        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 9999999, ErrorMessage = "Inserire un importo valido.")]
        public decimal Importo { get; set; }

        public decimal? ImportoSenzaIVA { get; set; }
        public decimal? ImportoAvviso { get; set; }
        public decimal? ImportoIVA { get; set; }
        public decimal? AliquotaIVA { get; set; }

        // 🔎 Metadati
        [StringLength(50)]
        public string MetodoPagamento { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }  // Es: "Da pagare", "Incassato"

        [StringLength(500)]
        public string Note { get; set; }

        public string NomePratica { get; set; }
        public string DescrizioneAvvisoParcella { get; set; }
        public decimal? TotalePratica { get; set; }

        // Utile / ripartizioni
        public decimal UtileCalcolato { get; set; }
        public decimal UtileNetto { get; set; }

        // Permessi
        public bool PuoEliminare { get; set; }
        public bool PuoModificare { get; set; }

        // Altri
        public int? ID_UtenteCreatore { get; set; }
        public bool? VersaInPlafond { get; set; }

        // (facoltativo ma utile in view)
        public string TipoRiga { get; set; } // "Avviso" o "Incasso"
    }
}
