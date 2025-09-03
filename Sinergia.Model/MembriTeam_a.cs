namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class MembriTeam_a
    {
        [Key]
        public int ID_VersioneMembroTeam { get; set; }

        public int ID_MembroTeam { get; set; }

        public int ID_Team { get; set; }

        public int ID_Professionista { get; set; }

        public decimal PercentualeCondivisione { get; set; }

        public bool Attivo { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
