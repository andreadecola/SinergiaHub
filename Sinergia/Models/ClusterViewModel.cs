using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class ClusterViewModel
    {
        public int ID_Pratiche { get; set; }
        public int ID_Utente { get; set; } // ← da usare come ID univoco assieme a ID_Pratiche
        public string TipoCluster { get; set; }
        public decimal PercentualePrevisione { get; set; }
        public DateTime? DataAssegnazione { get; set; }
        public string NomeUtente { get; set; }

        // Per la tabella: proprietà calcolata per visualizzare ID combinato (facoltativo)
        public string IDVisualizzato => $"{ID_Pratiche}-{ID_Utente}";

        // 💰 Importo calcolato
        public decimal ImportoCalcolato { get; set; }

    }
}