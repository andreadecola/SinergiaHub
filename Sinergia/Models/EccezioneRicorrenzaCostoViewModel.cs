using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class EccezioneRicorrenzaCostoViewModel
    {
        public int ID_Eccezione { get; set; }

        public int? ID_Professionista { get; set; }
        public string NomeProfessionista { get; set; }

        public int? ID_Team { get; set; }
        public string NomeTeam { get; set; }

        public DateTime DataInizio { get; set; }
        public DateTime DataFine { get; set; }

        public string Motivazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }
        public string NomeCreatore { get; set; }

        public string Categoria { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_TipologiaCosto { get; set; }
        public string NomeTipologiaCosto { get; set; } // viene da join su TipologieCosti.Descrizione

        public int? ID_RicorrenzaCosto { get; set; }

        public bool SaltaCosto { get; set; }           // arriverà come true/false dal form
        public decimal? NuovoImporto { get; set; }


    }
}