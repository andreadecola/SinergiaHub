namespace Sinergia.Model
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Notifiche")]
    public partial class Notifiche
    {
        [Key]
        public int ID_Notifica { get; set; }

        [Required]
        [StringLength(255)]
        public string Titolo { get; set; }

        public string Descrizione { get; set; }

        public DateTime DataCreazione { get; set; }

        public DateTime? DataLettura { get; set; }

        public int ID_Utente { get; set; }

        [StringLength(50)]
        public string Tipo { get; set; }

        [StringLength(20)]
        public string Stato { get; set; }

        public int Contatore { get; set; }

        public bool Letto { get; set; }

        public int? ID_Pratiche { get; set; }
    }
}
