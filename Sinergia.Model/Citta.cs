namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Citta")]
    public partial class Citta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID_BPCitta { get; set; }

        [StringLength(50)]
        public string CodeCitta { get; set; }

        [Required]
        [StringLength(100)]
        public string NameLocalita { get; set; }

        [StringLength(10)]
        public string CODREG { get; set; }

        [StringLength(10)]
        public string CODPRV { get; set; }

        [StringLength(10)]
        public string CODCOM { get; set; }

        [StringLength(10)]
        public string CODFIN { get; set; }

        [StringLength(10)]
        public string CAP { get; set; }

        [StringLength(10)]
        public string SGLPRV { get; set; }

        [StringLength(10)]
        public string CODUSL { get; set; }

        [StringLength(100)]
        public string Regione { get; set; }

        [StringLength(10)]
        public string SiglaNazione { get; set; }
    }
}
