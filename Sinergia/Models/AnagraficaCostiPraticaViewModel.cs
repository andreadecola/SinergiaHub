using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class AnagraficaCostiPraticaViewModel
    {
        public int ID_AnagraficaCosto { get; set; }

        [Required(ErrorMessage = "Il nome è obbligatorio.")]
        [StringLength(100)]
        public string Nome { get; set; }

        public string Descrizione { get; set; }

        public bool Attivo { get; set; }

        [Display(Name = "Data di Creazione")]
        [DataType(DataType.Date)]
        public DateTime DataCreazione { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public string TipoCreatore { get; set; }

        // ✅ Nuovi campi per gestione modifiche
        [Display(Name = "Stato")]
        [Required]
        public string Stato { get; set; }  // "Attivo", "Disattivato", "Eliminato"

        public int? ID_UtenteUltimaModifica { get; set; }

        [Display(Name = "Ultima Modifica")]
        [DataType(DataType.Date)]
        public DateTime? DataUltimaModifica { get; set; }

        // 🧠 Dati aggiuntivi per la vista
        public string NomeCreatore { get; set; }

        public string NomeUltimaModifica { get; set; }

        public string ID_CodiceCosto => $"CPROG-{ID_AnagraficaCosto}";

        // =====================================================
        // 🏷️ NUOVI CAMPI — Gestione categorie costi
        // =====================================================

        [Display(Name = "Categoria")]
        [Required(ErrorMessage = "La categoria è obbligatoria.")]
        public int? ID_Categoria { get; set; }   // FK -> CategorieCosti.ID_Categoria

        [Display(Name = "Nome Categoria")]
        public string NomeCategoria { get; set; } // Popolato in lista o in GetCostoProgetto

    }
}