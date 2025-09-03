using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Sinergia.Models
{
    public class PraticaViewModel
    {
        // 📌 Identificativi principali
        public int ID_Pratiche { get; set; }

        // 📋 Dati generali
        [Required(ErrorMessage = "Il campo Titolo è obbligatorio.")]
        public string Titolo { get; set; }

        public string Descrizione { get; set; }

        public bool HaIncaricoGenerato { get; set; }

        public int? ID_DocumentoIncarico { get; set; }

        public DateTime? DataInizioAttivitaStimata { get; set; }
        public DateTime? DataFineAttivitaStimata { get; set; }

        [Required(ErrorMessage = "Il campo Stato è obbligatorio.")]
        public string Stato { get; set; } // Contrattualizzazione, In lavorazione, ecc.

        // ⏳ Tipologia e dettagli
        [Required(ErrorMessage = "Il campo Tipologia è obbligatorio.")]
        public string Tipologia { get; set; } // "Fisso", "A tempo", "Giudiziale"

        public decimal? ImportoFisso { get; set; } // Se "Fisso"
        public string TerminiPagamento { get; set; } // Se "Fisso" o "Giudiziale"
        public decimal? TariffaOraria { get; set; } // Se "A tempo"
        public string GradoGiudizio { get; set; } // Se "Giudiziale"
        public decimal? AccontoGiudiziale { get; set; } // Se "Giudiziale"
        public decimal? OrePreviste { get; set; }
        public decimal? OreEffettive { get; set; }


        [Required]
        public int ID_Cliente { get; set; }
        public string NomeCliente { get; set; }
        public string TipoCliente { get; set; } // Azienda o Professionista

        [Required]
        public int ID_UtenteResponsabile { get; set; }
        public string NomeUtenteResponsabile { get; set; }

        public int? ID_Professione { get; set; }


        public int? ID_UtenteUltimaModifica { get; set; }
        public DateTime? DataCreazione { get; set; }
        public DateTime? UltimaModifica { get; set; }

        public decimal Budget { get; set; }
        public string Note { get; set; }

        public string PercCollaborazione { get; set; }

        public int? ID_Pratica_Originale { get; set; }

        // 📎 Upload incarico
        [Required(ErrorMessage = "È obbligatorio caricare il file di incarico professionale.")]
        public HttpPostedFileBase IncaricoProfessionale { get; set; }

        // 🔷 Cluster
        public string TipoCluster { get; set; }
        public decimal PercentualePrevisione { get; set; }

        // 📆 Evento iniziale
        public string StatoEvento { get; set; }

        // 👥 Collaboratori associati (solo ID)
        public List<int> ID_CollaboratoriAssociati { get; set; } = new List<int>();

        // 👥 Collaboratori associati (con ruoli, per relazioni)
        public List<UtenteAssociatoViewModel> UtentiAssociati { get; set; } = new List<UtenteAssociatoViewModel>();

        public class UtenteAssociatoViewModel
        {
            public int ID_Utente { get; set; }
            public string RuoloNellaPratica { get; set; } // "Collaboratore" o altro
            public string TipoCluster { get; set; } = "Collaboratore";
            public decimal PercentualePrevisione { get; set; }
        }

        // 💼 Compensi e Rimborsi e Costi
        public List<CompensoViewModel> Compensi { get; set; } = new List<CompensoViewModel>();
        public List<RimborsoViewModel> Rimborsi { get; set; } = new List<RimborsoViewModel>();
        public List<CostoPraticaViewModel> CostiPratica { get; set; } = new List<CostoPraticaViewModel>();


        public class CompensoViewModel
        {
            public string Descrizione { get; set; }
            public decimal Importo { get; set; }
            public string Tipo { get; set; } // "Fisso", "Orario", "Giudiziale"
        }

        public class RimborsoViewModel
        {
            public string Descrizione { get; set; }
            public decimal Importo { get; set; }
        }

        public class CostoPraticaViewModel
        {
            public string Categoria { get; set; } // Es: "Viaggio", "Albergo"
            public string Descrizione { get; set; }
            public decimal Importo { get; set; }
            public int? ID_ClienteAssociato { get; set; }
            public string NomeFornitoreManuale { get; set; }

            public int IDCostoHidden { get; set; }

            public int ID_AnagraficaCosto { get; set; }  // ✅ nuovo campo per salvataggio
        }


        // 🔔 Notifiche
        public bool InviaNotificheAutomatiche { get; set; } = true;
        public string MessaggioNotificaPersonalizzato { get; set; }

        public decimal ImportoIncassato { get; set; }

        public decimal Utile { get; set; }

        public List<CollaboratorePraticaViewModel> Collaboratori { get; set; } = new List<CollaboratorePraticaViewModel>();

        public string NomeOwner { get; set; }
        // trattenuta personalizzata lo fa solo admin 
        public decimal? TrattenutaPersonalizzata { get; set; }

    }
}
