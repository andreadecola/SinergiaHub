namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class PermessiDelegabiliPerProfessionista_a
    {
        [Key]
        public int ID_PermessiDelegabiliPerProfessionista_a { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Menu { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
