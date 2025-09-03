namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("AvvisiParcella")]
    public partial class AvvisiParcella
    {
        [Key]
        public int ID_AvvisoParcelle { get; set; }

        public int? ID_Pratiche { get; set; }

        public DateTime? DataAvviso { get; set; }

        public decimal? Importo { get; set; }

        [StringLength(500)]
        public string Note { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        [StringLength(250)]
        public string MetodoPagamento { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }

        public decimal? ContributoIntegrativoPercentuale { get; set; }

        public decimal? ContributoIntegrativoImporto { get; set; }

        public decimal? AliquotaIVA { get; set; }

        public decimal? ImportoIVA { get; set; }

        public decimal? TotaleAvvisiParcella { get; set; }

        public DateTime? DataModifica { get; set; }

        public int? ID_UtenteModifica { get; set; }
    }
}
