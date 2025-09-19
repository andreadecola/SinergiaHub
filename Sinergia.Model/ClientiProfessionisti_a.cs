namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class ClientiProfessionisti_a
    {
        [Key]
        public int ID_ClientiProfessionisti_a { get; set; }

        public int ID_Originale { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Professionista { get; set; }

        [StringLength(50)]
        public string Ruolo { get; set; }

        public DateTime DataAssegnazione { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
