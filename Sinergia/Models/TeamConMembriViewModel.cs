using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sinergia.Models
{
    public class TeamConMembriViewModel
    {
        public int ID_Team { get; set; }
        public string Nome { get; set; }
        public string Descrizione { get; set; }
        public List<MembroTeamViewModel> Membri { get; set; }
    }
    public class MembroTeamViewModel
    {
        public int ID_Utente { get; set; }
        public string NomeCompleto { get; set; }
    }
}