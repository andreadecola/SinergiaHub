namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class DistribuzioneCostiTeam_a
    {
        [Key]
        public int ID_DistribuzioneArchivio { get; set; }

        public int ID_Distribuzione { get; set; }

        public int ID_AnagraficaCostoTeam { get; set; }

        public int ID_Team { get; set; }

        public decimal Percentuale { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        [Column(TypeName = "text")]
        public string ModificheTestuali { get; set; }
    }
}
