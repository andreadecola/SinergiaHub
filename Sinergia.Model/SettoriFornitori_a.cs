namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class SettoriFornitori_a
    {
        [Key]
        public int ID_Storico { get; set; }

        public int ID_Settore { get; set; }

        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }

        public DateTime? DataInserimento { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
