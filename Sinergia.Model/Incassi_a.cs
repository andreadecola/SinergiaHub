namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Incassi_a
    {
        [Key]
        public int ID_Archivio { get; set; }

        public int? ID_IncassoOriginale { get; set; }

        public int? ID_Pratiche { get; set; }

        public DateTime? DataIncasso { get; set; }

        public decimal? Importo { get; set; }

        [StringLength(50)]
        public string ModalitaPagamento { get; set; }

        public decimal? Utile { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        public bool? VersaInPlafond { get; set; }

        public int? ID_AvvisoParcella { get; set; }
    }
}
