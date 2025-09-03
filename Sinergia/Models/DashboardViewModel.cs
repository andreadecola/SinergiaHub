using System;
using System.Collections.Generic;
using System.Web.Mvc;

namespace Sinergia.Models
{
    public class DashboardViewModel
    {
        // Intestazioni
        public string NomeCliente { get; set; }
        public string NomeUtente { get; set; }
        public int IntervalloGiorni { get; set; }

        // Clienti selezionabili (solo per admin e collaboratori)
        public List<SelectListItem> ClientiDisponibili { get; set; }
        public int? ID_ClienteSelezionato { get; set; }

        // Dati principali
        public List<PraticaViewModel> Pratiche { get; set; }
        public List<DocumentiPraticaViewModel> DocumentiRecenti { get; set; }

        public List<NotificaViewModel> Notifiche { get; set; }

        public List<AvvisoParcellaViewModel> AvvisiParcella { get; set; }
        public List<OperazioniPrevisionaliViewModel> OperazioniPrevisionali { get; set; }
        public List<OperazioniEconomicheViewModel> OperazioniEconomiche { get; set; }
        public List<OperaziomoFinanziarieViewModel> OperazioniFinanziarie { get; set; }

        // Solo per professionista
        public List<UtenteViewModel> CollaboratoriAssegnati { get; set; }
        public decimal? UtilePersonale { get; set; }

        // Solo per collaboratore
        public List<ProfessionistiViewModel> ProfessionistiSeguiti { get; set; }
    }
}
