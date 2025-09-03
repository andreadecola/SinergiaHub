using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CostiPraticaViewModel
    {
        public int ID_CostoPratica { get; set; }

        [Required(ErrorMessage = "La pratica è obbligatoria.")]
        public int ID_Pratiche { get; set; }

        public string Descrizione { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "L'importo deve essere maggiore di zero.")]
        public decimal? Importo { get; set; }

        public int? ID_Fornitore { get; set; }

        [Required(ErrorMessage = "La data di inserimento è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime DataInserimento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}