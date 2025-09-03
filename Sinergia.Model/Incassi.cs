namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Incassi")]
    public partial class Incassi
    {
        [Key]
        public int ID_Incasso { get; set; }

        public int? ID_Pratiche { get; set; }

        public DateTime DataIncasso { get; set; }

        public decimal Importo { get; set; }

        [StringLength(50)]
        public string ModalitaPagamento { get; set; }

        public decimal? Utile { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public bool? VersaInPlafond { get; set; }

        public int? ID_AvvisoParcella { get; set; }
    }
}
