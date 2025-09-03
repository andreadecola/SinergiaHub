using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class CostoTeamViewModel
    {
        public int ID_AnagraficaCostoTeam { get; set; }
        public string Descrizione { get; set; }
        public string TipoValore { get; set; }
        public string Frequenza { get; set; }
        public string Periodicita { get; set; }

        public bool Ricorrente { get; set; }
        public decimal Importo { get; set; }
        public int? ID_Professione { get; set; }
        public DateTime? DataInizio { get; set; }
        public DateTime? DataFine { get; set; }
        public int ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }
        public DateTime? DataUltimaModifica { get; set; }
        public int NumeroDistribuzioni { get; set; }

        public string Stato { get; set; }

        public string NomeCreatore { get; set; }

        public List<TeamAssegnatoViewModel> TeamAssegnati { get; set; }
        public List<int> ID_TeamSelezionati { get; set; }

        public bool RicorrenzaAttiva { get; set; }
        public DateTime? DataInizioRicorrenza { get; set; }
        public DateTime? DataFineRicorrenza { get; set; }

        public string StatoRicorrenza
        {
            get
            {
                if (RicorrenzaAttiva) return "Attiva";
                else if (DataInizioRicorrenza != null) return "Scaduta";
                else return "Nessuna";
            }
        }


        public string CodiceIdentificativo
        {
            get
            {
                return $"CT-{ID_AnagraficaCostoTeam}";
            }
        }

    }

 


    public class TeamAssegnatoViewModel
    {
        public int ID_Distribuzione { get; set; }
        public int ID_Team { get; set; }
        public decimal Percentuale { get; set; }
        public string NomeTeam { get; set; }
    }

}