using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class PracticeViewModel
    {
        public int ID_Practice { get; set; }

        [Required(ErrorMessage = "La descrizione è obbligatoria.")]
        [StringLength(100)]
        public string Descrizione { get; set; }

        public bool Personalizzata { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}