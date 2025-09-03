namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Cluster_a
    {
        [Key]
        public int ID_Cluster_a { get; set; }

        public int? ID_Cluster_Originale { get; set; }

        public int ID_Pratiche { get; set; }

        public int ID_Utente { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoCluster { get; set; }

        public decimal PercentualePrevisione { get; set; }

        public DateTime? DataAssegnazione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
