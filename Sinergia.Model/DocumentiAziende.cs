namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DocumentiAziende")]
    public partial class DocumentiAziende
    {
        [Key]
        public int ID_Documento { get; set; }

        public int ID_Cliente { get; set; }

        [Required]
        [StringLength(255)]
        public string NomeDocumento { get; set; }

        [Required]
        public byte[] FileContent { get; set; }

        [Required]
        [StringLength(255)]
        public string TipoMime { get; set; }

        public DateTime DataCaricamento { get; set; }

        public int? ID_UtenteCaricamento { get; set; }
    }
}
