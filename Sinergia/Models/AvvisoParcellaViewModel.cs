using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class AvvisoParcellaViewModel
    {
        public int ID_AvvisoParcelle { get; set; }

        [Required(ErrorMessage = "La pratica è obbligatoria.")]
        public int ID_Pratiche { get; set; }

        [Required(ErrorMessage = "La data dell'avviso è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime? DataAvviso { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.01, 999999.99, ErrorMessage = "Inserire un importo valido.")]
        public decimal? Importo { get; set; }

        [StringLength(500, ErrorMessage = "Le note non possono superare i 500 caratteri.")]
        public string Note { get; set; }

        [StringLength(50, ErrorMessage = "Lo stato non può superare i 50 caratteri.")]
        public string Stato { get; set; }  // Es: "Inviato", "Pagato", "In attesa"

        [StringLength(50, ErrorMessage = "Il metodo di pagamento non può superare i 50 caratteri.")]
        public string MetodoPagamento { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        // 🔍 Campi ausiliari (non mappati su DB)
        public string NomePratica { get; set; }
        public string NomeUtenteCreatore { get; set; }

        public bool PuoEliminare { get; set; } // utile per mostrare pulsante cancella

        public bool PuoModificare { get; set; } 

        public decimal? ContributoIntegrativoPercentuale { get; set; }
        public decimal? ContributoIntegrativoImporto { get; set; }

        // 🔍 Nuovi campi per IVA
        [Display(Name = "Aliquota IVA")]
        public decimal? AliquotaIVA { get; set; }

        public decimal? ImportoIVA { get; set; }

        public decimal? TotaleAvvisoParcella { get; set; }



        // ➕ Campi ausiliari per descrizione e calcolo compenso
        public string TipologiaPratica { get; set; }
        public decimal? TariffaOraria { get; set; }
        public decimal? OreEffettive { get; set; }
        public string DescrizioneCompenso { get; set; }

    }
}