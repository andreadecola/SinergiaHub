namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class FinanziamentiProfessionisti_a
    {
        [Key]
        public int ID_Finanziamento_Archivio { get; set; }

        public int ID_Finanziamento_Originale { get; set; }

        public int ID_Professionista { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataVersamento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataUltimaModifica { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }

        [Required]
        [StringLength(50)]
        public string Operazione { get; set; }
    }
}
