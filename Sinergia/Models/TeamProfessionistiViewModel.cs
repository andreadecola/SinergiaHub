using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class TeamProfessionistiViewModel
    {
        public int ID_Team { get; set; }

        [Required(ErrorMessage = "Il nome del team è obbligatorio")]
        [StringLength(100)]
        [Display(Name = "Nome Team")]
        public string Nome { get; set; }

        [StringLength(255)]
        [Display(Name = "Descrizione")]
        public string Descrizione { get; set; }

        [Display(Name = "Attivo")]
        public bool Attivo { get; set; }

        public int ID_UtenteCreatore { get; set; }

        [Display(Name = "Data creazione")]
        [DataType(DataType.Date)]
        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        [Display(Name = "Ultima modifica")]
        [DataType(DataType.Date)]
        public DateTime? DataUltimaModifica { get; set; }

        // 📌 Lista dei professionisti assegnati al team
        [Display(Name = "Professionisti assegnati")]
        public List<ProfessionistaAssegnatoViewModel> ProfessionistiAssegnati { get; set; } = new List<ProfessionistaAssegnatoViewModel>();

        public class ProfessionistaAssegnatoViewModel
        {
            public int ID_Utente { get; set; }

            [Display(Name = "Nome professionista")]
            public string NomeCompleto { get; set; }

            [Display(Name = "Email")]
            public string Email { get; set; }

            [Display(Name = "Data assegnazione")]
            [DataType(DataType.Date)]
            public DateTime? DataAssegnazione { get; set; }

            // ✅ Altri campi utili per vista o gestione
            public bool Attivo { get; set; }
        }
    }
}