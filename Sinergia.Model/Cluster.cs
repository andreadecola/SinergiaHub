namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Cluster")]
    public partial class Cluster
    {
        [Key]
        public int ID_Cluster { get; set; }

        public int ID_Pratiche { get; set; }

        public int ID_Utente { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoCluster { get; set; }

        public decimal PercentualePrevisione { get; set; }

        public DateTime? DataAssegnazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? UltimaModifica { get; set; }
    }
}
