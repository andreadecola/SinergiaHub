namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class OrdiniFornitori_a
    {
        [Key]
        public int ID_Ordine { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Fornitore { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataOrdine { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataConsegnaPrevista { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataConsegnaEffettiva { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        [Column(TypeName = "text")]
        public string Descrizione { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
