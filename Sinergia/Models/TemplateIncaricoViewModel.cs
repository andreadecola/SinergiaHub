using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sinergia.Models
{
    public class TemplateIncaricoViewModel
    {
        public int IDTemplateIncarichi { get; set; }

        [Required(ErrorMessage = "Il nome del template è obbligatorio.")]
        [StringLength(100)]
        public string NomeTemplate { get; set; }

        [Required(ErrorMessage = "Il contenuto HTML è obbligatorio.")]
        [AllowHtml]
        public string ContenutoHtml { get; set; }

        [Required(ErrorMessage = "La professione è obbligatoria.")]
        public int ID_Professione { get; set; }

        public string NomeProfessione { get; set; } // opzionale, utile per visualizzare

        [StringLength(20)]
        public string Stato { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
    }
}