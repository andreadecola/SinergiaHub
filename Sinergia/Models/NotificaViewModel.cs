using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class NotificaViewModel
    {
        public int ID_Notifica { get; set; }

        [Required]
        [StringLength(255)]
        public string Titolo { get; set; }

        public string Descrizione { get; set; }

        public DateTime DataCreazione { get; set; } = DateTime.Now;

        public DateTime? DataLettura { get; set; }

        [Required]
        public int ID_Utente { get; set; }

        [StringLength(50)]
        public string Tipo { get; set; }

        [StringLength(20)]
        public string Stato { get; set; } = "Non letta";

        public int Contatore { get; set; } = 1;

        // 🔄 Per visualizzazione nome utente o dettagli (opzionale)
        public string NomeUtente { get; set; }

        // 🔄 Per filtrare o raggruppare (es. per dashboard)
        public bool Letta => DataLettura.HasValue;

        public bool Letto { get; set; }

        // ✅ Link per reindirizzare alla pratica o entità correlata
        public string LinkPratica { get; set; }
    }
}