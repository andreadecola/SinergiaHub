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

        public string TitoloAvviso { get; set; }

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

        // ✅ Nuovi campi DB
        public int? ID_ResponsabilePratica { get; set; }
        public int? ID_OwnerCliente { get; set; }

        // 🔍 Campi ausiliari (non mappati su DB)
        public string NomePratica { get; set; }
        public string NomeUtenteCreatore { get; set; }

        public string NomeResponsabilePratica { get; set; }
        public string NomeOwnerCliente { get; set; }

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

        // ⚙️ Nuovo campo ausiliario (non mappato su DB)
        public string NomeCompensoOrigine { get; set; }

        // ⚙️ Nuovi campi  per gestione completa avvisi
        [StringLength(50)]
        public string TipologiaAvviso { get; set; } // Fisso, A Ore, Giudiziale

        [StringLength(255)]
        public string FaseGiudiziale { get; set; } // Esempio: "Fase introduttiva", "Discussione", ecc.

        public decimal? RimborsoSpesePercentuale { get; set; } // 0 o 15
        public decimal? ImportoRimborsoSpese { get; set; } // Calcolato

        public int? ID_CompensoOrigine { get; set; } // Collega al dettaglio compenso (solo giudiziale)

        // 📋 Campo derivato per UI (es. "Giudiziale - Fase introduttiva")
        public string DescrizioneAvviso { get; set; }

        // ⚡️ Nuovo campo: acconto parziale sulla parcella
        [Display(Name = "Importo Acconto")]
        [Range(0, 999999.99, ErrorMessage = "Inserire un importo valido.")]
        public decimal? ImportoAcconto { get; set; }

        // 🗓️ Nuovi campi per gestione invio e competenza
        [Display(Name = "Data Invio")]
        public DateTime? DataInvio { get; set; }

        [Display(Name = "Competenza Economica")]
        public DateTime? DataCompetenzaEconomica { get; set; }

        // 🔗 Stato incasso associato (derivato da Incassi)
        [Display(Name = "Stato Incasso")]
        public string StatoIncasso { get; set; }

        public string TrimestreCompetenza { get; set; }

        // 💰 Dati economici estesi per esportazioni e PDF
        public decimal? TotaleIncassato { get; set; }
        public decimal? ImportoResiduoEffettivo { get; set; }

        // serve per capire se ha template caricato del avviso parcella o e da caricare 
        public bool HaDocumentoCaricato { get; set; }
 

    }
}
