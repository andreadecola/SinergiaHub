namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CostiPratica")]
    public partial class CostiPratica
    {
        [Key]
        public int ID_CostoPratica { get; set; }

        public int ID_Pratiche { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        public int? ID_ClienteAssociato { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataInserimento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_AnagraficaCosto { get; set; }

        [StringLength(20)]
        public string TipoCosto { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }
    }
}
