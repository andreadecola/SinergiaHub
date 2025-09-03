using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace Sinergia.Models
{
    public class CostoProfessionistaCompletoViewModel
    {
        
    // 📌 Sezione Anagrafica Costo
    public int ID_AnagraficaCostoProfessionista { get; set; }

        [Required]
        [StringLength(255)]
        public string Descrizione { get; set; }

        [StringLength(50)]
        public string ModalitaRipartizione { get; set; } // Fisso / Variabile

        [StringLength(50)]
        public string TipoPeriodicita { get; set; } // Mensile / Una Tantum

        public decimal? ImportoBase { get; set; }

        public bool Attivo { get; set; }

        // ✅ Metadati creazione/modifica
        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public string NomeCreatore { get; set; }

        public int NumeroAssegnati { get; set; }

        public string ID_CodiceCosto => $"CPROF-{ID_AnagraficaCostoProfessionista}";

        // 📌 Stato ricorrenza associata
        public bool? RicorrenzaAttiva { get; set; }

        public string StatoRicorrenza => RicorrenzaAttiva == true ? "Attiva" : "Nessuna";


        // 📌 Lista professionisti selezionabili
        [Required(ErrorMessage = "Seleziona almeno un professionista.")]
        public List<int> ID_UtentiSelezionati { get; set; } = new List<int>();

        public List<SelectListItem> ListaProfessionisti { get; set; } = new List<SelectListItem>();

        // 📌 Sezione Costi Personali Assegnati
        public List<CostiPersonaliUtenteViewModel> CostiAssegnati { get; set; } = new List<CostiPersonaliUtenteViewModel>();
    }
}
