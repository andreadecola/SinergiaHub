using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class VisualizzaDettaglioPraticaViewModel
    {
        public PraticaViewModel Pratica { get; set; }
        public List<ClusterViewModel> Cluster { get; set; }
        public List<EventoViewModel> Eventi { get; set; }
        public List<UtenteViewModel> Utenti { get; set; }
        public List<AvvisoParcellaViewModel> AvvisiParcella { get; set; }

        public decimal ImportoFinale { get; set; }
        public decimal TotaleCompensi { get; set; }
        public decimal TotaleRimborsi { get; set; }
        public decimal TotaleCosti { get; set; }
        public decimal Utile { get; set; }
        public decimal ImportoIncassato { get; set; }

        // 👥 Nuovo campo per i collaboratori provenienti da CompensiPraticaDettaglio
        public List<CollaboratoreDettaglioViewModel> CollaboratoriDettaglio { get; set; } = new List<CollaboratoreDettaglioViewModel>();
    }

    public class CollaboratoreDettaglioViewModel
    {
        public string Nome { get; set; }
        public decimal Percentuale { get; set; }
        public decimal Importo { get; set; }

        public string NomeCompenso { get; set; }
    }

}