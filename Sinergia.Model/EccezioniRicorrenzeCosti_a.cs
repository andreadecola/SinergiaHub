namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class EccezioniRicorrenzeCosti_a
    {
        [Key]
        public int ID_Eccezione_a { get; set; }

        public int ID_Eccezione { get; set; }

        public int? ID_Professionista { get; set; }

        public int? ID_Team { get; set; }

        [Required]
        [StringLength(50)]
        public string Categoria { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInizio { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataFine { get; set; }

        [StringLength(100)]
        public string Motivazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataCreazione { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int NumeroVersione { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public string ModificheTestuali { get; set; }

        public int? ID_TipologiaCosto { get; set; }

        public int? ID_RicorrenzaCosto { get; set; }

        public bool? SaltaCosto { get; set; }

        public decimal? NuovoImporto { get; set; }
    }
}
