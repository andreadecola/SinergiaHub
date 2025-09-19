namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("ClientiProfessionisti")]
    public partial class ClientiProfessionisti
    {
        [Key]
        public int ID_ClientiProfessionisti { get; set; }

        public int ID_Cliente { get; set; }

        public int ID_Professionista { get; set; }

        [StringLength(50)]
        public string Ruolo { get; set; }

        public DateTime DataAssegnazione { get; set; }
    }
}
