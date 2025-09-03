namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class RimborsiPratica_a
    {
        [Key]
        public int ID_RimborsoArchivio { get; set; }

        public int ID_RimborsoOriginale { get; set; }

        public int ID_Pratiche { get; set; }

        [Required]
        [StringLength(200)]
        public string Descrizione { get; set; }

        public decimal Importo { get; set; }

        [Column(TypeName = "date")]
        public DateTime DataInserimento { get; set; }

        public int ID_UtenteCreatore { get; set; }

        public DateTime DataArchiviazione { get; set; }

        public int ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
