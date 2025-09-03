namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DistribuzioneCostiTeam")]
    public partial class DistribuzioneCostiTeam
    {
        [Key]
        public int ID_Distribuzione { get; set; }

        public int ID_AnagraficaCostoTeam { get; set; }

        public int ID_Team { get; set; }

        public decimal Percentuale { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public DateTime DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}
