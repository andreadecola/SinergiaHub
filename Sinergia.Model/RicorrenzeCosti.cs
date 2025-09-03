namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("RicorrenzeCosti")]
    public partial class RicorrenzeCosti
    {
        [Key]
        public int ID_Ricorrenza { get; set; }

        public int? ID_Professionista { get; set; }

        public int? ID_Professione { get; set; }

        public int? ID_Team { get; set; }

        [StringLength(50)]
        public string Periodicita { get; set; }

        [Required]
        [StringLength(20)]
        public string TipoValore { get; set; }

        public decimal Valore { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        [Required]
        [StringLength(50)]
        public string Categoria { get; set; }

        public bool EreditaDatiDaPratica { get; set; }

        public bool Attivo { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_TipoCostoGenerale { get; set; }

        public int? ID_CostoProfessionista { get; set; }

        public int? ID_CostoTeam { get; set; }
    }
}
