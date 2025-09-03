namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("FinanziamentiProfessionisti")]
    public partial class FinanziamentiProfessionisti
    {
        [Key]
        public int ID_Finanziamento { get; set; }

        public int ID_Professionista { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataVersamento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataUltimaModifica { get; set; }
    }
}
