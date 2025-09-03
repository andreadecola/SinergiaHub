using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CostiPersonaliUtenteViewModel
    {
        public int ID_CostoPersonale { get; set; }

        [Required(ErrorMessage = "L'utente è obbligatorio.")]
        public int ID_Utente { get; set; }

        public string NomeProfessionista { get; set; }


        public string Descrizione { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 1000000, ErrorMessage = "Inserire un importo valido.")]
        public decimal Importo { get; set; }

        [Required(ErrorMessage = "La data di inserimento è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime DataInserimento { get; set; }

        public bool Approvato { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}