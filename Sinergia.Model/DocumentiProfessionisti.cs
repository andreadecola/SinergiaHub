namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DocumentiProfessionisti")]
    public partial class DocumentiProfessionisti
    {
        [Key]
        public int ID_Documento { get; set; }

        public int ID_Professionista { get; set; }

        [Required]
        [StringLength(255)]
        public string NomeDocumento { get; set; }

        public byte[] FileContent { get; set; }

        [StringLength(255)]
        public string TipoMime { get; set; }

        public DateTime DataCaricamento { get; set; }

        public int ID_UtenteCaricamento { get; set; }
    }
}
