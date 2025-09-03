namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("PermessiDelegabiliPerProfessionista")]
    public partial class PermessiDelegabiliPerProfessionista
    {
        [Key]
        public int ID_PermessiDelegabiliPerProfessionista { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Menu { get; set; }
    }
}
