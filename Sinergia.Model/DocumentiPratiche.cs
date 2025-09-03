namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("DocumentiPratiche")]
    public partial class DocumentiPratiche
    {
        [Key]
        public int ID_Documento { get; set; }

        public int ID_Pratiche { get; set; }

        [Required]
        [StringLength(255)]
        public string NomeFile { get; set; }

        [StringLength(10)]
        public string Estensione { get; set; }

        [StringLength(100)]
        public string TipoContenuto { get; set; }

        [Required]
        public byte[] Documento { get; set; }

        public DateTime? DataCaricamento { get; set; }

        public int? ID_UtenteCaricamento { get; set; }

        public string Note { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }
    }
}
