using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class MenuViewModel
    {
        public int ID_Menu { get; set; }
        public string NomeMenu { get; set; }
        public string DescrizioneMenu { get; set; }
        public string Percorso { get; set; }
        public string Controller { get; set; }
        public string Azione { get; set; }
        public string CategoriaMenu { get; set; }
        public string CategoriaMenu2 { get; set; }
        public string Icona { get; set; }
        public string RuoloPredefinito { get; set; }
        public string VoceSingola { get; set; }
        public int Ordine { get; set; }
        public string ÈValido { get; set; }
        public string MostraNelMenu { get; set; }
        public int? ID_Azienda { get; set; }
        public string AccessoRiservato { get; set; }
        public string PermessoLettura { get; set; }
        public string PermessoAggiunta { get; set; }
        public string PermessoModifica { get; set; }
        public string PermessoEliminazione { get; set; }
        public DateTime? DataCreazione { get; set; }
        public DateTime? UltimaModifica { get; set; }
        public int? ID_UtenteCreatore { get; set; }
        public int? ID_UtenteUltimaModifica { get; set; }
    }
}