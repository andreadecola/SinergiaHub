namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DatiBancari")]
    public partial class DatiBancari
    {
        [Key]
        public int ID_DatoBancario { get; set; }

        public int ID_Cliente { get; set; }

        [StringLength(100)]
        public string NomeBanca { get; set; }

        [StringLength(34)]
        public string IBAN { get; set; }

        [StringLength(100)]
        public string Intestatario { get; set; }

        [StringLength(20)]
        public string BIC_SWIFT { get; set; }

        [StringLength(255)]
        public string IndirizzoBanca { get; set; }

        [StringLength(255)]
        public string Note { get; set; }

        [StringLength(50)]
        public string TipoCliente { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public DateTime? DataInserimento { get; set; }
    }
}
