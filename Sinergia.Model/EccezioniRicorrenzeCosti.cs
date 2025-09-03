namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("EccezioniRicorrenzeCosti")]
    public partial class EccezioniRicorrenzeCosti
    {
        [Key]
        public int ID_Eccezione { get; set; }

        public int? ID_Professionista { get; set; }

        public int? ID_Team { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        [StringLength(100)]
        public string Motivazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        [Required]
        [StringLength(100)]
        public string Categoria { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_TipologiaCosto { get; set; }

        public int? ID_RicorrenzaCosto { get; set; }

        public bool? SaltaCosto { get; set; }

        public decimal? NuovoImporto { get; set; }
    }
}
