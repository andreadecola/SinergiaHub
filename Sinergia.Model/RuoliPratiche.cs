namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("RuoliPratiche")]
    public partial class RuoliPratiche
    {
        [Key]
        public int ID_Ruolo { get; set; }

        [Required]
        [StringLength(50)]
        public string NomeRuolo { get; set; }
    }
}
