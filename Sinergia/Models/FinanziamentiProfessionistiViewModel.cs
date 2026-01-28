using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class FinanziamentiProfessionistiViewModel
    {
        public int ID_Finanziamento { get; set; }

        [Required(ErrorMessage = "Il professionista è obbligatorio.")]
        public int? ID_Professionista { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 1000000, ErrorMessage = "L'importo deve essere maggiore di zero.")]
        public decimal Importo { get; set; }

        [Required(ErrorMessage = "La data di versamento è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime? DataVersamento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        // 👤 Nome visualizzato (opzionale)
        public string NomeProfessionista { get; set; }

        // variabili incasso 
        public string TipoPlafond { get; set; } // "Finanziamento" o "Incasso"
        public int? ID_Plafond { get; set; }    // Solo se è un incasso
        public int? ID_CostoPersonale { get; set; } // costo personale utente


        public decimal FinanziamentiTotale { get; set; }
        public decimal IncassoTotale { get; set; }
        public decimal PlafondTotale => FinanziamentiTotale + IncassoTotale;


        public DateTime? DataInizio { get; set; } // da PlafondUtente
        public DateTime? DataFine { get; set; }

        public int? ID_Pratiche { get; set; }
        public int? ID_Incasso { get; set; }
        public int? ID_AvvisoParcella { get; set; }

        public string Riferimento { get; set; }   // es: "Pratica #123 – Avviso #45"
        public string OrigineMovimento { get; set; }
        // es: "Incasso", "Owner Fee", "Trattenuta Plafond", "Pagamento Costo"

        public string NomePratica { get; set; }
        public string NumeroAvviso { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }
    }
}