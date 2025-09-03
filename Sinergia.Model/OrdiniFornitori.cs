namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("OrdiniFornitori")]
    public partial class OrdiniFornitori
    {
        [Key]
        public int ID_Ordine { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Fornitore { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataOrdine { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataConsegnaPrevista { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataConsegnaEffettiva { get; set; }

        [Required]
        [StringLength(20)]
        public string Stato { get; set; }

        [Column(TypeName = "text")]
        public string Descrizione { get; set; }

        [Column(TypeName = "text")]
        public string Note { get; set; }

        public DateTime? UltimaModifica { get; set; }
    }
}
