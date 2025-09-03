using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class PlafondUtenteViewModel
    {
        public int ID_PlannedPlafond { get; set; }

        [Required(ErrorMessage = "L'utente è obbligatorio.")]
        public int ID_Utente { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0, 1000000, ErrorMessage = "Inserire un importo valido.")]
        public decimal ImportoTotale { get; set; }

        [Required(ErrorMessage = "Il tipo di plafond è obbligatorio.")]
        [StringLength(20)]
        public string TipoPlafond { get; set; } // 'Trattenuta' o 'Finanziamento'

        [Required(ErrorMessage = "La data di inizio è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime DataInizio { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DataFine { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public bool PuoModificare { get; set; }

        public bool PuoEliminare { get; set; }

        public string NomeProfessionista { get; set; }


    }
}