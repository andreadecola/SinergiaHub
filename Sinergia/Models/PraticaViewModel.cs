using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Sinergia.Models
{
    public class PraticaViewModel
    {
        // ================================
        // 📌 Identificativi principali
        // ================================
        public int ID_Pratiche { get; set; }
        public int? ID_Pratica_Originale { get; set; }

        // ================================
        // 📋 Dati generali
        // ================================
        [Required(ErrorMessage = "Il campo Titolo è obbligatorio.")]
        public string Titolo { get; set; }

        public string Descrizione { get; set; }
        public string Note { get; set; }
        public decimal Budget { get; set; }
        public string BudgetFormattato { get; set; }

        public bool HaIncaricoGenerato { get; set; }
        public int? ID_DocumentoIncarico { get; set; }

        public DateTime? DataInizioAttivitaStimata { get; set; }
        public DateTime? DataFineAttivitaStimata { get; set; }

        [Required(ErrorMessage = "Il campo Stato è obbligatorio.")]
        public string Stato { get; set; } // Contrattualizzazione, In lavorazione, ecc.

        public string StatoEvento { get; set; } // Evento iniziale

        // ================================
        // ⏳ Tipologia e dettagli compenso
        // ================================
        [Required(ErrorMessage = "Il campo Tipologia è obbligatorio.")]
        public string Tipologia { get; set; } // "Fisso", "A tempo", "Giudiziale"

        public decimal? ImportoFisso { get; set; }         // Se "Fisso"
        public string TerminiPagamento { get; set; }       // Se "Fisso" o "Giudiziale"
        public decimal? TariffaOraria { get; set; }        // Se "A ore"
        public decimal? OrePreviste { get; set; }
        public decimal? OreEffettive { get; set; }
        public string GradoGiudizio { get; set; }          // Se "Giudiziale"
        public decimal? AccontoGiudiziale { get; set; }    // Se "Giudiziale"

        // ================================
        // 👤 Cliente e riferimenti
        // ================================
        [Required]
        public int ID_Cliente { get; set; }
        public string NomeCliente { get; set; }
        public string TipoCliente { get; set; } // Azienda o Professionista

        public string ClienteRagioneSociale { get; set; }
        public string ClienteNomeCompleto { get; set; }


        // ================================
        // 👤 Owner e Responsabile
        // ================================
        [Required]
        public int ID_UtenteResponsabile { get; set; }
        public string NomeUtenteResponsabile { get; set; }

        public int? ID_Owner { get; set; } // Professionista proprietario
        public string NomeOwner { get; set; }

        public int? ID_Professione { get; set; }

        // ================================
        // 📆 Metadati creazione/modifica
        // ================================
        public int? ID_UtenteUltimaModifica { get; set; }
        public DateTime? DataCreazione { get; set; }
        public DateTime? UltimaModifica { get; set; }

        // ================================
        // 📎 Incarico
        // ================================
        [Required(ErrorMessage = "È obbligatorio caricare il file di incarico professionale.")]
        public HttpPostedFileBase IncaricoProfessionale { get; set; }

        // ================================
        // 👥 Collaboratori / Cluster
        // ================================
        public string TipoCluster { get; set; }
        public decimal PercentualePrevisione { get; set; }
        public string PercCollaborazione { get; set; }

        public List<int> ID_CollaboratoriAssociati { get; set; } = new List<int>();
        public List<UtenteAssociatoViewModel> UtentiAssociati { get; set; } = new List<UtenteAssociatoViewModel>();
        public List<CollaboratorePraticaViewModel> Collaboratori { get; set; } = new List<CollaboratorePraticaViewModel>();
        public List<CompensoPraticaDettaglioViewModel> CompensiDettaglio { get; set; }

        public class UtenteAssociatoViewModel
        {
            public int ID_Utente { get; set; }
            public string RuoloNellaPratica { get; set; } // "Collaboratore" o altro
            public string TipoCluster { get; set; } = "Collaboratore";
            public decimal PercentualePrevisione { get; set; }
        }

        // ================================
        // 💼 Compensi / Rimborsi / Costi
        // ================================
        public List<CompensoViewModel> Compensi { get; set; } = new List<CompensoViewModel>();
        public List<RimborsoViewModel> Rimborsi { get; set; } = new List<RimborsoViewModel>();
        public List<CostoPraticaViewModel> CostiPratica { get; set; } = new List<CostoPraticaViewModel>();

        public string CompensiJSON { get; set; } // JSON che arriva dal form
        public string MetodoCompenso { get; set; }


        public class CompensoViewModel
        {
            public string Metodo { get; set; }          // Fisso, A ore, Giudiziale
            public string Descrizione { get; set; }
            public decimal? Importo { get; set; }
            public string Tipologia { get; set; }       // Es. contrattuale, variabile
            public string Tipo { get; set; }            // 🔹 aggiunto qui
            public decimal? ValoreStimato { get; set; }
            public string EstremiGiudizio { get; set; }
            public string OggettoIncarico { get; set; }
            public int? ID_ProfessionistaIntestatario { get; set; }
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
            public int ID_AnagraficaCosto { get; set; } // ✅ nuovo campo per salvataggio
        }

        // ================================
        // 🔔 Notifiche
        // ================================
        public bool InviaNotificheAutomatiche { get; set; } = true;
        public string MessaggioNotificaPersonalizzato { get; set; }

        // ================================
        // 📊 Dati economici
        // ================================
        public decimal ImportoIncassato { get; set; }
        public decimal Utile { get; set; }
        public decimal? TrattenutaPersonalizzata { get; set; } // solo admin
    }
}
