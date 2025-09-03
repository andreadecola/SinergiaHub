namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("SettoriFornitori")]
    public partial class SettoriFornitori
    {
        [Key]
        public int ID_Settore { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; }

        [StringLength(50)]
        public string Stato { get; set; }

        public DateTime? DataInserimento { get; set; }

        public int? ID_UtenteCreatore { get; set; }

        public int? ID_UtenteUltimaModifica { get; set; }

        public DateTime? DataUltimaModifica { get; set; }
    }
}
