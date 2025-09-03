namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class MovimentiBancari_a
    {
        [Key]
        public int ID_Movimento { get; set; }

        public int ID_Cliente { get; set; }

        public int? ID_Pratiche { get; set; }

        public int? ID_DatoBancario { get; set; }

        [StringLength(20)]
        public string TipoMovimento { get; set; }

        [StringLength(30)]
        public string MetodoPagamento { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        public decimal Importo { get; set; }

        public DateTime DataOperazione { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataInserimento { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public DateTime? DataArchiviazione { get; set; }

        public int? ID_UtenteArchiviazione { get; set; }

        public int NumeroVersione { get; set; }

        public string ModificheTestuali { get; set; }
    }
}
