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

        [Display(Name = "Descrizione")]
        public string Descrizione { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 1000000, ErrorMessage = "L'importo deve essere maggiore di zero.")]
        [DataType(DataType.Currency)]
        public decimal? Importo { get; set; }

        [Display(Name = "Voce di costo")]
        public int? ID_AnagraficaCosto { get; set; }

        [Display(Name = "Fornitore")]
        public int? ID_Fornitore { get; set; }

        [Display(Name = "Data competenza economica")]
        [DataType(DataType.Date)]
        public DateTime? DataCompetenzaEconomica { get; set; }

        [Display(Name = "Data inserimento")]
        [DataType(DataType.Date)]
        public DateTime DataInserimento { get; set; } = DateTime.Now;

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        // 👇 Campi aggiuntivi opzionali per la vista
        [Display(Name = "Nome fornitore")]
        public string NomeFornitore { get; set; }

        [Display(Name = "Descrizione voce costo")]
        public string NomeVoceCosto { get; set; }

        [Display(Name = "Stato")]
        public string Stato { get; set; }
    }
}