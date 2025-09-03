namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("TipoRagioneSociale")]
    public partial class TipoRagioneSociale
    {
        [Key]
        public int ID_TipoRagioneSociale { get; set; }

        [Required]
        [StringLength(50)]
        public string NomeTipo { get; set; }

        [StringLength(255)]
        public string Descrizione { get; set; }

        [Required]
        [StringLength(15)]
        public string Stato { get; set; }
    }
}
