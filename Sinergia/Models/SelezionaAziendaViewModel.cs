using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;


namespace Sinergia.Models
{
    public class SelezionaAziendaViewModel
    {
        public int ID_AziendaSelezionata { get; set; }
        public List<SelectListItem> AziendeDisponibili { get; set; }
    }
}