using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class KnowledgeAziendaleViewModel
    {
        public int KnowledgeAziendaleID { get; set; }

        [Required(ErrorMessage = "Il tipo è obbligatorio.")]
        [StringLength(100)]
        public string Tipo { get; set; }

        [Required(ErrorMessage = "Il titolo è obbligatorio.")]
        [StringLength(200)]
        public string Titolo { get; set; }

        [Required(ErrorMessage = "La data documento è obbligatoria.")]
        [DataType(DataType.Date)]
        public DateTime DataDocumento { get; set; }

        [DataType(DataType.MultilineText)]
        public string Descrizione { get; set; }

        [Required(ErrorMessage = "Il destinatario è obbligatorio.")]
        [StringLength(50)]
        public string Destinatario { get; set; }

        // 📎 Per allegati multipli in creazione/modifica
        public List<HttpPostedFileBase> Allegati { get; set; } = new List<HttpPostedFileBase>();

        // 📂 Lista file già salvati
        public List<KnowledgeAziendaleAllegatoViewModel> AllegatiEsistenti { get; set; } = new List<KnowledgeAziendaleAllegatoViewModel>();

        public int Versione { get; set; } = 1;

        public bool Vecchio { get; set; } = false;

        public string CreatoDa { get; set; }
        public DateTime DataCreazione { get; set; }

        public string ModificatoDa { get; set; }
        public DateTime? DataModifica { get; set; }

        public string AllegatoNome { get; set; }
        public string AllegatoPercorso { get; set; }

        public bool PuoModificare { get; set; }
        public bool PuoEliminare { get; set; }

        // 📌 Campi aggiunti per gestione lettura
        public bool Letto { get; set; }
        public DateTime? DataLettura { get; set; }
    }

    // 📎 Modello per allegato già presente
    public class KnowledgeAziendaleAllegatoViewModel
    {
        public string NomeFile { get; set; }
        public string PercorsoFile { get; set; }
    }
}
