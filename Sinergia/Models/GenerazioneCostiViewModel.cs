using System;
using System.Collections.Generic;

namespace Sinergia.Models
{
    public class GenerazioneCostiViewModel
    {
        public int ID_GenerazioneCosto { get; set; }

        public int? ID_Utente { get; set; }
        public string NomeProfessionista { get; set; }

        public int? ID_Team { get; set; }
        public string NomeTeam { get; set; }

        public string Categoria { get; set; } // Es. Costo Generale, Costo Team, Costo Professionista
        public int? ID_Riferimento { get; set; } // ID del costo origine

        public string Descrizione { get; set; }
        public decimal? Importo { get; set; }
        public DateTime? DataRegistrazione { get; set; }
        public string Periodicita { get; set; }
        public string Origine { get; set; } // Da quale tabella viene
        public bool? Approvato { get; set; }

        public int? ID_UtenteCreatore { get; set; }
        public string NomeCreatore { get; set; }
        public string NomeModificatore { get; set; }

        public string TitoloPratica { get; set; }
        public int? ID_Pratiche { get; set; }

        public int? ID_RicorrenzaCosto { get; set; }

        public DateTime? DataCreazione { get; set; }
        public DateTime? DataUltimaModifica { get; set; }

        public bool Selezionato { get; set; }

        public string TipoCosto { get; set; }             // "Generale", "Team", "Personale"
        public DateTime DataCompetenza { get; set; }

        public int? ID_Professionista { get; set; }

        public string Stato { get; set; }                 // Es: "Da generare", "Generato", "Pagato"
        public string Utente_Professionista { get; set; } // Alias per NomeProfessionista
        public string Team { get; set; }                  // Alias per NomeTeam


        // Proprietà per indicare se la voce è un'eccezione (settable)
        public bool? HaEccezione { get; set; } // ← deve essere nullable!
        public int? ID_Eccezione { get; set; }

        public string ID_CodiceCosto
        {
            get
            {
                if (Categoria == "Costo Generale" && ID_Riferimento.HasValue)
                    return $"CGEN-{ID_Riferimento}";
                else if (Categoria == "Costo Professionista" && ID_Riferimento.HasValue)
                    return $"CPROF-{ID_Riferimento}";
                else if (Categoria == "Costo Team" && ID_Riferimento.HasValue)
                    return $"CT-{ID_Riferimento}";
                else if (Categoria == "Costo Progetto" && ID_Riferimento.HasValue)
                    return $"CPROG-{ID_Riferimento}";
                else
                    return "-";
            }
        }


    }
}
