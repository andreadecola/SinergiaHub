namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("CostiGeneraliUtente")]
    public partial class CostiGeneraliUtente
    {
        [Key]
        public int ID_CostoGenerale { get; set; }

        public int ID_Utente { get; set; }

        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal? Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataInserimento { get; set; }

        public bool? Approvato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public int? ID_TipoCosto { get; set; }
    }
}
