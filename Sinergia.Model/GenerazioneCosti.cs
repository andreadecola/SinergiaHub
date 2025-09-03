namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("GenerazioneCosti")]
    public partial class GenerazioneCosti
    {
        [Key]
        public int ID_GenerazioneCosto { get; set; }

        public int? ID_Utente { get; set; }

        public int? ID_Team { get; set; }

        [StringLength(50)]
        public string Categoria { get; set; }

        public int? ID_Riferimento { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataRegistrazione { get; set; }

        [StringLength(50)]
        public string Periodicita { get; set; }

        [StringLength(50)]
        public string Origine { get; set; }

        public bool? Approvato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }

        public int? ID_Pratiche { get; set; }

        public bool? HaEccezione { get; set; }
    }
}
