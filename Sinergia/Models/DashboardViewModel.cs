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

        public int? ID_UtenteImpers { get; set; }


        // Dati principali
        public List<PraticaViewModel> Pratiche { get; set; }
        public List<DocumentiPraticaViewModel> DocumentiRecenti { get; set; }
        public List<NotificaViewModel> Notifiche { get; set; }
        public List<AvvisoParcellaViewModel> AvvisiParcella { get; set; }
        public List<OperazioniPrevisionaliViewModel> OperazioniPrevisionali { get; set; }
        public List<OperazioniEconomicheViewModel> OperazioniEconomiche { get; set; }
        public List<OperaziomoFinanziarieViewModel> OperazioniFinanziarie { get; set; }

        public List<string> MesiGrafico { get; set; }
        public List<decimal> AndamentoIncassi { get; set; }
        public List<decimal> AndamentoCosti { get; set; }


        // ✅ KPI Cruscotto
        public int PraticheAttive { get; set; } // In contrattualizzazione + In lavorazione
        public int AvvisiEmessi { get; set; }   // Totale avvisi nel periodo
        public decimal IncassiTotali { get; set; }
        public decimal CostiTotali { get; set; }
        public decimal UtileNetto => IncassiTotali - CostiTotali;


        // 🧑‍💼 KPI PROFESSIONISTA (NUOVI)
        public decimal FatturatoLordo { get; set; }          // Somma avvisi parcella emessi
        public decimal UtileIncassato { get; set; }          // Incassi - Costi pagati
        public decimal DisponibilitaFinanziaria { get; set; } // (Incassi + Finanziamenti) - (Costi personali + Costi pagati)
        public decimal CreditoFatturabile { get; set; }      // Incassi - Costi totali - Trattenute
        public decimal TrattenuteSinergia { get; set; }      // Trattenute nel periodo
        public decimal FatturatoNetto { get; set; }          // Incassi - Costi totali - Trattenute


        // ==========================================================
        // 🏢 KPI AZIENDALI SINERGIA (visibili solo per Admin)
        // ==========================================================
        public decimal EntrateTotaliSinergia { get; set; }      // Somma di tutti i costi pagati (Generale, Team, Prof, Pratica)
        public decimal UsciteTotaliSinergia { get; set; }       // Somma dei costi previsionali / non pagati
        public decimal UtileAziendale { get; set; }             // Differenza tra entrate e uscite
        public decimal TrattenuteSinergiaTotali { get; set; }   // Somma delle trattenute Sinergia (da BilancioProfessionista, se disponibili)
        public decimal CreditoTotaleProfessionisti { get; set; } // Somma crediti fatturabili di tutti i professionisti
        public int AvvisiTotaliSinergia { get; set; }
        public decimal FatturatoTotaleGenerato { get; set; }
        public decimal DisponibilitaSinergia { get; set; }       // Entrate - Uscite


        // 📈 Serie temporale per grafico andamento utile mensile
        public List<string> MesiUtile { get; set; }             // Etichette mesi (es. ["Mag 2025", "Giu 2025", "Lug 2025", ...])
        public List<decimal> UtileMensile { get; set; }         // Valori utile per mese

        public bool IsAdmin { get; set; }



        // 📅 Filtro temporale
        public int AnnoCorrente { get; set; }
        public int TrimestreCorrente { get; set; }

        // 📊 Filtri periodo (nuovi)
        public string FiltroTrimestre { get; set; }
        public string SottoFiltro { get; set; }


        // Info accessorie
        public List<UtenteViewModel> CollaboratoriAssegnati { get; set; }
        public decimal? UtilePersonale { get; set; }
        public List<ProfessionistiViewModel> ProfessionistiSeguiti { get; set; }


    }
}
