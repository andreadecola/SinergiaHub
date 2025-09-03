namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("MembriTeam")]
    public partial class MembriTeam
    {
        [Key]
        public int ID_MembroTeam { get; set; }

        public int ID_Team { get; set; }

        public int ID_Professionista { get; set; }

        public decimal PercentualeCondivisione { get; set; }

        public bool Attivo { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}
