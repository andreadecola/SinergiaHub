namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Nazioni")]
    public partial class Nazioni
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID_BPCittaDN { get; set; }

        [StringLength(50)]
        public string CodeNazione { get; set; }

        [Required]
        [StringLength(100)]
        public string NameNazione { get; set; }

        [StringLength(3)]
        public string CODStato { get; set; }
    }
}
